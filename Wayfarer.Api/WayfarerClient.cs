using System.Text.Json;
using Wayfarer.Api.Dto;

namespace Wayfarer.Api;

/// <summary>Typed façade over Wayfarer's Dalamud IPC gates. Pure by design: the consumer
/// builds the three delegates from its own ICallGateSubscriber instances (a few lines on
/// their side) and hands them to this constructor, so this project never references
/// Dalamud and stays unit-testable. Never throws - version mismatches, missing gates and
/// malformed JSON all degrade to false/null/empty rather than propagating exceptions.</summary>
public sealed class WayfarerClient(
    Func<int>? apiVersion,
    Func<string>? navigationJson,
    Func<string, int, string>? unlocksJson)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public bool IsAvailable
    {
        get
        {
            if (apiVersion is null || navigationJson is null || unlocksJson is null)
            {
                return false;
            }

            try
            {
                return apiVersion() == WayfarerIpc.ApiVersion;
            }
            catch
            {
                return false;
            }
        }
    }

    public NavigationDto? GetNavigation()
    {
        if (navigationJson is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<NavigationDto>(navigationJson(), Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public List<UnlockRowDto> GetUnlocks(string scope = "available", int maxLevel = 0)
    {
        if (unlocksJson is null)
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<UnlockRowDto>>(unlocksJson(scope, maxLevel), Options) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
