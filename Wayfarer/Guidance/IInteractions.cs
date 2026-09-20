namespace Wayfarer.Guidance;

/// <summary>What the player has already tried. A step that sends them to search an area often has
/// several of the same thing standing in it and only one that answers; the game says nothing about
/// which, and a thing that has already been tried looks exactly like one that has not. Watching
/// what the player interacts with is the only way to stop sending them back to it.</summary>
internal interface IInteractions
{
    /// <summary>How many things have been tried.</summary>
    int Count { get; }

    /// <summary>Whether the player has interacted with this thing since the last <see cref="Forget"/>.</summary>
    /// <param name="baseId">The id the world gives the thing.</param>
    bool Tried(uint baseId);

    /// <summary>Forgets everything, because what is being guided to has changed and what was tried
    /// for the last step says nothing about this one.</summary>
    void Forget();
}
