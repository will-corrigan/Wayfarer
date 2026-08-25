// What each quest actually grants, joined onto the catalogue's entries.
//
// WHY THIS IS HERE AND NOT IN THE .mjs
// The join is a sheet walk — Quest.Reward -> Item.ItemAction -> Mount/Companion/Orchestrion, plus
// the twenty-odd feature tables that name their own unlock quest — and the sheets only exist on a
// machine with the game installed. It also has to fold names with Wayfarer.Core's QuestNameKey,
// which is the same fold the shipping plugin matches with; a second implementation in JavaScript
// would be a second set of answers.
//
// WHAT IT IS NOT
// It is not an enumeration of everything the game unlocks. Reflecting over every RowRef<Quest> in
// the schema turns up 36 channels and 3,091 candidate rows, and inverting the catalogue onto them
// is a separate decision with a UI cost attached. This answers one narrow question about entries
// that already exist: "this entry is gated on quest N and is called X - which row in which sheet
// is X?"
using Lumina;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Wayfarer.Core.Unlocks;

namespace Wayfarer.CatalogueGen;

/// <summary>Every reward identity the game states for a quest, indexed by quest row, plus the join
/// that picks the one a catalogue entry is about.</summary>
internal sealed class RewardIndex
{
    /// <summary><c>ItemAction</c> type numbers, and what the payload means for each. Measured, not
    /// assumed: for 1322/853/3357/1013/20086 the identity is <c>ItemAction.Data[0]</c> and
    /// <c>Item.AdditionalData</c> is zero; for 25183 and 37312 it is exactly the other way round and
    /// reading <c>Data[0]</c> yields row 0 for every roll in the game.</summary>
    private const uint ItemActionMount = 1322;
    private const uint ItemActionCompanion = 853;
    private const uint ItemActionOrchestrion = 25183;
    private const uint ItemActionTripleTriadCard = 3357;
    private const uint ItemActionBuddyEquip = 1013;
    private const uint ItemActionOrnament = 20086;
    private const uint ItemActionGlasses = 37312;
    private const uint ItemActionFramersKit = 29459;

    /// <summary>One type number for three unrelated things. <c>Data[0]</c> is an UnlockLink id and
    /// the only way to know what it opens is to look it up in <b>both</b> <c>Emote.UnlockLink</c>
    /// and <c>CharaMakeCustomize.UnlockLink</c> — "Ballroom Etiquette" items land in the first,
    /// "Modern Aesthetics" in the second, and the Aetheryte Pendulum in neither.</summary>
    private const uint ItemActionUnlockLink = 2633;

    /// <summary>A <c>ContentFinderCondition.Content</c> reference is untyped and spans four id
    /// spaces; only link type 1 is an <c>InstanceContent</c> row. <c>CSBonusContentIdentifier</c>
    /// carries its own copy of the same discriminator, which has to be checked separately.</summary>
    private const byte ContentLinkInstanceContent = 1;

    /// <summary><c>ContentFinderCondition.UnlockType</c>: only 1 means the criteria row is a
    /// Quest.</summary>
    private const byte UnlockCriteriaIsQuest = 1;

    /// <summary>The achievement condition kind whose <c>Key</c> is a Quest row. The other kinds key
    /// on kill counts, gil and levels, and are not quest-gated.</summary>
    private const byte AchievementTypeQuestCompletion = 6;

    /// <summary>The difficulty markers the game writes into a duty's own name and the guide writes
    /// into an entry's. Parenthesised so the word has to be the marker rather than part of a title —
    /// "The Howling Eye (Extreme)" is a tier, "Hard Times" is a quest. See
    /// <see cref="DifficultyAgrees"/>.</summary>
    private static readonly string[] Difficulties =
        ["(Hard)", "(Extreme)", "(Savage)", "(Unreal)", "(Chaotic)"];

    private readonly Dictionary<uint, List<Candidate>> byQuest;
    private readonly RewardIcons icons;

    private RewardIndex(Dictionary<uint, List<Candidate>> byQuest, RewardIcons icons)
    {
        this.byQuest = byQuest;
        this.icons = icons;
    }

    /// <summary>Reads every reward channel once. Ordering inside a quest's list is the order the
    /// channels are read in, and each channel walks its sheet in row order, so the index — and
    /// therefore the generated file — is a function of the game data alone.</summary>
    public static RewardIndex Build(GameData game)
    {
        var byQuest = new Dictionary<uint, List<Candidate>>();
        var items = game.GetExcelSheet<Item>();
        var unlockLinks = UnlockLinkIndex.Build(game);

        void Add(uint questRowId, Candidate? candidate)
        {
            if (questRowId == 0 || candidate is null || candidate.Name.Length == 0)
            {
                return;
            }

            if (!byQuest.TryGetValue(questRowId, out var list))
            {
                byQuest[questRowId] = list = [];
            }

            if (!list.Any(c => c.Kind == candidate.Kind && c.Id == candidate.Id))
            {
                list.Add(candidate);
            }
        }

        AddQuestChannels(game, items, unlockLinks, Add);
        AddFeatureChannels(game, Add);
        return new RewardIndex(byQuest, RewardIcons.Build(game));
    }

    /// <summary>The icon the game would draw for a candidate, or 0 when the kind has none.
    ///
    /// <para>The generator resolves this and the plugin does not read it: an icon id is a fact
    /// about the patch that is installed, so the shipping plugin looks it up live. What it is for
    /// is the fence — an icon-bearing kind whose row turns out to have no icon is a data bug, and
    /// it is caught here where the sheet walk that produced it can be corrected, rather than
    /// shipping as a blank square that only somebody looking at a screen can notice.</para>
    /// </summary>
    public uint IconId(Candidate candidate) => icons.For(candidate);

    /// <summary>The reward a catalogue entry is about, or null when the game states none.
    ///
    /// <para>Three rules, strongest first, and no fourth. The entry's own name matching a reward the
    /// game says its quest grants is the strongest evidence available — two independent statements
    /// agreeing. A label link the resolver already turned into a ContentFinderCondition row is the
    /// next: "[[The Aery]] Dungeon Access" <i>is</i> that duty whatever the guide calls it. Last,
    /// an entry the catalogue types as a mount, whose quest grants exactly one mount, and whose name
    /// shares a word with that mount, can only be about it — see <see cref="SharesAWord"/> for why
    /// all three clauses are needed. Anything weaker is a guess of the kind that bound seven entries
    /// to the wrong quest, so it returns null and the entry ships with no reward.</para>
    ///
    /// <para>All three rules draw from a pool that has already had the difficulty tier applied to it
    /// — see <see cref="DifficultyAgrees"/>. That is not one of the rules; it is a veto over all of
    /// them.</para></summary>
    public Match? Resolve(RewardJoin join)
    {
        if (join.Identity is { Kind.Length: > 0 } stated)
        {
            // The caller already knows the identity, because the enumeration handed it over: the
            // entry IS Title#858, rather than something inferred from a name. None of the three
            // rules below applies — they exist to decide which of a quest's several rewards an entry
            // is about, and that question is already answered. What coming through here still buys
            // is the fences: an unknown kind, and an icon-bearing kind whose row has no icon, both
            // fail generation instead of shipping.
            //
            // The quest's own candidates are consulted first so the answer keeps its
            // GrantingItemId — an orchestrion roll's picture lives on the item you are handed, not
            // on the Orchestrion row, which has only a name and a description.
            foreach (var questRowId in join.QuestRowIds)
            {
                foreach (var candidate in byQuest.GetValueOrDefault(questRowId) ?? [])
                {
                    if (string.Equals(candidate.Kind, stated.Kind, StringComparison.Ordinal)
                        && candidate.Id == stated.Id)
                    {
                        return new Match(candidate with { Name = stated.Name }, "stated-identity");
                    }
                }
            }

            return new Match(
                new Candidate(stated.Kind, stated.Id, stated.Name, "game-enumeration"),
                "stated-identity");
        }

        var pool = new List<Candidate>();
        foreach (var questRowId in join.QuestRowIds)
        {
            foreach (var candidate in byQuest.GetValueOrDefault(questRowId) ?? [])
            {
                if (!pool.Any(c => c.Kind == candidate.Kind && c.Id == candidate.Id))
                {
                    pool.Add(candidate);
                }
            }
        }

        foreach (var duty in join.Duties)
        {
            var candidate = new Candidate("ContentFinderCondition", duty.RowId, duty.Name, "label-link");
            if (!pool.Any(c => c.Kind == candidate.Kind && c.Id == candidate.Id))
            {
                pool.Add(candidate);
            }
        }

        pool.RemoveAll(c => !DifficultyAgrees(join.Unlock, c.Name));

        var wanted = EntryNameKeys(join.Unlock);
        var named = pool.Where(c => wanted.Contains(QuestNameKey.For(c.Name))).ToList();
        if (named.Count == 1)
        {
            return new Match(named[0], "name-match");
        }

        if (join.Duties.Count == 1 && DifficultyAgrees(join.Unlock, join.Duties[0].Name))
        {
            var duty = join.Duties[0];
            return new Match(
                new Candidate("ContentFinderCondition", duty.RowId, duty.Name, "label-link"), "label-link");
        }

        if (KindForCatalogueType(join.Type) is { } kind)
        {
            var ofKind = pool
                .Where(c => string.Equals(c.Kind, kind, StringComparison.Ordinal))
                .Where(c => SharesAWord(join.Unlock, c.Name))
                .ToList();
            if (ofKind.Count == 1)
            {
                return new Match(ofKind[0], "type-match");
            }
        }

        return null;
    }

    /// <summary>Whether an entry and a candidate reward name the same difficulty. A veto over every
    /// rule below, applied to the pool before any of them runs.
    ///
    /// <para><b>Why a difficulty is different from any other word.</b> A raid tier and its Savage
    /// tier are two separate duties with two separate unlock bits, and the catalogue has a separate
    /// entry for each. But the Savage entry is bound to the <i>normal</i> tier's final-floor unlock
    /// quest, because that clear is what opens Savage — so every channel that reasons from the bound
    /// quest states the normal tier, correctly, about a quest the entry only borrowed. Sharing a word
    /// then passes: "Sigmascape (Savage) Access" and "Sigmascape V4.0" share <i>sigmascape</i>, and
    /// three Savage tiers shipped carrying the normal tier's duty as their reward — a wrong plate on
    /// the page, and an identity gate reading the wrong duty's bit, which marked the Savage entry
    /// Done the moment the normal tier was cleared.</para>
    ///
    /// <para>The marker is the one part of these names that is never decorative: the game puts it in
    /// the duty's own name, and the guide puts it in the entry's. Where they disagree the candidate is
    /// about the other tier, whatever else the two names have in common.</para>
    ///
    /// <para><b>Why the answer is then "no reward" rather than the Savage row.</b> Nothing in the
    /// sheets connects that quest to the Savage tier — the pool has no Savage candidate to promote —
    /// and deriving one from the name would mean deciding which floor a tier's <i>access</i> means.
    /// The eight Savage entries that resolve correctly all name the tier's FIRST floor, which is not
    /// what a suffix rule would produce from the normal tier's last: "Asphodelos: The Fourth Circle"
    /// plus "(Savage)" is a real row, and the wrong one. So these three ship with no reward, which is
    /// what "the game states none through anything this entry is bound to" honestly is.</para>
    ///
    /// <para>Both directions are checked. A candidate carrying a difficulty the entry does not name is
    /// the same error the other way round.</para></summary>
    private static bool DifficultyAgrees(string unlock, string rewardName) =>
        Difficulties.All(d =>
            unlock.Contains(d, StringComparison.OrdinalIgnoreCase)
            == rewardName.Contains(d, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether an entry and a reward are talking about the same thing at all — the guard on
    /// the type rule, and the reason that rule is not a guess.
    ///
    /// <para>Without it the rule reads a <c>type</c> the catalogue derived from the first word of the
    /// entry's own name, which is a known-bad source: "Mount speed increased in northern Thanalan,
    /// Mist, the Lavender Beds, and the Goblet" is typed <c>mount</c> and its quest happens to hand
    /// out magitek armor, so the pool has exactly one Mount in it and the entry would have claimed to
    /// unlock a mount it has nothing to do with. Requiring one shared word rejects that and keeps the
    /// cases the rule exists for — "Chocobo Mount Access" and "company chocobo" share <i>chocobo</i>,
    /// "Airship Travel" and "wind-up airship" share <i>airship</i>.</para>
    ///
    /// <para>Words of one or two letters are ignored: "of", "the" and "a" are shared by everything
    /// and would make the guard vacuous.</para></summary>
    private static bool SharesAWord(string unlock, string rewardName)
    {
        var entryWords = Words(unlock);
        return Words(rewardName).Any(w => entryWords.Contains(w));

        static HashSet<string> Words(string text) =>
        [
            .. text
                .Split((char[])[' ', '\t', '-', ':', ',', '.', '(', ')', '\'', '"', '/'],
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.ToLowerInvariant())
                .Where(w => w.Length > 2),
        ];
    }

    /// <summary>The catalogue's own type words, where one of them narrows the pool to a single
    /// sheet. <c>system</c> and <c>zone</c> are deliberately absent: they name no sheet.</summary>
    private static string? KindForCatalogueType(string type) => type switch
    {
        "mount" => "Mount",
        "minion" => "Companion",
        "emote" => "Emote",
        "dungeon" or "trial" or "raid" or "alliance-raid" => "ContentFinderCondition",
        _ => null,
    };

    /// <summary>The names an entry might be filed under, folded. The catalogue writes an unlock the
    /// way its source guide does — "Firebird (Mount)", "The Aery Dungeon Access" — and neither is
    /// what the sheet calls the row, so the shapes the guide adds are peeled off and every reading
    /// is offered. Peeling is safe here in a way it is not for quest names: this set is only ever
    /// tested against rewards the game already says <i>this quest</i> grants.</summary>
    private static HashSet<string> EntryNameKeys(string unlock)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        void Take(string s)
        {
            var key = QuestNameKey.For(s);
            if (key.Length > 0)
            {
                keys.Add(key);
            }
        }

        Take(unlock);

        var trimmed = unlock.Trim();

        // "... Dungeon Access", "... Extreme Trial Access", "... Alliance Raid Access" — the
        // guide's own shape for "the duty called X becomes enterable".
        var access = trimmed.EndsWith(" Access", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^" Access".Length].TrimEnd()
            : trimmed;
        foreach (var tail in (string[])["Dungeon", "Trial", "Raid", "Alliance Raid", "Guildhest"])
        {
            if (access.EndsWith(' ' + tail, StringComparison.OrdinalIgnoreCase))
            {
                access = access[..^(tail.Length + 1)].TrimEnd();
                break;
            }
        }

        Take(access);

        // "Firebird (Mount)", "Wind-up Kupitan (Minion)", "Haurchefant (Emote)" — a parenthetical
        // the guide adds to say which list the thing lands in.
        var open = access.LastIndexOf('(');
        if (open > 0 && access.EndsWith(')'))
        {
            Take(access[..open].TrimEnd());
        }

        return keys;
    }

    private static void AddQuestChannels(
        GameData game,
        ExcelSheet<Item> items,
        UnlockLinkIndex unlockLinks,
        Action<uint, Candidate?> add)
    {
        var quests = game.GetExcelSheet<Quest>();
        var instanceContent = game.GetExcelSheet<InstanceContent>();
        var gcRanks = GrandCompanyRankNames.Build(game);

        foreach (var q in quests)
        {
            if (q.Name.ExtractText().Length == 0)
            {
                continue;
            }

            // Quest.Reward is an untyped collection discriminated by ItemRewardType. Types 1/3/5
            // are Item rows; 6 and 7 are the crystal and shard currency tables, which are not
            // unlocks and are skipped.
            if (q.ItemRewardType is 1 or 3 or 5)
            {
                foreach (var r in q.Reward)
                {
                    if (r.RowId != 0 && items.GetRowOrDefault(r.RowId) is { } item)
                    {
                        add(q.RowId, FromItem(item, unlockLinks));
                    }
                }
            }

            foreach (var r in q.OptionalItemReward)
            {
                if (r.RowId != 0 && r.ValueNullable is { } item)
                {
                    add(q.RowId, FromItem(item, unlockLinks));
                }
            }

            if (q.EmoteReward.ValueNullable is { } emote && emote.RowId != 0)
            {
                add(q.RowId, new Candidate("Emote", emote.RowId, emote.Name.ExtractText(), "Quest.EmoteReward"));
            }

            if (q.ClassJobUnlock.ValueNullable is { } job && job.RowId != 0)
            {
                add(q.RowId, new Candidate("ClassJob", job.RowId, job.Name.ExtractText(), "Quest.ClassJobUnlock"));
            }

            if (q.OtherReward.ValueNullable is { } other && other.RowId != 0)
            {
                add(
                    q.RowId,
                    new Candidate("QuestRewardOther", other.RowId, other.Name.ExtractText(), "Quest.OtherReward"));
            }

            if (q.InstanceContentUnlock.RowId != 0
                && instanceContent.GetRowOrDefault(q.InstanceContentUnlock.RowId) is { } ic
                && ic.ContentFinderCondition.ValueNullable is { } cfc)
            {
                add(
                    q.RowId,
                    new Candidate(
                        "ContentFinderCondition", cfc.RowId, cfc.Name.ExtractText(), "Quest.InstanceContentUnlock"));
            }

            if (q.GrandCompanyRank.RowId != 0
                && gcRanks.NameFor(q.GrandCompanyRank.RowId, q.GrandCompany.RowId) is { Length: > 0 } rank)
            {
                add(
                    q.RowId,
                    new Candidate("GrandCompanyRank", q.GrandCompanyRank.RowId, rank, "Quest.GrandCompanyRank"));
            }
        }
    }

    /// <summary>The reverse channels: a sheet that owns a feature and carries a
    /// <c>RowRef&lt;Quest&gt;</c> naming what opens it. Only the ones whose identity has a
    /// player-readable name are read — a row nobody can name is a row nothing can display, and
    /// including it would put an unnameable reward on an entry.</summary>
    private static void AddFeatureChannels(GameData game, Action<uint, Candidate?> add)
    {
        foreach (var row in game.GetExcelSheet<ContentFinderCondition>())
        {
            if (row.UnlockType == UnlockCriteriaIsQuest && row.UnlockCriteria.RowId != 0)
            {
                add(
                    row.UnlockCriteria.RowId,
                    new Candidate(
                        "ContentFinderCondition", row.RowId, row.Name.ExtractText(), "CFC.UnlockCriteria"));
            }
        }

        var instanceContent = game.GetExcelSheet<InstanceContent>();
        foreach (var row in game.GetExcelSheet<CSBonusContentIdentifier>())
        {
            if (row.ContentLinkType != ContentLinkInstanceContent
                || row.UnlockQuest0.RowId == 0
                || instanceContent.GetRowOrDefault(row.Content.RowId) is not { } ic
                || ic.ContentFinderCondition.ValueNullable is not { } cfc)
            {
                continue;
            }

            add(
                row.UnlockQuest0.RowId,
                new Candidate(
                    "ContentFinderCondition", cfc.RowId, cfc.Name.ExtractText(), "CSBonusContentIdentifier"));
        }

        foreach (var row in game.GetExcelSheet<ClassJob>())
        {
            add(
                row.UnlockQuest.RowId,
                new Candidate("ClassJob", row.RowId, row.Name.ExtractText(), "ClassJob.UnlockQuest"));
        }

        foreach (var row in game.GetExcelSheet<BeastTribe>())
        {
            add(
                row.IntersocietalQuest.RowId,
                new Candidate("BeastTribe", row.RowId, row.Name.ExtractText(), "BeastTribe.IntersocietalQuest"));
        }

        foreach (var row in game.GetExcelSheet<GatheringSubCategory>())
        {
            add(
                row.Quest.RowId,
                new Candidate(
                    "GatheringSubCategory", row.RowId, row.FolkloreBook.ExtractText(), "GatheringSubCategory.Quest"));
        }

        foreach (var row in game.GetExcelSheet<NotebookDivision>())
        {
            add(
                row.QuestUnlock.RowId,
                new Candidate(
                    "NotebookDivision", row.RowId, row.Name.ExtractText(), "NotebookDivision.QuestUnlock"));
        }

        foreach (var row in game.GetExcelSheet<ContentsNote>())
        {
            add(
                row.ReqUnlock.RowId,
                new Candidate("ContentsNote", row.RowId, row.Name.ExtractText(), "ContentsNote.ReqUnlock"));
        }

        // MobHuntOrderType has no name of its own; the board is named by the key item you are given
        // to record it on, which is what the guide calls it too ("Clan Hunt Board").
        foreach (var row in game.GetExcelSheet<MobHuntOrderType>())
        {
            add(
                row.Quest.RowId,
                new Candidate(
                    "MobHuntOrderType",
                    row.RowId,
                    row.EventItem.ValueNullable?.Name.ExtractText() ?? string.Empty,
                    "MobHuntOrderType.Quest"));
        }

        foreach (var row in game.GetExcelSheet<SatisfactionNpc>())
        {
            add(
                row.QuestRequired.RowId,
                new Candidate(
                    "SatisfactionNpc",
                    row.RowId,
                    row.Npc.ValueNullable?.Singular.ExtractText() ?? string.Empty,
                    "SatisfactionNpc.QuestRequired"));
        }

        // No quest column awards a title. The only RowRef<Title> in the whole schema is
        // Achievement.Title, and an achievement of condition kind 6 keys on a Quest row.
        foreach (var row in game.GetExcelSheet<Achievement>())
        {
            if (row.Type == AchievementTypeQuestCompletion && row.Title.RowId != 0 && row.Key.RowId != 0)
            {
                add(
                    row.Key.RowId,
                    new Candidate(
                        "Title", row.Title.RowId, row.Title.ValueNullable?.Masculine.ExtractText() ?? string.Empty,
                        "Achievement.Title"));
            }
        }
    }

    /// <summary>What an item actually grants, from its <c>ItemAction</c>. Null for an item that
    /// grants nothing enumerable — most quest rewards are gear and gil.
    ///
    /// <para>Shared with <see cref="UnlockEnumeration"/> rather than reimplemented there. The
    /// <c>ItemAction</c> type numbers and the which-column-holds-the-payload rule above are
    /// community-reverse-engineered and do move between Lumina releases; a second copy would be a
    /// second set of answers, and the one that was not being maintained would be the one producing
    /// the coverage figures.</para></summary>
    internal static Candidate? FromItem(Item item, UnlockLinkIndex unlockLinks)
    {
        if (item.ItemAction.ValueNullable is not { } action || action.RowId == 0)
        {
            return null;
        }

        var type = action.Action.RowId;
        var data = action.Data.Count > 0 ? action.Data[0] : (ushort)0;
        var additional = item.AdditionalData.RowId;

        var grantedBy = item.RowId;

        return type switch
        {
            ItemActionMount => Named("Mount", data, unlockLinks.MountName(data), "Item.ItemAction 1322"),
            ItemActionCompanion => Named("Companion", data, unlockLinks.CompanionName(data), "Item.ItemAction 853"),
            ItemActionOrchestrion => Named(
                "Orchestrion", additional, unlockLinks.OrchestrionName(additional), "Item.ItemAction 25183"),
            ItemActionTripleTriadCard => Named(
                "TripleTriadCard", data, unlockLinks.CardName(data), "Item.ItemAction 3357"),
            ItemActionBuddyEquip => Named("BuddyEquip", data, unlockLinks.BardingName(data), "Item.ItemAction 1013"),
            ItemActionOrnament => Named("Ornament", data, unlockLinks.OrnamentName(data), "Item.ItemAction 20086"),
            ItemActionGlasses => Named(
                "Glasses", additional, unlockLinks.GlassesName(additional), "Item.ItemAction 37312"),
            ItemActionFramersKit => new Candidate(
                "Item", item.RowId, item.Name.ExtractText(), "Item.ItemAction 29459", grantedBy),
            ItemActionUnlockLink => unlockLinks.FromUnlockLink(data) is { } linked
                ? linked with { GrantingItemId = grantedBy }
                : null,
            _ => null,
        };

        Candidate? Named(string kind, uint id, string name, string via) =>
            id == 0 || name.Length == 0 ? null : new Candidate(kind, id, name, via, grantedBy);
    }

    /// <summary>One thing a quest is stated to grant.</summary>
    /// <param name="Kind">The sheet that owns the identity.</param>
    /// <param name="Id">The row in it.</param>
    /// <param name="Name">The row's own player-facing name.</param>
    /// <param name="Via">The exact column the claim came from, for the generation report.</param>
    /// <param name="GrantingItemId">The Item row the reward arrived in, when it arrived in one, and
    /// 0 otherwise. This is the whole icon story for an Orchestrion roll: the Orchestrion sheet has
    /// two columns and neither is a picture, so the only thing that can be drawn is the roll you
    /// are handed.</param>
    internal sealed record Candidate(string Kind, uint Id, string Name, string Via, uint GrantingItemId = 0);

    /// <summary>A resolved reward and the rule that chose it.</summary>
    internal sealed record Match(Candidate Candidate, string How)
    {
        public UnlockReward Reward => new(Candidate.Kind, Candidate.Id, Candidate.Name);
    }
}

/// <summary>What the caller knows about one catalogue entry when it asks for its reward.</summary>
internal sealed class RewardJoin
{
    /// <summary>The caller's own handle for the entry, echoed back on the answer. The generator
    /// uses the entry's index, which is the only thing about a catalogue entry guaranteed unique.
    /// </summary>
    public string Ref { get; init; } = string.Empty;

    /// <summary>The entry's <c>unlock</c> field, exactly as the catalogue writes it.</summary>
    public string Unlock { get; init; } = string.Empty;

    /// <summary>The entry's <c>type</c>, used only to break a tie between rewards of different
    /// kinds.</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Every Quest row the entry is bound to.</summary>
    public List<uint> QuestRowIds { get; init; } = [];

    /// <summary>ContentFinderCondition rows the entry's own label link resolved to — the duty the
    /// entry is <i>about</i>, which the resolver already established.</summary>
    public List<RewardJoinDuty> Duties { get; init; } = [];

    /// <summary>The identity the caller already knows, for an entry the game-data enumeration
    /// proposed rather than the guide. Null for every curated entry, and when it is set the three
    /// inference rules are skipped entirely — see <see cref="RewardIndex.Resolve"/>.</summary>
    public RewardJoinIdentity? Identity { get; init; }
}

/// <summary>A reward the caller states outright: the sheet, the row, and the name that row carries.
/// </summary>
internal sealed class RewardJoinIdentity
{
    public string Kind { get; init; } = string.Empty;

    public uint Id { get; init; }

    public string Name { get; init; } = string.Empty;
}

internal sealed class RewardJoinDuty
{
    public uint RowId { get; init; }

    public string Name { get; init; } = string.Empty;
}
