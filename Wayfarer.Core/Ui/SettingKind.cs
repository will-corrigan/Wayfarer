namespace Wayfarer.Core.Ui;

/// <summary>The shape of a setting, which is all a presentation needs to know in order to render
/// it. Deliberately tiny: every Wayfarer setting is a flag, a pick from a short fixed list, or a
/// number in a range, and nothing needs free-text entry — which matters, because a native text
/// input on a controller summons the on-screen keyboard.</summary>
public enum SettingKind
{
    /// <summary>On/off. Native: a checkbox. ImGui: a checkbox.</summary>
    Toggle,

    /// <summary>One of a short fixed list. Native: a button that cycles. ImGui: a combo.</summary>
    Choice,

    /// <summary>A number in a range. Native: a slider. ImGui: a slider.</summary>
    Scale,
}
