using Wayfarer.Core.Guidance;
using Wayfarer.Core.Presentation;

namespace Wayfarer.Tests;

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
