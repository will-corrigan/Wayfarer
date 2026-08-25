using Wayfarer.Core.Navigation;
using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>The worst readout the composer can actually emit, and the ordinary one, in one place so
/// every geometry proof about the readout is run against the same fixtures rather than against a
/// plausible invention.
///
/// <para>Each field of the hostile snapshot is a specific thing a field report was about: four digits
/// of distance with an elevation suffix on the same line, the longest objective sentence a quest step
/// produces, three nearby-unlock sub-lines at once (the composer's own cap), a hunting summary
/// underneath all of it, and travel advice on top. Together they are the deepest readout that exists —
/// every optional line present at the same time — which is the arrangement the old cursor could not
/// survive.</para></summary>
internal static class HostileReadout
{
    /// <summary>The longest objective a quest step produces. Long enough to wrap past the readout's
    /// 250-pixel text column at every scale, which is what made every line under it move.</summary>
    public const string LongestObjective =
        "Speak with the Brass Blade sergeant at the Gate of Nald in Ul'dah, then return to the "
        + "Quicksand and report what you have learned to Momodi before the caravan departs";

    /// <summary>A name long enough that the plate cuts it short. The readout truncates the name rather
    /// than wrapping it — deliberately, so the whole readout does not reflow every time the quest
    /// changes — so this is the fixture that proves the banner's height does not depend on it.</summary>
    public const string TwoLineName = "The Ceremony of Eternal Bonding at the Sanctum of the Twelve";

    /// <summary>Four digits of distance. <c>NavMath.FormatDistance</c> groups it, and the elevation
    /// suffix goes on the same line, so this is the longest the distance line ever gets.</summary>
    public const float FourDigitDistance = 1240f;

    /// <summary>The composer's own cap of nearby unlocks, each with a medallion in the gutter.
    /// </summary>
    public static readonly string[] ThreeNearbyUnlocks =
    [
        "Chocobo Companion",
        "The Fractal Continuum (Hard)",
        "Ceremony of Eternal Bonding",
    ];

    /// <summary>Everything at once, in another zone, through an aethernet shard, with a teleport the
    /// player is attuned to and a hunt running underneath.</summary>
    public static ReadoutInputs Inputs => new()
    {
        State = new NavigationState
        {
            Mode = NavigationState.Modes.OtherZone,
            SourceLabel = "Main Scenario",
            SourceName = "quest",
            QuestName = TwoLineName,
            QuestId = 1234,
            StepLabel = LongestObjective,
            ZoneName = "The Churning Mists",
            EntranceName = "the Gates of Judgement",
            EntranceX = 12f,
            EntranceZ = -40f,
            AetheryteName = "Foundation",

            // With the id, so the teleport line still carries its action mark — the worst case for the
            // layout is the one where the line is a control, since that is when a hit box and a
            // controller anchor are placed on it.
            AetheryteId = 70,
            AetheryteUnlocked = true,
            AethernetEntryName = "Foundation",
            AethernetExitName = "The Forgotten Knight",
            RouteStop = 3,
            RouteTotal = 11,
        },
        DistanceYalms = FourDigitDistance,
        Elevation = ElevationHint.Above,
        HuntingSummary = "Ornery Karakul 2/3",
        NearbyUnlocks = ThreeNearbyUnlocks,
    };

    /// <summary>A quest and how far away it is, which is what the readout looks like almost all of the
    /// time.</summary>
    public static ReadoutInputs PlainInputs => new()
    {
        State = new NavigationState
        {
            Mode = NavigationState.Modes.SameZone,
            SourceLabel = "Main Scenario",
            SourceName = "quest",
            QuestName = "The Ul'dahn Envoy",
            QuestId = 66,
            TargetX = 12f,
            TargetZ = -40f,
        },
        DistanceYalms = 56f,
    };
}
