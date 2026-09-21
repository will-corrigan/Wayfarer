using System.Numerics;
using Lumina;

namespace Wayfarer.Mockup;

/// <summary>The settings window as it would be drawn, using the numbers the window itself uses.
/// Keeping them here in one block means a change to the window is a change to two places — but the
/// alternative is finding out how a page reads only by loading the game, which is how this page
/// came to be built three times over.</summary>
internal static class SettingsPage
{
    // These are the window's own, from SettingsAddon and from KamiToolKit's WindowNode.
    private const float WindowWidth = 680f;
    private const float WindowHeight = 620f;
    private const float WindowBorder = 4f;
    private const float HeaderHeight = 38f;
    private const float ContentPaddingX = 8f;
    private const float ContentPaddingY = 8f;

    private const float LeafLeftShare = 0.075f;
    private const float LeafRightShare = 0.095f;
    private const float LeafTopShare = 0.215f;
    private const float LeafBottomShare = 0.095f;

    private const float ScrollBarWidth = 16f;
    private const float RowLabelShare = 0.38f;
    private const float RowHeight = 28f;
    private const float CheckboxHeight = 24f;
    private const float SectionGap = 10f;
    private const float SectionSpacing = 4f;
    private const float PanelPadding = 8f;

    /// <summary>How far a module's own settings are stepped in from its heading, so a setting reads
    /// as belonging to the module above it rather than as another module.</summary>
    private const float SettingIndent = 20f;

    /// <summary>The larger face a section's name is set in.</summary>
    private const int HeadingSize = 18;

    /// <summary>The icon beside a module's name, and the gap between it and the name.</summary>
    private const float IconSize = 24f;
    private const float IconGap = 8f;

    private const string PageTexture = "ui/uld/AchievementBg.tex";

    /// <summary>How much of that picture draws nothing, measured off the picture.</summary>
    private const float LeafClearLeft = 33f / 976f;
    private const float LeafClearRight = 31f / 976f;
    private const float LeafClearTop = 5f / 664f;
    private const float LeafClearBottom = 7f / 664f;

    private static readonly Vector4 Desk = new(0.10f, 0.11f, 0.13f, 1f);

    public static Canvas Draw(GameData game, bool showBoxes)
    {
        var canvas = new Canvas((int)WindowWidth, (int)WindowHeight);
        canvas.Clear(Desk);

        var palette = new Palette(game);
        var ink = palette.Ink;
        var faint = palette.FaintInk;
        var rule = ink with { W = 0.3f };

        var words = GameFont.Axis(game, 12);
        var heading = GameFont.Axis(game, HeadingSize);

        // The window's own frame, as KamiToolKit sizes it.
        var background = new Vector2(WindowWidth - (WindowBorder * 2f), WindowHeight - (WindowBorder * 4f));
        var contentStart = new Vector2(WindowBorder + ContentPaddingX, WindowBorder + HeaderHeight);
        var contentSize = new Vector2(background.X - (ContentPaddingX * 2f), background.Y - HeaderHeight - ContentPaddingY);

        // The leaf takes the whole of the window's own plate, not the room inside it — and is drawn
        // bigger than the plate by its own bare margin, so its art reaches the plate's edges.
        var plateAt = contentStart - new Vector2(ContentPaddingX, 0f);
        var plate = contentSize + new Vector2(ContentPaddingX * 2f, ContentPaddingY);
        var leaf = new Vector2(
            plate.X / (1f - LeafClearLeft - LeafClearRight),
            plate.Y / (1f - LeafClearTop - LeafClearBottom));
        var leafAt = plateAt - new Vector2(leaf.X * LeafClearLeft, leaf.Y * LeafClearTop);

        canvas.Fill(plateAt.X, plateAt.Y, plate.X, plate.Y, new Vector4(0.16f, 0.17f, 0.19f, 1f));
        canvas.Stretch(Picture.From(game, PageTexture), leafAt.X, leafAt.Y, leaf.X, leaf.Y);

        var origin = leafAt + new Vector2(leaf.X * LeafLeftShare, leaf.Y * LeafTopShare);
        var area = new Vector2(
            leaf.X * (1f - LeafLeftShare - LeafRightShare),
            leaf.Y * (1f - LeafTopShare - LeafBottomShare));

        if (showBoxes)
        {
            canvas.Outline(leafAt.X, leafAt.Y, leaf.X, leaf.Y, new Vector4(0f, 0.6f, 1f, 0.6f));
            canvas.Outline(origin.X, origin.Y, area.X, area.Y, new Vector4(1f, 0f, 0.4f, 0.7f));
        }

        var pageWidth = area.X - ScrollBarWidth;
        var labelWidth = pageWidth * RowLabelShare;
        var pen = origin.Y;

        // Everything from here is the scrolling pane's, and the pane is exactly the page.
        using var _ = canvas.Clip(origin.X, origin.Y, area.X, area.Y);

        pen = Heading(canvas, heading, game, "The guide", 61840u, origin.X, pen, pageWidth, ink, rule);

        // The preview block, in its frame.
        const float PreviewHeight = 74f;
        canvas.Outline(origin.X, pen, pageWidth, PreviewHeight, rule);
        words.Write(canvas, "Speak with Minfilia at the Waking Sands.", origin.X + PanelPadding, pen + PanelPadding, ink);
        words.Write(canvas, "Teleport to Limsa Lominsa Lower Decks, then Aethernet", origin.X + PanelPadding, pen + PanelPadding + 20f, ink);
        words.Write(canvas, "143y", origin.X + pageWidth - 60f, pen + PanelPadding + 40f, faint);
        pen += PreviewHeight + SectionGap;

        foreach (var (label, control) in Rows())
        {
            words.Write(canvas, label, origin.X, pen + 6f, ink);
            canvas.Outline(origin.X + labelWidth, pen + 4f, pageWidth - labelWidth, RowHeight - 8f, rule);
            words.Write(canvas, control, origin.X + labelWidth + PanelPadding, pen + 6f, faint);
            pen += RowHeight;
        }

        pen += SectionGap;

        foreach (var (module, icon, about, settings) in Modules())
        {
            pen = Heading(canvas, heading, game, module, icon, origin.X, pen, pageWidth, ink, rule);
            words.Write(canvas, words.Wrap(about, pageWidth)[0], origin.X, pen, faint);
            pen += 18f + SectionSpacing;

            foreach (var (setting, note) in settings)
            {
                Box(canvas, origin.X + SettingIndent, pen, ink);
                words.Write(canvas, setting, origin.X + SettingIndent + CheckboxHeight, pen + 5f, ink);
                pen += CheckboxHeight;
                words.Write(canvas, words.Wrap(note, pageWidth - SettingIndent - CheckboxHeight)[0], origin.X + SettingIndent + CheckboxHeight, pen, faint);
                pen += 18f + SectionSpacing;
            }

            pen += SectionGap;
        }

        return canvas;
    }

    /// <summary>A section's name, its icon, and the hairline ruled under both. This is what tells a
    /// player that what follows belongs to one thing — on paper a rule does the work a panel of
    /// dark glass does on a dark window, and the leaf is paper.</summary>
    private static float Heading(Canvas canvas, GameFont face, GameData game, string name, uint icon, float x, float y, float width, Vector4 ink, Vector4 rule)
    {
        if (icon != 0)
        {
            canvas.Stretch(Picture.From(game, $"ui/icon/{icon / 1000 * 1000:000000}/{icon:000000}.tex"), x, y, IconSize, IconSize);
        }

        face.Write(canvas, name, x + IconSize + IconGap, y + 2f, ink);
        canvas.Fill(x, y + IconSize + 3f, width, 1f, rule);
        return y + IconSize + 3f + 1f + SectionGap;
    }

    private static void Box(Canvas canvas, float x, float y, Vector4 ink)
    {
        canvas.Outline(x, y + 4f, 16f, 16f, ink with { W = 0.6f });
        canvas.Fill(x + 4f, y + 8f, 8f, 8f, ink with { W = 0.8f });
    }

    private static (string Label, string Control)[] Rows() =>
    [
        ("Entry text size", "14"),
        ("Route text size", "12"),
        ("Line spacing", "2"),
        ("Text left edge", "0"),
        ("Compass size", "24"),
        ("Compass", "Right of the words"),
    ];

    private static (string Name, uint Icon, string About, (string Name, string Note)[] Settings)[] Modules() =>
    [
        ("Quests", 71221u, "Follows the main scenario, or any quest you choose from the journal, in the Main Scenario Guide.",
        [
            ("Guide me through my quests", "Follows the main scenario, or whichever quest you chose, and shows the way to its next step."),
            ("Follow quests from the journal", "Puts a button in the quest journal that follows the quest on show."),
            ("Mark duties your quests lead to", "Puts a quest's own mark beside its duty in the Duty Finder."),
        ]),
    ];
}
