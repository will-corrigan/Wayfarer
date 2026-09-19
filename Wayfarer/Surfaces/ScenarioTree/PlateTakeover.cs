using FFXIVClientStructs.FFXIV.Component.GUI;
using static Wayfarer.Surfaces.ScenarioTree.ScenarioTreeMetrics;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>The guide's plate and header retitled to the quest being followed, and put back when
/// the main scenario is followed again or the block goes. The game rewrites both texts whenever
/// its own quest changes, so every update the takeover checks whether the words on the plate are
/// still its own; when they are not, the game has spoken, and its new words are kept as the ones
/// to restore.
///
/// <para>Text nodes are looked up fresh on every call and never kept: the addon owns them, and a
/// pointer kept across frames is a pointer that can go stale.</para></summary>
internal sealed unsafe class PlateTakeover
{
    private const string FollowedHeader = "Followed Quest";

    private string? gameTitle;
    private string? gameHeader;
    private string? ourTitle;

    /// <summary>Whether the plate currently carries our words rather than the game's.</summary>
    public bool Active => ourTitle is not null;

    /// <summary>Retitles the plate to a headline, unless the game's own title already is it.</summary>
    public void Apply(AtkUnitBase* addon, string headline)
    {
        var title = PlateTitle(addon);
        var header = Header(addon);
        if (title == null || header == null)
        {
            return;
        }

        var shown = title->NodeText.ToString();
        if (!Active || !string.Equals(shown, ourTitle, StringComparison.Ordinal))
        {
            // Either we have not taken over yet, or the game rewrote the plate since we did.
            gameTitle = shown;
            gameHeader = header->NodeText.ToString();
        }

        if (string.Equals(headline, gameTitle, StringComparison.Ordinal))
        {
            Release(addon);
            return;
        }

        ourTitle = headline;
        title->SetText(headline);
        header->SetText(FollowedHeader);
    }

    /// <summary>Puts the game's own words back, if ours are on the plate.</summary>
    public void Release(AtkUnitBase* addon)
    {
        if (!Active)
        {
            return;
        }

        ourTitle = null;
        var title = PlateTitle(addon);
        var header = Header(addon);
        if (title != null && gameTitle is not null)
        {
            title->SetText(gameTitle);
        }

        if (header != null && gameHeader is not null)
        {
            header->SetText(gameHeader);
        }
    }

    private static AtkTextNode* PlateTitle(AtkUnitBase* addon)
    {
        var plate = addon->GetComponentByNodeId(PlateNodeId);
        return plate == null ? null : plate->GetTextNodeById(PlateTitleTextNodeId);
    }

    private static AtkTextNode* Header(AtkUnitBase* addon) =>
        addon->GetNodeById(HeaderTextNodeId) is var node && node != null ? node->GetAsAtkTextNode() : null;
}
