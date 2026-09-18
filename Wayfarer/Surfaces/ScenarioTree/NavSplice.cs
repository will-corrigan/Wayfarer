using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>Our lines spliced into the guide's cursor chain where they sit on screen: after the
/// last visible job row and before the cursor wraps back to the plate. Two of the game's own
/// records are rewritten for that, the row's "down" and the plate's "up", and both are put back
/// the moment the lines have nothing to press or the block goes.
///
/// <para>Only addresses are kept between frames; they are dereferenced only while the guide is
/// alive, which is the only time <see cref="Splice"/> and <see cref="Restore"/> are called.</para></summary>
internal sealed unsafe class NavSplice
{
    private nint above;
    private nint plate;
    private byte aboveDownBefore;
    private byte plateUpBefore;
    private int? firstStop;
    private int? lastStop;

    private bool Applied => above != 0;

    /// <summary>Links the block in after <paramref name="aboveUs"/>, or takes the links out when
    /// the block has nothing to press. Re-links only when something changed.</summary>
    public void Splice(AtkComponentBase* plateComponent, AtkComponentBase* aboveUs, GuidanceBlockNode block)
    {
        if (plateComponent == null || aboveUs == null || block.FirstStop is not { } first || block.LastStop is not { } last)
        {
            Restore();
            return;
        }

        var unchanged = Applied && above == (nint)aboveUs && plate == (nint)plateComponent && firstStop == first && lastStop == last;
        if (unchanged)
        {
            return;
        }

        Restore();
        above = (nint)aboveUs;
        plate = (nint)plateComponent;
        firstStop = first;
        lastStop = last;

        ref var aboveNav = ref aboveUs->CursorNavigationInfo;
        ref var plateNav = ref plateComponent->CursorNavigationInfo;
        aboveDownBefore = aboveNav.DownIndex;
        plateUpBefore = plateNav.UpIndex;
        aboveNav.DownIndex = (byte)first;
        plateNav.UpIndex = (byte)last;
        block.LinkNav(aboveNav.Index, plateNav.Index);
    }

    /// <summary>Puts the game's two records back as they were.</summary>
    public void Restore()
    {
        if (!Applied)
        {
            return;
        }

        ((AtkComponentBase*)above)->CursorNavigationInfo.DownIndex = aboveDownBefore;
        ((AtkComponentBase*)plate)->CursorNavigationInfo.UpIndex = plateUpBefore;
        above = 0;
        plate = 0;
        firstStop = null;
        lastStop = null;
    }
}
