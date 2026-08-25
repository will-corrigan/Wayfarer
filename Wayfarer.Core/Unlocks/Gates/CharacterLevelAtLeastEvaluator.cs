using System.Globalization;
using Wayfarer.Core.Unlocks.Live;

namespace Wayfarer.Core.Unlocks.Gates;

/// <summary>The active job's level. Curated only where the Quest sheet genuinely disagrees with
/// the level a player needs — the sheet's own level is read directly and is not a gate node.</summary>
public sealed class CharacterLevelAtLeastEvaluator : IGateEvaluator
{
    public string Kind => GateKinds.CharacterLevelAtLeast;

    public GateResult Evaluate(GateNode node, ILiveState state)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(state);
        if (node.Amount <= 0)
        {
            return GateResult.Unknown("malformed level requirement");
        }

        return state.Character.Level >= node.Amount
            ? GateResult.Ok()
            : GateResult.Blocked(
                UnlockStatus.LevelLocked,
                $"needs level {node.Amount.ToString(CultureInfo.InvariantCulture)}");
    }
}
