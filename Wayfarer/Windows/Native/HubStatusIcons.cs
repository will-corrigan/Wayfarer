using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;
using Wayfarer.Core.Ui;

namespace Wayfarer.Windows.Native;

/// <summary>Resolves the status vocabulary's icon ids against the game's own texture table before
/// any of them is drawn, and remembers the answer.
///
/// <para>This is the same guard <see cref="NamePlateMarkers"/> uses on the marker icon, and for the
/// same reason: "does this id exist in this patch?" is otherwise a question only somebody looking at
/// a screen can answer, and the failure mode is a silently blank column rather than an error. A
/// patch that renumbers or removes one of these must degrade to a row that still says what state it
/// is in — in words — not to a row with a hole where the state used to be.</para>
///
/// <para>Resolved once per session, lazily, on the first row that asks. The lookup is against
/// Dalamud's shared texture cache, so a hit costs nothing after the first; caching the
/// <b>miss</b> is the part that matters, because a missing id would otherwise be looked up for
/// every row of every rebuild.</para></summary>
internal sealed class HubStatusIcons(ITextureProvider textures, IPluginLog log)
{
    private readonly Dictionary<uint, bool> resolved = [];

    private bool loggedFailure;

    /// <summary>The id to draw, or 0 when it could not be resolved and the caller should fall back
    /// to words. Never throws: a texture lookup must not be the thing that stops a list from being
    /// built.</summary>
    public uint Resolve(uint iconId)
    {
        if (iconId == 0)
        {
            return 0;
        }

        if (resolved.TryGetValue(iconId, out var ok))
        {
            return ok ? iconId : 0;
        }

        ok = Probe(iconId);
        resolved[iconId] = ok;

        if (!ok && !loggedFailure)
        {
            // Once per session. Every row of every rebuild would otherwise report the same id, and
            // the first line already carries the whole story.
            loggedFailure = true;
            log.Warning(
                $"Wayfarer hub: status icon {iconId} does not resolve in this game version, so the rows it "
                + "belongs to will name their state in words instead. Nothing else is affected.");
        }

        return ok ? iconId : 0;
    }

    /// <summary>The state's shape, already validated. 0 means "say it in words instead".</summary>
    public uint For(Wayfarer.Core.Unlocks.UnlockStatus status) => Resolve(UnlockStatusDisplay.IconId(status));

    private bool Probe(uint iconId)
    {
        try
        {
            return textures.TryGetFromGameIcon(new GameIconLookup(iconId), out var texture)
                   && texture.TryGetWrap(out _, out _);
        }
        catch (Exception ex)
        {
            log.Warning(ex, $"Wayfarer hub: status icon {iconId} could not be checked, so it will not be drawn.");
            return false;
        }
    }
}
