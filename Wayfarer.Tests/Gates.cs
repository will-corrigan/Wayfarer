using Wayfarer.Core.Unlocks;

namespace Wayfarer.Tests;

/// <summary>Builds an <see cref="UnlockGateContext"/> where every live-game read is a stub that
/// says "no" unless a test overrides it, so a fixture only has to state the one fact it is about.</summary>
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
        Func<uint, bool>? isMountUnlocked = null) => new(
            playerLevel,
            playerGrandCompany,
            playerGrandCompanyRank,
            isQuestComplete ?? (_ => false),
            isQuestAccepted ?? (_ => false),
            getClassJobLevel ?? (_ => 0),
            isInstanceContentCompleted ?? (_ => false),
            isInstanceContentUnlocked ?? (_ => true),
            getBeastTribeRank ?? (_ => 0),
            isMountUnlocked ?? (_ => false));
}
