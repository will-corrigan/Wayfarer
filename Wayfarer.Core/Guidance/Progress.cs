namespace Wayfarer.Core.Guidance;

/// <summary>How much of something is done, as the game counts it. Numbers rather than words so
/// every surface draws a count the same way for every module.</summary>
/// <param name="Done">How many so far.</param>
/// <param name="Needed">How many in total.</param>
public sealed record Progress(int Done, int Needed);
