using FFXIVClientStructs.FFXIV.Client.Game;

namespace Wayfarer.Modules.Quests;

/// <summary>Which Duty Finder entries the player has a quest waiting on, and the mark the game
/// would draw beside that quest.
///
/// <para>A quest that sends the player into instanced content names the entry to queue for among
/// its own script parameters. Walking every accepted quest gives the set of entries that still
/// lead somewhere, which is what the Duty Finder does not say: a row there looks the same whether
/// a quest is waiting behind it or not.</para>
///
/// <para>Read when the window opens rather than every frame. Accepting or finishing a quest while
/// the Duty Finder is open is rare, and the window asks again when it is refreshed.</para>
/// </summary>
internal sealed unsafe class QuestDuties(QuestReader reader)
{
    private readonly Dictionary<uint, QuestBehind> byFinder = [];

    /// <summary>Whether any accepted quest leads anywhere instanced, which is whether the window is
    /// worth touching at all.</summary>
    public bool Any => byFinder.Count > 0;

    /// <summary>Reads the player's journal again. Cheap enough to do on opening the window.</summary>
    public void Read()
    {
        byFinder.Clear();

        var quests = QuestManager.Instance();
        if (quests == null)
        {
            return;
        }

        foreach (ref var accepted in quests->NormalQuests)
        {
            if (accepted.QuestId == 0 || reader.Duty(accepted.QuestId) is not { } duty)
            {
                continue;
            }

            // Several quests can lead to one duty. The first is as good an answer as any: the mark
            // says a quest is waiting, and pressing it opens that one.
            if (reader.QuestIcon(accepted.QuestId) is { } icon)
            {
                byFinder.TryAdd(duty.Finder, new QuestBehind(accepted.QuestId, icon));
            }
        }
    }

    /// <summary>The quest waiting behind a Duty Finder row, or null when none is.</summary>
    /// <param name="finder">The Duty Finder entry the row is for.</param>
    public QuestBehind? Behind(uint finder) =>
        byFinder.TryGetValue(finder, out var quest) ? quest : null;
}
