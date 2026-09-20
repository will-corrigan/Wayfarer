using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Wayfarer.Surfaces.DutyFinder;

/// <summary>A row's strip of icons, rebuilt as ours.
///
/// <para>The game's own pictures are hidden and drawn again by nodes of ours, beside the marks the
/// modules asked for, so the whole strip is one set laid out together rather than ours squeezed in
/// among theirs.</para>
///
/// <para>What the game keeps is the patch of row the pointer is tested against, one per slot,
/// because what it says when rested on is the window's to decide and cannot be read from here.
/// Each patch is carried to whichever icon now stands for that slot, so the game's own words follow
/// its picture. Ours are said through a patch the same way.</para>
///
/// <para>Everything the game owns that is moved or hidden is written down as it is done, and put
/// back exactly, before anything of ours is freed.</para></summary>
internal sealed unsafe class RowStrip : IDisposable
{
    private readonly List<RowIcon> icons = [];
    private readonly List<RowMoved> moved = [];
    private readonly List<RowTooltip> said = [];

    /// <summary>Lays the strip out afresh: the game's pictures as ours, then the marks asked for.</summary>
    /// <param name="row">The row being drawn.</param>
    /// <param name="marks">What the modules want on it.</param>
    /// <param name="strip">Where everything goes and how big it is.</param>
    /// <param name="addon">The window the row belongs to.</param>
    public void Lay(DutyFinderRow row, IReadOnlyList<DutyMark> marks, StripLayout.Strip strip, AddonContentsFinder* addon)
    {
        Restore();

        var lit = row.Places.Where(place => place.Lit).ToList();
        var size = new Vector2(strip.Size, strip.Size);
        var down = DutyFinderMetrics.StripTop + ((DutyFinderMetrics.StripSlotHeight - strip.Size) / 2f);
        var places = strip.Marks.Concat(strip.GameIcons).ToList();

        for (var index = 0; index < places.Count; index++)
        {
            var icon = At(index, row);
            if (icon is null)
            {
                continue;
            }

            var at = new Vector2(places[index], down);
            if (index < marks.Count)
            {
                icon.Draw(marks[index].IconId, at, size);
                Say(row, index, at, size, addon, marks[index].Tooltip);
                continue;
            }

            // One of the game's: drawn again by us, its own picture hidden, and the patch that
            // speaks for it carried over so its words come with it.
            var slot = lit[index - marks.Count];
            icon.Draw(slot, at, size);
            Hide(slot);
            Carry(slot, at, size);
        }

        for (var spare = places.Count; spare < icons.Count; spare++)
        {
            icons[spare].Hide();
        }
    }

    /// <summary>Puts everything of the game's back as it was found. Safe to call twice.</summary>
    public void Restore()
    {
        foreach (var tooltip in said)
        {
            tooltip.Dispose();
        }

        said.Clear();
        foreach (var was in moved)
        {
            was.Restore();
        }

        moved.Clear();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Restore();
        foreach (var icon in icons)
        {
            icon.Dispose();
        }

        icons.Clear();
    }

    /// <summary>The icon standing at this place in the strip, made if the row has not had one.</summary>
    private RowIcon? At(int index, DutyFinderRow row)
    {
        while (icons.Count <= index)
        {
            if (RowIcon.Beside(row.NameNode) is not { } made)
            {
                return null;
            }

            icons.Add(made);
        }

        return icons[index];
    }

    /// <summary>Hides the picture the game would have drawn, having written down that it was shown.</summary>
    private void Hide(RowSlot slot)
    {
        var picture = (AtkResNode*)slot.Picture;
        if (picture != null)
        {
            moved.Add(RowMoved.Of(picture));
            picture->ToggleVisibility(false);
        }
    }

    /// <summary>Carries a slot's patch to where its picture now stands, so the words the window
    /// says about it are said over the right icon.</summary>
    private void Carry(RowSlot slot, Vector2 at, Vector2 size)
    {
        var touch = (AtkResNode*)slot.Touch;
        if (touch == null)
        {
            return;
        }

        moved.Add(RowMoved.Of(touch));
        touch->SetXFloat(at.X);
        touch->SetYFloat(at.Y);
        touch->SetWidth((ushort)size.X);
        touch->SetHeight((ushort)size.Y);
        touch->ToggleVisibility(true);
    }

    /// <summary>Says what one of our marks means, through a patch the row already has: a window
    /// only tests the pointer against the parts it keeps a list of, and a patch of our own inside
    /// a row is never one of them.</summary>
    private void Say(DutyFinderRow row, int index, Vector2 at, Vector2 size, AddonContentsFinder* addon, string text)
    {
        var spare = row.Places.Select(place => place.Touch).Where(touch => touch != 0).Skip(index).FirstOrDefault();
        var touch = (AtkResNode*)spare;
        if (touch == null || addon == null || string.IsNullOrEmpty(text))
        {
            return;
        }

        moved.Add(RowMoved.Of(touch));
        touch->SetXFloat(at.X);
        touch->SetYFloat(at.Y);
        touch->SetWidth((ushort)size.X);
        touch->SetHeight((ushort)size.Y);
        if (RowTooltip.Attach(touch, addon->AtkUnitBase.Id, text) is { } tooltip)
        {
            said.Add(tooltip);
        }
    }
}
