using System.Globalization;
using Wayfarer.Core.Unlocks.Live;

namespace Wayfarer.Core.Unlocks.Gates;

/// <summary>The <b>active job's</b> level, and nothing wider. Curated only where the Quest sheet
/// genuinely disagrees with the level a player needs — the sheet's own level is read directly and is
/// not a gate node.
///
/// <para><b>The kind's name is a trap and this is the warning on it.</b> There is no such thing as a
/// character level in this game: every level belongs to a class or job, and the one this reads is
/// whichever job the player currently has equipped. A level-100 character standing there on a
/// level-1 alt job will be told "needs level 80", correctly by this evaluator's own lights and
/// uselessly by the player's. Data that means "this content wants a job at level N", which is what
/// the game's own quest gates mean, is expressed with <see cref="GateKinds.JobLevelAtLeast"/> naming
/// the job. Use this one only where the requirement really is about whatever the player is playing
/// right now.</para>
///
/// <para>Nothing in the shipped catalogue uses it. If something is about to, that is the moment to
/// rename the kind rather than to inherit the ambiguity.</para></summary>
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
