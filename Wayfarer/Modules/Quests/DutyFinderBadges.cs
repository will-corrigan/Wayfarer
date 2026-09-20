using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;

namespace Wayfarer.Modules.Quests;

/// <summary>Marks the Duty Finder's rows for the duties the player still has a quest for, with the
/// mark the journal itself puts beside that quest: the main scenario's own, or the ordinary quest
/// one.
///
/// <para>The game is what says when a row is drawn and what it is drawn as, so that is what is
/// listened to rather than the window being read every frame. A row is handed round as the list
/// scrolls, and the same telling that hands it to another duty is the one that sets its mark; a
/// row that stops wanting one is told to give it back.</para>
///
/// <para>The journal is read again only when it changes. Which duty a quest sends the player into
/// is fixed by the sheet, so nothing else about a quest can change the answer.</para>
///
/// <para>It lives only while the quests module is up, and every node it makes is freed when the
/// row gives it back, when the window closes, or when the module goes down.</para></summary>
internal sealed class DutyFinderBadges(QuestReader reader, IFramework framework, IPluginLog log) : IAsyncDisposable
{
    private const string AddonName = "ContentsFinder";

    /// <summary>TEMPORARY. How far along a row's parts to read when describing one. The game does
    /// not say how many there are, so this is far enough to reach past the icons.</summary>
    private const int DescribeDepth = 24;

    private readonly Dictionary<uint, IconImageNode> badgesByRow = [];
    private readonly Dictionary<uint, uint> iconsByDuty = [];

    private NativeListController<AddonContentsFinder, DutyFinderRow>? rows;
    private int journal;
    private bool read;
    private bool described;

    /// <summary>Starts marking. Safe to call off the framework thread.</summary>
    public void Start() => _ = framework.RunOnFrameworkThread(Enable);

    /// <summary>Stops marking and frees every mark. The window may still be open, so the nodes go
    /// now rather than at a close that may never come this session.</summary>
    public async Task StopAsync()
    {
        if (rows is { } watching)
        {
            rows = null;
            await watching.DisposeAsync().ConfigureAwait(false);
        }

        await framework.RunOnFrameworkThread(FreeAll).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    /// <summary>The row the duty list fills every one of its rows in from. Everything the game
    /// draws in this list goes through it, which is what there is to listen to.</summary>
    private static unsafe AtkComponentListItemRenderer* Template(AddonContentsFinder* addon) =>
        addon == null || addon->DutyList == null
            ? null
            : addon->DutyList->GetComponentItemRendererById(DutyFinderMetrics.RowTemplateNodeId);

    /// <summary>A number that changes when the journal does. The quests in it are what decides
    /// which duties are marked, and this is asked far more often than the journal changes, so it
    /// allocates nothing.</summary>
    private static unsafe int JournalSignature()
    {
        var quests = QuestManager.Instance();
        if (quests == null)
        {
            return 0;
        }

        var signature = default(HashCode);
        foreach (ref var quest in quests->NormalQuests)
        {
            signature.Add(quest.QuestId);
        }

        return signature.ToHashCode();
    }

    /// <summary>Hangs a new mark off a row, beside its name, or null when there is no name to hang
    /// it beside. Its place is the row's own, which is the space the name is placed in.</summary>
    private static unsafe IconImageNode? Attach(DutyFinderRow row)
    {
        var name = row.NameNode;
        if (name == null)
        {
            return null;
        }

        var badge = new IconImageNode
        {
            Size = new(DutyFinderMetrics.BadgeSize, DutyFinderMetrics.BadgeSize),
            Position = new(DutyFinderMetrics.BadgeLeft, DutyFinderMetrics.BadgeTop),
            IsVisible = false,
        };
        badge.AttachNode(name, NodePosition.AfterTarget);
        return badge;
    }

    private unsafe void Enable()
    {
        rows ??= new NativeListController<AddonContentsFinder, DutyFinderRow>
        {
            AddonName = AddonName,
            GetPopulatorNode = Template,
            ShouldModifyElement = Wants,
            UpdateElement = Set,
            ResetElement = Clear,
        };
        rows.Enable();
    }

    /// <summary>Whether this row wants a mark at all. Answering no here is what has the game tell
    /// us to take an old one back off.</summary>
    private unsafe bool Wants(AddonContentsFinder* addon, DutyFinderRow row)
    {
        Describe(row);
        return IconFor(row) is not null;
    }

    /// <summary>The mark this row should carry, or null when no quest in the journal leads there.</summary>
    private uint? IconFor(DutyFinderRow row)
    {
        Reread();
        return row.Duty is { } duty && iconsByDuty.TryGetValue(duty, out var icon) ? icon : null;
    }

    /// <summary>Reads the journal again if it has changed since last time.</summary>
    private unsafe void Reread()
    {
        var now = JournalSignature();
        if (read && now == journal)
        {
            return;
        }

        journal = now;
        read = true;
        iconsByDuty.Clear();

        var quests = QuestManager.Instance();
        if (quests == null)
        {
            return;
        }

        foreach (ref var quest in quests->NormalQuests)
        {
            if (quest.QuestId != 0
                && reader.Duty(quest.QuestId) is { } duty
                && reader.JournalIcon(quest.QuestId) is { } icon)
            {
                // Two quests can send the player into the same duty. The first one found marks it;
                // the mark says there is something left to do there, not how many things.
                iconsByDuty.TryAdd(duty.Finder, icon);
            }
        }
    }

    /// <summary>Marks a row the game has just drawn, making the mark if this row has not carried
    /// one before.</summary>
    private unsafe void Set(AddonContentsFinder* addon, DutyFinderRow row)
    {
        try
        {
            if (IconFor(row) is not { } icon)
            {
                return;
            }

            if (!badgesByRow.TryGetValue(row.NodeId, out var badge))
            {
                if (Attach(row) is not { } made)
                {
                    return;
                }

                badge = made;
                badgesByRow[row.NodeId] = badge;
            }

            badge.IconId = icon;
            badge.IsVisible = true;
        }
        catch (Exception ex)
        {
            log.Error(ex, "a Duty Finder row could not be marked, so it stays unmarked.");
        }
    }

    /// <summary>Takes the mark back off a row that no longer wants one. The game says when, which
    /// is the whole reason a row can be handed to another duty without a mark going with it.</summary>
    private unsafe void Clear(AddonContentsFinder* addon, DutyFinderRow row)
    {
        if (badgesByRow.Remove(row.NodeId, out var badge))
        {
            badge.Dispose();
        }
    }

    /// <summary>TEMPORARY. Writes out the parts of a row once, so where the game keeps its own
    /// strip of row icons can be read off rather than guessed at. Delete once the answer is in
    /// <see cref="DutyFinderMetrics"/>.</summary>
    private unsafe void Describe(DutyFinderRow row)
    {
        if (described || row.NodeList == null)
        {
            return;
        }

        described = true;
        for (var index = 0; index < DescribeDepth; index++)
        {
            var node = row.NodeList[index];
            if (node != null)
            {
                log.Information($"duty row part {index}: type {node->Type} at ({node->X}, {node->Y}) {node->Width}x{node->Height} shown {node->IsVisible()}");
            }
        }
    }

    /// <summary>Frees every mark, and safe to call when there is nothing to free.</summary>
    private void FreeAll()
    {
        foreach (var badge in badgesByRow.Values)
        {
            badge.Dispose();
        }

        badgesByRow.Clear();
    }
}
