namespace Wayfarer.Guidance;

/// <summary>Who holds guidance, and who is waiting to have it back.
///
/// <para>Only one source is guided to at a time, but a source that is pushed aside has not stopped
/// wanting it. The quest being followed is still there while a hunt is guided, and when the hunt
/// ends the quest should be guided again without anyone having to remember to ask. So a source
/// pushed aside waits, and letting go hands focus to whichever waiting source was pushed aside
/// most recently.</para>
///
/// <para>Claiming is taking over: what the player just chose to follow. Offering is being there
/// for when nobody else wants it: what a module that always has something to say does whenever
/// its settings are applied, which must never take guidance from something the player
/// chose.</para>
///
/// <para>Resuming is either of those, chosen by who held focus last: the source a character was
/// last guided to claims it back when they log in, and every other source offers.</para></summary>
internal sealed class Focus
{
    /// <summary>Sources waiting to hold focus again, the one to have it back first at the front.</summary>
    private readonly List<IObjectiveSource> waiting = [];

    /// <summary>The source guided to right now, or null.</summary>
    public IObjectiveSource? Holder { get; private set; }

    /// <summary>Takes focus now, from whoever holds it.</summary>
    /// <param name="source">The source taking over.</param>
    /// <returns>The source pushed aside, which now waits to have it back, or null.</returns>
    public IObjectiveSource? Claim(IObjectiveSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (ReferenceEquals(Holder, source))
        {
            return null;
        }

        waiting.RemoveAll(other => ReferenceEquals(other, source));
        var displaced = Holder;
        if (displaced is not null)
        {
            waiting.Insert(0, displaced);
        }

        Holder = source;
        return displaced;
    }

    /// <summary>Holds focus if nobody does, and otherwise waits for it behind everyone already
    /// waiting, pushing nobody aside. Offering again while holding or waiting changes nothing.</summary>
    /// <param name="source">The source offering to be guided to.</param>
    public void Offer(IObjectiveSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (ReferenceEquals(Holder, source) || waiting.Exists(other => ReferenceEquals(other, source)))
        {
            return;
        }

        if (Holder is null)
        {
            Holder = source;
        }
        else
        {
            waiting.Add(source);
        }
    }

    /// <summary>Takes focus back when this source is the one last held, and otherwise offers it:
    /// what a character logging in, or a module coming up, does to pick up where it left off.</summary>
    /// <param name="source">The source resuming.</param>
    /// <param name="last">The name of the source last held, or null when none is known.</param>
    /// <returns>The source pushed aside, which now waits to have it back, or null.</returns>
    public IObjectiveSource? Resume(IObjectiveSource source, string? last)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (string.Equals(source.Name, last, StringComparison.Ordinal))
        {
            return Claim(source);
        }

        Offer(source);
        return null;
    }

    /// <summary>Gives focus up, or stops waiting for it. When the holder gives it up, the source
    /// pushed aside most recently has it back.</summary>
    /// <param name="source">The source letting go.</param>
    public void Yield(IObjectiveSource source)
    {
        if (!ReferenceEquals(Holder, source))
        {
            waiting.RemoveAll(other => ReferenceEquals(other, source));
            return;
        }

        if (waiting.Count == 0)
        {
            Holder = null;
            return;
        }

        Holder = waiting[0];
        waiting.RemoveAt(0);
    }
}
