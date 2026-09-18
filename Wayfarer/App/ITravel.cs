namespace Wayfarer.App;

/// <summary>The two things a press on a route can do. The teleport is the plugin's only action
/// that reaches the server; one deliberate press is one cast. Opening the Duty Finder is client
/// UI only.</summary>
internal interface ITravel
{
    /// <summary>Casts a teleport to the aetheryte, if the player is attuned to it and the game
    /// allows it now. A refusal is logged, never silent.</summary>
    void TeleportTo(uint aetheryteId);

    /// <summary>Opens the game's Duty Finder at this duty, ready to queue.</summary>
    void OpenDutyFinder(uint dutyId);
}
