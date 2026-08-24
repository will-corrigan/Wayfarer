namespace Wayfarer.Core.Unlocks;

/// <summary>A reference to text the game itself authored — a sheet name, row, and column — instead
/// of a copy of that text.
///
/// <para>The client ships its own explanations for why something is unavailable: <c>HowToPage</c>'s
/// structured requirement checklists, per-quest and per-<c>CustomTalk</c> <c>_SYSTEM_</c> notices,
/// <c>Achievement.Description</c>, and parameterised <c>Addon</c>/<c>LogMessage</c> templates. See
/// <c>docs/superpowers/specs/2026-08-24-requirement-text-provenance.md</c> for the survey that found
/// them. Quoting a reference to one of those beats curating prose to describe the same condition: it
/// is Square Enix's own wording, already localised into whatever language the player's own client
/// runs in, and it cannot drift out of date with a patch the way a paraphrase can.</para>
///
/// <para>The generator (<c>tools/Wayfarer.CatalogueGen</c>) only ever records <i>where</i> the text
/// lives — never a copy of it — so the committed dataset stays free of game text that could go
/// stale. Resolution happens at runtime, against the live client, through
/// <see cref="UnlockGateContext.ResolveGameText"/>.</para></summary>
public sealed record GameTextRef(string Sheet, uint Row, int Column)
{
    public GameTextRef()
        : this(string.Empty, 0, 0)
    {
    }
}
