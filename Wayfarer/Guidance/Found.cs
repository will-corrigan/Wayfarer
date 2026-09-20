using Wayfarer.Routing;

namespace Wayfarer.Guidance;

/// <summary>Something in the world a module picked out to guide to.</summary>
/// <param name="Id">The one the world gives it, so it can be found again as it moves.</param>
/// <param name="At">Where it stood when it was picked.</param>
public readonly record struct Found(ulong Id, Place At);
