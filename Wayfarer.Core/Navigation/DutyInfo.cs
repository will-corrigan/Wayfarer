namespace Wayfarer.Core.Navigation;

/// <summary>A duty (instanced content) that owns a given TerritoryType, resolved from
/// the InstanceContent sheet's direct ContentFinderCondition link.</summary>
/// <param name="Name">The ContentFinderCondition row's display name, e.g. "The Fist of
/// the Father".</param>
/// <param name="InstanceContentId">The InstanceContent row id — what
/// UIState.IsInstanceContentUnlocked expects.</param>
/// <param name="ContentFinderConditionId">The ContentFinderCondition row id — what
/// AgentContentsFinder.OpenRegularDuty expects to queue the duty via Duty Finder.</param>
public readonly record struct DutyInfo(string Name, uint InstanceContentId, uint ContentFinderConditionId);
