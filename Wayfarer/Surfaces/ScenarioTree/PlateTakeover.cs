using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Wayfarer.Surfaces.ScenarioTree;

/// <summary>The guide's plate and header retitled to the quest being followed, and put back when
/// nothing is followed any more or the block goes. The game rewrites both texts whenever its own
/// quest changes, so every update the takeover checks whether the words on the plate are still its
/// own; when they are not, the game has spoken, and its new words are kept as the ones to restore.
///
/// <para>Nothing is ever written until there are words of the game's own to put back, and nothing
/// blank is ever written: the guide is built before the game fills it, so words read too early are
/// no words at all, and writing those back would leave the player with an empty plate.</para>
///
/// <para>Text nodes are looked up fresh on every call and never kept: the addon owns them, and a
/// pointer kept across frames is a pointer that can go stale.</para></summary>
internal sealed unsafe class PlateTakeover
{
    private string? gameTitle;
    private string? gameHeader;
    private string? ourTitle;

    /// <summary>Whether the plate currently carries our words rather than the game's.</summary>
    private bool Active => ourTitle is not null;

    /// <summary>Puts the guidance on the plate, or takes ours back off it. A plate the game has
    /// left blank is filled in with the headline, which is the same thing the game would write:
    /// it is only ever written into emptiness, so the game is never argued with.</summary>
    /// <param name="addon">The guide.</param>
    /// <param name="headline">What the guidance is about, or null when nothing is guided.</param>
    /// <param name="kind">What kind of thing it is, for the heading above the plate, or null to
    /// leave the game's own heading alone.</param>
    /// <param name="ours">Whether the guidance is about something other than the guide's own quest.</param>
    public void Update(AtkUnitBase* addon, string? headline, string? kind, bool ours)
    {
        if (ours && !string.IsNullOrEmpty(headline))
        {
            Apply(addon, headline, kind);
            return;
        }

        Release(addon);
        if (!string.IsNullOrEmpty(headline) && GuideNodes.PlateTitle(addon) is var plate && plate != null && plate->NodeText.Length == 0)
        {
            plate->SetText(headline);
        }
    }

    /// <summary>Puts the game's own words back, if ours are on the plate.</summary>
    public void Release(AtkUnitBase* addon)
    {
        if (!Active)
        {
            return;
        }

        ourTitle = null;
        Write(GuideNodes.PlateTitle(addon), gameTitle);
        Write(GuideNodes.Header(addon), gameHeader);
    }

    /// <summary>Writes words into one of the guide's text nodes, never nothing.</summary>
    private static void Write(AtkTextNode* node, string? words)
    {
        if (node != null && !string.IsNullOrEmpty(words))
        {
            node->SetText(words);
        }
    }

    /// <summary>Retitles the plate to a headline, unless the game's own title already is it.</summary>
    private void Apply(AtkUnitBase* addon, string headline, string? kind)
    {
        var title = GuideNodes.PlateTitle(addon);
        var header = GuideNodes.Header(addon);
        if (title == null || header == null || string.IsNullOrEmpty(headline))
        {
            return;
        }

        var shown = title->NodeText.ExtractText();
        if (!Active || !string.Equals(shown, ourTitle, StringComparison.Ordinal))
        {
            // Either we have not taken over yet, or the game rewrote the plate since we did.
            Remember(shown, header->NodeText.ExtractText());
        }

        if (gameTitle is null || string.Equals(headline, gameTitle, StringComparison.Ordinal))
        {
            // Either the guide has not been filled in yet, so there is nothing to put back and
            // nothing to take over from, or it already names the very thing we would write.
            Release(addon);
            return;
        }

        ourTitle = headline;
        title->SetText(headline);
        if (!string.IsNullOrEmpty(kind))
        {
            header->SetText(kind);
        }
    }

    /// <summary>Keeps the game's own words, unless it has not written any yet.</summary>
    private void Remember(string title, string header)
    {
        if (string.IsNullOrEmpty(title))
        {
            return;
        }

        gameTitle = title;
        gameHeader = header;
    }
}
