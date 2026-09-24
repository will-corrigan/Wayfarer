namespace Wayfarer.Modules.Hunting;

/// <summary>The FATE a monster only appears during.</summary>
/// <param name="Id">The FATE's row, which is also how the game names it while it is running.</param>
/// <param name="Name">What the FATE is called.</param>
internal sealed record QuarryFate(uint Id, string Name);
