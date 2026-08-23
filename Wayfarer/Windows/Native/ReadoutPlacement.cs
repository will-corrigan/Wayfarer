using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Wayfarer.Windows.Native;

/// <summary>Where the readout sits, in raw screen pixels. Shared by both hosts so the readout does
/// not move when the player switches between a mouse and a controller.
///
/// A plugin cannot join the game's HUD Layout editor, so the default instead <b>follows the game's
/// own quest tracker</b> — including the way the tracker mirrors itself when the player moves it to
/// the left half of the screen — which puts Wayfarer's guidance exactly where the player already
/// looks for objectives, wherever they have chosen to put that. The corner presets are the fallback
/// for anyone the default does not suit, and all of them respect the ten-foot safe area.</summary>
internal static unsafe class ReadoutPlacement
{
    // Microsoft's ten-foot guidance, and the reason it is here: on a TV the outer few percent of
    // the panel is behind the bezel or lost to overscan.
    private const float SafeMarginX = 48f;
    private const float SafeMarginY = 27f;

    public static Vector2 Resolve(ReadoutPosition preset, Vector2 size)
    {
        var screen = new Vector2(AtkStage.Instance()->ScreenSize.Width, AtkStage.Instance()->ScreenSize.Height);

        if (preset == ReadoutPosition.FollowQuestTracker && TryFollowQuestTracker(screen, size) is { } followed)
        {
            return followed;
        }

        var right = Math.Max(screen.X - size.X - SafeMarginX, SafeMarginX);
        var bottom = Math.Max(screen.Y - size.Y - SafeMarginY, SafeMarginY);
        return preset switch
        {
            ReadoutPosition.TopRight => new Vector2(right, SafeMarginY),
            ReadoutPosition.BottomLeft => new Vector2(SafeMarginX, bottom),
            ReadoutPosition.BottomRight => new Vector2(right, bottom),
            _ => new Vector2(SafeMarginX, SafeMarginY),
        };
    }

    private static Vector2? TryFollowQuestTracker(Vector2 screen, Vector2 size)
    {
        var tracker = RaptureAtkUnitManager.Instance()->GetAddonByName("_ToDoList");
        if (tracker is null || !tracker->IsVisible)
        {
            return null;
        }

        var trackerPosition = new Vector2(tracker->X, tracker->Y);
        var trackerSize = new Vector2(tracker->RootNode->Width, tracker->RootNode->Height) * tracker->Scale;
        if (trackerSize.Y <= 0f)
        {
            return null;
        }

        // The tracker mirrors its own layout depending on which half of the screen it is on, so
        // match it: hang below it on the left, and align right edges on the right.
        var below = trackerPosition.Y + trackerSize.Y + 8f;
        var x = trackerPosition.X < screen.X / 2f
            ? trackerPosition.X
            : trackerPosition.X + trackerSize.X - size.X;

        return new Vector2(
            Math.Clamp(x, SafeMarginX, Math.Max(screen.X - size.X - SafeMarginX, SafeMarginX)),
            Math.Clamp(below, SafeMarginY, Math.Max(screen.Y - size.Y - SafeMarginY, SafeMarginY)));
    }
}
