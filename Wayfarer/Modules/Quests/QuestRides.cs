using System.Text.RegularExpressions;
using Lumina.Excel.Sheets;

namespace Wayfarer.Modules.Quests;

/// <summary>Who in a quest puts the player on a ride or changes them: the amaro's keeper, the one
/// who hands out the magitek armor, the sorceress who transfigures them. The sheets say it, not the
/// words: a quest that changes the player names that change among its own parameters, and the
/// person's listener says how they take part.
///
/// <para>A quest with a mount (<c>MOUNT0</c>), a transformation (<c>TRANSFORMATION0</c>) or a
/// player state of being invisible, transformed or disguised (<c>ET_TOUMEI</c>, <c>ET_HENSIN</c>,
/// <c>ET_TRANSFORM</c>, <c>ET_KOROMOGAE</c>) makes its people givers, unless a person's listener
/// says they go along with the player (<c>AcceptBool</c>, as on "Accompany Yugiri"). A listener the
/// quest marks <c>QualifiedBool</c> is a giver even without such a key: the tonberry disguise of
/// "Get Along and Play Knife" names none. "Put to the Proof" has neither, so Y'shtola, there to be
/// asked again, is only company. Checked against every quest whose lines carry someone along.</para></summary>
internal static partial class QuestRides
{
    /// <summary>The givers of a quest, by who they are and the step they are listed for.</summary>
    public static IReadOnlySet<(uint Person, byte Sequence)> Givers(Quest quest)
    {
        var changesPlayer = quest.QuestParams.Any(parameter => parameter.ScriptArg != 0 && ChangesPlayer().IsMatch(parameter.ScriptInstruction.ExtractText()));
        return quest.QuestListenerParams
            .Where(listener => listener.Listener is >= 1_000_000 and < 2_000_000 && !listener.AcceptBool && (changesPlayer || listener.QualifiedBool))
            .Select(listener => (listener.Listener, listener.ActorSpawnSeq))
            .ToHashSet();
    }

    [GeneratedRegex(@"^(MOUNT_?\d+|TRANSFORMATION.*|ET_TOUMEI|ET_HENSIN|ET_TRANSFORM|ET_KOROMOGAE)$", RegexOptions.ExplicitCapture, 100)]
    private static partial Regex ChangesPlayer();
}
