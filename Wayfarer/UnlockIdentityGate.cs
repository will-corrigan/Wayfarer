using Lumina.Excel;
using Lumina.Excel.Sheets;
using Wayfarer.Core.Unlocks;
using Wayfarer.Core.Unlocks.Gates;

namespace Wayfarer;

/// <summary>Turns an entry's own reward identity into a gate that reads it live.
///
/// <para>The catalogue records what each entry unlocks as a sheet row rather than a sentence, so
/// that a picture can be drawn for it. That same row is an answer to a question the catalogue
/// otherwise had to shrug at: a third of the ungradeable entries are duty access, curated as
/// "unlocked by clearing X, and whether you then took the unlock is unreadable" — but the client
/// keeps an unlock bit for every duty, and the duty in question is the entry's own reward.</para>
///
/// <para>Dispatch is on the reward's <b>sheet kind</b>, never on an entry: any entry whose reward
/// is a duty gets the gate, and any entry whose reward is something else gets none. Other kinds
/// (a mount, a minion, an emote) have equally readable ownership bits and would each be four more
/// lines here — deliberately left for when there is a reason, so this change carries only what it
/// has evidence for.</para></summary>
internal static class UnlockIdentityGate
{
    /// <summary>The reward kind whose row identifies a duty. One of the closed set in
    /// <see cref="UnlockRewardKinds"/>; the string names a SHEET, not a catalogue entry.</summary>
    private const string DutyRewardKind = "ContentFinderCondition";

    /// <summary><c>ContentFinderCondition.ContentLinkType</c>. The <c>Content</c> column is an
    /// untyped reference and this is the only column that says which sheet it points into: 1 is
    /// InstanceContent (729 rows), 3 is PublicContent (44). The rest — retired Heavensward public
    /// content, Gold Saucer, and two unnamed groups — have no reader here and get no gate.</summary>
    private const byte ContentLinkInstanceContent = 1;

    /// <inheritdoc cref="ContentLinkInstanceContent"/>
    private const byte ContentLinkPublicContent = 3;

    /// <summary>The gate for this entry's identity, or null when the entry has no readable one.
    ///
    /// <para>Null rather than a guess is the whole discipline. Passing a public-content id to the
    /// instance-content reader does not fail — it reads a different duty's bit and answers
    /// confidently — so a row whose link type is neither of the two readable spaces is left
    /// ungated and the entry keeps saying it does not know.</para></summary>
    public static GateNode? For(UnlockReward? reward, ExcelSheet<ContentFinderCondition> duties)
    {
        if (reward is not { Kind: DutyRewardKind }
            || duties.GetRowOrDefault(reward.Id) is not { } row
            || row.Content.RowId == 0)
        {
            return null;
        }

        var scope = row.ContentLinkType switch
        {
            ContentLinkInstanceContent => GateKinds.ScopeInstance,
            ContentLinkPublicContent => GateKinds.ScopePublic,
            _ => null,
        };

        return scope is null
            ? null
            : new GateNode
            {
                Kind = GateKinds.DutyUnlocked,
                Ids = [row.Content.RowId],
                Scope = scope,
                Display = reward.Name,
            };
    }
}
