namespace Wayfarer.Core.Ui;

/// <summary>What the player has to do next to get closer to the active objective — the one fact the
/// server info bar entry is built from.
///
/// <para>Derived from the same guidance snapshot the readout composes its lines from, so the bar and
/// the readout cannot describe different next steps. The readout has room to spell the whole leg
/// out; the bar has room for about five words, so it says only this.</para></summary>
public enum DtrNextStep
{
    /// <summary>Nothing engaged, or nothing with a direction to it.</summary>
    None,

    /// <summary>Walk there. Same zone, or through a door already in this one.</summary>
    Walk,

    /// <summary>Teleport to an aetheryte first.</summary>
    Teleport,

    /// <summary>Take the aethernet to another shard in this city.</summary>
    Aethernet,
}
