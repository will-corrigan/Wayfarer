using Wayfarer.Core.Unlocks.Live;

namespace Wayfarer.Core.Unlocks.Gates;

/// <summary>One gate kind. Implementations are stateless, pure functions of (node, live state),
/// and must not know which catalogue entry they are evaluating — they never see one.</summary>
public interface IGateEvaluator
{
    /// <summary>The <c>kind</c> string this evaluator answers to. Must be unique in the registry.</summary>
    string Kind { get; }

    GateResult Evaluate(GateNode node, ILiveState state);
}
