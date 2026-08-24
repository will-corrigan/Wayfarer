using System.Numerics;
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

    /// <summary>The size an icon is authored at, which is what an image node's part rectangle has to
    /// be or it samples past the edge of the texture and draws a band of nothing.
    ///
    /// <para>Read off the loaded texture when the game can answer immediately; this is the seed for
    /// when it cannot. The three blocks this plugin draws from were measured against the live
    /// install: the 60640 padlock composites are 24x24, the 63xxx Hunting Log creature portraits are
    /// 48x48, and the 71000 markers are 32x32.</para></summary>
    /// <para><b>Why the reward row does not come through here.</b> A reward's icon cannot be sized
    /// by its id: the sheets' icon blocks interleave. Ornament art starts at 786 and runs to 8057,
    /// straight through the mount block; BeastTribe's 36x36 crests at 65016 sit inside the item
    /// range. What a reward icon <i>can</i> be sized by is the sheet it came from, which the caller
    /// already knows — see <see cref="HubRewardIcons"/>, which returns the authored size with the
    /// id.</para>
    public static Vector2 SourceSize(uint iconId) => iconId switch
    {
        >= 60000 and < 61000 => new Vector2(24f, 24f),
        >= 63000 and < 64000 => new Vector2(48f, 48f),
        _ => new Vector2(32f, 32f),
    };

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
