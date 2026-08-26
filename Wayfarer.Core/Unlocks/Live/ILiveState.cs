namespace Wayfarer.Core.Unlocks.Live;

/// <summary>Which id space a duty id belongs to. The <c>Content</c> column of
/// <c>ContentFinderCondition</c> is an untyped reference spanning several sheets, and
/// <c>ContentLinkType</c> is the only thing that says which — pass a public-content id to the
/// instance-content reader and it reads a different duty's bit and answers confidently.</summary>
public enum ContentSpace
{
    Instance,
    Public,
}

/// <summary>Which containers an item count may look in. Retainers, Free Company chests and house
/// storage are not enumerable while closed, so "not found" there is not "you don't have it".</summary>
public enum ItemScope
{
    Any,
    KeyItem,
    Saddlebag,
}

/// <summary>The two public-content directors whose level is readable only while standing in the
/// zone. Same API shape, opposite monotonicity — see <see cref="IMonotonicSource{TId}"/>.</summary>
public enum ZoneProgressKind
{
    EurekaElemental,
    BozjaResistance,
}

/// <summary>How much the client currently knows about which titles the player has earned. Three
/// states, because the list is request-gated and the two unknowns are not the same fact: one is
/// waiting for an answer, the other has never asked for one.
///
/// <para>This never decides a gate. <see cref="IProgressReader.TryTitleUnlocked"/> does that; this
/// only says which sentence to show when it could not — see
/// <c>TitleUnlockedEvaluator</c>. Keeping the decision and the wording apart is
/// what stops a state being read as an answer.</para></summary>
public enum TitleDataState
{
    /// <summary>Nothing has asked, so there is nothing to read. Not "the player has no
    /// titles".</summary>
    NotRequested,

    /// <summary>Asked for, not yet arrived.</summary>
    Pending,

    /// <summary>Arrived. Every title now reads as itself, earned or not.</summary>
    Known,
}

/// <summary>Everything a gate evaluator may read about the player.
///
/// <para><b>The naming rule is the guard.</b> A member that <i>can</i> be unknown is declared
/// <c>bool TryX(out T)</c>. A member that returns a value plainly is a promise that the value is
/// authoritative whenever <see cref="IsReady"/> is true. An evaluator physically cannot ignore an
/// unknown, because there is nothing to ignore — the <c>false</c> branch is the only path to a
/// value.</para></summary>
public interface ILiveState
{
    /// <summary>False whenever a read could return a plausible-but-wrong value: not logged in,
    /// player state not loaded, mid-zone-change. When false the calculator does not run at all,
    /// which removes the whole class of "everything reads as not-owned because we are on the
    /// title screen".</summary>
    bool IsReady { get; }

    ICharacterReader Character { get; }

    IProgressReader Progress { get; }

    IInventoryReader Inventory { get; }

    IContentReader Content { get; }
}

public interface ICharacterReader
{
    /// <summary>The active job's level. Authoritative when <see cref="ILiveState.IsReady"/>.</summary>
    int Level { get; }

    /// <summary>0 when the player has joined none. Authoritative when
    /// <see cref="ILiveState.IsReady"/>.</summary>
    byte GrandCompany { get; }

    /// <summary>Rank within <see cref="GrandCompany"/>. Authoritative when
    /// <see cref="ILiveState.IsReady"/>.</summary>
    int GrandCompanyRank { get; }

    /// <summary>That job's real level, never the level-synced one. Authoritative when
    /// <see cref="ILiveState.IsReady"/>; 0 means the class is not unlocked, which is a real
    /// answer rather than an absent one.</summary>
    int ClassJobLevel(uint classJobId);

    bool TryIsMountUnlocked(uint mountId, out bool owned);

    bool TryIsMinionUnlocked(uint companionId, out bool owned);

    bool TryTribeRank(byte beastTribeId, out byte rank);
}

public interface IProgressReader
{
    /// <summary>Why <see cref="TryTitleUnlocked"/> could not answer, for the sentence shown in its
    /// place. <see cref="TitleDataState.Known"/> whenever it can.</summary>
    TitleDataState TitleData { get; }

    /// <summary>Authoritative when <see cref="ILiveState.IsReady"/>.</summary>
    bool IsQuestComplete(uint questRowId);

    /// <summary>Authoritative when <see cref="ILiveState.IsReady"/>.</summary>
    bool IsQuestAccepted(uint questRowId);

    bool TryAetherCurrentZoneComplete(uint compFlgSetId, out bool complete);

    /// <summary>False until the achievement table has been fetched. The fetch is a server
    /// round-trip and is made once, when the list is first computed for a character — never on a
    /// timer and never retried.</summary>
    bool TryAchievementComplete(uint achievementId, out bool complete);

    /// <summary>Whether the player has earned one <c>Title</c> row. False until something has
    /// caused the client to hold an answer, and there are two different ways for that to be true —
    /// see <see cref="TitleData"/> for which, and for why the distinction is shown rather than
    /// swallowed.
    ///
    /// <para>The dangerous read in the game, and the reason this is a <c>Try</c>: the unlocked-titles
    /// bitfield is all zeroes until the list is requested, so an unguarded read tells a character
    /// with two hundred titles that they have none.</para></summary>
    bool TryTitleUnlocked(uint titleRowId, out bool unlocked);

    /// <summary>Whether the zone's Shared FATE rank is at least <paramref name="rank"/>. False
    /// until the FATE progress tab has arrived, with the same round-trip discipline as
    /// <see cref="TryAchievementComplete"/>.
    ///
    /// <para>The threshold is a parameter rather than the rank being returned, because the answer
    /// may come from a remembered observation rather than a live read, and a remembered value can
    /// prove a requirement met but never prove one unmet — see
    /// <see cref="ObservedFloor{TId}.TryAtLeast"/>.</para></summary>
    bool TrySharedFateRankAtLeast(uint territoryTypeId, int rank, out bool met);

    /// <summary>Whether elemental level or resistance rank is at least <paramref name="rank"/>.
    /// False outside the zone that owns the director. Bozja's rank survives the session through
    /// the observation store; Eureka's elemental level deliberately does not, because it can
    /// decrease. Threshold-shaped for the same reason as
    /// <see cref="TrySharedFateRankAtLeast"/>.</summary>
    bool TryZoneProgressAtLeast(ZoneProgressKind kind, int rank, out bool met);
}

public interface IInventoryReader
{
    /// <summary>False when the scope has no reader at all — today, <see cref="ItemScope.Saddlebag"/>
    /// on a host that did not wire one.
    ///
    /// <para><b>What a true with a count of zero does and does not mean.</b>
    /// <see cref="ItemScope.Any"/> counts the inventories the client can enumerate: the bags, the
    /// armoury, the currency and crystal tabs. It does <i>not</i> reach a retainer, a Free Company
    /// chest or house storage, none of which are enumerable while closed. So a positive count is
    /// proof the player has the item, and a zero is proof only that it is not on them — which for a
    /// tradeable item is a weaker statement than "they do not have it".</para>
    ///
    /// <para>That is deliberate rather than overlooked, and it is a live choice rather than an
    /// abstract one: the six treasure-map entries are gated on maps, and maps are exactly the thing
    /// players keep in a retainer. Answering "we cannot tell" on every zero would turn the common
    /// case — a player who genuinely has not got one — from a requirement they can go and satisfy
    /// into a shrug. Callers that need the distinction must ask for it, not infer it from a
    /// false.</para></summary>
    bool TryCount(uint itemId, ItemScope scope, out int count);
}

public interface IContentReader
{
    bool TryDutyUnlocked(ContentSpace space, uint contentId, out bool unlocked);

    bool TryDutyComplete(ContentSpace space, uint contentId, out bool complete);
}
