using System.Reflection;
using FFXIVClientStructs.Attributes;

namespace Wayfarer.App;

/// <summary>The game's own name for one of its windows, taken from the struct that maps it rather
/// than written out again, so a rename on either side becomes a build error rather than a lookup
/// that quietly finds nothing.
///
/// <para>The mapping states the name itself, and it is not always the struct's own: some windows
/// are called things like <c>_ActionBar</c>, which no amount of trimming the type name produces.
/// Some are known by more than one name, and the first is the one they answer to.</para>
///
/// <para>Only windows the mapping covers can be named this way. A window with no struct of its own,
/// the Main Scenario Guide among them, is named by a constant beside the rest of its
/// measurements.</para></summary>
internal static class GameAddon
{
    /// <inheritdoc cref="GameAddon"/>
    /// <typeparam name="T">The struct that maps the window.</typeparam>
    /// <exception cref="InvalidOperationException">The struct does not say what its window is
    /// called, so there is no name to look anything up by.</exception>
    public static string NameOf<T>()
        where T : unmanaged =>
        typeof(T).GetCustomAttribute<AddonAttribute>()?.AddonIdentifiers.FirstOrDefault()
        ?? throw new InvalidOperationException($"{typeof(T).Name} does not name the window it maps.");
}
