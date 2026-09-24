using System.Globalization;
using System.Text;
using Dalamud.Plugin.Services;

namespace Wayfarer.Modules.Hunting;

/// <summary>Writes down what a followed hunt was made of, for whoever reads the log after trying
/// it: what the sheets gave for each monster and where each one was placed. Nothing a player
/// needs, so it is said once, when the hunt is followed.</summary>
internal static class HuntLog
{
    /// <summary>Notes a hunt just followed and everything known about it.</summary>
    public static void Followed(IPluginLog log, Hunt hunt, HuntFacts? facts)
    {
        ArgumentNullException.ThrowIfNull(log);
        if (facts is null)
        {
            log.Warning($"hunting: followed {hunt}, but the sheets say nothing about it.");
            return;
        }

        var lines = new StringBuilder().Append(CultureInfo.InvariantCulture, $"hunting: followed {hunt}, \"{facts.Headline}\" ({facts.Kind}), {facts.Quarries.Count} monsters:");
        foreach (var quarry in facts.Quarries)
        {
            var where = quarry.Duty is { } duty ? $"inside duty {duty}"
                : quarry.Places.Count == 0 ? "NOWHERE"
                : string.Join(", ", quarry.Places.Select(place => string.Create(CultureInfo.InvariantCulture, $"({place.Territory}/{place.Map} {place.X:F0},{place.Z:F0})")));
            var fate = quarry.Fate is { } during ? $", FATE {during.Id} {during.Name}" : string.Empty;
            lines.Append(CultureInfo.InvariantCulture, $"\n  [{quarry.Entry}.{quarry.Target}] {quarry.Name} (name {quarry.NameId}) x{quarry.Need} at {where}{fate}");
        }

        log.Information(lines.ToString());
    }
}
