using Autofac;

namespace Wayfarer.App;

/// <summary>Registers the app: the part every feature module plugs into. Empty until its shapes
/// are decided; each service the app gains is registered here and nowhere else.</summary>
internal sealed class AppModule : Module
{
    /// <inheritdoc/>
    protected override void Load(ContainerBuilder builder)
    {
    }
}
