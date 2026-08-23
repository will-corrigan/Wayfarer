namespace Wayfarer.Core.Ui;

/// <summary>What a readout line does when it is clicked, on a surface that can be clicked at all.
///
/// The line is marked here, by the composer, rather than recognised by its wording where it is
/// drawn — which is what the plugin used to do, matching on a "(click)" suffix. Whether a line is
/// actionable is a property of the guidance, not of the English.</summary>
public enum ReadoutLineAction
{
    /// <summary>Read-only. Almost every line.</summary>
    None,

    /// <summary>Teleport to the aetheryte the line names. The default loop's one click.</summary>
    Teleport,
}
