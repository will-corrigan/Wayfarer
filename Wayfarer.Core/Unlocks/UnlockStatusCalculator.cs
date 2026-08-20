using System.Globalization;

namespace Wayfarer.Core.Unlocks;

public static class UnlockStatusCalculator
{
    /// <summary>Sets Status/LockReason on every entry. Done is checked for every
    /// matched entry (levels are per-job; completion is per-character and cheap);
    /// prerequisite chains are only walked for entries at/below the player level.</summary>
    public static void Compute(
        List<ResolvedUnlock> all,
        int playerLevel,
        Func<uint, bool> isQuestComplete,
        Func<uint, bool> isQuestAccepted)
    {
        // Alternative quests share the same unlock name: any one complete → all Done.
        var doneByName = new HashSet<string>(StringComparer.Ordinal);
        foreach (var u in all)
        {
            if (u.QuestRowId is { } id && isQuestComplete(id))
            {
                doneByName.Add(u.Def.Unlock);
            }
        }

        foreach (var u in all)
        {
            u.LockReason = null;
            if (u.QuestRowId is not { } rowId)
            {
                u.Status = UnlockStatus.Unverified;
                continue;
            }

            if (doneByName.Contains(u.Def.Unlock))
            {
                u.Status = UnlockStatus.Done;
                continue;
            }

            if (isQuestAccepted(rowId))
            {
                u.Status = UnlockStatus.Accepted;
                continue;
            }

            if (u.QuestLevel > playerLevel)
            {
                u.Status = UnlockStatus.LevelLocked;
                u.LockReason = $"needs level {u.QuestLevel}";
                continue;
            }

            var locked = false;
            for (var i = 0; i < u.PrereqRowIds.Count; i++)
            {
                if (isQuestComplete(u.PrereqRowIds[i]))
                {
                    continue;
                }

                u.Status = UnlockStatus.QuestLocked;
                u.LockReason = $"needs quest '{(i < u.PrereqNames.Count ? u.PrereqNames[i] : u.PrereqRowIds[i].ToString(CultureInfo.InvariantCulture))}'";
                locked = true;
                break;
            }

            if (!locked)
            {
                u.Status = UnlockStatus.Available;
            }
        }
    }

    public static int CountAvailableIn(IEnumerable<ResolvedUnlock> all, uint territory)
    {
        var n = 0;
        foreach (var u in all)
        {
            if (u.Status == UnlockStatus.Available && u.GiverTerritory == territory)
            {
                n++;
            }
        }

        return n;
    }
}
