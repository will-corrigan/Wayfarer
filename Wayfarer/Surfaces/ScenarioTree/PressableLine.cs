using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using Wayfarer.Ui;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>One line of the block: an optional game icon, the words beside it, and, while the
/// line is pressable, one control the size of the words that the pointer clicks and the
/// controller cursor rests on. Both run the one action the line was given.
///
/// <para>A pressable line is printed in the colour the game gives a link, the same pale blue a
/// map or item link takes in the chat log, and turns white under the pointer. That is what says
/// the words can be pressed, and it says it whether or not the line has an icon.</para></summary>
internal sealed class PressableLine : ResNode
{
    private const float IconGap = 4f;

    private readonly int maxLines;
    private readonly Vector4 restingColor;
    private readonly Vector4 restingEdge;
    private readonly IconImageNode icon;
    private readonly TextNode words;
    private readonly LineControl control;
    private float fontSize = ScenarioTreeStyle.SmallestFont;
    private float leading;
    private LineContent? content;

    public unsafe PressableLine(TextFlags flags, Vector4 color, int maxLines, Action onPressed)
    {
        this.maxLines = maxLines;
        restingColor = color;
        restingEdge = GameColors.BodyEdge;

        icon = new IconImageNode { IsVisible = false };
        icon.AttachNode(this);

        words = new TextNode
        {
            FontType = FontType.Axis,
            AlignmentType = AlignmentType.TopLeft,
            TextFlags = flags,
            TextColor = restingColor,
            TextOutlineColor = restingEdge,
        };
        words.AttachNode(this);

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

    /// <summary>The node the game's cursor rests on when this line is focused: the control's
    /// collision node, which is what the toolkit focuses too.</summary>
    public unsafe AtkResNode* FocusTarget => (AtkResNode*)control.CollisionNode.Node;

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

    /// <summary>Sets the type and the column, then re-lays whatever the line is showing.</summary>
    public void Restyle(uint fontSize, float leading, float left, float width)
    {
        this.fontSize = fontSize;
        this.leading = leading;
        words.FontSize = fontSize;
        words.LineSpacing = (uint)leading;
        Position = new Vector2(left, Position.Y);
        Width = width;
        Set(content);
    }

    /// <summary>Shows the content and sizes the line to its words. Null hides the line.</summary>
    public void Set(LineContent? content)
    {
        this.content = content;
        IsVisible = content is not null;
        if (content is null)
        {
            return;
        }

        icon.IsVisible = content.IconId is not null;
        icon.IconId = content.IconId ?? 0;

        // The icon is drawn at the height of the type rather than of the whole line, and sits in
        // the air the game leaves around the words, so it reads as a mark beside them rather than
        // as art crowding the line above and below.
        icon.Size = new Vector2(fontSize, fontSize);
        icon.Position = new Vector2(0f, (leading - fontSize) / 2f);
        var wordsLeft = icon.IsVisible ? fontSize + IconGap : 0f;

        words.Position = new Vector2(wordsLeft, 0f);
        words.Width = Width - wordsLeft;
        words.String = content.Words;
        words.Height = Lines() * leading;
        Height = words.Height;

        // The whole line takes the press, icon included: the icon is what the line is about, and
        // reaching past it to the words to use an item reads as a control that is half wired up.
        control.Position = Vector2.Zero;
        control.Size = new Vector2(wordsLeft + words.Width, words.Height);
        control.IsVisible = content.Pressable;
        Light(false);
    }

    /// <summary>Colours the line for what it is: its own colour while nothing can be done with it,
    /// the game's link colour while it is a control, and white under the pointer.</summary>
    private void Light(bool lit)
    {
        words.TextColor = control.IsVisible ? lit ? GameColors.Body : GameColors.Link : restingColor;
        words.TextOutlineColor = control.IsVisible ? GameColors.LinkEdge : restingEdge;
    }

    private int Lines() =>
        Math.Clamp((int)MathF.Ceiling(words.GetTextDrawSize(considerScale: false).Y / leading), 1, maxLines);
}
