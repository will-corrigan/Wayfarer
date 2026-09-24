using Wayfarer.Routing;

namespace Wayfarer.Modules.Hunting;

/// <summary>One monster a hunt asks for, as the sheets have it: everything but the kills.</summary>
/// <param name="Name">Its name as the game shows it.</param>
/// <param name="NameId">The game's id for that name.</param>
/// <param name="Need">How many the hunt wants.</param>
/// <param name="Places">Where it lives.</param>
/// <param name="Duty">The duty it lives inside, if it lives inside one.</param>
/// <param name="Fate">The FATE it appears in, if it is one of those.</param>
/// <param name="Entry">For a log page, which of the page's entries it is under; for a bill, which
/// page of the bill it is on. The game counts kills by it.</param>
/// <param name="Target">For a log page, which of the entry's monsters it is.</param>
internal sealed record HuntQuarry(
    string Name,
    uint NameId,
    int Need,
    IReadOnlyList<Place> Places,
    uint? Duty,
    QuarryFate? Fate,
    int Entry,
    int Target)
{
    /// <summary>This monster with how many have been killed.</summary>
    public Quarry With(int have) => new(Name, NameId, have, Need, Places, Duty, Fate);
}
