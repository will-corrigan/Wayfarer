using Wayfarer.Routing;

namespace Wayfarer.Modules.Hunting;

/// <summary>One monster a hunt asks for: what it is, how many are wanted and have been killed,
/// and where it can be found.</summary>
/// <param name="Name">Its name as the game writes it.</param>
/// <param name="NameId">The game's id for that name, which is how it is recognised standing in
/// the world: every monster of a kind carries its name's id.</param>
/// <param name="Have">How many have been killed for this hunt.</param>
/// <param name="Need">How many the hunt wants.</param>
/// <param name="Places">Where it lives: the parts of a map the game names for it. Several when it
/// lives in more than one, and routing picks the nearest. Empty when it lives inside a duty.</param>
/// <param name="Duty">The Duty Finder entry it lives inside, when it lives nowhere a player can
/// walk to.</param>
/// <param name="Fate">The FATE it only appears in, when it is one of those.</param>
internal sealed record Quarry(
    string Name,
    uint NameId,
    int Have,
    int Need,
    IReadOnlyList<Place> Places,
    uint? Duty = null,
    QuarryFate? Fate = null)
{
    /// <summary>Whether enough have been killed.</summary>
    public bool Done => Have >= Need;
}
