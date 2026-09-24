using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using Wayfarer.App;
using Wayfarer.Guidance;

namespace Wayfarer.Modules.Hunting;

/// <summary>The Follow buttons on every expansion's mark bill window, switched on and off together.
///
/// <para>Each expansion has a bill window of its own: <c>Mobhunt</c> for A Realm Reborn and
/// <c>Mobhunt2</c> onwards for each expansion after it. Which of the game's twenty-two kinds of
/// bill each one shows is read from the sheets, where each kind of bill names the quest that opens
/// it and that quest names its expansion; the Realm Reborn bills name no quest.</para></summary>
internal sealed class MarkBillButtons : IAsyncDisposable
{
    private const string FirstWindow = "Mobhunt";

    private readonly IFramework framework;
    private readonly List<MarkBillButton> buttons = [];

    public MarkBillButtons(IDataManager data, HuntFollowing following, HuntReader reader, HuntObjectives objectives, IGuidance guidance, IFramework framework, IPluginLog log)
    {
        this.framework = framework;
        var byExpansion = data.GetExcelSheet<MobHuntOrderType>()
            .GroupBy(kind => kind.Quest.ValueNullable?.Expansion.RowId ?? 0u)
            .OrderBy(group => group.Key);

        foreach (var expansion in byExpansion)
        {
            var window = expansion.Key == 0 ? FirstWindow : FirstWindow + (expansion.Key + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            buttons.Add(new MarkBillButton(window, [.. expansion.Select(kind => (byte)kind.RowId)], following, reader, objectives, guidance, log));
        }
    }

    /// <summary>Starts watching every bill window, on the game's thread.</summary>
    public Task StartAsync() => framework.OnTheGameThread(() =>
    {
        foreach (var button in buttons)
        {
            button.Start();
        }
    });

    /// <summary>Stops watching every bill window and frees any button on show.</summary>
    public async Task StopAsync()
    {
        foreach (var button in buttons)
        {
            await button.StopAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
