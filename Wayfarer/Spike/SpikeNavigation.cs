using KamiToolKit.BaseTypes;
using KamiToolKit.BaseTypes.ComponentNode;
using KamiToolKit.Interfaces;

namespace Wayfarer.Spike;

/// <summary>THROWAWAY SPIKE CODE — the whole <c>Wayfarer/Spike</c> folder is temporary and is
/// meant to be deleted in a single commit once its findings are written up. Nothing outside
/// <c>Wayfarer/Spike</c> may depend on it beyond the few lines of wiring in <see cref="Plugin"/>.
///
/// Faithful port of <c>Zeffuro/NativeMeters</c> →
/// <c>NativeMeters/Nodes/Configuration/ConfigurationNavigation.cs</c> (<c>Apply</c> +
/// <c>EnumerateNavigationTargets</c>), narrowed to the node kinds this spike actually builds.
/// The point of copying it rather than hand-numbering is that the index graph is absolute and
/// dense: any visibility/filter/size change has to renumber the whole region, which a walker does
/// for free and hand-numbering does not.</summary>
internal static class SpikeNavigationWalker
{
    /// <summary>Numbers every visible navigable descendant of <paramref name="root"/> from
    /// <paramref name="startIndex"/> upwards, chaining them vertically, with the first element's
    /// up-neighbour set to <paramref name="navUp"/> and the last element's down-neighbour set to
    /// <paramref name="navDown"/>. Returns the next free index.</summary>
    public static int Apply(NodeBase root, int startIndex, int navUp, int navDown, int navLeft = 0, int navRight = 0)
    {
        var targets = Enumerate(root).Where(target => target.IsVisible).ToList();
        if (targets.Count == 0)
        {
            return startIndex;
        }

        for (var index = 0; index < targets.Count; index++)
        {
            var navIndex = startIndex + index;
            var up = index == 0 ? navUp : navIndex - 1;
            var down = index == targets.Count - 1 ? navDown : navIndex + 1;

            targets[index].SetNavigation(navIndex, up, down, navLeft, navRight);
        }

        return startIndex + targets.Count;
    }

    /// <summary>Depth-first walk that stops at the first navigable thing it finds on each branch:
    /// a component node owns a nav slot itself, so its children are never separate slots. Invisible
    /// subtrees are skipped entirely — a hidden element that kept an index would be an unreachable
    /// hole in the graph.</summary>
    private static IEnumerable<SpikeNavTarget> Enumerate(NodeBase node)
    {
        if (!node.IsVisible)
        {
            yield break;
        }

        if (node is ComponentNode componentNode)
        {
            yield return SpikeNavTarget.From(componentNode);
            yield break;
        }

        if (node is IControllerNavigable navigable)
        {
            yield return SpikeNavTarget.From(node, navigable);
            yield break;
        }

        if (node is ILayoutListNode layoutNode)
        {
            foreach (var childNode in layoutNode.Nodes)
            {
                foreach (var target in Enumerate(childNode))
                {
                    yield return target;
                }
            }
        }
    }
}

/// <summary>THROWAWAY SPIKE CODE — see <see cref="SpikeNavigationWalker"/>. One navigable element
/// the walker can address. Exists because <c>ComponentNode</c> carries the five Nav* members
/// structurally but does not declare <see cref="IControllerNavigable"/>, so the two cases cannot be
/// unified by a type test alone — exactly the split the reference implementation makes.</summary>
internal sealed class SpikeNavTarget
{
    private readonly NodeBase node;
    private readonly Action<int, int, int, int, int> apply;

    private SpikeNavTarget(NodeBase node, Action<int, int, int, int, int> apply)
    {
        this.node = node;
        this.apply = apply;
    }

    public bool IsVisible => node.IsVisible;

    public string TypeName => node.GetType().Name;

    public static SpikeNavTarget From(ComponentNode component) => new(component, (index, up, down, left, right) =>
    {
        component.NavIndex = index;
        component.NavUp = up;
        component.NavDown = down;
        component.NavLeft = left;
        component.NavRight = right;
    });

    public static SpikeNavTarget From(NodeBase node, IControllerNavigable navigable) => new(node, (index, up, down, left, right) =>
    {
        navigable.NavIndex = index;
        navigable.NavUp = up;
        navigable.NavDown = down;
        navigable.NavLeft = left;
        navigable.NavRight = right;
    });

    public void SetNavigation(int index, int up, int down, int left, int right) => apply(index, up, down, left, right);
}
