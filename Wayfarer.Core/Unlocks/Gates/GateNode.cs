namespace Wayfarer.Core.Unlocks.Gates;

/// <summary>One node of a requirement tree — the same shape whatever the kind, so the dispatcher
/// never grows a case for a new one.
///
/// <para><b>Ids only.</b> <see cref="Display"/> and <see cref="From"/> never take part in a
/// decision. A name is a label on a row id, not a key: the Mount sheet alone has three duplicated
/// singular names and 85 unnamed rows, and the ContentFinderCondition sheet has 18 duplicated
/// names. Names are not identity anywhere in this game's data.</para>
///
/// <para>A plain record with no serialiser types on it, so Core stays serialiser-agnostic; the
/// parse lives in <see cref="UnlockDataset"/>.</para></summary>
public sealed class GateNode
{
    /// <summary>The only field the dispatcher reads.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Row ids — the only thing evaluation ever uses.</summary>
    public List<uint> Ids { get; set; } = [];

    /// <summary>Count, rank or level, kind-specific. 0 means "unused".</summary>
    public int Amount { get; set; }

    /// <summary>A discriminator the kind gives meaning to: which container to look in, which id
    /// space a duty id belongs to. The scope is how the Diadem class of error is prevented — an
    /// id from one space handed to the other space's reader returns a different duty's bit, quite
    /// confidently.</summary>
    public string? Scope { get; set; }

    /// <summary>Presentation only. Never evaluated.</summary>
    public string? Display { get; set; }

    /// <summary>Presentation only — "where you get it", the one fact no API answers.</summary>
    public string? From { get; set; }

    /// <summary>Combinators only.</summary>
    public List<GateNode> Children { get; set; } = [];

    /// <summary>Renders "rose lanner — Thok ast Thok (Extreme)" from <see cref="Display"/> and
    /// <see cref="From"/>. A formatting helper; it makes no decision.</summary>
    public string Describe()
    {
        var name = Display is { Length: > 0 } d ? d : Kind;
        return From is { Length: > 0 } f ? $"{name} — {f}" : name;
    }
}
