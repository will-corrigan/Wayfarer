using Dalamud.Plugin.Services;
using Wayfarer.Guidance;
using Wayfarer.Routing;

namespace Wayfarer.Modules.Hunting;

/// <summary>The hunting module's guidance half. Guides the followed hunting log page or mark bill
/// one monster at a time, in the game's order, and lets go when the last is done, when a bill is
/// handed in, or when this character is following nothing.
///
/// <para>Read every frame while it holds guidance, but only makes the objective again when
/// something it depends on moved: the hunt, a kill, which monster is in sight, whether its FATE is
/// up. Routing works out the way afresh whenever the objective is a new one, so handing back the
/// same one is what keeps a frame cheap.</para></summary>
internal sealed class HuntObjectives(HuntReader reader, HuntFollowing following, IGuidance guidance, IClientState clientState, IPluginLog log) : IObjectiveSource
{
    private Signature? last;
    private Objective? cached;

    /// <inheritdoc/>
    public string Name => HuntingModule.ModuleName;

    /// <inheritdoc/>
    public Objective? Current => Refresh();

    /// <inheritdoc/>
    public void Displaced()
    {
    }

    private static int Fingerprint(IEnumerable<int> kills)
    {
        var hash = default(HashCode);
        foreach (var kill in kills)
        {
            hash.Add(kill);
        }

        return hash.ToHashCode();
    }

    private Objective? Refresh()
    {
        if (following.Followed is not { } hunt)
        {
            // Nothing followed for whoever is logged in now: another character, or following was
            // let go of somewhere else. Guidance goes back to whatever it was guiding before.
            return Stop(unfollow: false);
        }

        if (reader.Facts(hunt) is not { } facts || HuntReader.Kills(hunt, facts) is not { } kills)
        {
            // No such hunt in the sheets, or the bill has been handed in.
            log.Debug($"hunting: stopped following {hunt}: {(reader.Facts(hunt) is null ? "the sheets have no such hunt" : "the bill is no longer held")}.");
            return Stop(unfollow: true);
        }

        var quarries = facts.Quarries.Select((quarry, i) => quarry.With(i < kills.Count ? kills[i] : 0)).ToList();
        if (quarries.FirstOrDefault(quarry => !quarry.Done) is not { } now)
        {
            // What the game said, so an ending nobody expected can be told apart from a finished
            // page: every count against its need, and for a page the rank the log is working.
            var counts = string.Join(", ", quarries.Select(quarry => $"{quarry.Have}/{quarry.Need}"));
            var rank = hunt.Kind == HuntKind.LogPage ? $"; the log is working rank {HuntReader.RankWorked(hunt.Slot) + 1}" : string.Empty;
            log.Debug($"hunting: {facts.Headline} is done ({counts}{rank}), so following it ends.");
            return Stop(unfollow: true);
        }

        var seen = reader.Seen(now.NameId);
        var fate = now.Fate is { } during ? reader.FateAt(during.Id) : null;
        var here = now.Places.Any(place => place.Territory == clientState.TerritoryType);
        var signature = new Signature(hunt, Fingerprint(kills), seen?.Id ?? 0uL, fate is not null, here);
        if (signature == last)
        {
            return cached;
        }

        last = signature;
        var (named, battle) = reader.Census(now.NameId);
        log.Debug($"hunting: after {now.Name} (name {now.NameId}), {now.Have}/{now.Need}: {(seen is { } s ? $"one in sight, object {s.Id}" : "none in sight")}; the game has {named} of that name loaded among {battle} battle monsters.");
        return cached = HuntObjectiveBuilder.Build(
            facts.Headline,
            facts.Kind,
            quarries,
            nameId => nameId == now.NameId ? seen : null,
            _ => fate,
            _ => here);
    }

    /// <summary>Stops guiding: lets go of guidance, so whatever it pushed aside has it back, and
    /// forgets the hunt when it is over.</summary>
    private Objective? Stop(bool unfollow)
    {
        if (unfollow)
        {
            following.Unfollow();
        }

        guidance.Yield(this);
        last = null;
        return cached = null;
    }

    /// <param name="Hunt">Which hunt.</param>
    /// <param name="Kills">Every kill count of it, folded together.</param>
    /// <param name="Seen">Which monster of the one being hunted is in sight, or zero.</param>
    /// <param name="FateUp">Whether its FATE is up.</param>
    /// <param name="Here">Whether the player is in the zone it lives in.</param>
    private sealed record Signature(Hunt Hunt, int Kills, ulong Seen, bool FateUp, bool Here);
}
