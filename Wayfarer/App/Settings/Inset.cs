using System.Numerics;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;

namespace Wayfarer.App.Settings;

/// <summary>Holds one thing shifted inside a box of its own: stepped in from the left, or dropped
/// to the middle of a taller row.
///
/// <para>A list lays its own children out, setting each one's X as it goes and leaving every one of
/// them at the top of the row -- so a thing moved by hand is put straight back the next time
/// anything on the page changes, which is why the settings page had an indent in it that never once
/// appeared and sliders that never lined up with their labels. What a list does not touch is the
/// inside of a child, so the shift is kept here: the list places this, and this places what it
/// holds.</para></summary>
internal sealed class Inset : ResNode
{
    private Inset(NodeBase held, float across, float down, float width, float height)
    {
        held.Position = new Vector2(across, down);
        Size = new Vector2(width, height);
        held.AttachNode(this);
    }

    /// <summary>Steps a node in from the left, to show it belongs to whatever is above it.</summary>
    /// <param name="held">What is being stepped in. It is attached here and freed with this.</param>
    /// <param name="by">How far in from the left.</param>
    public static Inset From(NodeBase held, float by) =>
        new(held, by, 0f, by + held.Width, held.Height);

    /// <summary>Drops a node to the middle of a row taller than itself, so a control sits level
    /// with the words naming it rather than riding above them.</summary>
    /// <param name="held">What is being centred. It is attached here and freed with this.</param>
    /// <param name="height">How tall the row is.</param>
    public static Inset Middle(NodeBase held, float height) =>
        new(held, 0f, MathF.Round((height - held.Height) / 2f), held.Width, height);
}
