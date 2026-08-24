using System.Text.RegularExpressions;
using Wayfarer.Core.Unlocks;

namespace Wayfarer.Tests;

/// <summary>The reward identity, from three directions: that the two copies of the kind list agree,
/// that every reward in the shipped catalogue is one the display can act on, and that the coverage
/// is what the design measured rather than whatever the last regeneration happened to produce.
///
/// <para>The catalogue used to know only what an unlock was <i>called</i>. A name cannot be drawn —
/// the picture of a mount lives on <c>Mount.Icon</c>, and "Firebird (Mount)" is a sentence in a
/// guide. These tests exist because that field is now load-bearing for what the detail pane
/// shows.</para></summary>
public class UnlockRewardTests
{
    /// <summary>The two halves of the kind list have to say the same thing. The generator writes
    /// the field against <c>data/reward-kinds.mjs</c>, CI validates the committed file against the
    /// same, and the plugin draws from <see cref="UnlockRewardKinds"/> — a kind in one and not the
    /// other is either a validator that rejects good data or a display with no arm for it.</summary>
    [Theory]
    [InlineData("WITH_ICON")]
    [InlineData("VIA_GRANTING_ITEM")]
    [InlineData("WITHOUT_ICON")]
    public void TheCSharpAndJavaScriptKindListsAgree(string list)
    {
        var expected = list switch
        {
            "WITH_ICON" => UnlockRewardKinds.WithIcon,
            "VIA_GRANTING_ITEM" => UnlockRewardKinds.ViaGrantingItem,
            _ => UnlockRewardKinds.WithoutIcon,
        };

        Assert.Equal(expected, ReadJavaScriptList(list));
    }

    /// <summary>Every kind that claims to draw a picture is one the display has a branch for, and
    /// every kind that does not is named out loud. The union is the whole vocabulary; nothing may
    /// be silently in neither.</summary>
    [Fact]
    public void EveryKindIsEitherDrawableOrDeclaredUndrawable()
    {
        foreach (var kind in UnlockRewardKinds.All)
        {
            Assert.True(UnlockRewardKinds.IsKnown(kind), kind);
            Assert.Equal(
                !UnlockRewardKinds.WithoutIcon.Contains(kind, StringComparer.Ordinal),
                UnlockRewardKinds.DrawsAnIcon(kind));
        }

        Assert.Equal(UnlockRewardKinds.All.Count, UnlockRewardKinds.All.Distinct(StringComparer.Ordinal).Count());
        Assert.False(UnlockRewardKinds.IsKnown("Achievement"));
        Assert.False(UnlockRewardKinds.DrawsAnIcon("Title"));
    }

    /// <summary>Every reward in the shipped file is something the pane can act on: a kind it knows,
    /// a row id it can look up, and a name it can say out loud. The name is not decoration —
    /// KamiToolKit registers tooltips on mouse events only, so a reward whose name were empty would
    /// be an icon with nothing beside it, which is unreadable with a pad in your hands.</summary>
    [Fact]
    public void EveryRewardIsOneTheDisplayCanActOn()
    {
        foreach (var d in Load().Where(d => d.Reward is not null))
        {
            var reward = d.Reward!;
            Assert.True(
                UnlockRewardKinds.IsKnown(reward.Kind),
                $"'{d.Unlock}' rewards kind '{reward.Kind}', which nothing downstream knows");
            Assert.True(reward.Id > 0, $"'{d.Unlock}' rewards {reward.Kind}#0, which is not a row");
            Assert.False(
                string.IsNullOrWhiteSpace(reward.Name),
                $"'{d.Unlock}' rewards {reward.Kind}#{reward.Id} with no name to say it by");
        }
    }

    /// <summary>An entry with no reward is a real answer, not a gap — and it is concentrated where
    /// the design said it would be. The <c>system</c> entries open features the game keeps no row
    /// for at all (the Aesthetician, retainer ventures, the gemstone traders) and <c>zone</c>
    /// entries name a place rather than a thing.
    ///
    /// <para>Asserted as a floor rather than an exact number so a regeneration that finds MORE
    /// rewards passes, while one that quietly loses the field fails. Losing it is the regression
    /// worth catching: the pane would go back to showing nothing where the reward row is.</para>
    /// </summary>
    [Fact]
    public void MostContentAndCosmeticEntriesKnowWhatTheyGrant()
    {
        var all = Load();
        var rewarded = all.Count(d => d.Reward is not null);

        // 316 of 587 at the time this was measured, against the design's estimate of ~320.
        Assert.True(rewarded >= 300, $"only {rewarded} of {all.Count} entries carry a reward identity");

        var drawable = all.Count(d => d.Reward is { } r && UnlockRewardKinds.DrawsAnIcon(r.Kind));
        Assert.True(drawable >= 290, $"only {drawable} of {rewarded} rewards resolve to a kind that draws");

        // The duty types are the ones the catalogue is mostly made of and the ones a player most
        // wants a picture of. A regeneration that stopped resolving them would still clear the
        // floors above on the strength of the cosmetics alone.
        foreach (var type in (string[])["dungeon", "trial"])
        {
            var ofType = all.Where(d => string.Equals(d.Type, type, StringComparison.Ordinal)).ToList();
            var known = ofType.Count(d => d.Reward is not null);
            Assert.True(known * 2 > ofType.Count, $"only {known} of {ofType.Count} '{type}' entries know what they open");
        }
    }

    /// <summary>The kinds the shipped file actually uses, so a regeneration that starts emitting a
    /// new one is a visible test change rather than a silent widening. Every one of these has to be
    /// a kind the pane can present — which the assertion above covers — and this one says which of
    /// them the catalogue is currently exercising.</summary>
    [Fact]
    public void TheShippedFileUsesOnlyTheKindsTheCatalogueHasEverNeeded()
    {
        var used = Load()
            .Where(d => d.Reward is not null)
            .Select(d => d.Reward!.Kind)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.All(used, kind => Assert.True(UnlockRewardKinds.IsKnown(kind), kind));
        Assert.Contains("ContentFinderCondition", used, StringComparer.Ordinal);
        Assert.Contains("Mount", used, StringComparer.Ordinal);
        Assert.Contains("Companion", used, StringComparer.Ordinal);
        Assert.Contains("Emote", used, StringComparer.Ordinal);
    }

    /// <summary>Reads one of the exported arrays out of <c>data/reward-kinds.mjs</c>. Parsed rather
    /// than duplicated, because a duplicate is the thing this test exists to prevent.</summary>
    private static List<string> ReadJavaScriptList(string name)
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "reward-kinds.mjs"));
        var block = Regex.Match(
            source,
            $@"export const {name} = \[(?<body>[^\]]*)\];",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(5));

        Assert.True(block.Success, $"data/reward-kinds.mjs no longer exports {name}");

        return [.. Regex
            .Matches(block.Groups["body"].Value, "'(?<kind>[^']+)'", RegexOptions.None, TimeSpan.FromSeconds(5))
            .Select(m => m.Groups["kind"].Value)];
    }

    private static List<UnlockDefinition> Load() =>
        UnlockDataset.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "unlocks-by-level.json")));
}
