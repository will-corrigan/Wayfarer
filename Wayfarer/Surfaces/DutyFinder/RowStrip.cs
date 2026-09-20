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
    private readonly List<nint> hidden = [];

    /// <summary>Lays the strip out afresh: the game's pictures as ours, then the marks asked for.</summary>
    /// <param name="row">The row being drawn.</param>
    /// <param name="marks">What the modules want on it.</param>
    /// <param name="strip">Where everything goes and how big it is.</param>
    /// <param name="addon">The window the row belongs to.</param>
    public void Lay(DutyFinderRow row, IReadOnlyList<DutyMark> marks, StripLayout.Strip strip, AddonContentsFinder* addon)
    {
        Restore();

        var all = row.Places;
        var lit = all.Where(place => place.Lit).ToList();
        var size = new Vector2(strip.Size, strip.Size);
        var down = DutyFinderMetrics.StripTop + ((DutyFinderMetrics.StripSlotHeight - strip.Size) / 2f);
        var places = strip.Marks.Concat(strip.GameIcons).ToList();

        for (var index = 0; index < places.Count; index++)
        {
            if (At(index, row) is not { } icon)
            {
                continue;
            }

            var at = new Vector2(places[index], down);
            if (index < marks.Count)
            {
                icon.Draw(marks[index].IconId, at, size);
                icon.Explain(marks[index].Tooltip);
                continue;
            }

            // One of the game's: drawn again by us, its own picture hidden, and the patch that
            // speaks for it carried over so its words come with it.
            var slot = lit[index - marks.Count];
            icon.Draw(slot, at, size);
            icon.Explain(string.Empty);
            Hide(slot);
            Carry(slot.Touch, at, size);
        }

        for (var extra = places.Count; extra < icons.Count; extra++)
        {
            icons[extra].Hide();
        }
    }

    /// <summary>Hides again any picture of the game's that it has shown again itself. The game
    /// redraws a row whenever it is chosen or let go of, which puts back the pictures we drew in
    /// its place, and both would then be drawn at once.</summary>
    public void Reassert()
    {
        foreach (var picture in hidden)
        {
            var node = (AtkResNode*)picture;
            if (node != null && node->IsVisible())
            {
                node->ToggleVisibility(false);
            }
        }
    }

    /// <summary>Puts everything of the game's back as it was found. Safe to call twice.</summary>
    public void Restore()
    {
        hidden.Clear();
        foreach (var was in moved)
        {
            was.Restore();
        }

        moved.Clear();
    }

    /// <summary>The icon the pointer is resting on, and what it means, or nothing.</summary>
    /// <param name="x">Across the screen.</param>
    /// <param name="y">Down the screen.</param>
    public RowIcon? Under(float x, float y) => icons.FirstOrDefault(icon => icon.Under(x, y));

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
            hidden.Add(slot.Picture);
            picture->ToggleVisibility(false);
        }
    }

    /// <summary>Carries a patch to where the icon it speaks for now stands.</summary>
    private void Carry(nint patch, Vector2 at, Vector2 size)
    {
        var touch = (AtkResNode*)patch;
        if (touch == null)
        {
            return;
        }

        moved.Add(RowMoved.Of(touch));
        touch->SetXFloat(at.X);
        touch->SetYFloat(at.Y);
        touch->SetWidth((ushort)size.X);
        touch->SetHeight((ushort)size.Y);
    }
}
