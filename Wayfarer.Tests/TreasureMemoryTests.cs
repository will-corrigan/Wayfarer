using System.Numerics;
using Wayfarer.App.Config;
using Wayfarer.Modules.Treasure;

namespace Wayfarer.Tests;

/// <summary>Remembering the metal each treasure spot has been seen holding, and following it if it
/// ever changes.</summary>
public class TreasureMemoryTests
{
    private static readonly TreasureSpot Spot = new(1252, 967, new Vector3(771.4f, 108f, -144.2f));

    [Fact]
    public void A_spot_never_seen_has_no_metal()
    {
        Assert.Null(new TreasureMemory(new Store()).At(Spot));
    }

    [Fact]
    public void A_metal_seen_is_remembered_across_sessions()
    {
        var store = new Store();
        new TreasureMemory(store).Saw(Spot, TreasureTier.Silver);

        Assert.Equal(TreasureTier.Silver, new TreasureMemory(store).At(Spot));
    }

    [Fact]
    public void A_spot_seen_holding_another_metal_is_remembered_as_the_newest()
    {
        var store = new Store();
        var memory = new TreasureMemory(store);
        memory.Saw(Spot, TreasureTier.Silver);
        var was = memory.Saw(Spot, TreasureTier.Bronze);

        Assert.Equal(TreasureTier.Silver, was);
        Assert.Equal(TreasureTier.Bronze, new TreasureMemory(store).At(Spot));
    }

    [Fact]
    public void A_metal_not_known_is_no_sighting()
    {
        var store = new Store();
        var memory = new TreasureMemory(store);
        memory.Saw(Spot, TreasureTier.Silver);
        memory.Saw(Spot, TreasureTier.Unknown);

        Assert.Equal(TreasureTier.Silver, memory.At(Spot));
    }

    [Fact]
    public void Seeing_the_same_metal_again_does_not_save_again()
    {
        var store = new Store();
        var memory = new TreasureMemory(store);
        memory.Saw(Spot, TreasureTier.Silver);
        memory.Saw(Spot, TreasureTier.Silver);

        Assert.Equal(1, store.Saves);
    }

    [Fact]
    public void A_spot_is_named_by_its_zone_and_its_ground_position_to_the_yalm()
    {
        Assert.Equal("1252:771:-144", TreasureMemory.KeyOf(Spot));
        Assert.Equal(TreasureMemory.KeyOf(Spot), TreasureMemory.KeyOf(Spot with { Position = new Vector3(771.2f, 90f, -143.9f) }));
    }

    /// <summary>Configs kept in memory, as JSON, so what is saved is what a later load reads.</summary>
    private sealed class Store : IConfigStore
    {
        private readonly Dictionary<string, string> files = [];

        public int Saves { get; private set; }

        public T Load<T>(string name)
            where T : class, new() =>
            files.TryGetValue(name, out var json) ? System.Text.Json.JsonSerializer.Deserialize<T>(json) ?? new T() : new T();

        public void Save<T>(string name, T value)
            where T : class
        {
            Saves++;
            files[name] = System.Text.Json.JsonSerializer.Serialize(value);
        }
    }
}
