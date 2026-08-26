using System.Security.Cryptography;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Wayfarer.Core.Unlocks.Gates;
using Wayfarer.Core.Unlocks.Live;

namespace Wayfarer;

/// <summary>The three progress reads the client does not simply have lying around, and the rules
/// that keep them honest.
///
/// <para>Two of them — achievements and Shared FATE rank — are <b>request-gated</b>: the client
/// holds nothing until it has asked the server, and an unasked table reads as "you have done none
/// of it". One — Eureka elemental level and Bozja resistance rank — is <b>context-limited</b>: it
/// lives on a director object that exists only while the player is standing in the zone.</para>
///
/// <para>Asking the server for the player's own data is ordinary reading, and this asks: once per
/// character, when the list is first computed for them, and only for the kinds the loaded
/// catalogue actually contains. Never on a timer, never retried, and never for something nothing
/// would read — which today means it asks for nothing at all.</para>
///
/// <para>What is remembered, and what deliberately is not: Bozja's resistance rank, Shared FATE
/// rank and achievement completion are all non-decreasing, so an observation of one stays a valid
/// lower bound and can answer "yes, you meet that" long after the reading. Eureka's elemental
/// level can go DOWN — a death not raised in time costs experience, and from level 11 that can
/// delevel the character — so it is excluded from remembering entirely and is simply unreadable
/// outside Eureka.</para></summary>
internal sealed unsafe class UnlockLiveProgress
{
    /// <summary>The observation store's whole vocabulary, per kind, so two kinds cannot collide on
    /// an id. Not user-visible.</summary>
    private const string AchievementKind = "achievementComplete";

    private const string SharedFateKind = "sharedFateRankAtLeast";
    private const string BozjaKind = "zoneProgressAtLeast.bozja";

    /// <summary>The Achievement agent's FATE progress tabs. Three, fixed by the struct.</summary>
    private const byte FateProgressTabCount = 3;

    /// <summary>Bozja's rank has no per-zone id — there is one resistance rank — so the store's
    /// id slot is a constant for it.</summary>
    private const uint SingleValueId = 0;

    private readonly ObservationStore store = new();
    private readonly ObservedFloor<uint> achievements;
    private readonly ObservedFloor<uint> sharedFate;
    private readonly ObservedFloor<uint> bozja;

    /// <summary>Random per process, and never written anywhere. The store lives in memory only, so
    /// this exists to keep a real character identifier from being the key even in a crash dump or
    /// a pasted log: two runs produce unrelated keys for the same character, and nothing resembling
    /// a name, world or content id is retained.</summary>
    private readonly byte[] salt = RandomNumberGenerator.GetBytes(32);

    private ulong requestedFor;

    public UnlockLiveProgress()
    {
        achievements = new ObservedFloor<uint>(
            new AchievementSource(), store, AchievementKind, id => id, () => DateTimeOffset.UtcNow);
        sharedFate = new ObservedFloor<uint>(
            new SharedFateSource(), store, SharedFateKind, id => id, () => DateTimeOffset.UtcNow);
        bozja = new ObservedFloor<uint>(
            new BozjaRankSource(), store, BozjaKind, id => id, () => DateTimeOffset.UtcNow);
    }

    /// <summary>Who is logged in, as the client's own 64-bit character id. Zero at the title
    /// screen, which is why every caller checks it.</summary>
    private static ulong LocalContentId =>
        PlayerState.Instance() is var state && state != null ? state->ContentId : 0;

    /// <summary>A stable, non-reversible key for the character currently logged in, so one
    /// character's observations are never visible to another. Recomputed only when the character
    /// changes.</summary>
    private string CharacterKey =>
        Convert.ToHexString(HMACSHA256.HashData(salt, BitConverter.GetBytes(LocalContentId)), 0, 8);

    /// <summary>Null means "we cannot tell", which is the answer whenever the achievement table has
    /// never been fetched for this character and nothing was observed earlier in the session.</summary>
    public bool? IsAchievementComplete(uint achievementId) =>
        achievements.TryAtLeast(CharacterKey, achievementId, 1, out var met) ? met : null;

    /// <summary>Which of the two unknowns a title read that could not answer is. Reported rather
    /// than collapsed, because "still on its way" and "nothing has asked" are different things for a
    /// player to be told, and neither of them is "you have not earned it".
    ///
    /// <para>The decision itself is not here — it is <c>UnlockService.IsTitleUnlocked</c>, through
    /// Dalamud's own <c>IUnlockState</c>, which is the supported reader and takes a typed row. This
    /// is the one fact that service does not carry: it has <c>IsTitleListLoaded</c>, which is the
    /// boundary between known and not, and nothing for the difference between a request in flight
    /// and no request at all.</para></summary>
    public TitleDataState TitleData()
    {
        // Both sources, because either can answer a title gate: the title list directly, and the
        // achievement table through the achievement that awards it. Reporting only the title list
        // would tell a player nothing has been asked for at the exact moment the plugin's own
        // achievement request is in flight.
        var ui = UIState.Instance();
        var titlesKnown = ui != null && ui->TitleList.DataReceived;
        var titlesAsked = ui != null && (ui->TitleList.DataRequested || ui->TitleList.DataPending);

        var agent = Achievement.Instance();
        var achievementsKnown = agent != null && agent->IsLoaded();
        var achievementsAsked = agent != null && agent->State == Achievement.AchievementState.Requested;

        if (titlesKnown || achievementsKnown)
        {
            return TitleDataState.Known;
        }

        return titlesAsked || achievementsAsked
            ? TitleDataState.Pending
            : TitleDataState.NotRequested;
    }

    /// <inheritdoc cref="IsAchievementComplete"/>
    public bool? SharedFateRankAtLeast(uint territoryTypeId, int rank) =>
        sharedFate.TryAtLeast(CharacterKey, territoryTypeId, rank, out var met) ? met : null;

    /// <summary>Bozja's rank is remembered; Eureka's elemental level is not, and outside Eureka
    /// this returns null rather than a value that a death may since have taken away.</summary>
    public bool? ZoneProgressAtLeast(ZoneProgressKind kind, int rank)
    {
        if (kind == ZoneProgressKind.BozjaResistance)
        {
            return bozja.TryAtLeast(CharacterKey, SingleValueId, rank, out var met) ? met : null;
        }

        // Eureka's state object exists only while its director does, so a non-null state IS the
        // "you are standing in Eureka" test; the level itself lives on the director, which every
        // public content shares. Outside Eureka this is null, and null is the honest answer — a
        // remembered elemental level could claim a requirement met that a death has since undone.
        var framework = EventFramework.Instance();
        if (PublicContentEureka.GetState() == null || framework == null)
        {
            return null;
        }

        var director = framework->GetPublicContentDirector();
        return director == null ? null : director->GetCurrentLevel() >= (uint)rank;
    }

    /// <summary>Asks the server for the player's own achievement and Shared FATE data, at most once
    /// per character and only for the kinds <paramref name="kindsInUse"/> says something would
    /// actually read. This is the same request the client makes when the player opens those
    /// windows themselves; what it must never become is a poll.</summary>
    public void RequestOwnProgressOnce(IReadOnlySet<string> kindsInUse)
    {
        ArgumentNullException.ThrowIfNull(kindsInUse);
        var contentId = LocalContentId;
        if (contentId == 0 || contentId == requestedFor)
        {
            return;
        }

        // A title gate wants the achievement table too, and this is the only place that could know
        // it. Every title in the catalogue is awarded by an achievement, so achievement completion
        // is what answers a title gate for a player who has never opened Acquired Titles — and if
        // this line were missing, the request that answers 870 rows would never be sent and every
        // one of them would say "we cannot tell" for the whole session. What is deliberately NOT
        // here is RequestTitleList(): a second gated source for a fact this one already settles.
        var wantsAchievements = kindsInUse.Contains(GateKinds.AchievementComplete)
            || kindsInUse.Contains(GateKinds.TitleUnlocked);
        var wantsFates = kindsInUse.Contains(GateKinds.SharedFateRankAtLeast);
        if (!wantsAchievements && !wantsFates)
        {
            return;
        }

        var agent = Achievement.Instance();
        if (agent == null)
        {
            return;
        }

        // Marked before the calls, not after: a request that fails is not retried either. The rule
        // is "ask once", and "once" has to include the attempt.
        requestedFor = contentId;
        if (wantsAchievements && !agent->IsLoaded())
        {
            agent->RequestCompletedAchievements();
        }

        if (!wantsFates)
        {
            return;
        }

        for (byte tab = 0; tab < FateProgressTabCount; tab++)
        {
            agent->RequestFateProgressTab(tab);
        }
    }

    /// <summary>Bounds the store's growth. Called on logout, where a character bucket stops being
    /// the current one; it is a storage measure only, since a floor for a non-decreasing value is
    /// never wrong however old it is.</summary>
    public void Prune() =>
        store.Prune(TimeSpan.FromDays(180), DateTimeOffset.UtcNow, maxEntriesPerCharacter: 512);

    /// <summary>Achievement completion as a 0/1 value, readable only once the table has arrived.
    /// Monotonic: nothing in the client ever clears a completed achievement's bit.</summary>
    private sealed class AchievementSource : IMonotonicSource<uint>
    {
        public bool TryReadLive(uint id, out int value)
        {
            value = 0;
            var agent = Achievement.Instance();
            if (agent == null || !agent->IsLoaded())
            {
                return false;
            }

            value = agent->IsComplete((int)id) ? 1 : 0;
            return true;
        }
    }

    /// <summary>Shared FATE rank for one zone.
    ///
    /// <para>An unpopulated slot is all zeroes and rank 0 is a legal rank, so the two would be
    /// indistinguishable. <c>MaxRank</c> is the discriminator: the server always sends it non-zero
    /// for a real zone, so a zero there means "this row has not arrived", not "you are rank 0".</para></summary>
    private sealed class SharedFateSource : IMonotonicSource<uint>
    {
        public bool TryReadLive(uint id, out int value)
        {
            value = 0;
            var agent = AgentFateProgress.Instance();
            if (agent == null)
            {
                return false;
            }

            foreach (ref var tab in agent->Tabs)
            {
                foreach (ref var zone in tab.Zones)
                {
                    if (zone.TerritoryTypeId != id)
                    {
                        continue;
                    }

                    if (zone.MaxRank == 0)
                    {
                        return false;
                    }

                    value = zone.CurrentRank;
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Bozja/Zadnor resistance rank, from the director that exists only inside the zone.
    /// Monotonic: the rank itself is never lost, only progress towards the next one.</summary>
    private sealed class BozjaRankSource : IMonotonicSource<uint>
    {
        public bool TryReadLive(uint id, out int value)
        {
            value = 0;
            var director = PublicContentBozja.GetInstance();
            if (director == null)
            {
                return false;
            }

            value = (int)director->GetCurrentLevel();
            return true;
        }
    }
}
