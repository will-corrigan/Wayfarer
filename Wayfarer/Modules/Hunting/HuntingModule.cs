using Dalamud.Plugin.Services;
using Wayfarer.App;
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
        await framework.OffTheGameThread(reader.Warm).ConfigureAwait(false);

        if (following.FromLog)
        {
            await framework.OnTheGameThread(logButton.Start).ConfigureAwait(false);
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

        await framework.OnTheGameThread(Resume).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task StopAsync()
    {
        await framework.OnTheGameThread(() => guidance.Yield(objectives)).ConfigureAwait(false);
        await billButtons.StopAsync().ConfigureAwait(false);
        await logButton.StopAsync().ConfigureAwait(false);
    }

    /// <summary>Takes up again a hunt this character was following: ahead of the quest when the
    /// hunt is what they were last guided to, and behind it otherwise. With nowhere left to follow
    /// from, lets go. Who is following what is the character's, so this is game thread only.</summary>
    private void Resume()
    {
        if (!following.FromLog && !following.FromBills)
        {
            guidance.Yield(objectives);
        }
        else if (following.Followed is not null)
        {
            guidance.Resume(objectives);
        }
    }
}
