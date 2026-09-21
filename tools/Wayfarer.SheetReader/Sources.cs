using Lumina;
using Lumina.Excel.Sheets;

namespace Wayfarer.SheetReader;

/// <summary>How many of a kind of thing the sheets can say where to get, and how many they cannot.
///
/// <para>A catalogue that guesses reads exactly like one that knows, so the useful number is not
/// how many entries it holds but how many of them it can account for. This counts that, per route
/// in, so a gap is something to go and close rather than something a player discovers.</para>
///
/// <para>The chain is item-first. Nothing in the Mount sheet says how a mount is obtained; what
/// exists is an item whose use teaches it, so the mount is found from the item and the item is then
/// looked for wherever the game hands things out.</para></summary>
internal static class Sources
{
    /// <summary>The game action an item performs when it teaches a mount. An item action does not
    /// say in words what kind of thing it is; it names the action it performs, and this is the one
    /// that adds a mount. Its first datum is the mount's row.</summary>
    private const uint TeachesMount = 1322;

    /// <summary>The same, for a minion.</summary>
    private const uint TeachesMinion = 853;

    public static void Mounts(GameData game) => Count(game, TeachesMount, "mount", game.GetExcelSheet<Mount>()!.Count);

    public static void Minions(GameData game) => Count(game, TeachesMinion, "minion", game.GetExcelSheet<Companion>()!.Count);

    private static void Count(GameData game, uint teaches, string what, int total)
    {
        // Which item teaches which thing.
        var taughtBy = new Dictionary<uint, uint>();
        foreach (var item in game.GetExcelSheet<Item>()!)
        {
            if (item.ItemAction.ValueNullable is not { } action || action.Action.RowId != teaches || action.Data[0] == 0)
            {
                continue;
            }

            taughtBy.TryAdd(action.Data[0], item.RowId);
        }

        // Every way the game is known to hand an item over.
        var fromQuest = new Dictionary<uint, uint>();
        foreach (var quest in game.GetExcelSheet<Quest>()!)
        {
            foreach (var reward in quest.Reward)
            {
                if (reward.RowId != 0)
                {
                    fromQuest.TryAdd(reward.RowId, quest.RowId);
                }
            }
        }

        var fromAchievement = new Dictionary<uint, uint>();
        foreach (var achievement in game.GetExcelSheet<Achievement>()!)
        {
            if (achievement.Item.RowId != 0)
            {
                fromAchievement.TryAdd(achievement.Item.RowId, achievement.RowId);
            }
        }

        var fromShop = new HashSet<uint>();
        foreach (var line in game.GetExcelSheet<SpecialShop>()!)
        {
            foreach (var offer in line.Item)
            {
                foreach (var received in offer.ReceiveItems)
                {
                    if (received.Item.RowId != 0)
                    {
                        fromShop.Add(received.Item.RowId);
                    }
                }
            }
        }

        foreach (var shop in game.GetSubrowExcelSheet<GilShopItem>()!.SelectMany(rows => rows))
        {
            if (shop.Item.RowId != 0)
            {
                fromShop.Add(shop.Item.RowId);
            }
        }

        var (quested, achieved, shopped, unknown, noItem) = (0, 0, 0, 0, 0);
        var missing = new List<uint>();

        foreach (var id in taughtBy.Keys.Order())
        {
            var item = taughtBy[id];
            if (fromQuest.ContainsKey(item))
            {
                quested++;
            }
            else if (fromAchievement.ContainsKey(item))
            {
                achieved++;
            }
            else if (fromShop.Contains(item))
            {
                shopped++;
            }
            else
            {
                unknown++;
                missing.Add(id);
            }
        }

        noItem = total - taughtBy.Count;

        Console.WriteLine($"{what}s in the sheet: {total}");
        Console.WriteLine($"  taught by an item:  {taughtBy.Count}");
        Console.WriteLine($"    from a quest:     {quested}");
        Console.WriteLine($"    from a shop:      {shopped}");
        Console.WriteLine($"    from achievement: {achieved}");
        Console.WriteLine($"    source unknown:   {unknown}");
        Console.WriteLine($"  no teaching item:   {noItem}");
        Console.WriteLine($"  accounted for:      {(taughtBy.Count - unknown) * 100 / Math.Max(1, total)}%");

        // Named, because a list of numbers says nothing about whether the gap is one the sheets
        // could close or one they never could -- a mount sold on the Mog Station is not missing
        // data, it is data the game was never given.
        if (missing.Count > 0)
        {
            Console.WriteLine("  unaccounted:");
            foreach (var id in missing)
            {
                var name = teaches == TeachesMount
                    ? game.GetExcelSheet<Mount>()!.GetRowOrDefault(id)?.Singular.ExtractText()
                    : game.GetExcelSheet<Companion>()!.GetRowOrDefault(id)?.Singular.ExtractText();
                Console.WriteLine($"    {id,4}  {name}  (item {taughtBy[id]})");
            }
        }
    }
}
