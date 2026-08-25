using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;

namespace Wayfarer;

/// <summary>Whether a game icon id exists in this patch — the check against Dalamud's texture cache
/// that decides whether an id may be drawn, done in one place.
///
/// <para><b>Why this is one method and not two.</b> <see cref="NamePlateMarkers"/>'s nameplate-marker
/// icon and <see cref="Windows.Native.HubStatusIcons"/>'s status icons both had to answer "does this
/// id exist in this patch?" — an id that has been renumbered or removed must degrade to no marker, or
/// to a state named in words, rather than draw a hole. Both wrote the same
/// <c>TryGetFromGameIcon</c>/<c>TryGetWrap</c> pair to answer it. What genuinely differs between the
/// two callers — a single-value cache against a many-id cache, and two different warning messages —
/// stays with each of them; only the probe itself lives here.</para>
///
/// <para><b>Why it returns three states.</b> The two calls answer two different questions and only
/// one of them is about existence. <c>TryGetFromGameIcon</c> resolves the id to a path in the game's
/// files, so its failure really does mean "not in this patch". <c>TryGetWrap</c> only reports whether
/// that path's texture is in memory <i>right now</i>, and its own documentation says false covers both
/// "still being loaded" and "the load failed" — the <c>out</c> exception is what separates them. Read
/// as a single boolean, a cold cache is indistinguishable from a deleted icon.</para></summary>
internal static class GameIconProbe
{
    /// <summary>What the id is. Throws whatever <c>TryGetFromGameIcon</c>/<c>TryGetWrap</c> throw —
    /// callers decide how a failure is logged and how it is cached.
    ///
    /// <para>A <see cref="GameIconAvailability.Pending"/> result is a "come back later", not a
    /// verdict: nothing may be cached from it, because the very next frame may resolve it.</para></summary>
    public static GameIconAvailability Check(ITextureProvider textures, uint iconId)
    {
        if (!textures.TryGetFromGameIcon(new GameIconLookup(iconId), out var texture))
        {
            return GameIconAvailability.Absent;
        }

        if (texture.TryGetWrap(out _, out var failure))
        {
            return GameIconAvailability.Present;
        }

        // A load that threw is a real failure and worth remembering. A load that has not finished is
        // the ordinary state of a texture nobody has asked for before, and says nothing at all.
        return failure is null ? GameIconAvailability.Pending : GameIconAvailability.Absent;
    }
}
