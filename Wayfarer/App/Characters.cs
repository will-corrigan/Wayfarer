using Dalamud.Plugin.Services;

namespace Wayfarer.App;

/// <summary>Who is playing, for everything Wayfarer remembers per character: a player with several
/// characters is following something different on each.</summary>
internal static class Characters
{
    /// <summary>The logged-in character's content id, or null while nobody is logged in. Game
    /// thread only: it is the game's own state, read unguarded.</summary>
    public static ulong? LoggedIn(this IPlayerState player) => player.ContentId is var id and not 0 ? id : null;
}
