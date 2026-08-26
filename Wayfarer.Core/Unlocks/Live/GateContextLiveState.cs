namespace Wayfarer.Core.Unlocks.Live;

/// <summary>Presents an <see cref="UnlockGateContext"/> — a flat bag of delegates the calculator
/// has always taken — as the four readers a gate evaluator talks to.
///
/// <para>The seam is here rather than in the plugin so that Core owns the rule that turns "the
/// host did not wire this reader" into Indeterminate. A missing delegate is not a "no": every
/// optional reader below returns false from its <c>Try</c> when it was never supplied, which is
/// the same answer it would give if the game were mid-load.</para></summary>
internal sealed class GateContextLiveState(UnlockGateContext ctx)
    : ILiveState, ICharacterReader, IProgressReader, IInventoryReader, IContentReader
{
    public bool IsReady => ctx.LiveStateReady;

    public ICharacterReader Character => this;

    public IProgressReader Progress => this;

    public IInventoryReader Inventory => this;

    public IContentReader Content => this;

    public int Level => ctx.PlayerLevel;

    public byte GrandCompany => ctx.PlayerGrandCompany;

    public int GrandCompanyRank => ctx.PlayerGrandCompanyRank;

    // A host that wired no title reader has asked for nothing, which is exactly what NotRequested
    // means — the same "not wired therefore unknown" rule every optional reader here follows.
    public TitleDataState TitleData => ctx.GetTitleDataState?.Invoke() ?? TitleDataState.NotRequested;

    public int ClassJobLevel(uint classJobId) => ctx.GetClassJobLevel(classJobId);

    // Mounts, minions and reputation all live on the same always-resident player state, so the
    // one thing that can make them unreadable is that state not being there yet - which is
    // exactly what IsReady means. There is no separate per-id failure to report.
    public bool TryIsMountUnlocked(uint mountId, out bool owned)
    {
        owned = IsReady && ctx.IsMountUnlocked(mountId);
        return IsReady;
    }

    public bool TryIsMinionUnlocked(uint companionId, out bool owned)
    {
        owned = IsReady && ctx.IsMinionUnlocked(companionId);
        return IsReady;
    }

    public bool TryTribeRank(byte beastTribeId, out byte rank)
    {
        rank = IsReady ? ctx.GetBeastTribeRank(beastTribeId) : (byte)0;
        return IsReady;
    }

    public bool IsQuestComplete(uint questRowId) => ctx.IsQuestComplete(questRowId);

    public bool IsQuestAccepted(uint questRowId) => ctx.IsQuestAccepted(questRowId);

    public bool TryAetherCurrentZoneComplete(uint compFlgSetId, out bool complete) =>
        Unwrap(ctx.IsAetherCurrentZoneComplete?.Invoke(compFlgSetId), out complete);

    public bool TryAchievementComplete(uint achievementId, out bool complete) =>
        Unwrap(ctx.IsAchievementComplete?.Invoke(achievementId), out complete);

    public bool TryTitleUnlocked(uint titleRowId, out bool unlocked) =>
        Unwrap(ctx.IsTitleUnlocked?.Invoke(titleRowId), out unlocked);

    public bool TrySharedFateRankAtLeast(uint territoryTypeId, int rank, out bool met) =>
        Unwrap(ctx.SharedFateRankAtLeast?.Invoke(territoryTypeId, rank), out met);

    public bool TryZoneProgressAtLeast(ZoneProgressKind kind, int rank, out bool met) =>
        Unwrap(ctx.ZoneProgressAtLeast?.Invoke(kind, rank), out met);

    public bool TryCount(uint itemId, ItemScope scope, out int count)
    {
        count = 0;
        if (!IsReady)
        {
            return false;
        }

        switch (scope)
        {
            case ItemScope.KeyItem:
                count = ctx.GetKeyItemCount(itemId);
                return true;
            case ItemScope.Saddlebag:
                if (ctx.GetSaddlebagItemCount is not { } saddlebag)
                {
                    return false;
                }

                count = saddlebag(itemId);
                return true;
            default:
                count = ctx.GetOwnedItemCount(itemId);
                return true;
        }
    }

    public bool TryDutyUnlocked(ContentSpace space, uint contentId, out bool unlocked)
    {
        unlocked = false;
        if (!IsReady)
        {
            return false;
        }

        // A public-content id handed to the instance-content reader does not fail: it reads a
        // different duty's bit. When the host has not wired the public reader at all, saying so is
        // the only safe answer - the other reader is not a fallback, it is a wrong answer.
        if (space == ContentSpace.Public)
        {
            if (ctx.IsPublicContentUnlocked is not { } read)
            {
                return false;
            }

            unlocked = read(contentId);
            return true;
        }

        unlocked = ctx.IsInstanceContentUnlocked(contentId);
        return true;
    }

    public bool TryDutyComplete(ContentSpace space, uint contentId, out bool complete)
    {
        complete = false;
        if (!IsReady)
        {
            return false;
        }

        if (space == ContentSpace.Public)
        {
            if (ctx.IsPublicContentCompleted is not { } read)
            {
                return false;
            }

            complete = read(contentId);
            return true;
        }

        complete = ctx.IsInstanceContentCompleted(contentId);
        return true;
    }

    private static bool Unwrap(bool? value, out bool result)
    {
        result = value ?? false;
        return value.HasValue;
    }
}
