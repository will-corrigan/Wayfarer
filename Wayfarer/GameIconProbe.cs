using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;

namespace Wayfarer;

/// <summary>Whether a game icon id currently resolves to a loadable texture — the two-call check
/// against Dalamud's texture cache that decides whether an id survives a patch, done in one place.
///
/// <para><b>Why this is one method and not two.</b> <see cref="NamePlateMarkers"/>'s nameplate-marker
/// icon and <see cref="Windows.Native.HubStatusIcons"/>'s status icons both had to answer "does this
/// id exist in this patch?" — an id that has been renumbered or removed must degrade to no marker, or
/// to a state named in words, rather than draw a hole. Both wrote the same
/// <c>TryGetFromGameIcon</c>/<c>TryGetWrap</c> pair to answer it. What genuinely differs between the
/// two callers — a single-value cache against a many-id cache, and two different warning messages —
/// stays with each of them; only the probe itself lives here.</para></summary>
internal static class GameIconProbe
{
    /// <summary>True when the id loads. Throws whatever <c>TryGetFromGameIcon</c>/<c>TryGetWrap</c>
    /// throw — callers decide how a resolution failure is logged and how it is cached.</summary>
    public static bool Exists(ITextureProvider textures, uint iconId) =>
        textures.TryGetFromGameIcon(new GameIconLookup(iconId), out var texture)
        && texture.TryGetWrap(out _, out _);
}
