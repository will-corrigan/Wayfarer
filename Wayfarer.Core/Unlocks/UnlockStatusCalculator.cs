using System.Globalization;
using Wayfarer.Core.Unlocks.Gates;

namespace Wayfarer.Core.Unlocks;

/// <summary>Works out, for every catalogue entry, whether the player can go and get it — and, when
/// they cannot, which gate is in the way. The single place that decides what the checklist claims,
/// and deliberately the most conservative code in the plugin: see <see cref="Compute"/>.</summary>
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
        ArgumentNullException.ThrowIfNull(ctx);

        // Nothing is worth computing from a client that is not there yet. Every "is it unlocked"
        // read answers false against an unloaded player state, and a pass run then would replace a
        // correct list with the claim that the player owns nothing at all. Leaving the previous
        // statuses in place is the only honest response, and the surfaces already have a
        // "not loaded" affordance for the case where there are none yet.
        if (!ctx.LiveStateReady)
        {
            return;
        }

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
        ResetComputedFields(u);

        // Ahead of everything, including the quest chain: the entry's own identity, read live. If
        // the thing this entry unlocks is already open to the player then they have it, whatever
        // any quest row says — and that is a stronger fact than "the quest we matched by name is
        // complete", because it is about the unlock rather than about our name match.
        var identity = EvaluateIdentityGate(u, ctx);
        if (identity is { Outcome: GateOutcome.Satisfied })
        {
            u.Status = UnlockStatus.Done;
            return;
        }

        // An entry with no quest bound to it has no completion evidence of its own, and cannot
        // borrow another entry's: it is never Done. It can still be told apart from "we know
        // nothing", though, when the catalogue curated a gate for it.
        if (u.QuestRowId is not { } rowId)
        {
            ComputeWithoutQuest(u, ctx, identity);
            return;
        }

        if (JournalStateResolves(u, ctx, doneGroups, rowId))
        {
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

        ComputeRemainingGates(u, ctx, identity);
    }

    /// <summary>The three verdicts the quest journal alone settles, in precedence order: Done,
    /// Accepted, and the refusal to grade an entry whose name matches several rows.
    ///
    /// <para>Done and Accepted are safe here because they ask about all the sibling rows at once.
    /// Nothing after this point can: every gate below is read off ONE Quest row, and when several
    /// share the catalogue's name the matcher picked one arbitrarily — the character's starting
    /// city decides which is really theirs and the plugin cannot see it. Graded on the wrong
    /// sibling, a Gridanian was told a Limsa Lominsa quest was in their way, in the confident voice
    /// this plugin reserves for things it knows.</para></summary>
    /// <returns>True when the entry is resolved and no further gate should run.</returns>
    private static bool JournalStateResolves(
        ResolvedUnlock u, UnlockGateContext ctx, HashSet<AlternativeGroup> doneGroups, uint rowId)
    {
        if (doneGroups.Contains(AlternativeGroup.Of(u)))
        {
            u.Status = UnlockStatus.Done;
            return true;
        }

        if (ctx.IsQuestAccepted(rowId) || AnyAlternativeAccepted(u, ctx))
        {
            u.Status = UnlockStatus.Accepted;
            return true;
        }

        if (u.AlternativeQuestRowIds.Count <= 1)
        {
            return false;
        }

        u.Status = UnlockStatus.RequirementsUnknown;
        u.LockReason = $"{u.AlternativeQuestRowIds.Count} quests share this name";
        return true;
    }

    /// <summary>Runs <see cref="ResolvedUnlock.IdentityGate"/>, or null when the entry has none.
    /// Dispatch is by the node's <c>kind</c> through the registry, exactly as for a curated gate —
    /// there is no separate path here and no knowledge of which entry is being graded.</summary>
    private static GateResult? EvaluateIdentityGate(ResolvedUnlock u, UnlockGateContext ctx) =>
        u.IdentityGate is { } node ? ctx.Gates.Evaluate(node, ctx.Live) : null;

    /// <summary>The three fields a fresh pass over one entry always starts from a clean slate: a
    /// status this plugin no longer stands behind (the quest was just accepted elsewhere, a gate
    /// that used to block now doesn't) must not leave a stale reason or condition note sitting
    /// alongside whatever gets computed this time.</summary>
    private static void ResetComputedFields(ResolvedUnlock u)
    {
        u.LockReason = null;
        u.AvailableCondition = null;
        u.AvailableConditionDetail = null;
    }

    /// <summary>Entries with no Quest row at all. Most are honestly unknowable and say so. Some
    /// are not: the guide gates them on clearing a duty or on carrying a treasure map, and the
    /// catalogue records that as a curated requirement. Running it here turns "status unknown" —
    /// which is all these entries could ever say — into "requires clearing Sigmascape V4.0
    /// (Savage)", which is the difference between a shrug and an answer.
    ///
    /// <para>Satisfying a <see cref="UnlockRequirement.Duties"/> gate does not on its own yield
    /// Available: clearing the prerequisite duty opens the door, and whether the player then walked
    /// through it is a separate question. What answers that question is
    /// <see cref="ResolvedUnlock.IdentityGate"/> — the unlock bit of the duty this entry IS. When
    /// that gate returns a determinate answer the entry can be graded outright, and when it cannot
    /// the entry keeps the old, honest shrug rather than guessing, which is the class of confident
    /// wrongness this calculator exists to avoid. A curated
    /// <see cref="UnlockRequirement.RequiresAnotherPlayer"/> gate resolves the same way for a
    /// different reason: once it is the only thing left,
    /// <see cref="CuratedRequirementBlocking"/> already resolved the entry to Available with the
    /// condition named, and that verdict is kept rather than papered over.</para></summary>
    private static void ComputeWithoutQuest(ResolvedUnlock u, UnlockGateContext ctx, GateResult? identity)
    {
        if (u.Def.Requires?.HasCheckableRequirement != true && identity is null)
        {
            u.Status = UnlockStatus.Unverified;
            return;
        }

        if (CuratedRequirementBlocking(u, ctx, identity, out var reason, out var status))
        {
            u.Status = status;
            u.LockReason = reason;
            return;
        }

        // Nothing blocks. Available is still a conclusion rather than a default: it is reached
        // either because a partner-shaped condition already granted it, or because the identity
        // gate read the very thing that used to be unknowable and said the player has not taken
        // this unlock yet — which, with every prerequisite met, is precisely "go and get it".
        if (u.AvailableCondition is not null || identity is { Outcome: GateOutcome.Blocked })
        {
            u.Status = UnlockStatus.Available;
            return;
        }

        u.Status = UnlockStatus.RequirementsUnknown;
        u.LockReason = "no quest to read for this";
    }

    /// <summary>InstanceContent, Grand Company, beast tribe, mount, and unmodeled-gate checks —
    /// the tail of the precedence chain, reached only once every earlier stage has passed.</summary>
    private static void ComputeRemainingGates(ResolvedUnlock u, UnlockGateContext ctx, GateResult? identity)
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

        ComputeFinalGates(u, ctx, identity);
    }

    /// <summary>The gates that live outside the Quest row's own columns — a hard job requirement,
    /// the separate accept-condition sheet, the catalogue's curated requirements — and then the
    /// two "we don't know" outcomes that stand between this and reporting Available.</summary>
    private static void ComputeFinalGates(ResolvedUnlock u, UnlockGateContext ctx, GateResult? identity)
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

        if (CuratedRequirementBlocking(u, ctx, identity, out var curatedReason, out var curatedStatus))
        {
            u.Status = curatedStatus;
            u.LockReason = curatedReason;
            return;
        }

        if (u.HasUnmodeledGate)
        {
            // CuratedRequirementBlocking may have already granted Available-with-a-condition just
            // above (a RequiresAnotherPlayer gate resolves without blocking) before this check
            // finds a second, different problem. That tentative verdict must not survive alongside
            // a status that says the entry is locked.
            u.Status = UnlockStatus.UnknownGate;
            u.LockReason = "needs a festival or a house";
            u.AvailableCondition = null;
            u.AvailableConditionDetail = null;
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
        //
        // An entry whose own identity gate answered definitively is exempt: "the game records no
        // requirement" is a statement about the Quest row, and the identity read is a statement
        // about the unlock itself. When the second one is available it is the better evidence.
        if (u.HasNoDiscoverableGate
            && u.Def.Requires?.HasCheckableRequirement != true
            && identity is not { Outcome: GateOutcome.Blocked })
        {
            u.Status = UnlockStatus.RequirementsUnknown;
            u.LockReason = "the game records no requirement for this";
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

        reason = $"needs {JobGateText.Describe(u.HardRequiredJobName ?? "a specific job", [], u.QuestLevel)}";
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
        reason = "has a requirement Wayfarer cannot read";
        return true;
    }

    /// <summary>The catalogue's curated <c>requires</c> block: level and job first (so the level
    /// gate keeps winning over the collection gate, as it does everywhere else), then the
    /// collectibles, then the honest fallback for a requirement that is known to exist but can't
    /// be expressed. Fills <see cref="ResolvedUnlock.MissingRequirements"/> with the whole list —
    /// telling the player only the first of seven missing mounts would be its own small lie.</summary>
    private static bool CuratedRequirementBlocking(
        ResolvedUnlock u,
        UnlockGateContext ctx,
        GateResult? identity,
        out string? reason,
        out UnlockStatus status)
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
                reason = $"needs {JobGateText.Describe(job.Name, [], job.Level)}";
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
                : $"needs {u.MissingRequirements.Count} more of {req.Label ?? "a set"} — next: {u.MissingRequirements[0]}";
            return true;
        }

        if (DeclaredGatesBlocking(ctx, req, out reason, out status))
        {
            return true;
        }

        return UncheckableRequirementBlocking(u, ctx, req, identity, out reason, out status);
    }

    /// <summary>The declarative half of a <c>requires</c> block, and the whole of it for every kind
    /// added after the typed lists. Dispatch is a dictionary lookup on a string that came out of
    /// the data file: no requirement kind is named here, no catalogue entry is recognised, and a
    /// kind this build has never heard of comes back as Indeterminate rather than as a pass.</summary>
    private static bool DeclaredGatesBlocking(
        UnlockGateContext ctx, UnlockRequirement req, out string? reason, out UnlockStatus status)
    {
        reason = null;
        status = UnlockStatus.CollectionLocked;
        if (req.Gates.Count == 0)
        {
            return false;
        }

        var result = ctx.Gates.EvaluateAll(req.Gates, ctx.Live);
        if (result.Outcome == GateOutcome.Satisfied)
        {
            return false;
        }

        status = result.Status;
        reason = result.Reason;
        return true;
    }

    /// <summary>Everything left once level, job, duty and collectible checks all pass: the two
    /// "there is nothing further to check" fallbacks, and they resolve in opposite directions on
    /// purpose.
    ///
    /// <para><see cref="UnlockRequirement.RequiresAnotherPlayer"/> — checked first, ahead of the
    /// generic <see cref="UnlockRequirement.Unverifiable"/> catch-all — does <b>not</b> block. Every
    /// checkable gate has already passed by the time this runs (the quest is done, the level is
    /// met, the wristlet is even in the bags): the one thing left is a fact this plugin cannot read,
    /// not a fact that stands in the player's way, so the entry reports Available with the
    /// condition named on <see cref="ResolvedUnlock.AvailableCondition"/> rather than a block a
    /// couple who both play the game would have no way to satisfy.</para>
    ///
    /// <para><see cref="UnlockRequirement.Unverifiable"/> still blocks, because it means the
    /// opposite thing: not "known but unreadable", but "we don't know what this needs at all" —
    /// there is no "everything checkable" to have finished satisfying. The single exception is an
    /// entry whose <see cref="ResolvedUnlock.IdentityGate"/> returned a determinate answer <b>and</b>
    /// which has something checkable for that answer to sit on top of.
    /// <c>Unverifiable</c> is a statement about what the CATALOGUE can express, written when the
    /// only readable fact was a prerequisite; an identity gate reads the unlock itself, which is
    /// the very thing the flag was hedging about. Where the plugin can now answer the question,
    /// the hedge is stale rather than authoritative — and only there: an entry with no identity
    /// gate, or one whose gate could not be read, keeps the shrug exactly as before.</para>
    ///
    /// <para><b>Why the second half of that condition.</b> The whole argument above rests on "every
    /// checkable gate has already passed by the time this runs". On an entry with no checkable gate
    /// at all there are none to have passed, and the identity gate's "you have not taken this
    /// unlock" then says nothing whatever about whether the player <i>can</i>. Without the
    /// <see cref="UnlockRequirement.HasCheckableRequirement"/> half, such an entry read plainly
    /// <see cref="UnlockStatus.Available"/> with the gold "go and do this" marker, pointing at
    /// nothing, off a field that says in so many words that the requirement is unknown. One entry in
    /// the shipped catalogue is that shape today.</para></summary>
    private static bool UncheckableRequirementBlocking(
        ResolvedUnlock u,
        UnlockGateContext ctx,
        UnlockRequirement req,
        GateResult? identity,
        out string? reason,
        out UnlockStatus status)
    {
        if (req.RequiresAnotherPlayer)
        {
            reason = null;
            status = UnlockStatus.Available;
            u.AvailableCondition = "needs a partner";
            u.AvailableConditionDetail = ResolveConditionDetail(ctx, req);
            return false;
        }

        if (!req.Unverifiable || (req.HasCheckableRequirement && identity is { Outcome: GateOutcome.Blocked }))
        {
            reason = null;
            status = UnlockStatus.CollectionLocked;
            return false;
        }

        status = UnlockStatus.RequirementsUnknown;
        reason = req.Label is { Length: > 0 } label ? label : "has a requirement Wayfarer cannot read";
        return true;
    }

    /// <summary>The three-tier fallback for requirement text, as far as this codebase currently
    /// wires it (see <c>data/README.md</c>): prefer the game's own words
    /// (<see cref="UnlockRequirement.ConditionSource"/>, resolved live against the player's own
    /// client), then the curated <see cref="UnlockRequirement.Label"/> — which must stay short and
    /// honestly ours, never a paraphrased list of conditions — and only when both miss, an
    /// admission that the game does not say. The seam is general-purpose: any future requirement
    /// that sets <see cref="UnlockRequirement.RequiresAnotherPlayer"/> and a
    /// <see cref="UnlockRequirement.ConditionSource"/> gets the same resolution, no entry-specific
    /// code required.</summary>
    private static string ResolveConditionDetail(UnlockGateContext ctx, UnlockRequirement req)
    {
        if (req.ConditionSource is { } source && ctx.ResolveGameText?.Invoke(source) is { Length: > 0 } fromGame)
        {
            return fromGame;
        }

        return req.Label is { Length: > 0 } label ? label : "The game does not say more than that.";
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

    /// <summary>Why a job/level gate is closed, said in the game's own name for the job set rather
    /// than by enumerating it. See <see cref="JobGateText"/> for the whole of that argument — the
    /// short of it is that a thirty-job category has a name and the name is one line.</summary>
    private static string BuildJobLevelReason(ResolvedUnlock u, bool cat1Real)
    {
        var cat0Reason = u.RequiredJobRowIds.Count == 0
            ? $"needs level {u.QuestLevel}"
            : $"needs {JobGateText.Describe(u.RequiredJobCategoryName, u.RequiredJobNames, u.QuestLevel)}";

        if (!cat1Real)
        {
            return cat0Reason;
        }

        var cat1 = JobGateText.Describe(
            u.AltRequiredJobCategoryName, u.AltRequiredJobNames, u.AltRequiredJobLevel);
        return $"{cat0Reason} or {(cat1.Length > 0 ? cat1 : "an alternate job")}";
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
    /// leaking sideways into a tier it says nothing about.</para>
    ///
    /// <para>The channel is in the key for the same reason the level is, and became necessary when
    /// the catalogue started listing every kind of unlock rather than mostly duties: two DIFFERENT
    /// things can share a name and a level. The quest behind "The Promise of Tomorrow" grants both a
    /// title and an orchestrion roll of that name, and "Tiisol Ja" is both a custom-delivery client
    /// and that client's crafting-log division. Those are two unlocks with two sheet rows, and
    /// finishing one says nothing about the other — whereas the three "Levequests" rows, one per
    /// starting city, are one unlock and share a channel. Alternatives are always the same kind of
    /// thing; a collision never is.</para></summary>
    private readonly record struct AlternativeGroup(string Unlock, int? Level, string Channel)
    {
        public static AlternativeGroup Of(ResolvedUnlock u) => new(u.Def.Unlock, u.Def.Level, u.Def.Channel);
    }
}
