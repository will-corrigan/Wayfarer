namespace Wayfarer.App;

/// <summary>The game's own name for one of its windows, taken from the name of the struct that maps
/// it rather than written out again. Every mapped window is named <c>Addon</c> and then the name
/// the game knows it by, so <c>AddonChatLog</c> is the window called <c>ChatLog</c>, and a rename
/// on either side becomes a build error rather than a lookup that quietly finds nothing.
///
/// <para>Only windows the mapping covers can be named this way. A window with no struct of its own,
/// the Main Scenario Guide among them, is named by a constant beside the rest of its
/// measurements.</para></summary>
internal static class GameAddon
{
    private const string StructPrefix = "Addon";

    /// <inheritdoc cref="GameAddon"/>
    /// <typeparam name="T">The struct that maps the window.</typeparam>
    public static string NameOf<T>()
        where T : unmanaged => typeof(T).Name[StructPrefix.Length..];
}
