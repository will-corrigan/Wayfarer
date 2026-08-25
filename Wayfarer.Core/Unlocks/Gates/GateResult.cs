namespace Wayfarer.Core.Unlocks.Gates;

/// <summary>The three answers a gate may give. There is no fourth, and there is no default.</summary>
public enum GateOutcome
{
    /// <summary>The player meets this requirement. Read live, not assumed.</summary>
    Satisfied,

    /// <summary>The player does not meet this requirement, and we can say why.</summary>
    Blocked,

    /// <summary>We cannot currently tell. Not a failure mode — the correct answer whenever the
    /// live read that would decide the gate is not authoritative right now.</summary>
    Indeterminate,
}

/// <summary>The only thing an evaluator may say.
///
/// <para><see cref="GateOutcome.Indeterminate"/> is what stops a confident falsehood reaching the
/// player: a reader that cannot answer must never be allowed to look like a reader that answered
/// "no". <see cref="Status"/> is the status the calculator publishes when
/// <see cref="GateOutcome.Blocked"/>, and it must be an existing <see cref="UnlockStatus"/> member,
/// because that enum is the stable contract with the UI — an evaluator may not invent one.</para></summary>
public readonly record struct GateResult(GateOutcome Outcome, UnlockStatus Status, string? Reason)
{
    public static GateResult Ok() => new(GateOutcome.Satisfied, UnlockStatus.Available, null);

    public static GateResult Blocked(UnlockStatus status, string reason) =>
        new(GateOutcome.Blocked, status, reason);

    public static GateResult Unknown(string reason) =>
        new(GateOutcome.Indeterminate, UnlockStatus.RequirementsUnknown, reason);
}
