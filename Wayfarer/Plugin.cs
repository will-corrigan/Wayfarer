using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using KamiToolKit;
using Wayfarer.Core.Guidance;
using Wayfarer.Guidance;
using Wayfarer.Guidance.Coordinators;
using Wayfarer.Guidance.Sources;
using Wayfarer.Modules;
using Wayfarer.Windows;

namespace Wayfarer;

/// <summary>Composition root: the only class allowed to hold Dalamud services or a
/// <see cref="Configuration"/> instance directly. Acquires services, loads config, builds
/// the object graph via constructor injection, registers modules, and disposes
/// everything in exact reverse of construction order.</summary>
public sealed class Plugin : IDalamudPlugin
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commands;
    private readonly ModuleRegistry modules;
    private readonly InputModeService inputMode;
    private readonly ContextMenuActions contextMenuActions;

    /// <summary>The plugin's one entry in Dalamud's server info bar — see its own doc comment for
    /// why it exists. Built from the same <see cref="ReadoutFeed"/> every other surface reads, so
    /// they cannot disagree.</summary>
    private readonly DtrEntry dtrEntry;

    /// <summary>Wayfarer's settings window, which in this build says only that settings are still to
    /// come. Every door that used to open settings opens this.</summary>
    private readonly SettingsWindow settingsWindow;

    /// <summary>The single writer of the game map flag — held here purely so it is unsubscribed
    /// and the player's own flag restored on unload.</summary>
    private readonly MapFlagCoordinator mapFlag;

    private readonly IPluginLog log;

    private readonly IFramework framework;

    /// <summary>KamiToolKit's own start-up, which is asynchronous since upstream reworked it:
    /// it reads its addon-config file and then hops to the framework thread to install the
    /// close-callback hook. Dalamud constructs plugins on the framework thread, so blocking on
    /// this here would wait for a hop onto the thread doing the waiting. Every native surface is
    /// therefore started as a continuation of it instead.</summary>
    private readonly Task kamiToolKitReady;

    /// <summary>What every surface composes its own presentation from, so no two of them can say
    /// different things.</summary>
    private readonly ReadoutFeed feed;

    /// <summary>Every action Wayfarer offers, decided once, so no two menus onto them can offer
    /// different things.</summary>
    private readonly GuidanceActions guidanceActions;

    private bool loggedSettingsFailure;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        IFramework framework,
        IDataManager dataManager,
        IClientState clientState,
        IObjectTable objects,
        ICondition condition,
        ICommandManager commands,
        IGameConfig gameConfig,
        IGamepadState gamepadState,
        IContextMenu contextMenu,
        IDtrBar dtrBar,
        IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commands = commands;
        this.log = log;

        this.framework = framework;

        // Required before any KamiToolKit type (native windows, nodes) is touched. The async part
        // is the addon-config file and the close-callback hook; PluginInterface and the default
        // subtitle are set synchronously before the first await, so constructing nodes and addons
        // below is safe. Opening one is not, until this completes — see SubscribeAndStart.
        kamiToolKitReady = KamiToolKitLibrary.InitializeAsync(pluginInterface, "Wayfarer");

        var config = LoadConfig(pluginInterface, log);
        void SaveConfig() => pluginInterface.SavePluginConfig(config);

        modules = new(log, config);

        inputMode = new InputModeService(gameConfig, gamepadState, config.InputMode, log);

        var guidance = BuildGuidance(log, config, clientState, condition, objects, dataManager);
        mapFlag = guidance.MapFlag;

        // Built here, before anything that reads it: every surface composes its presentation from
        // this one feed.
        feed = new ReadoutFeed(guidance.Navigator, config.QuestHelper, objects);

        settingsWindow = BuildSettingsWindow(framework, log);

        // Every action reads the module registry at the moment a menu opens, so building this
        // before the modules are registered is safe — nothing is resolved now.
        guidanceActions = new GuidanceActions(
            modules, config.QuestHelper, clientState, OpenSettings, log);

        modules.Register(
            new QuestHelperModule(
                framework,
                commands,
                config.QuestHelper,
                SaveConfig,
                guidance.Navigator,
                guidance.Arbiter,
                guidance.QuestSource),
            enabledByDefault: true);

        contextMenuActions = new ContextMenuActions(
            contextMenu, modules, config.QuestHelper, guidanceActions, inputMode, log);

        dtrEntry = BuildDtrEntry(dtrBar, framework, config.QuestHelper);
        dtrEntry.Start();

        SubscribeAndStart(pluginInterface);

        // The version belongs in this line: it is the first question asked of every pasted log,
        // and the plugin list's answer is whatever is installed now, not what was running then.
        log.Information($"Wayfarer {typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "?"} loaded.");
    }

    public void Dispose()
    {
        commands.RemoveHandler("/wayfarer");
        pluginInterface.UiBuilder.Draw -= inputMode.OnFrame;
        pluginInterface.UiBuilder.OpenConfigUi -= OpenSettings;
        pluginInterface.UiBuilder.OpenMainUi -= OpenSettings;

        dtrEntry.Dispose();
        contextMenuActions.Dispose();

        try
        {
            // ModuleRegistry's own Dispose() guards each module individually, and
            // SettingsWindow.Dispose() guards its own main-thread marshalling — but this
            // try/finally is the actual fix for the unload crash + leaked hook: whatever throws or
            // however long disposal takes above, ShutDownKamiToolKit() below is what releases the
            // static FireCallback hook, and it must always run.
            mapFlag.Dispose();
            modules.Dispose();
            settingsWindow.Dispose();
        }
        finally
        {
            ShutDownKamiToolKit();
        }
    }

    /// <summary>Reads the player's config and brings it up to date. The migration is written back
    /// immediately rather than left to ride along with the next setting change: one that only lands
    /// when the player happens to touch something else is one that runs again every session.</summary>
    private static Configuration LoadConfig(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        var config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (config.Migrate())
        {
            pluginInterface.SavePluginConfig(config);
            log.Information($"Wayfarer: configuration migrated to version {Configuration.CurrentVersion}.");
        }

        return config;
    }

    /// <summary>The guidance object graph, built once: one arbiter (the single writer for what the
    /// arrow follows), one router (how to get anywhere), one source per feature (what to guide to
    /// and — crucially — when it is done), the per-frame service, and the adapter the existing
    /// surfaces still talk to. Each module registers its own source when it is enabled.</summary>
    private static GuidanceGraph BuildGuidance(
        IPluginLog log,
        Configuration config,
        IClientState clientState,
        ICondition condition,
        IObjectTable objects,
        IDataManager dataManager)
    {
        var arbiter = new GuidanceArbiter((message, ex) => log.Error(ex, message));
        var router = new GuidanceRouter(dataManager);
        var questSource = new QuestObjectiveSource(dataManager);
        var service = new GuidanceService(
            log, config.QuestHelper, clientState, condition, objects, arbiter, router);
        var navigator = new QuestNavigator(service, questSource);

        // The only writer of the game's single, destructive map flag. Objectives declare that they
        // want to be flagged; this performs it, snapshots the player's own flag first and gives it
        // back on exit.
        var gameFlag = new GameMapFlag(clientState, log);
        var flagCoordinator = new MapFlagCoordinator(
            arbiter,
            () => config.Guidance.MarkObjectiveWithMapFlag,
            gameFlag.Read,
            gameFlag.Set,
            gameFlag.Restore).Start();

        return new GuidanceGraph(arbiter, questSource, navigator, flagCoordinator);
    }

    /// <summary>Factored out of the constructor purely to stay under the method-length
    /// analyzer.</summary>
    private static SettingsWindow BuildSettingsWindow(IFramework framework, IPluginLog log) =>
        new(framework, log)
        {
            InternalName = "WayfarerSettings",
            Title = "Wayfarer",

            // Explicitly empty. KamiToolKit draws a subtitle beside the title and defaults it to the
            // plugin name passed to KamiToolKitLibrary.Initialize, which is what made the title bar
            // read "Wayfarer Wayfarer" — its own guidance is to drop the subtitle when the window's
            // title is already the plugin's name, and here it is.
            Subtitle = string.Empty,
        };

    /// <summary>Releases everything KamiToolKit allocated, on whichever thread Dalamud unloads
    /// on. The synchronous variant asserts the main thread; the asynchronous one hops there
    /// itself, and is bounded because on game exit the framework has stopped ticking and the hop
    /// would never land.</summary>
    private void ShutDownKamiToolKit()
    {
        try
        {
            if (framework.IsInFrameworkUpdateThread)
            {
                KamiToolKitLibrary.Dispose();
                return;
            }

            if (!KamiToolKitLibrary.DisposeAsync().Wait(TimeSpan.FromSeconds(2)))
            {
                log.Warning("Wayfarer: KamiToolKit's shutdown timed out, so some native nodes may be leaked until the game is restarted.");
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Wayfarer: KamiToolKit's shutdown threw, so some native nodes may be leaked until the game is restarted.");
        }
    }

    /// <summary>Factored out of the constructor purely to stay under the method-length analyzer.
    /// A click is the settings equivalent of the Dalamud cog (<see cref="OpenSettings"/>);
    /// shift-click is the same universal exit <c>/wayfarer stop</c> uses.</summary>
    private DtrEntry BuildDtrEntry(IDtrBar dtrBar, IFramework framework, QuestHelperConfig cfg) => new(
        dtrBar,
        feed,
        cfg,
        framework,
        OpenSettings,
        () => modules.Get<QuestHelperModule>()?.Navigator.ClearPickup(),
        log);

    private void SubscribeAndStart(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.UiBuilder.Draw += inputMode.OnFrame;
        pluginInterface.UiBuilder.OpenConfigUi += OpenSettings;
        pluginInterface.UiBuilder.OpenMainUi += OpenSettings;

        // Nothing native is started here any more — the guidance block that draws inside the game's
        // own Main Scenario Guide is not wired up in this build. The continuation stays because a
        // failed KamiToolKit start-up is still worth one line in the log: it is why the settings
        // window will not open.
        _ = kamiToolKitReady.ContinueWith(
            task =>
            {
                if (task.IsFaulted)
                {
                    log.Error(task.Exception, "Wayfarer: KamiToolKit failed to initialise, so no native surface can be shown this session.");
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);

        commands.AddHandler("/wayfarer", new(OnCommand)
        {
            HelpMessage = "Opens Wayfarer's settings. Also: stop.",
        });
    }

    /// <summary>The one door into Wayfarer's own window, taken by Dalamud's cog, the plugin list's
    /// main button, the info-bar entry, the context menu and <c>/wayfarer</c> alike. Never throws:
    /// a window that will not open is logged once and every other entry point keeps working.</summary>
    private void OpenSettings()
    {
        try
        {
            settingsWindow.OpenWindow();
        }
        catch (Exception ex)
        {
            // Once: this is reachable from the plugin list, the cog and every command, and the
            // reason it would not open does not change between attempts.
            if (!loggedSettingsFailure)
            {
                loggedSettingsFailure = true;
                const string message =
                    "Wayfarer: the settings window would not open, so there is no settings surface this "
                    + "session. Guidance itself is unaffected. Reported once.";
                log.Warning(ex, message);
            }
        }
    }

    /// <summary>Settings, and the universal exit. These are convenience aliases: the window is
    /// reachable from the plugin list, the Dalamud cog and the game's own context menu, and nothing
    /// here is the only route to anything.</summary>
    private void OnCommand(string command, string arguments)
    {
        switch (arguments.Trim().ToLowerInvariant())
        {
            case "stop":
                modules.Get<QuestHelperModule>()?.Navigator.ClearPickup();
                break;
            default:
                OpenSettings();
                break;
        }
    }

    /// <summary>What <see cref="BuildGuidance"/> hands back, so its callers can take one parameter
    /// instead of four.</summary>
    private sealed record GuidanceGraph(
        GuidanceArbiter Arbiter,
        QuestObjectiveSource QuestSource,
        QuestNavigator Navigator,
        MapFlagCoordinator MapFlag);
}
