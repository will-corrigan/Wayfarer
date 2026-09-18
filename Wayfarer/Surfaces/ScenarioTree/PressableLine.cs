using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using Wayfarer.Ui;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>One line of the block: an optional game icon, the words beside it, and, while the
/// line is pressable, the two nodes a control on a heads-up surface needs: an invisible collision
/// box the pointer clicks and a zero-sized anchor the controller cursor rests on. Both run the one
/// action the line was given. The words dim a little while pressable and light on hover, which is
/// what says the line can be pressed.</summary>
internal sealed class PressableLine : ResNode
{
    private const float PressableIdleAlpha = 0.8f;
    private const float NavAnchorInset = 2f;
    private const float IconGap = 4f;

    private readonly float leading;
    private readonly int maxLines;
    private readonly IconImageNode icon;
    private readonly TextNode words;
    private readonly CollisionNode hitBox;
    private readonly NavFocusNode navAnchor;

    public PressableLine(TextFlags flags, Vector4 color, uint fontSize, float leading, int maxLines, Action onPressed)
    {
        this.leading = leading;
        this.maxLines = maxLines;

        icon = new IconImageNode { Size = new Vector2(leading, leading), IsVisible = false };
        icon.AttachNode(this);

        words = new TextNode
        {
            FontType = FontType.Axis,
            FontSize = fontSize,
            LineSpacing = (uint)leading,
            AlignmentType = AlignmentType.TopLeft,
            TextFlags = flags,
            TextColor = color,
            TextOutlineColor = GameColors.BodyEdge,
        };
        words.AttachNode(this);

        hitBox = new CollisionNode { IsVisible = false, ShowClickableCursor = true };
        hitBox.AddEvent(AtkEventType.MouseClick, onPressed);
        hitBox.AddEvent(AtkEventType.MouseOver, () => words.Alpha = 1f);
        hitBox.AddEvent(AtkEventType.MouseOut, () => words.Alpha = PressableIdleAlpha);
        hitBox.AttachNode(this);

        navAnchor = new NavFocusNode
        {
            OnSelected = onPressed,
            OnHoverStart = () => words.Alpha = 1f,
            OnHoverEnd = () => words.Alpha = PressableIdleAlpha,
            Size = Vector2.Zero,
            IsVisible = false,
        };
        navAnchor.CollisionNode.RemoveNodeFlags(NodeFlags.Fill);
        navAnchor.AttachNode(this);
    }

    public bool Pressable => hitBox.IsVisible;

    /// <summary>The controller cursor's stop on this line: its index and where up and down lead.</summary>
    public void SetNav(int index, int up, int down)
    {
        navAnchor.NavIndex = index;
        navAnchor.NavUp = up;
        navAnchor.NavDown = down;
    }

    /// <summary>Shows the content and sizes the line to its words. Null hides the line.</summary>
    public void Set(LineContent? content)
    {
        IsVisible = content is not null;
        if (content is null)
        {
            return;
        }

        icon.IsVisible = content.IconId is not null;
        icon.IconId = content.IconId ?? 0;
        var wordsLeft = icon.IsVisible ? leading + IconGap : 0f;

        words.Position = new Vector2(wordsLeft, 0f);
        words.Width = Width - wordsLeft;
        words.String = content.Words;
        words.Height = Lines() * leading;
        words.Alpha = content.Pressable ? PressableIdleAlpha : 1f;
        Height = words.Height;

        hitBox.Position = words.Position;
        hitBox.Size = words.Size;
        hitBox.IsVisible = content.Pressable;
        navAnchor.Position = words.Position + new Vector2(NavAnchorInset, words.Height / 2f);
        navAnchor.IsVisible = content.Pressable;
    }

    private int Lines() =>
        Math.Clamp((int)MathF.Ceiling(words.GetTextDrawSize(considerScale: false).Y / leading), 1, maxLines);
}
