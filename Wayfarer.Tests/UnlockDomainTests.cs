using Wayfarer.Core.Unlocks;

namespace Wayfarer.Tests;

/// <summary>The seven-way split, and the one property it has to have: <b>total and disjoint</b>.
/// Every channel lands in exactly one domain, and a channel added later fails a test rather than
/// silently vanishing into a default bucket.
///
/// <para><b>The failure mode this exists for.</b> The four category chips read off <c>type</c> and
/// ended with <c>_ =&gt; d.Cosmetic ? "cosmetic" : "system"</c>. That default is what put 158 titles,
/// 53 orchestrion rolls and every emote on the Cosmetics chip and buried 235 game features inside
/// it — and nothing failed, because a default bucket cannot fail. It just quietly answered. So the
/// mapping has no default, and these are the assertions that say the absence of one is safe.</para>
///
/// <para>Loads the real shipped <c>data/unlocks-by-level.json</c>, like
/// <see cref="UnlockChannelTests"/> and for the same reason: the imported half of the catalogue is a
/// function of the installed game data, so a claim about it is worth making against the file rather
/// than against a fixture that would agree with itself.</para></summary>
public class UnlockDomainTests
{
    /// <summary>Every value <c>channel</c> may hold, kept in step with <c>ENTRY_CHANNELS</c> in
    /// <c>data/unlock-channels.mjs</c> — the same closed set <see cref="UnlockChannelTests"/> writes
    /// out, and written out here for the same reason: the point of a closed set is that adding to it
    /// is a decision somebody makes twice.
    ///
    /// <para>Independent of <see cref="UnlockDomains"/> on purpose. Derived from it, every assertion
    /// below would be a tautology — the union of the domains' channels equals the domains' channels —
    /// and the one thing worth catching, a channel that exists and has no domain, would be
    /// uncatchable.</para></summary>
    private static readonly string[] ClosedChannelSet =
    [
        "aether-current", "allied-society", "barding", "challenge-log", "chocobo-companion",
        "crafting-log-division", "custom-delivery", "duty", "emote", "facewear",
        "fashion-accessory", "framers-kit", "gathering-folklore", "general-action",
        "grand-company-rank", "hairstyle", "hunt-board", "job", "minion", "mount", "orchestrion",
        "stone-sky-sea", "system", "title", "triple-triad-card", "variant-dungeon", "zone",
    ];

    private static readonly string[] ExpectedDomains =
    [
        UnlockDomains.Duties, UnlockDomains.Capabilities, UnlockDomains.Collection,
        UnlockDomains.Titles, UnlockDomains.Logs, UnlockDomains.Jobs, UnlockDomains.Travel,
    ];

    /// <summary><b>Total.</b> Every channel the enumeration allows has a domain — including the five
    /// the catalogue has no entries under yet, because "no entries yet" is when a channel is cheapest
    /// to place and the worst time to discover it has nowhere to go.</summary>
    [Fact]
    public void EveryChannelTheEnumerationAllowsHasADomain()
    {
        var missing = ClosedChannelSet.Where(c => UnlockDomains.Of(c) is null).ToList();

        var message =
            $"{missing.Count} channel(s) map to no domain: {string.Join(", ", missing)}. Add each to "
            + "the table in UnlockDomains — the rows would otherwise be drawn under 'Unclassified', "
            + "which is the state the seven domains exist to have ended.";

        Assert.True(missing.Count == 0, message);
    }

    /// <summary><b>Nothing extra.</b> The other direction: a domain claiming a channel the
    /// enumeration does not produce is a chip that can never match anything, and it would look
    /// exactly like a working one.</summary>
    [Fact]
    public void NoDomainClaimsAChannelTheEnumerationDoesNotProduce()
    {
        var closed = new HashSet<string>(ClosedChannelSet, StringComparer.Ordinal);
        var invented = UnlockDomains.MappedChannels.Where(c => !closed.Contains(c)).ToList();

        var message = $"UnlockDomains claims {invented.Count} channel(s) that are not channels: "
            + $"{string.Join(", ", invented)}.";

        Assert.True(invented.Count == 0, message);
    }

    /// <summary><b>Disjoint.</b> No channel is claimed by two domains. Without this a channel's
    /// entries would appear under whichever domain the table listed first and be missing from the
    /// other, and both would still draw and both would still count.</summary>
    [Fact]
    public void NoChannelBelongsToTwoDomains()
    {
        var message = $"claimed by more than one domain: {string.Join(", ", UnlockDomains.Conflicts)}.";
        Assert.True(UnlockDomains.Conflicts.Count == 0, message);

        // And the counts agree, which is what catches a channel dropped from the table rather than
        // duplicated in it.
        var listed = UnlockDomains.All.Sum(d => UnlockDomains.ChannelsOf(d).Count);
        Assert.Equal(UnlockDomains.MappedChannels.Count, listed);
    }

    /// <summary>The seven, named, in order. Pinned because the order is what a player learns —
    /// Capabilities being second is the point of the redesign, and a table reordered by accident
    /// would move it without anything noticing.</summary>
    [Fact]
    public void TheSevenDomainsAreTheSevenDomains()
    {
        Assert.Equal(ExpectedDomains, UnlockDomains.All, StringComparer.Ordinal);
    }

    /// <summary><c>system</c> presents as <b>Capabilities</b> and the channel string stays
    /// <c>system</c>. Both halves asserted: the rename is presentation, and the catalogue's own
    /// identities are pinned by <c>data/validate-catalogue-identity.mjs</c> and must not move.</summary>
    [Fact]
    public void SystemPresentsAsCapabilitiesAndTheChannelKeyStays()
    {
        Assert.Equal("Capabilities", UnlockDomains.Label(UnlockDomains.Of("system")!));
        Assert.Contains("system", UnlockDomains.ChannelsOf(UnlockDomains.Capabilities), StringComparer.Ordinal);

        // Nothing in the data was renamed: the catalogue still says "system" on all of them, and the
        // Capabilities domain is exactly that set.
        var all = Load();
        var inDomain = all.Count(d => string.Equals(UnlockDomains.Of(d), UnlockDomains.Capabilities, StringComparison.Ordinal));
        var byChannel = all.Count(d => string.Equals(d.Channel, "system", StringComparison.Ordinal));
        Assert.Equal(byChannel, inDomain);
    }

    /// <summary><b>Total over the real file, too.</b> The closed-set test catches a channel added to
    /// the policy; this catches one that turns up in the shipped catalogue — which is the order those
    /// two things actually happen in, since the catalogue is regenerated from the game's sheets.</summary>
    [Fact]
    public void EveryShippedEntryLandsInExactlyOneDomain()
    {
        var stray = Load().Where(d => UnlockDomains.Of(d) is null).ToList();

        var message = $"{stray.Count} shipped entries have no domain, e.g. "
            + $"'{stray.FirstOrDefault()?.Unlock}' (channel '{stray.FirstOrDefault()?.Channel}').";

        Assert.True(stray.Count == 0, message);
    }

    /// <summary>The split accounts for the whole catalogue: the seven per-domain counts sum to the
    /// number of entries, with nothing double-counted and nothing dropped.
    ///
    /// <para>Summed against the file's own length rather than against the literal 1,208. The literal
    /// is pinned once, by <see cref="UnlockCatalogueShapeTests"/>; pinning it again here would make a
    /// legitimate regeneration fail a test that says nothing about whether the split is total.</para>
    /// </summary>
    [Fact]
    public void ThePerDomainCountsSumToTheWholeCatalogue()
    {
        var all = Load();
        var byDomain = UnlockDomains.All.ToDictionary(
            d => d,
            d => all.Count(e => string.Equals(UnlockDomains.Of(e), d, StringComparison.Ordinal)),
            StringComparer.Ordinal);

        Assert.Equal(all.Count, byDomain.Values.Sum());

        // Duties is the largest and Capabilities the runner-up: the shape the domains were chosen to
        // expose, and the reason Capabilities is not allowed to be a footnote inside Cosmetics.
        Assert.True(byDomain[UnlockDomains.Duties] > byDomain[UnlockDomains.Capabilities]);
        Assert.True(byDomain[UnlockDomains.Capabilities] > byDomain[UnlockDomains.Titles]);

        // A domain with no entries at all is a chip that does nothing.
        Assert.All(byDomain, kv => Assert.True(kv.Value > 0, $"{kv.Key} has no entries"));
    }

    /// <summary>The domain is read off <c>channel</c>, not <c>type</c>. The entries typed
    /// <c>system</c> span several domains — which is the reason the channel field was added, stated as
    /// an assertion over the real file rather than as a comment.</summary>
    [Fact]
    public void TypeCannotAnswerWhatTheChannelAnswers()
    {
        var systemTyped = Load()
            .Where(d => string.Equals(d.Type, "system", StringComparison.Ordinal))
            .ToList();

        var domains = systemTyped
            .Select(UnlockDomains.Of)
            .Where(d => d is not null)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(systemTyped.Count > 300, $"only {systemTyped.Count} entries are typed 'system'");

        var message =
            $"the {systemTyped.Count} entries typed 'system' span only {domains.Count} domains, so the "
            + "split is not buying anything the type did not already say";

        Assert.True(domains.Count >= 4, message);
    }

    private static List<UnlockDefinition> Load() =>
        UnlockDataset.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "unlocks-by-level.json")));
}
