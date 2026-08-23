using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Wayfarer.Core.Ui;

namespace Wayfarer.Windows.Native;

/// <summary>Where the readout sits, in raw screen pixels. Shared by both hosts so the readout does
/// not move when the player switches between a mouse and a controller, and the single place the
/// player's chosen position is read and written.
///
/// <b>What changed and why.</b> This used to offer four corners and "follow the quest tracker", with
/// the tracker as the default. On a 16:9 television that default put the readout underneath the
/// minimap and the objective line was drawn behind it, unreadable. So: the presets remain, top and
/// bottom centre are added, everything is pushed clear of the minimap and the tracker rather than
/// being allowed to sit on them, and — the actual point — the player can put the readout wherever
/// they like. A mouse drags it (see <c>ReadoutMoveMode</c>); a controller nudges it with two sliders
/// in Settings. Either one switches to <see cref="ReadoutPosition.Custom"/> and keeps the result.
///
/// The arithmetic all lives in <see cref="ReadoutLayout"/>, where it is tested without a game
/// attached. What is left here is the part that can only be done live: reading the screen size and
/// the two HUD addons' rectangles, and owning the player's setting.</summary>
internal sealed unsafe class ReadoutPlacement(QuestHelperConfig cfg, Action saveConfig)
{
    /// <summary>The HUD elements the readout is never allowed to be parked on top of by a preset.
    /// Deliberately only these two: they are where the player already looks for objectives, they are
    /// the ones the reported overlap involved, and they are stationary. Adding transient addons
    /// (target info, the enemy list) would make the readout jump around as a fight starts.</summary>
    private static readonly string[] ObstacleAddons = ["_NaviMap", "_ToDoList"];

    private Vector2 lastSize = new(320f, 120f);
    private Vector2 lastScreen = new(1920f, 1080f);

    /// <summary>The readout's horizontal position as the settings slider sees it: 0 at the left safe
    /// margin, 1 at the right one. While a preset is selected this reads back wherever the preset
    /// actually put the readout, so nudging it continues from there instead of jumping.</summary>
    public float FractionX => Math.Clamp(cfg.ReadoutFractionX, 0f, 1f);

    /// <inheritdoc cref="FractionX"/>
    public float FractionY => Math.Clamp(cfg.ReadoutFractionY, 0f, 1f);

    /// <summary>This frame's position for a readout of <paramref name="size"/> pixels. Called from
    /// both hosts' per-frame paths, so it does no allocation beyond the obstacle list and never
    /// throws — a missing addon simply is not an obstacle.</summary>
    public Vector2 Resolve(Vector2 size)
    {
        var screen = ScreenSize();
        lastSize = size;
        lastScreen = screen;

        if (cfg.ReadoutPosition == ReadoutPosition.Custom)
        {
            return ReadoutLayout.FromFraction(new Vector2(FractionX, FractionY), size, screen);
        }

        var anchored = cfg.ReadoutPosition == ReadoutPosition.FollowQuestTracker && Rect("_ToDoList") is { } tracker
            ? ReadoutLayout.FollowTracker(tracker, size, screen)
            : ReadoutLayout.Anchor(cfg.ReadoutPosition, size, screen);

        var position = ReadoutLayout.Avoid(anchored, size, screen, Obstacles());

        // Mirror where the preset landed into the stored fraction, without saving. This is what
        // makes the sliders and the drag handle start from where the readout actually is: the
        // player's first nudge moves it a step, rather than teleporting it to a stale coordinate.
        var fraction = ReadoutLayout.ToFraction(position, size, screen);
        cfg.ReadoutFractionX = fraction.X;
        cfg.ReadoutFractionY = fraction.Y;
        return position;
    }

    /// <summary>Moves the readout horizontally, from the settings slider. Switches to
    /// <see cref="ReadoutPosition.Custom"/>, because the player has now said where they want it.</summary>
    public void SetFractionX(float value) => SetFraction(new Vector2(value, FractionY));

    /// <inheritdoc cref="SetFractionX"/>
    public void SetFractionY(float value) => SetFraction(new Vector2(FractionX, value));

    /// <summary>Records the result of a drag, given the readout's new top-left in screen pixels.</summary>
    public void MoveTo(Vector2 position) =>
        SetFraction(ReadoutLayout.ToFraction(position, lastSize, lastScreen));

    private static Vector2 ScreenSize()
    {
        var stage = AtkStage.Instance();
        if (stage is null)
        {
            return new Vector2(1920f, 1080f);
        }

        return new Vector2(stage->ScreenSize.Width, stage->ScreenSize.Height);
    }

    private static ScreenRect? Rect(string addonName)
    {
        var addon = RaptureAtkUnitManager.Instance()->GetAddonByName(addonName);
        if (addon is null || !addon->IsVisible || addon->RootNode is null)
        {
            return null;
        }

        var size = new Vector2(addon->RootNode->Width, addon->RootNode->Height) * addon->Scale;
        return size.Y <= 0f ? null : new ScreenRect(new Vector2(addon->X, addon->Y), size);
    }

    private static List<ScreenRect> Obstacles()
    {
        var rects = new List<ScreenRect>(ObstacleAddons.Length);
        foreach (var name in ObstacleAddons)
        {
            if (Rect(name) is { } rect)
            {
                rects.Add(rect);
            }
        }

        return rects;
    }

    private void SetFraction(Vector2 fraction)
    {
        cfg.ReadoutFractionX = Math.Clamp(fraction.X, 0f, 1f);
        cfg.ReadoutFractionY = Math.Clamp(fraction.Y, 0f, 1f);
        cfg.ReadoutPosition = ReadoutPosition.Custom;
        saveConfig();
    }
}
