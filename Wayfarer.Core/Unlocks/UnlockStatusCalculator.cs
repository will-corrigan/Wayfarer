using System.Globalization;

namespace Wayfarer.Core.Unlocks;

public static class UnlockStatusCalculator
{
    /// <summary>Sets Status/LockReason on every entry, in precedence order: Done, Accepted,
    /// LockedOut (QuestLock), job/level gate, prereq chain (PreviousQuest + Join), InstanceContent,
    /// Grand Company, beast tribe, mount, unmodeled-gate, else Available. Gate checks after
    /// LockedOut are only run for entries not already resolved by an earlier stage — later gates
    /// are skipped once one blocks, matching the level-gate short-circuit the original
    /// implementation relied on.</summary>
    public static void Compute(List<ResolvedUnlock> all, UnlockGateContext ctx)
    {
        // Alternative quests share the same unlock name: any one complete → all Done.
        var doneByName = new HashSet<string>(StringComparer.Ordinal);
        foreach (var u in all)
        {
            if (u.QuestRowId is { } id && (ctx.IsQuestComplete(id) || AnyAlternativeComplete(u, ctx)))
            {
                doneByName.Add(u.Def.Unlock);
            }
        }

        foreach (var u in all)
        {
            ComputeOne(u, ctx, doneByName);
        }
    }

    /// <summary>Resolves Status/LockReason for a single entry through the first four precedence
    /// stages (Done, Accepted, LockedOut, job/level, prereq chain), then hands off to
    /// <see cref="ComputeRemainingGates"/> for the rest.</summary>
    private static void ComputeOne(ResolvedUnlock u, UnlockGateContext ctx, HashSet<string> doneByName)
    {
        u.LockReason = null;
        if (u.QuestRowId is not { } rowId)
        {
            u.Status = UnlockStatus.Unverified;
            return;
        }

        if (doneByName.Contains(u.Def.Unlock))
        {
            u.Status = UnlockStatus.Done;
            return;
        }

        if (ctx.IsQuestAccepted(rowId))
        {
            u.Status = UnlockStatus.Accepted;
            return;
        }

        if (IsLockedOut(u, ctx, out var lockoutReason))
        {
            u.Status = UnlockStatus.LockedOut;
            u.LockReason = lockoutReason;
            return;
        }

        if (!JobLevelMet(u, ctx, out var jobReason))
        {
            u.Status = UnlockStatus.LevelLocked;
            u.LockReason = jobReason;
            return;
        }

        if (PrereqBlocking(u, ctx, out var prereqReason))
        {
            u.Status = UnlockStatus.QuestLocked;
            u.LockReason = prereqReason;
            return;
        }

        ComputeRemainingGates(u, ctx);
    }

    /// <summary>InstanceContent, Grand Company, beast tribe, mount, and unmodeled-gate checks —
    /// the tail of the precedence chain, reached only once every earlier stage has passed.</summary>
    private static void ComputeRemainingGates(ResolvedUnlock u, UnlockGateContext ctx)
    {
        if (InstanceContentBlocking(u, ctx, out var icReason))
        {
            u.Status = UnlockStatus.InstanceLocked;
            u.LockReason = icReason;
            return;
        }

        if (!GrandCompanyMet(u, ctx, out var gcReason))
        {
            u.Status = UnlockStatus.GrandCompanyLocked;
            u.LockReason = gcReason;
            return;
        }

        if (!BeastTribeMet(u, ctx, out var beastReason))
        {
            u.Status = UnlockStatus.BeastTribeLocked;
            u.LockReason = beastReason;
            return;
        }

        if (u.RequiredMountId is { } mountId && !ctx.IsMountUnlocked(mountId))
        {
            u.Status = UnlockStatus.MountLocked;
            u.LockReason = u.RequiredMountName is { } mountName
                ? $"needs mount '{mountName}' unlocked"
                : "needs a specific mount unlocked";
            return;
        }

        if (u.HasUnmodeledGate)
        {
            u.Status = UnlockStatus.UnknownGate;
            u.LockReason = "has a requirement this plugin can't check (festival window or housing) — status unknown";
            return;
        }

        u.Status = UnlockStatus.Available;
    }

    /// <summary>Completing any one of the rows a duplicated quest name could mean counts as
    /// completing the unlock. A character gets exactly one of the three <c>Simply the Hest</c>
    /// rows, decided by their starting city, so checking only the bound row reported "not done"
    /// for two thirds of characters.</summary>
    private static bool AnyAlternativeComplete(ResolvedUnlock u, UnlockGateContext ctx)
    {
        foreach (var id in u.AlternativeQuestRowIds)
        {
            if (ctx.IsQuestComplete(id))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary><c>QuestLock</c>: completing any (Join 2, the only value observed in game data)
    /// or all (Join 1) of the listed quests permanently locks this one out.</summary>
    private static bool IsLockedOut(ResolvedUnlock u, UnlockGateContext ctx, out string? reason)
    {
        reason = null;
        if (u.LockoutQuestRowIds.Count == 0)
        {
            return false;
        }

        var requireAll = u.LockoutJoin == 1;
        var allComplete = true;
        var anyComplete = false;
        string? firstCompleteName = null;
        for (var i = 0; i < u.LockoutQuestRowIds.Count; i++)
        {
            if (ctx.IsQuestComplete(u.LockoutQuestRowIds[i]))
            {
                anyComplete = true;
                firstCompleteName ??= i < u.LockoutQuestNames.Count
                    ? u.LockoutQuestNames[i]
                    : u.LockoutQuestRowIds[i].ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                allComplete = false;
            }
        }

        if (!(requireAll ? allComplete : anyComplete))
        {
            return false;
        }

        reason = $"no longer obtainable — '{firstCompleteName}' already completed";
        return true;
    }

    /// <summary><c>ClassJobCategory0</c> (+ <c>ClassJobLevel[0]</c>) is the primary, always-real
    /// job/level gate; empty <see cref="ResolvedUnlock.RequiredJobRowIds"/> means unrestricted,
    /// checked against the player's currently active job level instead. <c>ClassJobCategory1</c>
    /// is a genuine independent alternative — checked against its own <c>ClassJobLevel[1]</c> —
    /// only when <see cref="ResolvedUnlock.AltRequiredJobLevel"/> is nonzero. Live sheet data
    /// (5,533 quest rows scanned) shows <c>ClassJobLevel[1] != 0</c> never co-occurs with
    /// <c>ClassJobCategory1</c> being the "every job" sentinel mask, and the sentinel always
    /// pairs with <c>ClassJobLevel[1] == 0</c>: the zero level is exactly the "category1 isn't
    /// real" flag. Eligible iff cat0 is met OR the real cat1 alternative (when present) is met —
    /// never a flat union of both categories' job sets, which would silently reopen every
    /// sentinel-carrying job quest (most single-job storyline quests) to every job.</summary>
    private static bool JobLevelMet(ResolvedUnlock u, UnlockGateContext ctx, out string? reason)
    {
        reason = null;
        var cat0Met = Cat0Met(u, ctx);
        var cat1Real = u.AltRequiredJobRowIds.Count > 0 && u.AltRequiredJobLevel != 0;
        if (cat0Met || (cat1Real && MaxJobLevel(u.AltRequiredJobRowIds, ctx) >= u.AltRequiredJobLevel))
        {
            return true;
        }

        reason = BuildJobLevelReason(u, cat1Real);
        return false;
    }

    private static bool Cat0Met(ResolvedUnlock u, UnlockGateContext ctx) =>
        u.RequiredJobRowIds.Count == 0
            ? ctx.PlayerLevel >= u.QuestLevel
            : MaxJobLevel(u.RequiredJobRowIds, ctx) >= u.QuestLevel;

    private static int MaxJobLevel(List<uint> jobRowIds, UnlockGateContext ctx)
    {
        var maxLevel = 0;
        foreach (var jobId in jobRowIds)
        {
            var level = ctx.GetClassJobLevel(jobId);
            if (level > maxLevel)
            {
                maxLevel = level;
            }
        }

        return maxLevel;
    }

    private static string BuildJobLevelReason(ResolvedUnlock u, bool cat1Real)
    {
        var cat0Reason = u.RequiredJobRowIds.Count == 0
            ? $"needs level {u.QuestLevel}"
            : $"needs {string.Join(" or ", u.RequiredJobNames)} {u.QuestLevel}";

        if (!cat1Real)
        {
            return cat0Reason;
        }

        var cat1Label = u.AltRequiredJobNames.Count > 0 ? string.Join(" or ", u.AltRequiredJobNames) : "an alternate job";
        return $"{cat0Reason} or {cat1Label} {u.AltRequiredJobLevel}";
    }

    /// <summary><c>PreviousQuest</c> + <c>PreviousQuestJoin</c>: 2 = OR (blocked only if none are
    /// complete), anything else (including unset) = AND (blocked by the first incomplete one).</summary>
    private static bool PrereqBlocking(ResolvedUnlock u, UnlockGateContext ctx, out string? reason)
    {
        reason = null;
        if (u.PrereqRowIds.Count == 0)
        {
            return false;
        }

        if (u.PrereqJoin == 2)
        {
            for (var i = 0; i < u.PrereqRowIds.Count; i++)
            {
                if (ctx.IsQuestComplete(u.PrereqRowIds[i]))
                {
                    return false;
                }
            }

            var parts = new List<string>(u.PrereqRowIds.Count);
            for (var i = 0; i < u.PrereqRowIds.Count; i++)
            {
                parts.Add(i < u.PrereqNames.Count ? u.PrereqNames[i] : u.PrereqRowIds[i].ToString(CultureInfo.InvariantCulture));
            }

            reason = $"needs quest '{string.Join("' or '", parts)}'";
            return true;
        }

        for (var i = 0; i < u.PrereqRowIds.Count; i++)
        {
            if (ctx.IsQuestComplete(u.PrereqRowIds[i]))
            {
                continue;
            }

            reason = $"needs quest '{(i < u.PrereqNames.Count ? u.PrereqNames[i] : u.PrereqRowIds[i].ToString(CultureInfo.InvariantCulture))}'";
            return true;
        }

        return false;
    }

    /// <summary><c>InstanceContent</c> + <c>InstanceContentJoin</c>: 1 = AND (every duty must be
    /// cleared), anything else = OR (any one cleared suffices). Distinguishes "not unlocked yet"
    /// from "unlocked but not cleared" via <see cref="UnlockGateContext.IsInstanceContentUnlocked"/>
    /// for the first blocking entry's reason.</summary>
    private static bool InstanceContentBlocking(ResolvedUnlock u, UnlockGateContext ctx, out string? reason)
    {
        reason = null;
        if (u.InstanceContentRowIds.Count == 0)
        {
            return false;
        }

        var requireAll = u.InstanceContentJoin == 1;
        var completedCount = 0;
        string? firstBlockedReason = null;
        for (var i = 0; i < u.InstanceContentRowIds.Count; i++)
        {
            var id = u.InstanceContentRowIds[i];
            if (ctx.IsInstanceContentCompleted(id))
            {
                completedCount++;
                continue;
            }

            if (firstBlockedReason != null)
            {
                continue;
            }

            var name = i < u.InstanceContentNames.Count ? u.InstanceContentNames[i] : id.ToString(CultureInfo.InvariantCulture);
            firstBlockedReason = ctx.IsInstanceContentUnlocked(id)
                ? $"requires completing {name}"
                : $"requires unlocking {name}";
        }

        var satisfied = requireAll ? completedCount == u.InstanceContentRowIds.Count : completedCount > 0;
        if (satisfied)
        {
            return false;
        }

        reason = firstBlockedReason;
        return true;
    }

    private static bool GrandCompanyMet(ResolvedUnlock u, UnlockGateContext ctx, out string? reason)
    {
        reason = null;
        if (u.RequiredGrandCompanyId is not { } gcId)
        {
            return true;
        }

        if (ctx.PlayerGrandCompany != gcId)
        {
            reason = u.RequiredGrandCompanyName is { } name ? $"needs {name} membership" : "needs a different Grand Company";
            return false;
        }

        if (u.RequiredGrandCompanyRank is { } rankId && ctx.PlayerGrandCompanyRank < rankId)
        {
            reason = $"needs Grand Company rank {rankId}";
            return false;
        }

        return true;
    }

    private static bool BeastTribeMet(ResolvedUnlock u, UnlockGateContext ctx, out string? reason)
    {
        reason = null;
        if (u.RequiredBeastTribeId is not { } tribeId)
        {
            return true;
        }

        if (u.RequiredBeastTribeRank is not { } rankId || ctx.GetBeastTribeRank(tribeId) >= rankId)
        {
            return true;
        }

        var tribe = u.RequiredBeastTribeName ?? "a beast tribe";
        var rankLabel = u.RequiredBeastTribeRankName ?? $"rank {rankId}";
        reason = $"needs {tribe} {rankLabel}";
        return false;
    }
}
