using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using Lumina.Text;
using Lumina.Text.Payloads;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>One line of the block: the whole sentence in one of the game's own text nodes, with the
/// words that name what a press does coloured as a link inside it.
///
/// <para>Wrapping is the game's. The node is given the width it has to fit and the game breaks the
/// sentence across as many lines as it needs, then sizes itself to them. Nothing here measures a
/// word, places one, or decides where the words should stop.</para>
///
/// <para>The keyword is a colour inside the sentence rather than a node of its own, which is what
/// lets the game treat the whole line as one run of text to wrap. The line is one control either
/// way, so the pad's cursor rests on it exactly as before, and a click anywhere on the words
/// presses it.</para></summary>
internal sealed class PressableLine : ResNode
{
    /// <summary>Air between the icon and the words it is about.</summary>
    private const float IconGap = 4f;

    /// <summary>Everything about how the words are set, left to the game: an outline, wrapping at
    /// the node's width, more than one line, and the boxes it works out for any link inside them.
    ///
    /// <para>Nothing is clipped and nothing is cut. The line is given the width it has to fit, and
    /// <see cref="TextFlags.AutoAdjustNodeSize"/> with the game's own resize makes the node as tall
    /// as the words it wrapped. The game has the whole sentence, so the whole sentence is shown.</para></summary>
    private const TextFlags WordFlags = TextFlags.Edge | TextFlags.WordWrap | TextFlags.MultiLine | TextFlags.AutoAdjustNodeSize | TextFlags.LinkData;

    /// <summary>What the keyword is wrapped in so the game measures it. Nothing in the guide acts
    /// on a link of this kind, and our own control sits over the words in any case: the payload is
    /// here to be measured, not to be followed.</summary>
    private const LinkMacroPayloadType KeywordLink = LinkMacroPayloadType.Description;

    private readonly Vector4 restingColor;
    private readonly Vector4 restingEdge;
    private readonly TextNode words;
    private readonly IconImageNode icon;
    private readonly LineControl control;
    private float fontSize = ScenarioTreeStyle.SmallestFont;
    private float leading;
    private bool lit;
    private LineContent? content;

    public unsafe PressableLine(Vector4 color, Action onPressed)
    {
        restingColor = color;
        restingEdge = GameColors.BodyEdge;

        words = new TextNode
        {
            FontType = FontType.Axis,
            AlignmentType = AlignmentType.TopLeft,
            TextFlags = WordFlags,
            TextColor = color,
            TextOutlineColor = restingEdge,
        }.AttachedTo(this);

        // FitTexture is what makes the icon drawn at the node's own size. Without it the node
        // keeps the texture's size, which for a game icon is 32 and stands over two lines of type.
        icon = new IconImageNode { FitTexture = true, IsVisible = false }.AttachedTo(this);

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

    /// <summary>Sets the type and the width the line has to fit, then writes whatever it is showing
    /// again. Where the line sits is the list's to say, not ours.</summary>
    public void Restyle(uint fontSize, float leading, float width)
    {
        this.fontSize = fontSize;
        this.leading = leading;
        words.FontSize = fontSize;
        words.LineSpacing = (uint)leading;
        Width = width;
        Set(content);
    }

    /// <summary>Shows the content and sizes the line to the words the game drew. Null hides it.</summary>
    public void Set(LineContent? content)
    {
        this.content = content;
        IsVisible = content is not null;
        if (content is null)
        {
            return;
        }

        control.IsVisible = content.Pressable;

        // A game icon is not one of the font's own marks, so it cannot be a character in the
        // sentence. It can still sit inside one: the words make room for it where it belongs, the
        // game lays that room out and wraps around it like any other text, and the icon is put in
        // the gap the game made. It then moves with the words it is about rather than hanging off
        // the front of the line.
        var beside = Beside(content);
        words.Position = Vector2.Zero;
        words.Width = Width;
        Write();
        Resize();
        Height = MathF.Max(leading, words.Height);

        // The control is the keyword when the game says where it drew it, and the whole line when
        // it does not: one control either way, so the line stays one stop for the pad.
        var box = KeywordBox();
        var pressed = box ?? new LineBox(Vector2.Zero, new Vector2(Width, Height));
        control.Position = pressed.At;
        control.Size = pressed.Size;
        ShowIcon(content.IconId, beside ? box : null);
    }

    /// <summary>Whether the icon is to sit inside the sentence rather than in front of it, which it
    /// can only do where there is a word for it to sit beside.</summary>
    private static bool Beside(LineContent content) => content.IconId is not null && content.Keyword is not null;

    private static void Coloured(SeStringBuilder builder, string text, Vector4 color, Vector4 edge)
    {
        if (text.Length > 0)
        {
            builder.PushColorRgba(color).PushEdgeColorRgba(edge).Append(text).PopEdgeColor().PopColor();
        }
    }

    /// <summary>Has the game size the node to the words now in it, which with
    /// <see cref="TextFlags.WordWrap"/> means as many lines as they wrapped to.</summary>
    private unsafe void Resize()
    {
        var node = words.Node;
        if (node != null)
        {
            node->ResizeNodeForCurrentText();
        }
    }

    /// <summary>Where the game drew the keyword, or null when it did not draw one: the sentence may
    /// name no keyword, or the game may have cut the words before reaching it.
    ///
    /// <para>The answer is the game's own, worked out while it wrapped the sentence, and it is only
    /// true for the words in the node right now. Nothing here is kept: every step from the node to
    /// the box is a pointer the game owns and may free, so each is taken fresh and checked.</para></summary>
    private unsafe LineBox? KeywordBox()
    {
        var node = words.Node;
        if (node == null || node->LinkData == null || node->LinkData->Count == 0)
        {
            return null;
        }

        var entry = node->LinkData->First.Value;
        if (entry == null)
        {
            return null;
        }

        var link = entry->Value.Value;
        if (link == null)
        {
            return null;
        }

        return new LineBox(
            new Vector2(link->MinX, link->MinY),
            new Vector2(link->MaxX - link->MinX, link->MaxY - link->MinY));
    }

    /// <summary>Writes the sentence: the game's own mark in front of it when it has one, then the
    /// words, with the keyword in the colour the game gives a link.</summary>
    private void Write()
    {
        if (content is not { } showing)
        {
            return;
        }

        var builder = new SeStringBuilder();
        if (showing.Glyph is { } mark)
        {
            builder.AppendIcon((uint)mark);
        }

        var live = (control.IsVisible, lit) switch
        {
            (false, _) => restingColor,
            (true, true) => GameColors.Body,
            (true, false) => GameColors.Link,
        };
        var liveEdge = control.IsVisible ? GameColors.LinkEdge : restingEdge;
        var at = showing.Keyword is null ? -1 : showing.Words.IndexOf(showing.Keyword, StringComparison.OrdinalIgnoreCase);
        if (at < 0)
        {
            // Nothing inside the sentence is named, so the sentence itself is what a press is about.
            Coloured(builder, showing.Words, live, liveEdge);
        }
        else
        {
            Coloured(builder, showing.Words[..at], restingColor, restingEdge);

            // Wrapped in a link so the game works out where the keyword ends up once it has broken
            // the sentence across lines, and says so in the node's own link boxes.
            builder.PushLink(KeywordLink, 0u, 0u, 0u);

            // The room for the icon goes inside the link, so the box the game reports is the gap
            // and the word together and the icon lands in the gap wherever the line broke.
            var room = Beside(showing) ? new string(' ', SpacesForIcon()) : string.Empty;
            Coloured(builder, string.Concat(room, showing.Words.AsSpan(at, showing.Keyword!.Length)), live, liveEdge);
            builder.PopLink();
            Coloured(builder, showing.Words[(at + showing.Keyword.Length)..], restingColor, restingEdge);
        }

        words.String = builder.ToReadOnlySeString();
    }

    /// <summary>Lights the line, which is the keyword going white while the pointer is on it. The
    /// colour lives inside the sentence, so the sentence is written again rather than a node being
    /// recoloured.</summary>
    private void Light(bool wanted)
    {
        if (lit != wanted)
        {
            lit = wanted;
            Write();
        }
    }

    /// <summary>Puts the icon in the gap the words left for it, or at the front of the line when
    /// they left none. Either way it is the size of the type it sits beside.</summary>
    /// <param name="iconId">The icon, or null to show none.</param>
    /// <param name="gap">Where the words left room for it, or null when they left none.</param>
    private void ShowIcon(uint? iconId, LineBox? gap)
    {
        icon.IsVisible = iconId is not null;
        if (iconId is not { } id)
        {
            return;
        }

        icon.IconId = id;
        icon.Size = new Vector2(fontSize, fontSize);
        icon.Position = gap is { } room
            ? new Vector2(room.At.X, room.At.Y + ((room.Size.Y - fontSize) / 2f))
            : new Vector2(0f, (leading - fontSize) / 2f);
    }

    /// <summary>How many spaces stand as wide as the icon and its air, as the game measures a space
    /// at the size the line is set in. Nothing is assumed about the font: it is asked.</summary>
    private unsafe int SpacesForIcon()
    {
        var node = words.Node;
        if (node == null)
        {
            return 0;
        }

        ushort width = 0, height = 0;
        var space = stackalloc byte[2];
        space[0] = (byte)' ';
        space[1] = 0;
        node->GetTextDrawSize(&width, &height, space, 0, 1, false);

        // A space of no width would ask for an unbounded run of them, so the icon goes in front of
        // the line instead, which is what a line with nothing to sit beside gets anyway.
        return width == 0 ? 0 : (int)MathF.Ceiling((fontSize + IconGap) / width);
    }
}
