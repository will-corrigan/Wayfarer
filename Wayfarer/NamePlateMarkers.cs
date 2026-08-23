using Dalamud.Game.Gui.NamePlate;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;
using Wayfarer.Modules;

namespace Wayfarer;

/// <summary>Puts the game's own quest-marker icon over the heads of hunting-log targets and unlock
/// quest givers, so the player can see what to go for without reading a list.
///
/// <b>Why <c>OnDataUpdate</c> and not <c>OnNamePlateUpdate</c>.</b> Dalamud documents
/// <c>MarkerIconId</c> as "read from and reset by the game every frame, not just when a nameplate
/// changes", and <c>OnNamePlateUpdate</c> as firing only when at least one plate has important
/// updates, with a handler list containing only those plates. A value the game re-zeroes every
/// frame has to be written every frame for every plate, so <c>OnDataUpdate</c> — which fires every
/// frame with all of them — is the only subscription that works. The other one produces a marker
/// that flickers.
///
/// <b>Why it never overwrites a non-zero marker.</b> That field is not a quest-only channel: it
/// also carries party target markers, hunt marks and the game's own quest icons. Writing it
/// unconditionally erases whichever of those the game had put there — plausibly a party target
/// marker mid-fight. Writing only into an empty slot also happens to be the right behaviour
/// anyway, since a mob the game has already marked does not need marking twice.
///
/// <b>Fail-safes, because nobody can watch this.</b> It is off by default; the icon id is a
/// setting rather than a constant and is checked against the game's own texture table before it is
/// ever written, so a bad id means "no marker" rather than a broken nameplate; the match set is
/// rebuilt on a timer and the per-frame path is a set lookup and nothing else; and the handler
/// unsubscribes itself permanently on its first exception, because a feature that turns itself off
/// is safe and one that throws sixty times a second in the render path is not.</summary>
internal sealed class NamePlateMarkers(
    INamePlateGui namePlates,
    ITextureProvider textures,
    IFramework framework,
    ModuleRegistry modules,
    GuidanceConfig cfg,
    IPluginLog log) : IDisposable
{
    private const double RefreshSeconds = 1.0;

    // Case-insensitive on purpose. The match set is built from data-sheet text and compared against
    // the game's own live display name, and the two do not agree on case — the BNpcName sheet stores
    // "dragonfly" where the nameplate reads "Dragonfly". Comparing ordinally meant the enemy set
    // matched nothing at all.
    private readonly HashSet<string> enemyNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> giverNames = new(StringComparer.OrdinalIgnoreCase);

    private DateTimeOffset lastRefresh = DateTimeOffset.MinValue;
    private int validatedIcon;
    private int validatedFor = -1;
    private bool subscribed;
    private bool disabledByFailure;

    public void Start()
    {
        framework.Update += OnFrameworkUpdate;
        Subscribe();
    }

    public void Dispose()
    {
        framework.Update -= OnFrameworkUpdate;
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (subscribed || disabledByFailure)
        {
            return;
        }

        namePlates.OnDataUpdate += OnDataUpdate;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
        {
            return;
        }

        namePlates.OnDataUpdate -= OnDataUpdate;
        subscribed = false;

        // Nothing to clean up: the game re-zeroes the field every frame, so no longer writing it
        // is the whole teardown. A redraw just makes it immediate.
        namePlates.RequestRedraw();
    }

    private void OnFrameworkUpdate(IFramework fw)
    {
        if (disabledByFailure)
        {
            return;
        }

        if (!cfg.MarkTargetsOnNameplates)
        {
            Unsubscribe();
            enemyNames.Clear();
            giverNames.Clear();
            return;
        }

        Subscribe();

        var now = DateTimeOffset.UtcNow;
        if ((now - lastRefresh).TotalSeconds < RefreshSeconds)
        {
            return;
        }

        lastRefresh = now;
        RefreshMatchSets();
    }

    private void RefreshMatchSets()
    {
        enemyNames.Clear();
        giverNames.Clear();

        if (modules.Get<HuntingLogModule>() is { Enabled: true } huntingModule)
        {
            foreach (var target in huntingModule.Hunting.HuntHereOrder)
            {
                enemyNames.Add(target.MonsterName);
            }
        }

        if (modules.Get<UnlockChecklistModule>() is { Enabled: true } unlockModule)
        {
            foreach (var unlock in unlockModule.Unlocks.GlanceableHere)
            {
                if (unlock.GiverName is { Length: > 0 } giver)
                {
                    giverNames.Add(giver);
                }
            }
        }
    }

    /// <summary>Resolves the configured icon against the game's own texture table, once per change.
    /// This is the highest-value guard here: it turns "does this id exist?" — which otherwise could
    /// only be answered by someone looking at a screen — into something the player's own machine
    /// answers at runtime, before the id is ever written to a nameplate.</summary>
    private int ResolveIcon()
    {
        if (validatedFor == cfg.NamePlateMarkerIcon)
        {
            return validatedIcon;
        }

        validatedFor = cfg.NamePlateMarkerIcon;
        validatedIcon = 0;

        if (cfg.NamePlateMarkerIcon <= 0)
        {
            return 0;
        }

        try
        {
            var lookup = new GameIconLookup((uint)cfg.NamePlateMarkerIcon);
            if (textures.TryGetFromGameIcon(lookup, out var texture) && texture.TryGetWrap(out _, out _))
            {
                validatedIcon = cfg.NamePlateMarkerIcon;
            }
            else
            {
                log.Warning($"Wayfarer markers: icon {cfg.NamePlateMarkerIcon} does not resolve, so nothing will be marked.");
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, $"Wayfarer markers: icon {cfg.NamePlateMarkerIcon} could not be checked, so nothing will be marked.");
        }

        return validatedIcon;
    }

    private void OnDataUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        try
        {
            var icon = ResolveIcon();
            if (icon == 0 || (enemyNames.Count == 0 && giverNames.Count == 0))
            {
                return;
            }

            foreach (var handler in handlers)
            {
                Mark(handler, icon);
            }
        }
        catch (Exception ex)
        {
            // Permanently, and once. This runs every frame for every plate; an exception here is an
            // exception in the game's own render path sixty times a second.
            disabledByFailure = true;
            Unsubscribe();
            log.Error(ex, "Wayfarer markers: the nameplate handler failed and has switched itself off for this session.");
        }
    }

    private void Mark(INamePlateUpdateHandler handler, int icon)
    {
        // Never stomp on whatever the game put there — party target markers, hunt marks and its own
        // quest icons all live in this one field.
        if (handler.MarkerIconId != 0)
        {
            return;
        }

        var names = handler.NamePlateKind switch
        {
            NamePlateKind.EventNpcCompanion => giverNames,
            NamePlateKind.BattleNpcEnemy => enemyNames,
            _ => null,
        };

        if (names is null || names.Count == 0)
        {
            return;
        }

        // Matched by name, which is not unique — the same monster name appears in several zones and
        // NPC names repeat. It is the only key the hunting dataset and the nameplate agree on
        // today; carrying the resident/base row id in the datasets would make this exact. Until
        // then the feature stays off by default and the match set is limited to the current zone's
        // targets, which bounds the blast radius to "an unrelated mob of the same name".
        if (handler.GameObject is { } gameObject && names.Contains(gameObject.Name.TextValue))
        {
            handler.MarkerIconId = icon;
        }
    }
}
