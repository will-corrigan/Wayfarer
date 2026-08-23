using System.Globalization;

namespace Wayfarer.Core.Unlocks;

public static class UnlockStatusCalculator
{
    /// <summary>Sets Status/LockReason on every entry, in precedence order: Done, Accepted,
    /// ambiguous name, LockedOut (QuestLock), job/level gate, prereq chain (PreviousQuest + Join),
    /// InstanceContent, Grand Company, beast tribe, mount, hard job requirement, accept-condition
    /// quests, curated requirements, unmodeled gate, no discoverable gate, else Available. Gate
    /// checks after LockedOut are only run for entries not already resolved by an earlier stage —
    /// later gates are skipped once one blocks, matching the level-gate short-circuit the original
    /// implementation relied on.
    ///
    /// <para>Available is a conclusion, not a default: it is reached only when every gate this
    /// plugin knows how to read has been read and the entry is known to have no others. Anything
    /// else is <see cref="UnlockStatus.RequirementsUnknown"/>.</para></summary>
    public static void Compute(List<ResolvedUnlock> all, UnlockGateContext ctx)
    {
        // Alternative quests — one per starting city, the player gets exactly one — share both an
        // unlock name and a level, so completing any one of them completes the unlock for all.
        // See AlternativeGroup for why the level has to be part of that key.
        var doneGroups = new HashSet<AlternativeGroup>();
        foreach (var u in all)
        {
            if (u.QuestRowId is { } id && (ctx.IsQuestComplete(id) || AnyAlternativeComplete(u, ctx)))
            {
                doneGroups.Add(AlternativeGroup.Of(u));
            }
        }

        foreach (var u in all)
        {
            ComputeOne(u, ctx, doneGroups);
        }
    }

    /// <summary>Resolves Status/LockReason for a single entry through the first four precedence
    /// stages (Done, Accepted, LockedOut, job/level, prereq chain), then hands off to
    /// <see cref="ComputeRemainingGates"/> for the rest.</summary>
    private static void ComputeOne(ResolvedUnlock u, UnlockGateContext ctx, HashSet<AlternativeGroup> doneGroups)
    {
        u.LockReason = null;

        // An entry with no quest bound to it has no completion evidence of its own, and cannot
        // borrow another entry's: it is never Done. It can still be told apart from "we know
        // nothing", though, when the catalogue curated a gate for it.
        if (u.QuestRowId is not { } rowId)
        {
            ComputeWithoutQuest(u, ctx);
            return;
        }

        if (doneGroups.Contains(AlternativeGroup.Of(u)))
        {
            u.Status = UnlockStatus.Done;
            return;
        }

        if (ctx.IsQuestAccepted(rowId) || AnyAlternativeAccepted(u, ctx))
        {
            u.Status = UnlockStatus.Accepted;
            return;
        }

        // Every gate below this line is read off one Quest row, and when several rows share the
        // catalogue's name the matcher picked one of them arbitrarily — the character's starting
        // city decides which is really theirs and the plugin cannot see it. Done and Accepted are
        // safe above, because they ask about all the siblings at once; nothing below can. Graded
        // on the wrong sibling, a Gridanian was told a Limsa Lominsa quest was in their way, in
        // the confident voice this plugin reserves for things it knows.
        if (u.AlternativeQuestRowIds.Count > 1)
        {
            u.Status = UnlockStatus.RequirementsUnknown;
            u.LockReason = $"the game ships {u.AlternativeQuestRowIds.Count} quests with this name and only your character knows which is yours — status unknown";
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

    /// <summary>Entries with no Quest row at all. Most are honestly unknowable and say so. Some
    /// are not: the guide gates them on clearing a duty or on carrying a treasure map, and the
    /// catalogue records that as a curated requirement. Running it here turns "status unknown" —
    /// which is all these entries could ever say — into "requires clearing Sigmascape V4.0
    /// (Savage)", which is the difference between a shrug and an answer.
    ///
    /// <para>Satisfying the gate still never yields Available. Clearing the prerequisite duty
    /// opens the door; whether the player has walked through it (talked to the Wandering Minstrel)
    /// is not something the client records anywhere a plugin can read, and guessing at it is
    /// exactly the class of confident wrongness this calculator exists to avoid.</para></summary>
    private static void ComputeWithoutQuest(ResolvedUnlock u, UnlockGateContext ctx)
    {
        if (u.Def.Requires?.HasCheckableRequirement != true)
        {
            u.Status = UnlockStatus.Unverified;
            return;
        }

        if (CuratedRequirementBlocking(u, ctx, out var reason, out var status))
        {
            u.Status = status;
            u.LockReason = reason;
            return;
        }

        u.Status = UnlockStatus.RequirementsUnknown;
        u.LockReason = "everything this plugin can check for it is done, but there is no quest to read for whether you have taken it — status unknown";
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

        ComputeFinalGates(u, ctx);
    }

    /// <summary>The gates that live outside the Quest row's own columns — a hard job requirement,
    /// the separate accept-condition sheet, the catalogue's curated requirements — and then the
    /// two "we don't know" outcomes that stand between this and reporting Available.</summary>
    private static void ComputeFinalGates(ResolvedUnlock u, UnlockGateContext ctx)
    {
        if (!HardRequiredJobMet(u, ctx, out var hardJobReason))
        {
            u.Status = UnlockStatus.LevelLocked;
            u.LockReason = hardJobReason;
            return;
        }

        if (AcceptConditionBlocking(u, ctx, out var acceptReason, out var acceptStatus))
        {
            u.Status = acceptStatus;
            u.LockReason = acceptReason;
            return;
        }

        if (CuratedRequirementBlocking(u, ctx, out var curatedReason, out var curatedStatus))
        {
            u.Status = curatedStatus;
            u.LockReason = curatedReason;
            return;
        }

        if (u.HasUnmodeledGate)
        {
            u.Status = UnlockStatus.UnknownGate;
            u.LockReason = "has a requirement this plugin can't check (festival window or housing) — status unknown";
            return;
        }

        // The change this whole audit exists for. "No gate found" and "no gate exists" look
        // identical in the Quest sheet: row 67086 has every gate column empty and a recorded level
        // of 1, and still wants seven Extreme-trial mounts, because its real condition lives in a
        // server-side accept script that is not shipped in sqpack and has no client API. Falling
        // through to Available here is what sent a player to a quest they could not accept.
        //
        // The curated block only lifts that verdict if it actually checks something. A `requires`
        // carrying nothing but prose — or nothing at all — is a note, not a gate, and letting its
        // mere presence disable the guard would reopen the hole it was written to close.
        if (u.HasNoDiscoverableGate && u.Def.Requires?.HasCheckableRequirement != true)
        {
            u.Status = UnlockStatus.RequirementsUnknown;
            u.LockReason = "the game records no requirement for this at all, which usually means it has one this plugin can't see — status unknown";
            return;
        }

        u.Status = UnlockStatus.Available;
    }

    /// <summary><c>ClassJobRequired</c>: a single job that must be at the quest's level, whatever
    /// the category mask allows. Reuses <see cref="UnlockStatus.LevelLocked"/> and its phrasing,
    /// so nothing downstream has to learn a new shape.</summary>
    private static bool HardRequiredJobMet(ResolvedUnlock u, UnlockGateContext ctx, out string? reason)
    {
        reason = null;
        if (u.HardRequiredJobRowId is not { } jobId || ctx.GetClassJobLevel(jobId) >= u.QuestLevel)
        {
            return true;
        }

        reason = $"needs {u.HardRequiredJobName ?? "a specific job"} {u.QuestLevel}";
        return false;
    }

    /// <summary><c>QuestAcceptAdditionCondition</c>: prerequisite quests kept in their own sheet,
    /// AND-ed. An id that doesn't resolve to a Quest row is an unknown requirement, not an absent
    /// one, and blocks with <see cref="UnlockStatus.RequirementsUnknown"/>.</summary>
    private static bool AcceptConditionBlocking(
        ResolvedUnlock u, UnlockGateContext ctx, out string? reason, out UnlockStatus status)
    {
        reason = null;
        status = UnlockStatus.QuestLocked;
        for (var i = 0; i < u.AcceptConditionQuestRowIds.Count; i++)
        {
            if (ctx.IsQuestComplete(u.AcceptConditionQuestRowIds[i]))
            {
                continue;
            }

            var name = i < u.AcceptConditionQuestNames.Count
                ? u.AcceptConditionQuestNames[i]
                : u.AcceptConditionQuestRowIds[i].ToString(CultureInfo.InvariantCulture);
            reason = $"needs quest '{name}'";
            return true;
        }

        if (!u.HasUnresolvedAcceptCondition)
        {
            return false;
        }

        status = UnlockStatus.RequirementsUnknown;
        reason = "has an extra requirement this plugin can't identify — status unknown";
        return true;
    }

    /// <summary>The catalogue's curated <c>requires</c> block: level and job first (so the level
    /// gate keeps winning over the collection gate, as it does everywhere else), then the
    /// collectibles, then the honest fallback for a requirement that is known to exist but can't
    /// be expressed. Fills <see cref="ResolvedUnlock.MissingRequirements"/> with the whole list —
    /// telling the player only the first of seven missing mounts would be its own small lie.</summary>
    private static bool CuratedRequirementBlocking(
        ResolvedUnlock u, UnlockGateContext ctx, out string? reason, out UnlockStatus status)
    {
        reason = null;
        status = UnlockStatus.CollectionLocked;
        u.MissingRequirements = [];
        if (u.Def.Requires is not { } req)
        {
            return false;
        }

        if (req.MinLevel is { } minLevel && ctx.PlayerLevel < minLevel)
        {
            status = UnlockStatus.LevelLocked;
            reason = $"needs level {minLevel}";
            return true;
        }

        foreach (var job in req.Jobs)
        {
            if (ctx.GetClassJobLevel(job.Id) < job.Level)
            {
                status = UnlockStatus.LevelLocked;
                reason = $"needs {job.Name} {job.Level}";
                return true;
            }
        }

        // Duties before collectibles, and with their own status: "you have not cleared this duty"
        // is a different kind of answer from "you are missing four minions", and the checklist
        // groups them differently.
        if (MissingDuty(u, ctx, req) is { } duty)
        {
            status = UnlockStatus.InstanceLocked;
            reason = $"requires clearing {duty}";
            return true;
        }

        CollectMissing(u, ctx, req);
        if (u.MissingRequirements.Count > 0)
        {
            reason = u.MissingRequirements.Count == 1
                ? $"requires {u.MissingRequirements[0]}"
                : $"requires {u.MissingRequirements.Count} more of {req.Label ?? "a set of collectibles"}; next: {u.MissingRequirements[0]}";
            return true;
        }

        if (!req.Unverifiable)
        {
            return false;
        }

        status = UnlockStatus.RequirementsUnknown;
        reason = req.Label is { Length: > 0 } label
            ? $"{label} — status unknown"
            : "has a requirement this plugin can't check — status unknown";
        return true;
    }

    /// <summary>The first curated duty the player has not cleared, or null when they all are.
    /// Also records every uncleared one in <see cref="ResolvedUnlock.MissingRequirements"/>, for
    /// the same reason the collectible list does: naming only the first of several is a small lie
    /// the window can easily avoid telling.</summary>
    private static string? MissingDuty(ResolvedUnlock u, UnlockGateContext ctx, UnlockRequirement req)
    {
        string? first = null;
        foreach (var duty in req.Duties)
        {
            if (ctx.IsInstanceContentCompleted(duty.Id))
            {
                continue;
            }

            first ??= duty.Name;
            u.MissingRequirements.Add(duty.Name);
        }

        return first;
    }

    private static void CollectMissing(ResolvedUnlock u, UnlockGateContext ctx, UnlockRequirement req)
    {
        foreach (var mount in req.Mounts)
        {
            if (!ctx.IsMountUnlocked(mount.Id))
            {
                u.MissingRequirements.Add(Describe(mount.Name, mount.From));
            }
        }

        foreach (var minion in req.Minions)
        {
            if (!ctx.IsMinionUnlocked(minion.Id))
            {
                u.MissingRequirements.Add(Describe(minion.Name, minion.From));
            }
        }

        foreach (var item in req.Items)
        {
            var owned = item.KeyItem ? ctx.GetKeyItemCount(item.Id) : ctx.GetOwnedItemCount(item.Id);
            var needed = item.Count > 0 ? item.Count : 1;
            if (owned < needed)
            {
                u.MissingRequirements.Add(needed > 1 ? $"{item.Name} x{needed}" : item.Name);
            }
        }
    }

    private static string Describe(string name, string? from) =>
        from is { Length: > 0 } ? $"{name} — {from}" : name;

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

    /// <summary>Same reasoning as <see cref="AnyAlternativeComplete"/>: the character may have
    /// picked up a sibling row rather than the one the matcher bound.</summary>
    private static bool AnyAlternativeAccepted(ResolvedUnlock u, UnlockGateContext ctx)
    {
        foreach (var id in u.AlternativeQuestRowIds)
        {
            if (ctx.IsQuestAccepted(id))
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

    /// <summary>The identity of a set of interchangeable quests: entries the catalogue lists under
    /// the same unlock name <b>at the same level</b>.
    ///
    /// <para>The name alone is not that identity, and treating it as one told the player something
    /// false and then hid the evidence. Eight labels are duplicated in the shipped catalogue and
    /// only two are genuine alternatives — <c>Levequests</c> (three city introductions, all level
    /// 10) and <c>Glamours</c> (two, both level 15). The other six are <b>progression tiers</b>:
    /// <c>Sightseeing Log Expansion</c> is five different quests at levels 52, 60, 70, 80 and 90,
    /// and <c>Stone, Sky, Sea Access</c> another five from 60 to 100. Keyed on the name alone,
    /// finishing the level-52 quest reported all five tiers Complete — and because <c>ShowDone</c>
    /// defaults to false, the four that were not complete vanished from the checklist rather than
    /// being visibly wrong.</para>
    ///
    /// <para>Membership is still established by quest identity: a group is marked done only when a
    /// Quest row belonging to it is actually complete. The level is what stops that evidence
    /// leaking sideways into a tier it says nothing about.</para></summary>
    private readonly record struct AlternativeGroup(string Unlock, int Level)
    {
        public static AlternativeGroup Of(ResolvedUnlock u) => new(u.Def.Unlock, u.Def.Level);
    }
}
