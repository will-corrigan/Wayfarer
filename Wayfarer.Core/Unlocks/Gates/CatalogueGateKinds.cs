namespace Wayfarer.Core.Unlocks.Gates;

/// <summary>Which gate kinds a loaded catalogue actually asks for.
///
/// <para>Two jobs, both of which want the same answer. The dataset tests use it to prove that
/// every kind the shipped data names has an evaluator, so the two cannot drift apart without CI
/// noticing. The plugin uses it to decide whether it has any business asking the server for
/// something: three of the nineteen kinds read data the client does not hold until it has been
/// fetched, and fetching it when nothing in the catalogue would read it is the definition of a
/// speculative request.</para></summary>
public static class CatalogueGateKinds
{
    /// <summary>Every <c>kind</c> named anywhere in these definitions' requirement trees or in
    /// their <see cref="UnlockDefinition.State"/> gates, including nested combinator children.
    ///
    /// <para>Both, because both drive a read. A <c>state</c> gate is what proves the player has the
    /// entry's own identity, and if it were left out here the request that would answer it would
    /// never be made — the whole catalogue would say "we cannot tell" about facts that were one
    /// packet away.</para></summary>
    public static HashSet<string> Of(IEnumerable<UnlockDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var kinds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            if (definition.State is { } state)
            {
                Collect(state, kinds);
            }

            if (definition.Requires is not { } requires)
            {
                continue;
            }

            foreach (var node in requires.Gates)
            {
                Collect(node, kinds);
            }
        }

        return kinds;
    }

    private static void Collect(GateNode node, HashSet<string> kinds)
    {
        kinds.Add(node.Kind);
        foreach (var child in node.Children)
        {
            Collect(child, kinds);
        }
    }
}
