using Wayfarer.Routing;

namespace Wayfarer.World;

/// <summary>One thing standing in the world, as much of it as deciding needs: which one it is,
/// what it is, what spawned it, whether the player could act on it now, and where it is.</summary>
/// <param name="Id">The one the world gives this very thing, which no other shares and which is
/// how it is found again a moment later when it has moved.</param>
/// <param name="BaseId">The id the world gives its kind, which is what a mark names. Several
/// things standing together can share one.</param>
/// <param name="Event">The event the game says spawned it, or zero. The game stamps this onto
/// what it spawns for an event and onto nothing else — people it placed beforehand carry no
/// stamp at all.</param>
/// <param name="Targetable">Whether the player could act on it this moment. The game's own answer,
/// and the only thing that says a step has come round: a thing can stand there inert for a whole
/// quest and become targetable for one step of it.</param>
/// <param name="Plate">The mark the game draws over its head, or zero for none. The game puts one
/// there while it still wants something of the player and takes it away once it has had it, which
/// is the only account of what is left to do that outlives our own memory of doing it.</param>
/// <param name="At">Where it is standing.</param>
public readonly record struct Candidate(ulong Id, uint BaseId, uint Event, bool Targetable, uint Plate, Place At);
