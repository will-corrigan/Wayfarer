using System.Numerics;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using Lumina.Text.ReadOnly;
using Wayfarer.Presentation;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>One line of the block. The sentence is laid out stretch by stretch around its keyword,
/// the words that name what a press does, so the keyword alone carries the icon, the game's link
/// colour and the control: the pointer clicks those words and the pad's cursor rests on them,
/// while the rest of the sentence reads as ordinary words.
///
/// <para>A line whose sentence does not name its keyword is a control end to end, which is the
/// same rule with nothing to narrow it down to.</para></summary>
internal sealed class PressableLine : ResNode
{
    /// <summary>Air between the keyword's icon and the keyword.</summary>
    private const float IconGap = 4f;

    /// <summary>How many stretches of ordinary words one line can need: either side of the keyword
    /// on each line it may wrap to, and headroom.</summary>
    private const int MaxPlainRuns = 6;

    private const TextFlags RunFlags = TextFlags.Edge;

    private readonly int maxLines;
    private readonly Vector4 restingColor;
    private readonly Vector4 restingEdge;
    private readonly TextNode[] plain;
    private readonly TextNode keyword;
    private readonly TextNode glyph;
    private readonly IconImageNode icon;
    private readonly LineControl control;
    private float fontSize = ScenarioTreeStyle.SmallestFont;
    private float leading;
    private bool keywordShown;
    private LineContent? content;

    public unsafe PressableLine(Vector4 color, int maxLines, Action onPressed)
    {
        this.maxLines = maxLines;
        restingColor = color;
        restingEdge = GameColors.BodyEdge;

        glyph = Text().AttachedTo(this);
        plain = [.. Enumerable.Range(0, MaxPlainRuns).Select(_ => Text().AttachedTo(this))];
        icon = new IconImageNode { IsVisible = false }.AttachedTo(this);
        keyword = Text().AttachedTo(this);

        control = new LineControl
        {
            OnSelected = onPressed,
            OnHoverStart = () => Light(true),
            OnHoverEnd = () => Light(false),
            IsVisible = false,
        };
        control.CollisionNode.ShowClickableCursor = true;
        control.CollisionNode.AddEvent(AtkEventType.MouseClick, onPressed);
        control.CollisionNode.AddEvent(AtkEventType.MouseOver, () => Light(true));
        control.CollisionNode.AddEvent(AtkEventType.MouseOut, () => Light(false));
        control.AttachNode(this);
    }

    /// <summary>Whether this line is a control: it has an action, so it takes a click and the pad's
    /// cursor can rest on it.</summary>
    public bool Pressable => control.IsVisible;

    /// <summary>Tells the control which addon it lives in, so its dispose can take back any of the
    /// addon's pointers aimed at it.</summary>
    public unsafe void GuestOf(AtkUnitBase* addon, AtkResNode* fallbackFocus) => control.GuestOf(addon, fallbackFocus);

    /// <summary>The controller cursor's stop on this line: its index and where up and down lead.</summary>
    public void SetNav(int index, int up, int down)
    {
        control.NavIndex = index;
        control.NavUp = up;
        control.NavDown = down;
    }

    /// <summary>Sets the type and the width it has to fit, then re-lays whatever the line is
    /// showing. Where the line sits is the list's to say, not ours.</summary>
    public void Restyle(uint fontSize, float leading, float width)
    {
        this.fontSize = fontSize;
        this.leading = leading;
        foreach (var run in Runs())
        {
            run.FontSize = fontSize;
            run.LineSpacing = (uint)leading;
            run.Height = leading;
        }

        Width = width;
        Set(content);
    }

    /// <summary>Shows the content and sizes the line to the words it drew. Null hides the line.</summary>
    public void Set(LineContent? content)
    {
        this.content = content;
        IsVisible = content is not null;
        if (content is null)
        {
            TextTooltip = string.Empty;
            return;
        }

        // The icon belongs in front of the words it is about. When the sentence says them, room is
        // kept inside the line for it; when it does not, the whole line is the control and the icon
        // goes in front of the line instead.
        var named = TextFlow.Names(content.Words, content.Keyword);
        var iconRoom = content.IconId is null ? 0f : fontSize + IconGap;
        var mark = ShowGlyph(content.Glyph) + (named ? 0f : iconRoom);
        var keywordRoom = named ? iconRoom : 0f;
        var runs = TextFlow.Arrange(content.Words, content.Keyword, Measure, Width - mark, leading, maxLines, keywordRoom, out var cut);

        // Words that would not fit are cut with an ellipsis, so the whole sentence is offered on
        // hover instead. Setting a tooltip is also what gives the line the collision it needs to
        // be hovered at all, so it is cleared again the moment the words do fit.
        TextTooltip = cut ? content.Words : string.Empty;
        var lead = named || content.IconId is null ? Rect.Empty : new Rect(0f, 0f, iconRoom, leading);

        var (wordsBox, keywordBox) = Draw(runs, mark, keywordRoom, lead);
        keywordShown = keywordBox.Any;
        keyword.IsVisible = keywordShown;
        ShowIcon(content.IconId, keywordShown ? keywordBox.Left : 0f, keywordShown ? keywordBox.Top : 0f);

        // The control is the keyword when the sentence names it, and every word when it does not:
        // one control either way, so the line stays one stop for the pad's cursor.
        var pressed = keywordShown ? keywordBox : wordsBox;
        control.Position = new Vector2(pressed.Left, pressed.Top);
        control.Size = new Vector2(pressed.Width, pressed.Height);
        control.IsVisible = content.Pressable;

        Height = MathF.Max(leading, wordsBox.Height);
        Light(false);
    }

    /// <summary>Draws every stretch the flow placed, and reports what they all fit inside and where
    /// the keyword landed. Stretches past the ones this line keeps nodes for are dropped, which the
    /// flow's own line limit means cannot happen for any sentence the block shows.</summary>
    private (Rect Words, Rect Keyword) Draw(IReadOnlyList<LineRun> runs, float mark, float keywordRoom, Rect lead)
    {
        var words = lead;
        var keywordBox = Rect.Empty;
        var drawn = 0;
        foreach (var run in runs)
        {
            var node = run.Keyword ? keyword : drawn < plain.Length ? plain[drawn++] : null;
            if (node is null)
            {
                continue;
            }

            var room = run.Keyword ? keywordRoom : 0f;
            var box = new Rect(mark + run.Left, run.Top, run.Width, leading);
            node.Position = new Vector2(box.Left + room, box.Top);
            node.Width = box.Width - room;
            node.String = run.Text;
            node.IsVisible = true;
            words = words.Union(box);
            if (run.Keyword)
            {
                keywordBox = box;
            }
        }

        for (var i = drawn; i < plain.Length; i++)
        {
            plain[i].IsVisible = false;
        }

        return (words, keywordBox);
    }

    /// <summary>Lights the line: the keyword alone when the sentence names it, every word when it
    /// does not, and nothing at all when the line cannot be pressed. Lit words are white, settled
    /// ones the colour the game gives a link.</summary>
    private void Light(bool lit)
    {
        var live = control.IsVisible ? lit ? GameColors.Body : GameColors.Link : restingColor;
        var liveEdge = control.IsVisible ? GameColors.LinkEdge : restingEdge;

        keyword.TextColor = live;
        keyword.TextOutlineColor = liveEdge;

        var ordinary = keywordShown ? restingColor : live;
        var ordinaryEdge = keywordShown ? restingEdge : liveEdge;
        foreach (var run in plain)
        {
            run.TextColor = ordinary;
            run.TextOutlineColor = ordinaryEdge;
        }

        glyph.TextColor = ordinary;
        glyph.TextOutlineColor = ordinaryEdge;
    }

    /// <summary>Draws the game's own mark in front of the line and reports how much room it took,
    /// which is what the words are then laid out inside.</summary>
    private float ShowGlyph(BitmapFontIcon? mark)
    {
        glyph.IsVisible = mark is not null;
        if (mark is not { } icon)
        {
            return 0f;
        }

        var words = new ReadOnlySeString(new SeStringBuilder().AddIcon(icon).Build().Encode());
        glyph.String = words;
        glyph.Position = Vector2.Zero;
        glyph.Width = glyph.GetTextDrawSize(words, considerScale: false).X;
        return glyph.Width;
    }

    /// <summary>Puts the icon in the room kept for it, level with the type: in front of the keyword
    /// when the sentence names it, and in front of the line when it does not.</summary>
    private void ShowIcon(uint? iconId, float left, float top)
    {
        icon.IsVisible = iconId is not null;
        if (iconId is not { } id)
        {
            return;
        }

        icon.IconId = id;
        icon.Size = new Vector2(fontSize, fontSize);
        icon.Position = new Vector2(left, top + ((leading - fontSize) / 2f));
    }

    private float Measure(string words) => plain[0].GetTextDrawSize(new ReadOnlySeString(words), considerScale: false).X;

    private IEnumerable<TextNode> Runs() => plain.Append(keyword).Append(glyph);

    private TextNode Text() => new()
    {
        FontType = FontType.Axis,
        AlignmentType = AlignmentType.TopLeft,
        TextFlags = RunFlags,
        TextColor = restingColor,
        TextOutlineColor = restingEdge,
        IsVisible = false,
    };
}
