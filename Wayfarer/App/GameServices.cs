using Autofac.Builder;
using Autofac.Core;
using Autofac.Core.Registration;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Wayfarer.App;

/// <summary>Dalamud's own services, fetched from Dalamud the moment the container is asked for
/// one, rather than listed by hand in the composition root.
///
/// <para>A list written twice — once as the services the plugin's constructor takes, once as the
/// registrations made from them — is a list that can disagree with itself, and it disagrees at
/// load, in the game, with the whole plugin failing to start over one name. This source ends the
/// list: a class asks for a service by taking it, and nothing else has to be told.</para>
///
/// <para>Dalamud fills in a property marked <see cref="PluginServiceAttribute"/> by the type that
/// property is declared as, so one <see cref="Holder{T}"/> closed over the wanted type is the
/// whole of asking. It fills it through the plugin's own service scope, which is what makes a
/// service scoped to this plugin get cleaned up with it.</para>
///
/// <para>Every service belongs to Dalamud, which made it and will dispose it, so nothing this
/// source hands out is owned by the container.</para></summary>
internal sealed class GameServices(IDalamudPluginInterface plugin) : IRegistrationSource
{
    /// <summary>Where Dalamud keeps the services a plugin may ask for. Taken from one of them
    /// rather than written out, so the namespace moving is a build error.</summary>
    private static readonly string ServicesNamespace = typeof(IPluginLog).Namespace!;

    /// <summary>The one service a <see cref="Holder{T}"/> was made to be given, whatever its
    /// type, so the generic it was closed over does not have to be named again to read it.</summary>
    private interface IHolder
    {
        object? Service { get; }
    }

    /// <inheritdoc/>
    public bool IsAdapterForIndividualComponents => false;

    /// <inheritdoc/>
    public IEnumerable<IComponentRegistration> RegistrationsFor(Service service, Func<Service, IEnumerable<ServiceRegistration>> registered)
    {
        ArgumentNullException.ThrowIfNull(registered);

        // Anything already registered is left alone: this source only answers for what nothing
        // else has, so a service the composition root wires up by hand still wins.
        if (service is not IServiceWithType wanted || !IsGameService(wanted.ServiceType) || registered(service).Any())
        {
            return [];
        }

        return
        [
            RegistrationBuilder
                .ForDelegate(wanted.ServiceType, (_, _) => Ask(wanted.ServiceType))
                .ExternallyOwned()
                .SingleInstance()
                .CreateRegistration(),
        ];
    }

    private static bool IsGameService(Type type) =>
        type.IsInterface && string.Equals(type.Namespace, ServicesNamespace, StringComparison.Ordinal);

    /// <summary>Asks Dalamud for one service by type. Throws rather than handing back nothing: a
    /// null service would be a null reference somewhere far away from the cause.</summary>
    private object Ask(Type service)
    {
        var holder = (IHolder)Activator.CreateInstance(typeof(Holder<>).MakeGenericType(service))!;
        if (!plugin.Inject(holder))
        {
            throw new DependencyResolutionException($"Dalamud would not hand over {service.Name}.");
        }

        return holder.Service ?? throw new DependencyResolutionException($"Dalamud left {service.Name} empty.");
    }

    /// <summary>Somewhere for Dalamud to put one service. Its property is public on both sides
    /// because Dalamud sets it by reflection.</summary>
    /// <typeparam name="T">The service wanted.</typeparam>
    private sealed class Holder<T> : IHolder
        where T : class
    {
        [PluginService]
        public T? Value { get; set; }

        /// <inheritdoc/>
        object? IHolder.Service => Value;
    }
}
