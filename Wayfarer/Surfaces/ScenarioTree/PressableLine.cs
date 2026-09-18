using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using Wayfarer.Ui;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>One line of the block: an optional game icon, the words beside it, and, while the
/// line is pressable, one control the size of the words that the pointer clicks and the
/// controller cursor rests on. Both run the one action the line was given. The words dim a
/// little while pressable and light on hover, which is what says the line can be pressed.</summary>
internal sealed class PressableLine : ResNode
{
    private const float PressableIdleAlpha = 0.8f;
    private const float IconGap = 4f;

    private readonly float leading;
    private readonly int maxLines;
    private readonly IconImageNode icon;
    private readonly TextNode words;
    private readonly NavFocusNode control;

    public unsafe PressableLine(TextFlags flags, Vector4 color, uint fontSize, float leading, int maxLines, Action onPressed)
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

        control = new NavFocusNode
        {
            OnSelected = onPressed,
            OnHoverStart = () => words.Alpha = 1f,
            OnHoverEnd = () => words.Alpha = PressableIdleAlpha,
            IsVisible = false,
        };
        control.CollisionNode.ShowClickableCursor = true;
        control.CollisionNode.AddEvent(AtkEventType.MouseClick, onPressed);
        control.CollisionNode.AddEvent(AtkEventType.MouseOver, () => words.Alpha = 1f);
        control.CollisionNode.AddEvent(AtkEventType.MouseOut, () => words.Alpha = PressableIdleAlpha);
        control.AddEvent(AtkEventType.InputReceived, HandlePadInput);
        control.AttachNode(this);
    }

    public bool Pressable => control.IsVisible;

    /// <summary>What a press of up or down on the pad does while the cursor is on this line.</summary>
    public Action? OnUp { get; set; }

    /// <inheritdoc cref="OnUp"/>
    public Action? OnDown { get; set; }

    /// <summary>The control's own node, for the addon's focusable slots.</summary>
    public unsafe AtkResNode* FocusTarget => (AtkResNode*)control.Node;

    /// <summary>The controller cursor's stop on this line: its index and where up and down lead.</summary>
    public void SetNav(int index, int up, int down)
    {
        control.NavIndex = index;
        control.NavUp = up;
        control.NavDown = down;
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

        control.Position = words.Position;
        control.Size = words.Size;
        control.IsVisible = content.Pressable;
    }

    private unsafe void HandlePadInput(AtkEventListener* listener, AtkEventType type, int param, AtkEvent* atkEvent, AtkEventData* data)
    {
        if (type != AtkEventType.InputReceived || data->InputData.State != InputState.Down)
        {
            return;
        }

        switch ((InputId)data->InputData.InputId)
        {
            case InputId.UP:
                OnUp?.Invoke();
                break;
            case InputId.DOWN:
                OnDown?.Invoke();
                break;
            default:
                break;
        }
    }

    private int Lines() =>
        Math.Clamp((int)MathF.Ceiling(words.GetTextDrawSize(considerScale: false).Y / leading), 1, maxLines);
}
