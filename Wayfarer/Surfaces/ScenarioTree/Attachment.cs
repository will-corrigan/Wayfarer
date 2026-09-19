using KamiToolKit.BaseTypes;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>Building a node and hanging it on its parent, in one expression, so the field that
/// keeps it can be set from the same line that makes it.</summary>
internal static class Attachment
{
    /// <inheritdoc cref="Attachment"/>
    /// <typeparam name="T">The node's own type, which is what comes back.</typeparam>
    /// <param name="node">The node being hung.</param>
    /// <param name="parent">What it hangs from.</param>
    public static T AttachedTo<T>(this T node, NodeBase parent)
        where T : NodeBase
    {
        node.AttachNode(parent);
        return node;
    }
}
