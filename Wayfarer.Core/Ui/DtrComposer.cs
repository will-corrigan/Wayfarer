using Wayfarer.Core.Navigation;

namespace Wayfarer.Core.Ui;

/// <summary>Turns the same guidance snapshot the readout draws from into the one short line the
/// server info bar entry can afford.
///
/// This exists because the readout is a click-through overlay by design (see
/// <c>Windows.Native.GuidanceOverlay</c>'s doc comment) and the ImGui window it falls back to is
/// only ever on screen when that overlay is off — so on a default setup neither surface is a
/// reliable, always-present, always-clickable way back into Wayfarer. The server info bar entry
/// this feeds is that surface, and it is exactly as testable as the readout's own text for the
/// same reason: the decision of what to say lives here, not in whatever draws it.
///
/// <b>Every part of the entry has to mean something specific.</b> The entry used to read something
/// like "❗ ⌂ Stop 1/6": an alert, an aetheryte crystal that meant only "a route exists", and the
/// route's progress. The player asked why there was an aetheryte on it when the target was
/// fifty-six yalms away in the same zone, and he was right — the crystal was describing the mode
/// rather than the next step, so it was actively misleading. The rule now:
///
/// <list type="bullet">
/// <item><description>The next step is a <b>teleport</b> or an <b>aethernet hop</b> — aetheryte
/// glyph, and say where.</description></item>
/// <item><description>The next step is a <b>walk</b> — no glyph at all, and say the two things worth
/// knowing at a glance: how far, and how far through.</description></item>
/// <item><description><b>Nothing engaged</b> — the "there is something to pick up here" alert, or
/// the plugin's bare name.</description></item>
/// </list>
///
/// The alert rides alongside whatever is engaged rather than replacing it. It is not guidance and
/// never competes with the active objective — it is the "there is something to grab near you"
/// signal, and it has to survive being in the middle of something, because being in the middle of
/// something is when the player walks past things.</summary>
public static class DtrComposer
{
    public static DtrText Compose(DtrInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        if (!inputs.Engaged)
        {
            return Idle(inputs.NearbyUnlockCount);
        }

        var alert = inputs.NearbyUnlockCount > 0;
        return inputs.Step switch
        {
            DtrNextStep.Teleport => Aetheryte("Teleport", inputs.StepTarget, alert),
            DtrNextStep.Aethernet => Aetheryte("Aethernet", inputs.StepTarget, alert),
            _ => Walking(inputs, alert),
        };
    }

    private static DtrText Idle(int nearbyUnlockCount) => nearbyUnlockCount switch
    {
        0 => DtrText.Wayfarer,
        1 => new DtrText("1 unlock here", DtrGlyph.None, true),
        var n => new DtrText($"{n} unlocks here", DtrGlyph.None, true),
    };

    /// <summary>"Teleport: Horizon", "Aethernet: Aetheryte Plaza" — the verb plus the destination.
    /// Falls back to the bare verb when the leg has no name to give, which is still a true
    /// statement about the next step.</summary>
    private static DtrText Aetheryte(string verb, string? target, bool alert) =>
        new(target is { Length: > 0 } name ? $"{verb}: {name}" : verb, DtrGlyph.Aetheryte, alert);

    /// <summary>Walking there: the progress through the plan and the distance left, and no glyph.
    /// Either half may be missing — a solo hunt has no stop count, and a target with no coordinates
    /// has no distance — so the parts are joined rather than templated.</summary>
    private static DtrText Walking(DtrInputs inputs, bool alert)
    {
        var progress = Progress(inputs);
        var distance = inputs.Step == DtrNextStep.Walk && inputs.DistanceYalms is { } yalms
            ? NavMath.FormatDistanceShort(yalms)
            : null;

        var text = (progress, distance) switch
        {
            ({ Length: > 0 }, { Length: > 0 }) => $"{progress}, {distance}",
            ({ Length: > 0 }, _) => progress,
            (_, { Length: > 0 }) => distance,
            _ => null,
        };

        return text is { Length: > 0 } words
            ? new DtrText(words, DtrGlyph.None, alert)
            : new DtrText(DtrText.Wayfarer.Text, DtrGlyph.None, alert);
    }

    private static string? Progress(DtrInputs inputs)
    {
        if (inputs.RouteStop is { } stop && inputs.RouteTotal is { } total)
        {
            return $"{stop}/{total}";
        }

        return inputs.HuntingIsPrimary && inputs.HuntingLabel is { Length: > 0 } hunting ? hunting : null;
    }
}
