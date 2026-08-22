namespace Wayfarer.Core.Hunting;

/// <summary>A rank page's state relative to the player's current rank on that log. Per the
/// verified <c>MonsterNoteManager</c> semantics: live per-monster kill counts only exist for the
/// current page — earlier pages are simply "done" (no counts to read, the rank was cleared) and
/// later pages are "locked" (not reached yet, also no counts).</summary>
public enum HuntingPageState
{
    Done,
    Current,
    Locked,
}
