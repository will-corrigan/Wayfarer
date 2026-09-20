using FFXIVClientStructs.FFXIV.Client.Game;
using Wayfarer.Surfaces.DutyFinder;

namespace Wayfarer.Modules.Quests;

/// <summary>Marks the Duty Finder's rows for the duties the player still has a quest for, with the
/// mark the journal itself puts beside that quest: the main scenario's own, or the ordinary quest
/// one. Nothing here draws anything; it only answers which duties are wanted and how they look.
///
/// <para>The journal is read again only when it changes. Which duty a quest sends the player into
/// is fixed by the sheet, so nothing else about a quest — how far along it is, where the player is
/// standing — can change the answer.</para></summary>
internal sealed unsafe class QuestDutyBadges(QuestReader reader) : IDutyBadgeSource
{
    private readonly Dictionary<uint, uint> iconsByDuty = [];

    private int journal;
    private bool read;

    /// <inheritdoc/>
    public uint? IconFor(uint duty)
    {
        Reread();
        return iconsByDuty.TryGetValue(duty, out var icon) ? icon : null;
    }

    /// <summary>A number that changes when the journal does. The quests in it are what decides
    /// which duties are marked, so this is what says the answer is stale — and it is asked far
    /// more often than the journal changes, so it allocates nothing.</summary>
    private static int JournalSignature()
    {
        var quests = QuestManager.Instance();
        if (quests == null)
        {
            return 0;
        }

        var signature = default(HashCode);
        foreach (ref var quest in quests->NormalQuests)
        {
            signature.Add(quest.QuestId);
        }

        return signature.ToHashCode();
    }

    /// <summary>Reads the journal again if it has changed since last time.</summary>
    private void Reread()
    {
        var now = JournalSignature();
        if (read && now == journal)
        {
            return;
        }

        journal = now;
        read = true;
        iconsByDuty.Clear();

        var quests = QuestManager.Instance();
        if (quests == null)
        {
            return;
        }

        foreach (ref var quest in quests->NormalQuests)
        {
            if (quest.QuestId != 0
                && reader.Duty(quest.QuestId) is { } duty
                && reader.JournalIcon(quest.QuestId) is { } icon)
            {
                // Two quests can send the player into the same duty. The first one found marks it;
                // the mark says there is something left to do there, not how many things.
                iconsByDuty.TryAdd(duty.Finder, icon);
            }
        }
    }
}
