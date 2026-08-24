// Everything the GAME says is unlockable, with no wiki input at all.
//
// WHY THIS EXISTS
// The catalogue's EXISTENCE set comes from one wiki guide, so anything the guide omits is
// something the pipeline can never learn about. That is not hypothetical: a sixth trophy-mount
// quest was missing until another plugin's data revealed it, and the game's own sheets name 151
// aether currents against the 30 rows the guide lists. This walk is the other half of the answer —
// the game proposes, and the generator diffs the proposal against the catalogue it is about to
// emit. The diff is committed as data/coverage.json so CI can hold the two together with no game
// installation.
//
// WHAT IT IS NOT
// It is not a source the catalogue is built from. Nothing here writes an entry, and nothing here
// decides whether a row is worth shipping — that is policy, it lives in data/coverage-policy.mjs
// where it can be read and argued with, and it is applied on the JavaScript side. This file only
// states facts: which sheet owns the identity, which row it is, what the game calls it, and which
// quest the game says opens it.
//
// HOW THE CHANNELS WERE FOUND
// Mechanically, not by guesswork: every property in Lumina.Excel.Sheets whose type is
// RowRef<Quest> or Collection<RowRef<Quest>> is a place where the game states "this is gated on a
// quest". Sixty-eight sheets carry one. The ones below are the subset that owns a player-facing
// feature; the rest are dialogue, shop and map plumbing. Two are deliberately left out and it is
// worth saying why, because a later reader will find them: PreHandler.UnlockQuest (86 rows) gates
// an NPC interaction and has no identity a player would recognise, and Aetheryte.RequiredQuest
// (13 rows) names aetherytes the catalogue reaches through its zone entries instead.
using System.Globalization;
using Lumina;
using Lumina.Excel.Sheets;
using Wayfarer.Core.Unlocks;

namespace Wayfarer.CatalogueGen;

/// <summary>One thing the game states is unlockable, and the quest it states opens it.</summary>
internal sealed class EnumeratedUnlock
{
    /// <summary>The kind of unlock, in the vocabulary data/coverage-policy.mjs is written
    /// against.</summary>
    public string Channel { get; init; } = string.Empty;

    /// <summary>The sheet that owns the identity. Together with <see cref="IdentityId"/> this is
    /// the whole point of the exercise: an identity is a row, never a name.</summary>
    public string IdentityKind { get; init; } = string.Empty;

    public uint IdentityId { get; init; }

    /// <summary>The subrow, for the two channels whose sheet has them — a row id alone is not an
    /// identity there and collapsing the subrows would undercount the channel. Null everywhere
    /// else.</summary>
    public ushort? IdentitySubrowId { get; init; }

    /// <summary>The row's own player-facing name, or empty when the sheet has none. Empty is a
    /// fact worth carrying rather than papering over: a row nobody can name is a row nothing can
    /// display, and the policy excludes those by rule.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The gate quest the game states, or null when it states none. Null is common and
    /// expected — for 577 of the 857 duties the unlock condition is server-side, which is exactly
    /// the gap the wiki guide exists to fill.</summary>
    public uint? QuestRowId { get; init; }

    /// <summary>The gate quest's display name, empty when there is no gate or the row it names is
    /// absent from the live sheet.</summary>
    public string QuestName { get; init; } = string.Empty;

    /// <summary>Whether <see cref="QuestRowId"/> names a row that exists and is named in the sheet
    /// this install has. False means the gate points at unreleased or removed content, which the
    /// policy excludes by rule.</summary>
    public bool GateLive { get; init; }

    /// <summary>The gate quest's accept level, null when there is no live gate.</summary>
    public int? Level { get; init; }

    /// <summary>The gate quest's <c>Festival</c> row, 0 for a quest available all year. Non-zero
    /// makes the row seasonal, which is decidable from data and is why the policy can treat it as
    /// a rule rather than a list.</summary>
    public uint Festival { get; init; }

    /// <summary>The exact column the claim rests on, so a disputed row can be checked against the
    /// sheet it came from.</summary>
    public string Via { get; init; } = string.Empty;

    /// <summary><c>ContentFinderCondition.ContentType</c>'s own name, for duty rows only. The
    /// policy is written against these words — "Gold Saucer", "PvP", "Quest Battles" — because
    /// they are what decides whether a duty is a kind the catalogue lists at all.</summary>
    public string ContentType { get; init; } = string.Empty;

    /// <summary><c>ContentFinderCondition.IsInDutyFinder</c>, for duty rows only. A duty that is
    /// neither in the finder nor gated on a quest is retired.</summary>
    public bool? InDutyFinder { get; init; }
}

/// <summary>The game-data side of the completeness check: every channel walked once, in row
/// order, so the output is a function of the installed sqpack alone.</summary>
internal static class UnlockEnumeration
{
    /// <summary>A <c>ContentFinderCondition.Content</c> reference is untyped and spans four id
    /// spaces; only link type 1 is an <c>InstanceContent</c> row. <c>CSBonusContentIdentifier</c>
    /// carries its own copy of the same discriminator and it has to be checked separately —
    /// skipping either check silently binds a different duty's row.</summary>
    private const byte ContentLinkInstanceContent = 1;

    /// <summary><c>ContentFinderCondition.UnlockType</c>: only 1 means the criteria row is a
    /// Quest. The other three values appear once each.</summary>
    private const byte UnlockCriteriaIsQuest = 1;

    /// <summary>The achievement condition kind whose <c>Key</c> is a Quest row. There is exactly
    /// one <c>RowRef&lt;Title&gt;</c> in the whole schema — <c>Achievement.Title</c> — so this join
    /// is the only way to enumerate a quest-granted title at all.</summary>
    private const byte AchievementTypeQuestCompletion = 6;

    /// <summary><c>QuestRewardOther</c> row 2, <c>Aether Current</c>. It accounts for 150 of the
    /// column's 163 uses, and every one of them is already enumerated per-current by
    /// <c>AetherCurrent.Quest</c>. Counting it here as well would double count the same unlock,
    /// which is the same reason the aether-current-set aggregate is excluded by policy.</summary>
    private const uint QuestRewardOtherAetherCurrent = 2;

    /// <summary>What each <c>ItemAction</c>-borne reward kind is called in the channel vocabulary.
    /// The taxonomy itself lives in <see cref="RewardIndex.FromItem"/> and is deliberately not
    /// repeated here — one place decides what an item grants.</summary>
    private static readonly Dictionary<string, string> ChannelForItemRewardKind = new(StringComparer.Ordinal)
    {
        ["Mount"] = "mount",
        ["Companion"] = "minion",
        ["Orchestrion"] = "orchestrion",
        ["TripleTriadCard"] = "triple-triad-card",
        ["BuddyEquip"] = "barding",
        ["Ornament"] = "fashion-accessory",
        ["Glasses"] = "facewear",
        ["Item"] = "framers-kit",
        ["Emote"] = "emote",
        ["CharaMakeCustomize"] = "hairstyle",
    };

    public static List<EnumeratedUnlock> Build(GameData game)
    {
        var quests = game.GetExcelSheet<Quest>() ?? throw new InvalidOperationException("no Quest sheet");
        var gates = QuestGates.Build(quests);
        var rows = new List<EnumeratedUnlock>();

        AddQuestRewardChannels(game, quests, gates, rows);
        AddDuties(game, gates, rows);
        AddTitles(game, gates, rows);
        AddFeatureChannels(game, gates, rows);
        return rows;
    }

    /// <summary>The Quest sheet's own reward columns, plus everything reached through
    /// <c>Quest.Reward</c> → <c>Item.ItemAction</c>.</summary>
    private static void AddQuestRewardChannels(
        GameData game,
        Lumina.Excel.ExcelSheet<Quest> quests,
        QuestGates gates,
        List<EnumeratedUnlock> rows)
    {
        var items = game.GetExcelSheet<Item>();
        var unlockLinks = UnlockLinkIndex.Build(game);
        var otherRewardSeen = new HashSet<uint>();

        foreach (var q in quests)
        {
            if (q.Name.ExtractText().Length == 0)
            {
                continue;
            }

            // Quest.Reward is an untyped collection discriminated by ItemRewardType. Types 1/3/5
            // are Item rows; 6 and 7 are the crystal and shard currency tables, which are not
            // unlocks.
            if (q.ItemRewardType is 1 or 3 or 5)
            {
                foreach (var r in q.Reward)
                {
                    if (r.RowId != 0 && items.GetRowOrDefault(r.RowId) is { } item)
                    {
                        AddItemReward(gates, rows, q.RowId, RewardIndex.FromItem(item, unlockLinks));
                    }
                }
            }

            foreach (var r in q.OptionalItemReward)
            {
                if (r.RowId != 0 && r.ValueNullable is { } item)
                {
                    AddItemReward(gates, rows, q.RowId, RewardIndex.FromItem(item, unlockLinks));
                }
            }

            if (q.EmoteReward.ValueNullable is { RowId: not 0 } emote)
            {
                rows.Add(gates.Row(
                    "emote", "Emote", emote.RowId, emote.Name.ExtractText(), q.RowId, "Quest.EmoteReward"));
            }

            if (q.ActionReward.ValueNullable is { RowId: not 0 } action)
            {
                rows.Add(gates.Row(
                    "action", "Action", action.RowId, action.Name.ExtractText(), q.RowId, "Quest.ActionReward"));
            }

            foreach (var g in q.GeneralActionReward)
            {
                if (g.ValueNullable is { RowId: not 0 } general)
                {
                    rows.Add(gates.Row(
                        "general-action",
                        "GeneralAction",
                        general.RowId,
                        general.Name.ExtractText(),
                        q.RowId,
                        "Quest.GeneralActionReward"));
                }
            }

            if (q.ClassJobUnlock.ValueNullable is { RowId: not 0 } job)
            {
                rows.Add(gates.Row(
                    "job", "ClassJob", job.RowId, job.Name.ExtractText(), q.RowId, "Quest.ClassJobUnlock"));
            }

            // QuestRewardOther is the entire "system unlock" vocabulary the Quest sheet has — 18
            // rows, of which one is Aether Current and the rest are the Aether Compass, Wondrous
            // Tails, Spearfishing and the job soul stones. It is not a general system-unlock
            // table, which is why the catalogue's `system` entries have to stay curated. Deduped
            // on the reward row: a soul stone granted by one quest is one unlock, and the column's
            // 163 uses collapse to 13 identities.
            if (q.OtherReward.ValueNullable is { RowId: not 0 } other
                && other.RowId != QuestRewardOtherAetherCurrent
                && otherRewardSeen.Add(other.RowId))
            {
                rows.Add(gates.Row(
                    "system",
                    "QuestRewardOther",
                    other.RowId,
                    other.Name.ExtractText(),
                    q.RowId,
                    "Quest.OtherReward"));
            }
        }
    }

    private static void AddItemReward(
        QuestGates gates,
        List<EnumeratedUnlock> rows,
        uint questRowId,
        RewardIndex.Candidate? candidate)
    {
        if (candidate is null || !ChannelForItemRewardKind.TryGetValue(candidate.Kind, out var channel))
        {
            return;
        }

        rows.Add(gates.Row(
            channel, candidate.Kind, candidate.Id, candidate.Name, questRowId, candidate.Via));
    }

    /// <summary>Every named duty, and the gate quest where the game states one.
    ///
    /// <para>The duty SET is fully enumerable; the duty → quest link mostly is not. Three joins
    /// state it, they are disjoint, and their union covers 280 of 857 rows. For the rest the
    /// condition is server-side — the client shows locked prose from
    /// <c>ContentFinderConditionTransient</c>, which is a string and not a reference. That is the
    /// single strongest argument for keeping the wiki guide: its whole reason to exist is the fact
    /// the game withholds here.</para></summary>
    private static void AddDuties(GameData game, QuestGates gates, List<EnumeratedUnlock> rows)
    {
        var cfc = game.GetExcelSheet<ContentFinderCondition>();
        var instanceContent = game.GetExcelSheet<InstanceContent>();
        var quests = game.GetExcelSheet<Quest>();

        // Gate quest per ContentFinderCondition row, from the three joins in the order they are
        // trusted: the duty row's own criteria column first, then the two tables that point at it.
        var gateFor = new Dictionary<uint, (uint QuestRowId, string Via)>();
        void Claim(uint cfcRowId, uint questRowId, string via)
        {
            if (cfcRowId != 0 && questRowId != 0)
            {
                gateFor.TryAdd(cfcRowId, (questRowId, via));
            }
        }

        foreach (var row in cfc)
        {
            if (row.UnlockType == UnlockCriteriaIsQuest)
            {
                Claim(row.RowId, row.UnlockCriteria.RowId, "ContentFinderCondition.UnlockCriteria");
            }
        }

        foreach (var q in quests)
        {
            if (q.Name.ExtractText().Length != 0
                && q.InstanceContentUnlock.RowId != 0
                && instanceContent.GetRowOrDefault(q.InstanceContentUnlock.RowId) is { } ic
                && ic.ContentFinderCondition.ValueNullable is { RowId: not 0 } unlocked)
            {
                Claim(unlocked.RowId, q.RowId, "Quest.InstanceContentUnlock");
            }
        }

        // CSBonusContentIdentifier is the find of the enumeration work: 218 rows, 159 of which
        // resolve to a live duty and name the quest that opens it. It more than triples what
        // Quest.InstanceContentUnlock alone gives. Its own ContentLinkType must be checked, not
        // the duty row's.
        foreach (var row in game.GetExcelSheet<CSBonusContentIdentifier>())
        {
            if (row.ContentLinkType == ContentLinkInstanceContent
                && row.UnlockQuest0.RowId != 0
                && instanceContent.GetRowOrDefault(row.Content.RowId) is { } ic
                && ic.ContentFinderCondition.ValueNullable is { RowId: not 0 } gated)
            {
                Claim(gated.RowId, row.UnlockQuest0.RowId, "CSBonusContentIdentifier.UnlockQuest0");
            }
        }

        foreach (var row in cfc)
        {
            var name = row.Name.ExtractText();
            if (name.Length == 0)
            {
                continue;
            }

            var hasGate = gateFor.TryGetValue(row.RowId, out var gate);
            rows.Add(gates.Row(
                "duty",
                "ContentFinderCondition",
                row.RowId,
                name,
                hasGate ? gate.QuestRowId : null,
                hasGate ? gate.Via : "none",
                row.ContentType.ValueNullable?.Name.ExtractText() ?? string.Empty,
                row.IsInDutyFinder));
        }
    }

    /// <summary>Quest-completion titles. No quest column awards a title, so the only enumerable
    /// join runs the other way: an <c>Achievement</c> of condition kind 6 whose <c>Key</c> is a
    /// Quest row.</summary>
    private static void AddTitles(GameData game, QuestGates gates, List<EnumeratedUnlock> rows)
    {
        foreach (var row in game.GetExcelSheet<Achievement>())
        {
            if (row.Type != AchievementTypeQuestCompletion || row.Title.RowId == 0 || row.Key.RowId == 0)
            {
                continue;
            }

            // Only kind-6 achievements whose Key resolves to a NAMED quest row are quest titles;
            // the same column on other kinds holds kill counts and gil amounts.
            if (!gates.IsLiveQuest(row.Key.RowId))
            {
                continue;
            }

            rows.Add(gates.Row(
                "title",
                "Title",
                row.Title.RowId,
                row.Title.ValueNullable?.Masculine.ExtractText() ?? string.Empty,
                row.Key.RowId,
                "Achievement.Title"));
        }
    }

    /// <summary>The reverse channels: a sheet that owns a feature and carries a
    /// <c>RowRef&lt;Quest&gt;</c> naming what opens it.</summary>
    private static void AddFeatureChannels(GameData game, QuestGates gates, List<EnumeratedUnlock> rows)
    {
        foreach (var row in game.GetExcelSheet<AetherCurrent>())
        {
            if (row.Quest.RowId != 0)
            {
                rows.Add(gates.Row(
                    "aether-current",
                    "AetherCurrent",
                    row.RowId,
                    gates.QuestDisplayName(row.Quest.RowId),
                    row.Quest.RowId,
                    "AetherCurrent.Quest"));
            }
        }

        // The per-zone current SET, named by its territory. It states no quest of its own — it is
        // an aggregate over the currents above, and the policy excludes it for exactly that
        // reason rather than because it is uninteresting.
        var territoryForFlagSet = new Dictionary<uint, string>();
        var territoryForMountSpeed = new Dictionary<uint, string>();
        foreach (var t in game.GetExcelSheet<TerritoryType>())
        {
            var place = t.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
            if (place.Length == 0)
            {
                continue;
            }

            if (t.AetherCurrentCompFlgSet.RowId != 0)
            {
                territoryForFlagSet.TryAdd(t.AetherCurrentCompFlgSet.RowId, place);
            }

            if (t.MountSpeed.RowId != 0)
            {
                territoryForMountSpeed.TryAdd(t.MountSpeed.RowId, place);
            }
        }

        foreach (var row in game.GetExcelSheet<AetherCurrentCompFlgSet>())
        {
            if (row.AetherCurrents.Any(c => c.RowId != 0))
            {
                rows.Add(gates.Row(
                    "aether-current-set",
                    "AetherCurrentCompFlgSet",
                    row.RowId,
                    territoryForFlagSet.GetValueOrDefault(row.RowId, string.Empty),
                    null,
                    "AetherCurrentCompFlgSet.AetherCurrents"));
            }
        }

        foreach (var row in game.GetExcelSheet<MountSpeed>())
        {
            if (row.Quest.RowId != 0)
            {
                rows.Add(gates.Row(
                    "flight",
                    "MountSpeed",
                    row.RowId,
                    territoryForMountSpeed.GetValueOrDefault(row.RowId, string.Empty),
                    row.Quest.RowId,
                    "MountSpeed.Quest"));
            }
        }

        foreach (var row in game.GetExcelSheet<ClassJob>())
        {
            if (row.UnlockQuest.RowId != 0)
            {
                rows.Add(gates.Row(
                    "job", "ClassJob", row.RowId, row.Name.ExtractText(), row.UnlockQuest.RowId,
                    "ClassJob.UnlockQuest"));
            }
        }

        foreach (var row in game.GetExcelSheet<BeastTribe>())
        {
            if (row.IntersocietalQuest.RowId != 0)
            {
                rows.Add(gates.Row(
                    "allied-society", "BeastTribe", row.RowId, row.Name.ExtractText(),
                    row.IntersocietalQuest.RowId, "BeastTribe.IntersocietalQuest"));
            }
        }

        foreach (var row in game.GetExcelSheet<SatisfactionNpc>())
        {
            if (row.QuestRequired.RowId != 0)
            {
                rows.Add(gates.Row(
                    "custom-delivery",
                    "SatisfactionNpc",
                    row.RowId,
                    row.Npc.ValueNullable?.Singular.ExtractText() ?? string.Empty,
                    row.QuestRequired.RowId,
                    "SatisfactionNpc.QuestRequired"));
            }
        }

        // TripleTriadCardResident.Quest is populated for 475 of 476 rows: it is the NPC-match
        // prerequisite, not "the quest that awards this card". Enumerating it is right — the
        // column exists and says what it says — and the policy is where it gets thrown out, so
        // that the reason is written down once instead of being lost in a sheet walk.
        var cards = game.GetExcelSheet<TripleTriadCard>();
        foreach (var row in game.GetExcelSheet<TripleTriadCardResident>())
        {
            if (row.Quest.RowId != 0)
            {
                rows.Add(gates.Row(
                    "triple-triad-card",
                    "TripleTriadCard",
                    row.RowId,
                    cards.GetRowOrDefault(row.RowId)?.Name.ExtractText() ?? string.Empty,
                    row.Quest.RowId,
                    "TripleTriadCardResident.Quest"));
            }
        }

        // One row per opponent, not per prerequisite. TripleTriad.PreviousQuest is a list joined by
        // PreviousQuestJoin — several quests that together gate ONE match — so taking the first is
        // enumerating the opponent, and taking all of them would enumerate the same opponent
        // several times.
        foreach (var row in game.GetExcelSheet<TripleTriad>())
        {
            if (row.PreviousQuest.FirstOrDefault(p => p.RowId != 0) is { RowId: not 0 } prev)
            {
                rows.Add(gates.Row(
                    "triple-triad-npc", "TripleTriad", row.RowId, string.Empty, prev.RowId,
                    "TripleTriad.PreviousQuest"));
            }
        }

        foreach (var row in game.GetExcelSheet<GatheringSubCategory>())
        {
            var book = row.FolkloreBook.ExtractText();
            if (row.Quest.RowId != 0 && book.Length != 0)
            {
                rows.Add(gates.Row(
                    "gathering-folklore", "GatheringSubCategory", row.RowId, book, row.Quest.RowId,
                    "GatheringSubCategory.Quest"));
            }
        }

        foreach (var row in game.GetExcelSheet<NotebookDivision>())
        {
            if (row.QuestUnlock.RowId != 0)
            {
                rows.Add(gates.Row(
                    "crafting-log-division", "NotebookDivision", row.RowId, row.Name.ExtractText(),
                    row.QuestUnlock.RowId, "NotebookDivision.QuestUnlock"));
            }
        }

        foreach (var row in game.GetExcelSheet<ContentsNote>())
        {
            if (row.ReqUnlock.RowId != 0)
            {
                rows.Add(gates.Row(
                    "challenge-log", "ContentsNote", row.RowId, row.Name.ExtractText(), row.ReqUnlock.RowId,
                    "ContentsNote.ReqUnlock"));
            }
        }

        // MobHuntOrderType has no name of its own; the board is named by the key item you record
        // it on, which is what the guide calls it too ("Clan Hunt Board").
        foreach (var row in game.GetExcelSheet<MobHuntOrderType>())
        {
            if (row.Quest.RowId != 0)
            {
                rows.Add(gates.Row(
                    "hunt-board",
                    "MobHuntOrderType",
                    row.RowId,
                    row.EventItem.ValueNullable?.Name.ExtractText() ?? string.Empty,
                    row.Quest.RowId,
                    "MobHuntOrderType.Quest"));
            }
        }

        foreach (var row in game.GetExcelSheet<DpsChallengeOfficer>())
        {
            if (row.UnlockQuest.RowId == 0)
            {
                continue;
            }

            var challenge = row.ChallengeName.FirstOrDefault(c => c.RowId != 0);
            rows.Add(gates.Row(
                "stone-sky-sea",
                "DpsChallengeOfficer",
                row.RowId,
                challenge.ValueNullable?.Name.ExtractText() ?? string.Empty,
                row.UnlockQuest.RowId,
                "DpsChallengeOfficer.UnlockQuest"));
        }

        foreach (var row in game.GetExcelSheet<VVDData>())
        {
            if (row.UnlockQuest.RowId != 0)
            {
                rows.Add(gates.Row(
                    "variant-dungeon",
                    "VVDData",
                    row.RowId,
                    row.ContentFinderCondition.ValueNullable?.Name.ExtractText() ?? string.Empty,
                    row.UnlockQuest.RowId,
                    "VVDData.UnlockQuest"));
            }
        }

        // GrandCompanyRank has no name column; the words live in per-company text sheets keyed on
        // the same row id, and the company is known from which of the three quest columns the row
        // was reached through.
        var rankNames = GrandCompanyRankNames.Build(game);
        foreach (var row in game.GetExcelSheet<GrandCompanyRank>())
        {
            foreach (var (company, quest) in (( uint Company, uint Quest )[])
                [
                    (1u, row.QuestMaelstrom.RowId),
                    (2u, row.QuestSerpents.RowId),
                    (3u, row.QuestFlames.RowId),
                ])
            {
                if (quest != 0)
                {
                    rows.Add(gates.Row(
                        "grand-company-rank", "GrandCompanyRank", row.RowId,
                        rankNames.NameFor(row.RowId, company), quest, "GrandCompanyRank.Quest"));
                }
            }
        }

        foreach (var row in game.GetExcelSheet<Buddy>())
        {
            foreach (var quest in (uint[])[row.QuestRequirement1.RowId, row.QuestRequirement2.RowId])
            {
                if (quest != 0)
                {
                    rows.Add(gates.Row(
                        "chocobo-companion", "Buddy", row.RowId, string.Empty, quest,
                        "Buddy.QuestRequirement"));
                }
            }
        }

        foreach (var row in game.GetExcelSheet<Trait>())
        {
            if (row.Quest.RowId != 0)
            {
                rows.Add(gates.Row(
                    "trait", "Trait", row.RowId, row.Name.ExtractText(), row.Quest.RowId, "Trait.Quest"));
            }
        }

        foreach (var row in game.GetExcelSheet<CraftAction>())
        {
            if (row.QuestRequirement.RowId != 0)
            {
                rows.Add(gates.Row(
                    "craft-action", "CraftAction", row.RowId, row.Name.ExtractText(),
                    row.QuestRequirement.RowId, "CraftAction.QuestRequirement"));
            }
        }

        foreach (var row in game.GetExcelSheet<ItemStainCondition>())
        {
            if (row.UnlockQuest.RowId != 0)
            {
                rows.Add(gates.Row(
                    "dye-slot", "ItemStainCondition", row.RowId, string.Empty, row.UnlockQuest.RowId,
                    "ItemStainCondition.UnlockQuest"));
            }
        }

        foreach (var row in game.GetExcelSheet<Fate>())
        {
            if (row.RequiredQuest.RowId != 0)
            {
                rows.Add(gates.Row(
                    "fate", "Fate", row.RowId, row.Name.ExtractText(), row.RequiredQuest.RowId,
                    "Fate.RequiredQuest"));
            }
        }

        // Two subrow sheets. Their identity is (row, subrow) and neither carries a name a player
        // would recognise, so the row id alone is the identity and the name is left empty for the
        // policy's unnameable rule to catch.
        foreach (var row in game.GetSubrowExcelSheet<AkatsukiNote>())
        {
            foreach (var sub in row)
            {
                if (sub.UnlockOnQuest.RowId != 0)
                {
                    rows.Add(gates.Row(
                        "occult-note", "AkatsukiNote", sub.RowId, string.Empty, sub.UnlockOnQuest.RowId,
                        "AkatsukiNote.UnlockOnQuest", subrowId: sub.SubrowId));
                }
            }
        }

        var costumes = game.GetSubrowExcelSheet<EmjCostume>()
            ?? throw new InvalidOperationException("no EmjCostume sheet");
        foreach (var row in costumes)
        {
            foreach (var sub in row)
            {
                if (sub.UnlockQuest.RowId != 0)
                {
                    rows.Add(gates.Row(
                        "emj-costume", "EmjCostume", sub.RowId, string.Empty, sub.UnlockQuest.RowId,
                        "EmjCostume.UnlockQuest", subrowId: sub.SubrowId));
                }
            }
        }
    }

    /// <summary>Quest facts every channel needs — is the gate live, what is it called, what level
    /// is it, and is it seasonal. Built once because most channels ask about the same few hundred
    /// rows.</summary>
    private sealed class QuestGates
    {
        private readonly Dictionary<uint, (string Name, int Level, uint Festival)> live;

        private QuestGates(Dictionary<uint, (string, int, uint)> live) => this.live = live;

        public static QuestGates Build(Lumina.Excel.ExcelSheet<Quest> quests)
        {
            var live = new Dictionary<uint, (string, int, uint)>();
            foreach (var q in quests)
            {
                var raw = q.Name.ExtractText();
                if (raw.Length == 0)
                {
                    continue;
                }

                live[q.RowId] = (
                    QuestNameKey.Display(raw),
                    q.ClassJobLevel[0] + q.QuestLevelOffset,
                    q.Festival.RowId);
            }

            return new QuestGates(live);
        }

        public bool IsLiveQuest(uint rowId) => this.live.ContainsKey(rowId);

        public string QuestDisplayName(uint rowId) =>
            this.live.TryGetValue(rowId, out var f) ? f.Name : string.Empty;

        public EnumeratedUnlock Row(
            string channel,
            string identityKind,
            uint identityId,
            string name,
            uint? questRowId,
            string via,
            string contentType = "",
            bool? inDutyFinder = null,
            ushort? subrowId = null)
        {
            var gate = questRowId is { } id && this.live.TryGetValue(id, out var f) ? f : default;
            var gateLive = questRowId is { } q && this.live.ContainsKey(q);
            return new EnumeratedUnlock
            {
                Channel = channel,
                IdentityKind = identityKind,
                IdentityId = identityId,
                IdentitySubrowId = subrowId,
                Name = name,
                QuestRowId = questRowId,
                QuestName = gate.Name ?? string.Empty,
                GateLive = gateLive,
                Level = gateLive ? gate.Level : null,
                Festival = gateLive ? gate.Festival : 0,
                Via = via,
                ContentType = contentType,
                InDutyFinder = inDutyFinder,
            };
        }
    }
}

/// <summary>The <c>enumerate</c> verb's answer. Counts per channel are emitted alongside the rows
/// so a channel that silently drops to zero — the failure mode that matters when a Lumina release
/// renames a column — is visible in the diff without reading 3,000 rows.</summary>
internal sealed class EnumerateResponse
{
    public string Sqpack { get; init; } = string.Empty;

    public int QuestRowCount { get; init; }

    public SortedDictionary<string, int> ChannelCounts { get; init; } = [];

    public List<EnumeratedUnlock> Unlocks { get; init; } = [];

    public static EnumerateResponse From(string sqpack, GameData game)
    {
        var rows = UnlockEnumeration.Build(game);
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            counts[row.Channel] = counts.GetValueOrDefault(row.Channel) + 1;
        }

        var quests = game.GetExcelSheet<Quest>();
        return new EnumerateResponse
        {
            Sqpack = sqpack,
            QuestRowCount = quests.Count,
            ChannelCounts = counts,

            // Deterministic order, independent of the order the channels happen to be walked in,
            // so the committed artefact is a function of the game data alone.
            Unlocks =
            [
                .. rows
                    .OrderBy(r => r.Channel, StringComparer.Ordinal)
                    .ThenBy(r => r.IdentityId)
                    .ThenBy(r => r.IdentitySubrowId ?? 0)
                    .ThenBy(r => r.QuestRowId ?? 0)
                    .ThenBy(r => r.Via, StringComparer.Ordinal),
            ],
        };
    }

    /// <summary>A one-line summary for the generator's console output.</summary>
    public string Summary() => string.Join(
        ", ",
        this.ChannelCounts.Select(c => $"{c.Key}={c.Value.ToString(CultureInfo.InvariantCulture)}"));
}
