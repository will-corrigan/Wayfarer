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
/// Priority mirrors the readout's own heading: a route's chain progress is the most specific
/// thing either surface can say, a solo hunt's rank and kill count is next, and — with nothing
/// engaged — a count of what is available right here beats the plugin's bare name.</summary>
public static class DtrComposer
{
    public static DtrText Compose(DtrInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        // The alert rides alongside whatever is engaged rather than replacing it. It is not
        // guidance and never competes with the active objective — it is the "there is something to
        // grab near you" signal, and it has to survive being in the middle of something, because
        // being in the middle of something is when the player is walking past things.
        var alert = inputs.NearbyUnlockCount > 0;

        if (inputs.Engaged)
        {
            if (inputs.RouteStop is { } stop && inputs.RouteTotal is { } total)
            {
                return new DtrText($"Stop {stop}/{total}", DtrGlyph.Route, alert);
            }

            if (inputs.HuntingIsPrimary && inputs.HuntingLabel is { Length: > 0 } hunting)
            {
                return new DtrText(hunting, DtrGlyph.Hunting, alert);
            }

            return alert ? new DtrText("Wayfarer", DtrGlyph.None, true) : DtrText.Wayfarer;
        }

        return inputs.NearbyUnlockCount switch
        {
            0 => DtrText.Wayfarer,
            1 => new DtrText("1 unlock here", DtrGlyph.None, true),
            var n => new DtrText($"{n} unlocks here", DtrGlyph.None, true),
        };
    }
}
