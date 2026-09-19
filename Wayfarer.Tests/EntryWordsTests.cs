using Wayfarer.Guidance;
using Wayfarer.Presentation;

namespace Wayfarer.Tests;

/// <summary>The words a surface prints for one entry, and how its count is written.</summary>
public class EntryWordsTests
{
    [Fact]
    public void A_counted_entry_writes_have_over_needed_after_its_words()
    {
        var entry = new ObjectiveEntry("Slay opo-opos.", new Progress(3, 8), new Destination.Blocked("test"));

        Assert.Equal("Slay opo-opos. 3/8", EntryWords.Describe(entry));
    }

    [Fact]
    public void A_plain_entry_is_its_words()
    {
        var entry = new ObjectiveEntry("Speak with Momodi.", null, new Destination.Blocked("test"));

        Assert.Equal("Speak with Momodi.", EntryWords.Describe(entry));
    }
}
