namespace Wayfarer.Core.Navigation;

/// <summary>How a single aether current is obtained, which is the only thing that changes where the
/// player has to go for it.</summary>
public enum AetherCurrentKind
{
    /// <summary>A placed object in the world: fly to it and attune. Its position is fixed in the
    /// game's own data, so it never has to be guessed or live-tracked.</summary>
    Attunable,

    /// <summary>Granted by completing a side quest. There is nothing in the world to fly to, so the
    /// destination is the quest's GIVER — the same thing the unlock route already walks to.</summary>
    Quest,
}
