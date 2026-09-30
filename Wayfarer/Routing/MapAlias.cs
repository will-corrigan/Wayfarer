namespace Wayfarer.Routing;

/// <summary>A map of a zone that is only another of the same zone's maps under a different number:
/// the same room at another point of the story. The Waking Sands has three maps whose ranges are one
/// box, and a quest names whichever it likes, so routing treats them as one.</summary>
/// <param name="Territory">The zone.</param>
/// <param name="Map">The map a place may be given on.</param>
/// <param name="SameAs">The map the graph uses for the same ground.</param>
public sealed record MapAlias(uint Territory, uint Map, uint SameAs);
