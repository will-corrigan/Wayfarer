using Wayfarer.Core.Unlocks;
using Wayfarer.Core.Unlocks.Gates;
using Wayfarer.Core.Unlocks.Live;

namespace Wayfarer.Tests;

/// <summary>Builds an <see cref="UnlockGateContext"/> where every live-game read is a stub that
/// says "no" unless a test overrides it, so a fixture only has to state the one fact it is about.
///
/// <para>The readers that can be unavailable default to <c>null</c> rather than to "no" — that is
/// the shape of the real thing, and it means a fixture that forgets to wire one gets
/// "cannot determine" rather than a quietly confident negative.</para></summary>
internal static class Gates
{
    public static UnlockGateContext Ctx(
        int playerLevel,
        byte playerGrandCompany = 0,
        int playerGrandCompanyRank = 0,
        Func<uint, bool>? isQuestComplete = null,
        Func<uint, bool>? isQuestAccepted = null,
        Func<uint, int>? getClassJobLevel = null,
        Func<uint, bool>? isInstanceContentCompleted = null,
        Func<uint, bool>? isInstanceContentUnlocked = null,
        Func<byte, byte>? getBeastTribeRank = null,
        Func<uint, bool>? isMountUnlocked = null,
        Func<uint, bool>? isMinionUnlocked = null,
        Func<uint, int>? getOwnedItemCount = null,
        Func<uint, int>? getKeyItemCount = null,
        Func<GameTextRef, string?>? resolveGameText = null,
        bool liveStateReady = true,
        Func<uint, bool>? isPublicContentUnlocked = null,
        Func<uint, bool>? isPublicContentCompleted = null,
        Func<uint, bool?>? isAchievementComplete = null,
        Func<uint, bool?>? isAetherCurrentZoneComplete = null,
        Func<uint, int, bool?>? sharedFateRankAtLeast = null,
        Func<ZoneProgressKind, int, bool?>? zoneProgressAtLeast = null,
        Func<uint, int>? getSaddlebagItemCount = null,
        GateEvaluatorRegistry? gates = null)
    {
        var ctx = new UnlockGateContext(
            playerLevel,
            playerGrandCompany,
            playerGrandCompanyRank,
            isQuestComplete ?? (_ => false),
            isQuestAccepted ?? (_ => false),
            getClassJobLevel ?? (_ => 0),
            isInstanceContentCompleted ?? (_ => false),
            isInstanceContentUnlocked ?? (_ => true),
            getBeastTribeRank ?? (_ => 0),
            isMountUnlocked ?? (_ => false),
            isMinionUnlocked ?? (_ => false),
            getOwnedItemCount ?? (_ => 0),
            getKeyItemCount ?? (_ => 0),
            resolveGameText,
            liveStateReady,
            isPublicContentUnlocked,
            isPublicContentCompleted,
            isAchievementComplete,
            isAetherCurrentZoneComplete,
            sharedFateRankAtLeast,
            zoneProgressAtLeast,
            getSaddlebagItemCount);

        return gates is null ? ctx : ctx with { Gates = gates };
    }

    /// <summary>A gate node, spelled the way the data file spells one.</summary>
    public static GateNode Node(
        string kind,
        IEnumerable<uint>? ids = null,
        int amount = 0,
        string? scope = null,
        string? display = null,
        IEnumerable<GateNode>? children = null) => new()
        {
            Kind = kind,
            Ids = ids is null ? [] : [.. ids],
            Amount = amount,
            Scope = scope,
            Display = display,
            Children = children is null ? [] : [.. children],
        };
}
