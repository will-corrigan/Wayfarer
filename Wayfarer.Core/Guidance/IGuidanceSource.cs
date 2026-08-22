namespace Wayfarer.Core.Guidance;

/// <summary>Whether an objective is an explicit mode the player entered
/// (<see cref="Engaged"/> — a route, a hunt) or the passive default
/// (<see cref="Ambient"/> — following a quest).</summary>
public enum GuidanceEngagement
{
    Ambient,
    Engaged,
}

/// <summary>Why a source lost the engagement token. The reason is the difference between
/// "resumable" and "cancellable" — a preempted chain keeps its plan, a cancelled one drops
/// it.</summary>
public enum DisengageReason
{
    /// <summary>Another source took the token. KEEP the plan; re-engaging resumes at the same leg
    /// with the same progress.</summary>
    Preempted,

    /// <summary>The player pressed the exit. DROP the plan.</summary>
    UserCancelled,

    /// <summary><see cref="IGuidanceSource.Poll"/> returned null of its own accord. Nothing to
    /// drop.</summary>
    Completed,

    /// <summary>The module was turned off or unregistered — or its <see cref="IGuidanceSource.Poll"/>
    /// threw and it was treated as unavailable. DROP the plan.</summary>
    ModuleDisabled,
}

/// <summary>A feature that can own the arrow. One implementation per feature; the arbiter polls
/// them all identically and never asks any of them what they are.</summary>
public interface IGuidanceSource
{
    /// <summary>Stable id, matching the module's own id. Prefixes every
    /// <see cref="ObjectiveKey"/> this source produces.</summary>
    string SourceId { get; }

    /// <summary>THE COMPLETION CONTRACT. Returns the objective this source wants guidance for RIGHT
    /// NOW, or null when it has nothing (finished, disengaged, no data, not applicable).
    ///
    /// <list type="bullet">
    /// <item>same <see cref="ObjectiveKey"/> as last tick — still working on it (position/progress
    /// may differ)</item>
    /// <item>DIFFERENT <see cref="ObjectiveKey"/> — the previous objective is done; this is the
    /// next one</item>
    /// <item>null — finished or disengaged</item>
    /// </list>
    ///
    /// The arbiter never inspects the returned objective to second-guess any of this. There is no
    /// "is it done?" question it can ask, because the answer is not in the object: completion
    /// signals share no id space across features (quest accept/complete flags, a per-task kill-count
    /// byte array, eleven distinct unlock gate predicates), so completion cannot be centralised.
    ///
    /// Framework thread only. Must be cheap — this runs every tick; expensive recomputation belongs
    /// in the owning service's own change-detected pass. Must not throw; the arbiter guards anyway,
    /// but a throwing source forfeits the engagement token.</summary>
    GuidanceOffer? Poll(GuidanceContext ctx);

    /// <summary>Called exactly once when this source loses the engagement token. See
    /// <see cref="DisengageReason"/> — it is the difference between resuming and starting
    /// over.</summary>
    void OnDisengaged(DisengageReason reason);

    /// <summary>Source-PRIVATE side effects on the active objective changing. Default no-ops, so a
    /// source that needs neither implements neither.
    ///
    /// STRICT BOUNDARY — the reason these exist alongside <see cref="ObjectiveAffordances"/>:
    /// <list type="bullet">
    /// <item>SHARED / SINGLETON / DESTRUCTIVE game state (map flag, nameplates, sounds) is DECLARED
    /// via <see cref="ObjectiveAffordances"/> and performed by one framework coordinator. Never
    /// here.</item>
    /// <item>SOURCE-PRIVATE state (reset a live-tracking cache, kick a recompute, log a chain
    /// advance) is done here.</item>
    /// </list>
    /// Putting the flag in a source hook would hand every module a writer for a hard singleton that
    /// destroys the player's own flag — an invariant enforced by review instead of by type.</summary>
    void OnObjectiveActivated(GuidanceObjective objective)
    {
    }

    /// <summary>Counterpart to <see cref="OnObjectiveActivated"/>, called when this source's
    /// objective stops being the active one (it advanced, it finished, or another source took
    /// over).</summary>
    void OnObjectiveDeactivated(GuidanceObjective objective)
    {
    }
}

/// <summary>Everything a source is allowed to know about the world when it is polled. Deliberately
/// tiny and Dalamud-free: anything richer belongs in the source's own plugin-side service, which
/// keeps its state fresh on the framework thread.</summary>
public sealed record GuidanceContext(
    uint Territory, uint MapId, float PlayerX, float PlayerY, float PlayerZ, bool LoggedIn);

/// <summary>What a source offers for this tick.</summary>
public sealed record GuidanceOffer(GuidanceObjective Objective, GuidanceEngagement Engagement);
