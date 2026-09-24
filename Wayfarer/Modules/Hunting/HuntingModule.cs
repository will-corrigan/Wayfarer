using Dalamud.Plugin.Services;
using Wayfarer.App.Modules;
using Wayfarer.Guidance;

namespace Wayfarer.Modules.Hunting;

/// <summary>The hunting module: follow a page of a hunting log or a mark bill, and be guided to its
/// monsters one at a time, in order. Each place to follow from is a switch of its own, and the
/// module is whatever those switches add up to.
///
/// <para>A hunt the player chose takes guidance over from the quest being followed, and hands it
/// back when it ends.</para></summary>
internal sealed class HuntingModule(
    HuntObjectives objectives,
    HuntFollowing following,
    HuntReader reader,
    HuntingLogButton logButton,
    MarkBillButtons billButtons,
    IFramework framework,
    IGuidance guidance) : IModule
{
    /// <summary>What the module is called, everywhere: the checkbox, the guidance it publishes, and
    /// the enabled set saved in <c>app.json</c>, which is keyed by this.</summary>
    public const string ModuleName = "Hunting";

    /// <summary>The game's own picture for the Hunting Log, as its main menu shows it.</summary>
    private const uint HuntingLogIcon = 21;

    private const string LogName = "Follow hunting log pages";
    private const string LogDescription = "Puts a Follow button on the Hunting Log's page, which guides to that page's monsters one at a time, in order.";
    private const string BillsName = "Follow mark bills";
    private const string BillsDescription = "Puts a Follow button beside Close on a mark bill you hold, which guides to its marks one at a time, in order.";

    /// <inheritdoc/>
    public string Name => ModuleName;

    /// <inheritdoc/>
    public uint Icon => HuntingLogIcon;

    /// <inheritdoc/>
    public string Description => "Follows a hunting log page or a mark bill you choose, one monster at a time.";

    /// <inheritdoc/>
    public IReadOnlyList<ModuleSetting> Settings =>
    [
        new ModuleSetting(LogName, LogDescription, () => following.FromLog, on => following.FromLog = on),
        new ModuleSetting(BillsName, BillsDescription, () => following.FromBills, on => following.FromBills = on),
    ];

    /// <inheritdoc/>
    public async Task ApplyAsync()
    {
        await OffTheGameThread(reader.Warm).ConfigureAwait(false);

        if (following.FromLog)
        {
            await logButton.StartAsync().ConfigureAwait(false);
        }
        else
        {
            await logButton.StopAsync().ConfigureAwait(false);
        }

        if (following.FromBills)
        {
            await billButtons.StartAsync().ConfigureAwait(false);
        }
        else
        {
            await billButtons.StopAsync().ConfigureAwait(false);
        }

        // A hunt followed before the plugin was last unloaded is taken up again. Who is following
        // what is the character's, so it is asked on the game's thread.
        var followed = await framework.RunOnFrameworkThread(() => following.Followed).ConfigureAwait(false);
        if (followed is not null && (following.FromLog || following.FromBills))
        {
            guidance.Claim(objectives);
        }
        else if (!following.FromLog && !following.FromBills)
        {
            guidance.Yield(objectives);
        }
    }

    /// <inheritdoc/>
    public async Task StopAsync()
    {
        guidance.Yield(objectives);
        await billButtons.StopAsync().ConfigureAwait(false);
        await logButton.StopAsync().ConfigureAwait(false);
    }

    /// <summary>Runs sheet reading where it cannot cost a frame: here when already off the game's
    /// thread, on the pool when on it.</summary>
    private Task OffTheGameThread(Action read)
    {
        if (framework.IsInFrameworkUpdateThread)
        {
            return Task.Run(read);
        }

        read();
        return Task.CompletedTask;
    }
}
