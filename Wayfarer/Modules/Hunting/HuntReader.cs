using System.Globalization;
using Dalamud.Game.ClientState.Fates;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using Wayfarer.Routing;
using Wayfarer.World;
using InstanceContent = Lumina.Excel.Sheets.InstanceContent;
using Map = Lumina.Excel.Sheets.Map;

namespace Wayfarer.Modules.Hunting;

/// <summary>Every read the hunting module makes of the game, so the rest of it is pure.
///
/// <para>What a hunt asks for is the same for every player and comes from the sheets: a log
/// page's monsters and how many of each, a bill's targets, where each lives. It is read once per
/// hunt and kept. How many have been killed, whether one is standing in sight and whether a FATE
/// is up are this character's and this moment's, and are read from the game every time, on its own
/// thread.</para></summary>
internal sealed unsafe class HuntReader(IDataManager data, IObjectTable objects, IClientState clientState, IFateTable fates)
{
    /// <summary>How many monster entries a hunting log page has.</summary>
    private const int EntriesPerPage = 10;

    /// <summary>How many monsters one hunting log entry can name.</summary>
    private const int TargetsPerEntry = 4;

    /// <summary>A class's hunting log rows are counted from this; a Grand Company's from <see cref="GrandCompanyBase"/>.</summary>
    private const uint ClassBase = 10_000;

    /// <summary>A Grand Company's hunting log rows are counted from this.</summary>
    private const uint GrandCompanyBase = 1_000_000;

    /// <summary>How many targets a mark bill has, one a page.</summary>
    private const int TargetsPerBill = 5;

    /// <summary>The pixel at a map's centre, which is world (0, 0) before the map's offset.</summary>
    private const float CentrePixel = 1024f;

    /// <summary>A map's scale is stored as a percentage.</summary>
    private const float ScalePercent = 100f;

    private readonly Dictionary<Hunt, HuntFacts> facts = [];
    private Dictionary<uint, List<Map>>? mapsByZone;
    private Dictionary<string, uint>? dutiesByName;

    /// <summary>How many of each target have been killed, in the order <see cref="Facts"/> lists
    /// them, or null when the game has nothing to say: the bill is no longer held. Game thread
    /// only.</summary>
    public static IReadOnlyList<int>? Kills(Hunt hunt, HuntFacts known)
    {
        ArgumentNullException.ThrowIfNull(hunt);
        ArgumentNullException.ThrowIfNull(known);
        return hunt.Kind == HuntKind.LogPage ? PageKills(hunt, known) : BillKills(hunt, known);
    }

    /// <summary>Which of a log's pages the game says this character is working on, counted from
    /// zero, or null when the log is not there to ask. Game thread only.</summary>
    public static int? RankWorked(byte slot)
    {
        var manager = MonsterNoteManager.Instance();
        if (manager == null || slot >= manager->RankData.Length)
        {
            return null;
        }

        return manager->RankData[slot].Rank;
    }

    /// <summary>The bill of this kind the character holds, or zero. Game thread only.</summary>
    public static uint HeldBill(byte markIndex)
    {
        var hunt = MobHunt.Instance();
        if (hunt == null || !hunt->IsMarkBillObtained(markIndex))
        {
            return 0;
        }

        return (uint)Math.Max(0, hunt->GetObtainedHuntOrderRowId(markIndex));
    }

    /// <summary>Everything the sheets say about a hunt: what it is called, and every monster it
    /// asks for in order, with how many and where. Null when the sheets have no such hunt.
    /// Read once per hunt; safe off the game's thread.</summary>
    public HuntFacts? Facts(Hunt hunt)
    {
        ArgumentNullException.ThrowIfNull(hunt);
        if (facts.TryGetValue(hunt, out var known))
        {
            return known;
        }

        var read = hunt.Kind == HuntKind.LogPage ? ReadPage(hunt) : ReadBill(hunt);
        if (read is not null)
        {
            facts[hunt] = read;
        }

        return read;
    }

    /// <summary>Reads the whole-game tables the hunts are looked up in, so the first frame that
    /// guides one does not pay for them. Safe off the game's thread.</summary>
    public void Warm()
    {
        mapsByZone ??= ReadMaps();
        dutiesByName ??= ReadDuties();
    }

    /// <summary>The nearest monster standing in sight with this name, alive and attackable, or
    /// null. Game thread only.</summary>
    public Found? Seen(uint nameId)
    {
        if (objects.LocalPlayer is not { } player)
        {
            return null;
        }

        Found? nearest = null;
        var shortest = float.MaxValue;
        foreach (var thing in objects)
        {
            if (thing is not IBattleNpc npc
                || npc.NameId != nameId
                || npc.BattleNpcKind != BattleNpcSubKind.Combatant
                || npc.IsDead
                || !npc.IsTargetable)
            {
                continue;
            }

            var far = Vector3Distance(npc.Position, player.Position);
            if (far < shortest)
            {
                shortest = far;
                nearest = new Found(npc.GameObjectId, new Place(clientState.TerritoryType, clientState.MapId, npc.Position.X, npc.Position.Y, npc.Position.Z));
            }
        }

        return nearest;
    }

    /// <summary>Where a FATE is being fought right now, with its edge, or null when it is not up.
    /// The game only knows the FATEs of the zone the player is in. Game thread only.</summary>
    public Place? FateAt(uint fateId)
    {
        foreach (var fate in fates)
        {
            if (fate.FateId == fateId && fate.State is FateState.Preparing or FateState.Running)
            {
                return new Place(clientState.TerritoryType, clientState.MapId, fate.Position.X, fate.Position.Y, fate.Position.Z, fate.Radius);
            }
        }

        return null;
    }

    /// <summary>A name as the game shows it in its windows: each word begun with a capital. The
    /// sheet writes most monster names in lower case and leaves the capitals to the window.</summary>
    private static string Shown(string name) =>
        string.Join(' ', name.Split(' ').Select(word => word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..]));

    private static float Vector3Distance(System.Numerics.Vector3 a, System.Numerics.Vector3 b) => System.Numerics.Vector3.Distance(a, b);

    private static List<int> PageKills(Hunt hunt, HuntFacts known)
    {
        var manager = MonsterNoteManager.Instance();
        if (manager == null || hunt.Slot >= manager->RankData.Length)
        {
            return [];
        }

        // The game keeps kills only for the page being worked. Every page before it is done, and
        // every page after it has nothing yet.
        ref var log = ref manager->RankData[hunt.Slot];
        var kills = new List<int>(known.Quarries.Count);
        foreach (var quarry in known.Quarries)
        {
            kills.Add(log.Rank > hunt.Rank ? quarry.Need
                : log.Rank < hunt.Rank ? 0
                : log.RankData[quarry.Entry].Counts[quarry.Target]);
        }

        return kills;
    }

    private static List<int>? BillKills(Hunt hunt, HuntFacts known)
    {
        var mobHunt = MobHunt.Instance();
        var markIndex = (byte)hunt.Log;
        if (mobHunt == null || HeldBill(markIndex) != hunt.Order)
        {
            return null;
        }

        return [.. known.Quarries.Select(quarry => mobHunt->GetKillCount(markIndex, (byte)quarry.Entry))];
    }

    private HuntFacts? ReadPage(Hunt hunt)
    {
        var (id, logBase) = hunt.Log >= GrandCompanyBase ? (hunt.Log / GrandCompanyBase, GrandCompanyBase) : (hunt.Log / ClassBase, ClassBase);
        var who = logBase == GrandCompanyBase
            ? data.GetExcelSheet<GrandCompany>().GetRowOrDefault(id)?.Name.ExtractText()
            : data.GetExcelSheet<ClassJob>().GetRowOrDefault(id)?.Name.ExtractText();
        if (string.IsNullOrEmpty(who))
        {
            return null;
        }

        var notes = data.GetExcelSheet<MonsterNote>();
        var quarries = new List<HuntQuarry>();
        for (var entry = 0; entry < EntriesPerPage; entry++)
        {
            if (notes.GetRowOrDefault(hunt.Log + (hunt.Rank * 10u) + (uint)entry + 1) is not { } note)
            {
                continue;
            }

            for (var slot = 0; slot < TargetsPerEntry; slot++)
            {
                if (note.MonsterNoteTarget[slot].ValueNullable is not { } target || target.BNpcName.RowId == 0 || note.Count[slot] == 0)
                {
                    continue;
                }

                var (places, duty) = WhereTargetLives(target);
                quarries.Add(new HuntQuarry(
                    Shown(target.BNpcName.ValueNullable?.Singular.ExtractText() ?? string.Empty),
                    target.BNpcName.RowId,
                    note.Count[slot],
                    places,
                    duty,
                    null,
                    entry,
                    slot));
            }
        }

        var headline = $"{CultureInfo.InvariantCulture.TextInfo.ToTitleCase(who)}, rank {hunt.Rank + 1}";
        return quarries.Count == 0 ? null : new HuntFacts(headline, "Hunting Log", quarries);
    }

    private HuntFacts? ReadBill(Hunt hunt)
    {
        var markIndex = hunt.Log;
        if (data.GetExcelSheet<MobHuntOrderType>().GetRowOrDefault(markIndex) is not { } kind
            || data.GetSubrowExcelSheet<MobHuntOrder>().GetRowOrDefault(hunt.Order) is not { } order)
        {
            return null;
        }

        var quarries = new List<HuntQuarry>();
        for (var page = 0; page < Math.Min(order.Count, TargetsPerBill); page++)
        {
            var line = order[page];
            if (line.Target.ValueNullable is not { } target || target.Name.RowId == 0 || line.NeededKills == 0)
            {
                continue;
            }

            var fate = target.FATE.ValueNullable is { } during && target.FATE.RowId != 0
                ? new QuarryFate(target.FATE.RowId, during.Name.ExtractText())
                : null;

            quarries.Add(new HuntQuarry(
                Shown(target.Name.ValueNullable?.Singular.ExtractText() ?? string.Empty),
                target.Name.RowId,
                line.NeededKills,
                BillPlaces(target),
                null,
                fate,
                page,
                0));
        }

        var headline = kind.EventItem.ValueNullable?.Name.ExtractText() is { Length: > 0 } named ? named : "Mark Bill";
        return quarries.Count == 0 ? null : new HuntFacts(headline, "Mark Bill", quarries);
    }

    /// <summary>Where a hunting log monster lives: each part of a map the log names for it, or the
    /// duty it lives in when the part it names is inside one.</summary>
    private (IReadOnlyList<Place> Places, uint? Duty) WhereTargetLives(MonsterNoteTarget target)
    {
        Warm();
        var places = new List<Place>();
        for (var i = 0; i < target.PlaceNameZone.Count; i++)
        {
            var zone = target.PlaceNameZone[i].RowId;
            var location = target.PlaceNameLocation[i].RowId;
            if (zone == 0)
            {
                continue;
            }

            places.AddRange(Marked(zone, location));
        }

        if (places.Count > 0)
        {
            return (places, null);
        }

        // A monster that lives inside a duty is named by the zone the duty's entrance is in and by
        // the duty itself as the place within it: "Southern Thanalan", "The Sunken Temple of
        // Qarn". Either can be the duty's own name, so both are asked.
        // Several place names share one set of words, and only one of them is the duty's own, so
        // they are matched by their words.
        foreach (var name in target.PlaceNameLocation.Concat(target.PlaceNameZone))
        {
            if (name.ValueNullable?.Name.ExtractText() is { Length: > 0 } words && dutiesByName!.TryGetValue(words, out var duty))
            {
                return ([], duty);
            }
        }

        return (places, null);
    }

    /// <summary>Where a bill's target lives: the part of its map the bill names, or when the bill
    /// names only the map, which is how an elite mark is named, the map's own aetheryte, to arrive
    /// by and look around from.</summary>
    private List<Place> BillPlaces(MobHuntTarget target)
    {
        if (target.Map.ValueNullable is not { } map)
        {
            return [];
        }

        if (target.PlaceName.RowId != 0 && Marker(map, target.PlaceName.RowId) is { } marked)
        {
            return [marked];
        }

        return Aetheryte(map) is { } arrival ? [arrival] : [];
    }

    /// <summary>The parts of every map of a zone that the game labels with this name.</summary>
    private IEnumerable<Place> Marked(uint zone, uint location)
    {
        if (!mapsByZone!.TryGetValue(zone, out var maps))
        {
            yield break;
        }

        foreach (var map in maps)
        {
            if (location != 0 && Marker(map, location) is { } marked)
            {
                yield return marked;
            }
        }
    }

    /// <summary>Where on a map the game writes a name, or null when it does not.</summary>
    private Place? Marker(Map map, uint placeName)
    {
        if (map.MapMarkerRange == 0 || data.GetSubrowExcelSheet<MapMarker>().GetRowOrDefault(map.MapMarkerRange) is not { } markers)
        {
            return null;
        }

        foreach (var marker in markers)
        {
            if (marker.PlaceNameSubtext.RowId == placeName)
            {
                var scale = map.SizeFactor / ScalePercent;
                var x = ((marker.X - CentrePixel) / scale) - map.OffsetX;
                var z = ((marker.Y - CentrePixel) / scale) - map.OffsetY;
                return new Place(map.TerritoryType.RowId, map.RowId, x, 0f, z);
            }
        }

        return null;
    }

    /// <summary>Where a map's aetheryte stands, or null when it has none.</summary>
    private Place? Aetheryte(Map map)
    {
        foreach (var aetheryte in data.GetExcelSheet<Lumina.Excel.Sheets.Aetheryte>())
        {
            if (!aetheryte.IsAetheryte || aetheryte.Territory.RowId != map.TerritoryType.RowId)
            {
                continue;
            }

            if (aetheryte.Level[0].ValueNullable is { } level)
            {
                return new Place(level.Territory.RowId, map.RowId, level.X, level.Y, level.Z);
            }
        }

        return null;
    }

    /// <summary>Every map a zone has, by the zone's name, for finding where a sheet's place names are.</summary>
    private Dictionary<uint, List<Map>> ReadMaps()
    {
        var byZone = new Dictionary<uint, List<Map>>();
        foreach (var map in data.GetExcelSheet<Map>())
        {
            if (map.PlaceName.RowId == 0 || map.TerritoryType.RowId == 0)
            {
                continue;
            }

            if (!byZone.TryGetValue(map.PlaceName.RowId, out var list))
            {
                byZone[map.PlaceName.RowId] = list = [];
            }

            list.Add(map);
        }

        return byZone;
    }

    /// <summary>The Duty Finder entry for each piece of instanced content, by the words naming the
    /// ground it is fought on, for monsters that live nowhere a player can walk. The first entry
    /// for a name wins, which is the ordinary difficulty: the sheet lists it first.</summary>
    private Dictionary<string, uint> ReadDuties()
    {
        var byName = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        foreach (var condition in data.GetExcelSheet<ContentFinderCondition>())
        {
            if (condition.Content.Is<InstanceContent>()
                && condition.TerritoryType.ValueNullable?.PlaceName.ValueNullable?.Name.ExtractText() is { Length: > 0 } words)
            {
                byName.TryAdd(words, condition.RowId);
            }
        }

        return byName;
    }
}
