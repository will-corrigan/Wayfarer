using System.Text.Json;
using Wayfarer.Api;
using Wayfarer.Api.Dto;
using Wayfarer.Core.Navigation;
using Wayfarer.Core.Unlocks;

namespace Wayfarer.Tests;

public class ApiContractTests
{
    // Replicates FFXIVInventory.Core.SnapshotSerializer.Options exactly - this pins the
    // cross-plugin wire shape both sides of the IPC boundary agree on.
    private static readonly JsonSerializerOptions PluginOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <summary>Every field set to a distinct, recognizable value so the round-trip assertions can
    /// tell a dropped field from a defaulted one.</summary>
    private static NavigationState FullyPopulated =>
        new()
        {
            Mode = NavigationState.Modes.OtherZone,
            QuestId = 1234,
            QuestName = "A Realm Reborn",
            StepLabel = "Speak to Momodi",
            ZoneName = "Ul'dah",
            TargetX = 1.5f,
            TargetY = 2.5f,
            TargetZ = 3.5f,
            DistanceYalms = 42.1f,
            TargetRadiusYalms = 20.5f,
            AetheryteId = 9,
            AetheryteName = "Ul'dah - Steps of Nald",
            AetheryteUnlocked = true,
            AethernetEntryName = "Entry Shard",
            AethernetExitName = "Exit Shard",
            EntranceName = "Zone Entrance",
            EntranceX = 4.5f,
            EntranceZ = 5.5f,
            RemainingYalms = 12.5f,
            IsPickup = true,
            RouteStop = 2,
            RouteTotal = 5,
            Reason = "unlock quest pickup",
            DutyContentFinderConditionId = 456,
            SourceId = "unlocks",
            SourceLabel = "Unlock route",
            Engaged = true,
            ObjectiveKey = "unlocks:65821",
            ProgressText = "2 of 5 targets",
            IsLiveTarget = true,
        };

    [Fact]
    public void NavigationDto_RoundTrips_EveryFieldOfNavigationState()
    {
        var state = FullyPopulated;

        var json = JsonSerializer.Serialize(state, PluginOptions);
        var dto = JsonSerializer.Deserialize<NavigationDto>(json, PluginOptions);

        Assert.NotNull(dto);
        Assert.Equal(state.Mode, dto.Mode);
        Assert.Equal(state.QuestId, dto.QuestId);
        Assert.Equal(state.QuestName, dto.QuestName);
        Assert.Equal(state.StepLabel, dto.StepLabel);
        Assert.Equal(state.ZoneName, dto.ZoneName);
        Assert.Equal(state.TargetX, dto.TargetX);
        Assert.Equal(state.TargetY, dto.TargetY);
        Assert.Equal(state.TargetZ, dto.TargetZ);
        Assert.Equal(state.DistanceYalms, dto.DistanceYalms);
        Assert.Equal(state.TargetRadiusYalms, dto.TargetRadiusYalms);
        Assert.Equal(state.AetheryteId, dto.AetheryteId);
        Assert.Equal(state.AetheryteName, dto.AetheryteName);
        Assert.Equal(state.AetheryteUnlocked, dto.AetheryteUnlocked);
        Assert.Equal(state.AethernetEntryName, dto.AethernetEntryName);
        Assert.Equal(state.AethernetExitName, dto.AethernetExitName);
        Assert.Equal(state.EntranceName, dto.EntranceName);
        Assert.Equal(state.EntranceX, dto.EntranceX);
        Assert.Equal(state.EntranceZ, dto.EntranceZ);
        Assert.Equal(state.RemainingYalms, dto.RemainingYalms);
        Assert.Equal(state.IsPickup, dto.IsPickup);
        Assert.Equal(state.RouteStop, dto.RouteStop);
        Assert.Equal(state.RouteTotal, dto.RouteTotal);
        Assert.Equal(state.Reason, dto.Reason);
        Assert.Equal(state.DutyContentFinderConditionId, dto.DutyContentFinderConditionId);
        Assert.Equal(state.SourceId, dto.SourceId);
        Assert.Equal(state.SourceLabel, dto.SourceLabel);
        Assert.Equal(state.Engaged, dto.Engaged);
        Assert.Equal(state.ObjectiveKey, dto.ObjectiveKey);
        Assert.Equal(state.ProgressText, dto.ProgressText);
        Assert.Equal(state.IsLiveTarget, dto.IsLiveTarget);
    }

    /// <summary>A consumer compiled against the pre-guidance wire shape must keep working: the new
    /// fields are additive, so JSON written without them deserializes to null/false rather than
    /// failing.</summary>
    [Fact]
    public void NavigationDto_Deserializes_LegacyJsonWithoutGuidanceFields()
    {
        const string legacy = """
            {"mode":"otherZone","questId":1234,"questName":"A Realm Reborn","isPickup":true,
             "routeStop":2,"routeTotal":5}
            """;

        var dto = JsonSerializer.Deserialize<NavigationDto>(legacy, PluginOptions);

        Assert.NotNull(dto);
        Assert.Equal("otherZone", dto.Mode);
        Assert.True(dto.IsPickup);
        Assert.Null(dto.SourceId);
        Assert.Null(dto.SourceLabel);
        Assert.False(dto.Engaged);
        Assert.Null(dto.ObjectiveKey);
        Assert.Null(dto.ProgressText);
        Assert.False(dto.IsLiveTarget);
        Assert.Null(dto.TargetRadiusYalms);
    }

    /// <summary>The other half of the additive-change argument: a NEWER provider sending fields
    /// this client has never heard of must not break deserialization.</summary>
    [Fact]
    public void WayfarerClient_Tolerates_UnknownExtraProperties()
    {
        const string json = """{"mode":"idle","somethingFromTheFuture":{"a":1},"anotherOne":"x"}""";
        var client = new WayfarerClient(() => WayfarerIpc.ApiVersion, () => json, (_, _) => "[]");

        var dto = client.GetNavigation();

        Assert.NotNull(dto);
        Assert.Equal("idle", dto.Mode);
    }

    /// <summary>Pins the decision NOT to bump the API version for additive wire fields.
    /// <see cref="WayfarerClient.IsAvailable"/> tests EXACT equality against
    /// <see cref="WayfarerIpc.ApiVersion"/>, so bumping it would make every consumer compiled
    /// against v1 report the whole IPC surface unavailable — a strictly worse outcome than an
    /// older consumer simply ignoring fields it does not know about.</summary>
    [Fact]
    public void ApiVersion_IsUnchanged_ForAdditiveOnlyChanges() => Assert.Equal(1, WayfarerIpc.ApiVersion);

    [Fact]
    public void NavigationDto_RoundTrips_AllNullOptionalFields()
    {
        var state = new NavigationState { Mode = NavigationState.Modes.Idle };

        var json = JsonSerializer.Serialize(state, PluginOptions);
        var dto = JsonSerializer.Deserialize<NavigationDto>(json, PluginOptions);

        Assert.NotNull(dto);
        Assert.Equal("idle", dto.Mode);
        Assert.Null(dto.QuestId);
        Assert.Null(dto.QuestName);
        Assert.False(dto.AetheryteUnlocked);
        Assert.False(dto.IsPickup);
        Assert.Null(dto.RouteStop);
        Assert.Null(dto.RouteTotal);
        Assert.Null(dto.DutyContentFinderConditionId);
        Assert.Null(dto.TargetRadiusYalms);
    }

    [Fact]
    public void IsAvailable_False_WhenVersionMismatches()
    {
        var client = new WayfarerClient(() => WayfarerIpc.ApiVersion + 1, () => "{}", (_, _) => "[]");
        Assert.False(client.IsAvailable);
    }

    [Fact]
    public void IsAvailable_False_WhenApiVersionDelegateThrows()
    {
        var client = new WayfarerClient(
            () => throw new InvalidOperationException("gate not registered"), () => "{}", (_, _) => "[]");
        Assert.False(client.IsAvailable);
    }

    [Fact]
    public void IsAvailable_False_WhenDelegatesAreNull()
    {
        var client = new WayfarerClient(null, null, null);
        Assert.False(client.IsAvailable);
    }

    [Fact]
    public void IsAvailable_True_WhenVersionMatchesAndDelegatesPresent()
    {
        var client = new WayfarerClient(() => WayfarerIpc.ApiVersion, () => "{}", (_, _) => "[]");
        Assert.True(client.IsAvailable);
    }

    [Fact]
    public void GetNavigation_ReturnsNull_OnMalformedJson()
    {
        var client = new WayfarerClient(() => WayfarerIpc.ApiVersion, () => "{not json", (_, _) => "[]");
        Assert.Null(client.GetNavigation());
    }

    [Fact]
    public void GetNavigation_ReturnsNull_WhenDelegateIsNull()
    {
        var client = new WayfarerClient(() => WayfarerIpc.ApiVersion, null, (_, _) => "[]");
        Assert.Null(client.GetNavigation());
    }

    [Fact]
    public void GetNavigation_Deserializes_ValidJson()
    {
        var client = new WayfarerClient(() => WayfarerIpc.ApiVersion, () => """{"mode":"idle"}""", (_, _) => "[]");

        var dto = client.GetNavigation();

        Assert.NotNull(dto);
        Assert.Equal("idle", dto.Mode);
    }

    [Fact]
    public void GetUnlocks_ReturnsEmpty_OnMalformedJson()
    {
        var client = new WayfarerClient(() => WayfarerIpc.ApiVersion, () => "{}", (_, _) => "not json");
        Assert.Empty(client.GetUnlocks());
    }

    [Fact]
    public void GetUnlocks_ReturnsEmpty_WhenDelegateIsNull()
    {
        var client = new WayfarerClient(() => WayfarerIpc.ApiVersion, () => "{}", null);
        Assert.Empty(client.GetUnlocks());
    }

    [Fact]
    public void GetUnlocks_Deserializes_ValidJson()
    {
        const string json = """
            [{"unlock":"Glamours","status":"Available","lockReason":null,"quest":"A Self-improving Man",
              "giver":"Mahenne","level":15,"zone":"Ul'dah","priority":"essential","category":"system",
              "description":"Change how gear looks."}]
            """;
        var client = new WayfarerClient(() => WayfarerIpc.ApiVersion, () => "{}", (_, _) => json);

        var rows = client.GetUnlocks();

        var row = Assert.Single(rows);
        Assert.Equal("Glamours", row.Unlock);
        Assert.Equal("Available", row.Status);
        Assert.Null(row.LockReason);
        Assert.Equal("A Self-improving Man", row.Quest);
        Assert.Equal("Mahenne", row.Giver);
        Assert.Equal(15, row.Level);
        Assert.Equal("Ul'dah", row.Zone);
        Assert.Equal("essential", row.Priority);
        Assert.Equal("system", row.Category);
        Assert.Equal("Change how gear looks.", row.Description);
    }

    // UnlockRowDto.Status is a plain string, so a new status is an additive change on the wire —
    // but only if the names consumers will start seeing are actually the ones the enum produces.
    [Theory]
    [InlineData(UnlockStatus.CollectionLocked, "CollectionLocked")]
    [InlineData(UnlockStatus.RequirementsUnknown, "RequirementsUnknown")]
    public void GetUnlocks_CarriesTheNewStatuses_AsPlainStrings(UnlockStatus status, string wireValue)
    {
        Assert.Equal(wireValue, status.ToString());
        var json = $$"""
            [{"unlock":"Firebird (Mount)","status":"{{wireValue}}",
              "lockReason":"requires 7 more of all seven Heavensward Extreme-trial Lanner mounts; next: Rose Lanner — Thok ast Thok (Extreme)",
              "quest":"Fiery Wings, Fiery Hearts","giver":null,"level":50,"zone":"Idyllshire",
              "priority":"optional","category":"cosmetic","description":"A collectible mount."}]
            """;
        var client = new WayfarerClient(() => WayfarerIpc.ApiVersion, () => "{}", (_, _) => json);

        var row = Assert.Single(client.GetUnlocks());

        Assert.Equal(wireValue, row.Status);
        Assert.Contains("Rose Lanner", row.LockReason, StringComparison.Ordinal);
    }

    [Fact]
    public void GetUnlocks_Deserializes_NullGiver()
    {
        const string json = """
            [{"unlock":"Chocobo Issuance","status":"Available","lockReason":null,"quest":"My Feisty Little Chocobo",
              "giver":null,"level":20,"zone":null,"priority":"essential","category":"system",
              "description":null}]
            """;
        var client = new WayfarerClient(() => WayfarerIpc.ApiVersion, () => "{}", (_, _) => json);

        var rows = client.GetUnlocks();

        var row = Assert.Single(rows);
        Assert.Null(row.Giver);
    }

    [Fact]
    public void GetUnlocks_PassesScopeAndMaxLevel_ToDelegate()
    {
        string? capturedScope = null;
        var capturedMaxLevel = -1;
        var client = new WayfarerClient(
            () => WayfarerIpc.ApiVersion,
            () => "{}",
            (scope, maxLevel) =>
            {
                capturedScope = scope;
                capturedMaxLevel = maxLevel;
                return "[]";
            });

        client.GetUnlocks("here", 50);

        Assert.Equal("here", capturedScope);
        Assert.Equal(50, capturedMaxLevel);
    }
}
