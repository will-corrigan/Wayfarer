using Wayfarer.Core.Unlocks.Live;

namespace Wayfarer.Core.Unlocks.Gates;

/// <summary>Reads a duty node's <c>scope</c>, and refuses to guess.
///
/// <para>A duty id means nothing without the id space it belongs to. Handing a public-content id
/// to the instance-content reader does not fail — it reads a different duty's bit and answers,
/// confidently, about something else. Defaulting the scope would make that the common case, so an
/// absent or unrecognised scope is null and the caller reports Indeterminate.</para></summary>
internal static class DutyScope
{
    public static ContentSpace? Of(GateNode node) => node.Scope switch
    {
        GateKinds.ScopeInstance => ContentSpace.Instance,
        GateKinds.ScopePublic => ContentSpace.Public,
        _ => null,
    };
}
