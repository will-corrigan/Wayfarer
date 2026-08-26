using Wayfarer.Core.Guidance;
using Wayfarer.Guidance.Sources;

namespace Wayfarer.Modules;

/// <summary>Routes the player through the aether currents their zone still owes them.
///
/// <para>The smallest module Wayfarer has, and deliberately so: it owns no window and no tab. Aether
/// currents are already listed in the game's own Aether Currents panel, which says perfectly well
/// WHICH ones are missing; what it does not do is put them in an order or point at the next one. So
/// this module adds a route and nothing else, and reaches the player through the readout and the
/// game's own menu — the surfaces every other route already uses.</para></summary>
internal sealed class AetherCurrentsModule(
    IGuidanceArbiter arbiter,
    AetherCurrentService currents,
    AetherCurrentSource source) : IModule
{
    public string Name => "Aether Currents";

    public string Description => "Routes you through the aether currents a zone still owes you.";

    public bool Enabled { get; private set; }

    /// <summary>Read by <see cref="GuidanceActions"/> to decide whether there is a route to offer and
    /// how much of one.</summary>
    internal AetherCurrentService Currents { get; } = currents;

    /// <summary>Starts the route for a zone. The one way in, so the game's menu and the readout's
    /// menu drive exactly the same thing.</summary>
    public void StartRoute(uint territory) => source.StartRoute(territory);

    public void Enable()
    {
        Enabled = true;

        // Registration is the whole of enabling: with the source unregistered there is no route to
        // engage and no arrow to lose, which is what "off" should mean. There is no framework
        // subscription because nothing has to be kept fresh — every read this feature makes is free
        // at the moment it is asked.
        arbiter.Register(source);
    }

    public void Disable()
    {
        Enabled = false;

        // Unregistering releases the engagement token if a current route owns the arrow.
        arbiter.Unregister(source);
    }

    public void Dispose()
    {
        if (Enabled)
        {
            Disable();
        }
    }
}
