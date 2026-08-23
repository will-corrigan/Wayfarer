using KamiToolKit.BaseTypes;
using KamiToolKit.BaseTypes.ComponentNode;
using KamiToolKit.Interfaces;
using KamiToolKit.Nodes;
using Wayfarer.Core.Ui;

namespace Wayfarer.Windows.Native;

/// <summary>Numbers a region of a native window into the game's cursor-navigation graph.
///
/// KamiToolKit populates that graph automatically only for the children of its own list containers,
/// and only when the container's <c>NavIndex</c> is non-zero; anything added directly to an addon
/// gets nothing, which is why every control in the hub used to be an unreachable island. This
/// walker closes that gap by numbering whatever is actually on screen, and it renumbers the whole
/// region every time rather than patching, because the indices are absolute and dense.
///
/// Two cases have to be handled separately and cannot be unified by a type test: <c>ComponentNode</c>
/// carries the five <c>Nav*</c> members structurally (they forward straight into the game's
/// <c>AtkComponentBase.CursorNavigationInfo</c>) but does <b>not</b> declare
/// <see cref="IControllerNavigable"/>, while <c>TabBarNode</c>/<c>ListNode</c>/<c>ListBoxNode</c>
/// declare the interface but store the values as plain properties consumed by their own layout pass.
///
/// Invisible subtrees are skipped entirely: a hidden element that kept an index would be a hole the
/// cursor could fall into and not come back from.</summary>
internal static class NavigationWalker
{
    /// <summary>Numbers every visible navigable element under <paramref name="root"/>, laying it out
    /// as a vertical stack of rows: the children of a horizontal container form one row that chains
    /// left/right, everything else is a row of its own. The first row's "up" and the last row's
    /// "down" leave the region via <paramref name="navUp"/> / <paramref name="navDown"/>.</summary>
    /// <param name="root">Subtree to number.</param>
    /// <param name="startIndex">First index to allocate.</param>
    /// <param name="navUp">Where "up" from the first row leaves the region.</param>
    /// <param name="navDown">Where "down" from the last row leaves the region.</param>
    /// <param name="ceiling">Highest index this region is allowed to occupy. The hub's control
    /// region shares one byte-wide space with the tab bar above it and the list block below, so its
    /// real limit is where the list starts, not 255.</param>
    /// <returns>The next free index after this region, or <paramref name="startIndex"/> when there
    /// was nothing visible to number or the region would not fit.</returns>
    public static int Apply(NodeBase root, int startIndex, int navUp, int navDown, int ceiling)
    {
        var rows = new List<List<NavTarget>>();
        CollectRows(root, rows);
        if (rows.Count == 0)
        {
            return startIndex;
        }

        var sizes = rows.ConvertAll(row => row.Count);
        if (!NavGraphPlanner.Fits(sizes, startIndex, ceiling))
        {
            // Two ways to overrun and both are silent. Past 255, KamiToolKit's unchecked byte cast
            // turns an index into 0 ("not in the graph") and the region disappears. Past the
            // neighbouring region's start, the indices are valid but belong to something else, and
            // the cursor teleports out of this window's controls into a list row. A region that is
            // uniformly unreachable is better than either, so refuse and leave it alone; the caller
            // sees startIndex back and says so in the log.
            return startIndex;
        }

        var plan = NavGraphPlanner.Plan(sizes, startIndex, navUp, navDown);
        for (var row = 0; row < rows.Count; row++)
        {
            for (var column = 0; column < rows[row].Count; column++)
            {
                rows[row][column].Apply(plan[row][column]);
            }
        }

        return NavGraphPlanner.HighestIndex(sizes, startIndex) + 1;
    }

    /// <summary>The number of rows <see cref="Apply"/> would produce, for the one-line graph
    /// summary logged after every rebuild.</summary>
    public static int CountTargets(NodeBase root)
    {
        var rows = new List<List<NavTarget>>();
        CollectRows(root, rows);
        var total = 0;
        foreach (var row in rows)
        {
            total += row.Count;
        }

        return total;
    }

    // A horizontal container's contents read as one row: left/right moves between them and
    // up/down leaves the row as a whole. Chaining them vertically like everything else would make
    // a five-chip filter row cost five presses to walk past.
    private static bool IsHorizontal(NodeBase node) =>
        node is HorizontalListNode or AlignedHorizontalListNode or HorizontalFlexNode;

    private static void CollectRows(NodeBase node, List<List<NavTarget>> rows)
    {
        if (!node.IsVisible)
        {
            return;
        }

        if (TryTarget(node) is { } target)
        {
            rows.Add([target]);
            return;
        }

        if (node is not ILayoutListNode layout)
        {
            return;
        }

        if (IsHorizontal(node))
        {
            var row = new List<NavTarget>();
            CollectFlat(node, row);
            if (row.Count > 0)
            {
                rows.Add(row);
            }

            return;
        }

        foreach (var child in layout.Nodes)
        {
            CollectRows(child, rows);
        }
    }

    private static void CollectFlat(NodeBase node, List<NavTarget> targets)
    {
        if (!node.IsVisible)
        {
            return;
        }

        if (TryTarget(node) is { } target)
        {
            targets.Add(target);
            return;
        }

        if (node is ILayoutListNode layout)
        {
            foreach (var child in layout.Nodes)
            {
                CollectFlat(child, targets);
            }
        }
    }

    private static NavTarget? TryTarget(NodeBase node) => node switch
    {
        ComponentNode component => NavTarget.From(component),
        IControllerNavigable navigable => NavTarget.From(navigable),
        _ => null,
    };

    /// <summary>One addressable element, hiding the <c>ComponentNode</c> / <see cref="IControllerNavigable"/>
    /// split behind a single setter.</summary>
    private sealed class NavTarget(Action<NavLink> apply)
    {
        public static NavTarget From(ComponentNode component) => new(link =>
        {
            component.NavIndex = link.Index;
            component.NavUp = link.Up;
            component.NavDown = link.Down;
            component.NavLeft = link.Left;
            component.NavRight = link.Right;
        });

        public static NavTarget From(IControllerNavigable navigable) => new(link =>
        {
            navigable.NavIndex = link.Index;
            navigable.NavUp = link.Up;
            navigable.NavDown = link.Down;
            navigable.NavLeft = link.Left;
            navigable.NavRight = link.Right;
        });

        public void Apply(NavLink link) => apply(link);
    }
}
