using System.Text.Json;
using Wayfarer.Modules.Quests;
using Wayfarer.Routing;

namespace Wayfarer.Tests;

/// <summary>What the guidance makes of real quests, read out of the game's own sheets by
/// <c>tools/Wayfarer.QuestCorpus</c> and kept in <c>data/quest-corpus.json</c> so these run
/// without the game installed.
///
/// <para>Each quest in the corpus was chosen for a shape that has to come out right: a door a
/// quest carries from beginning to end, a person it sends you back to over and over, one of a
/// city's people standing among the parts of it you were told to search. The rules that decide
/// between them are not obvious from any one quest, so they are put to a spread of them.</para>
/// </summary>
public class QuestCorpusTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly List<CorpusQuest> Corpus = Load();

    public static TheoryData<string> QuestNames() => [.. Corpus.Select(quest => quest.Name)];

    [Fact]
    public void The_corpus_covers_a_spread_of_quests()
    {
        Assert.True(Corpus.Count >= 20, $"only {Corpus.Count} quests in the corpus");
        Assert.True(Corpus.Sum(quest => quest.Steps.Count) >= 100);
    }

    [Theory]
    [MemberData(nameof(QuestNames))]
    public void A_step_that_named_somewhere_still_names_somewhere(string quest)
    {
        foreach (var (step, chosen) in Walk(quest))
        {
            Assert.True(chosen.Count > 0, $"{quest} seq {step.Sequence} was left with nowhere to go");
        }
    }

    [Theory]
    [MemberData(nameof(QuestNames))]
    public void Nowhere_is_invented_that_the_step_did_not_name(string quest)
    {
        foreach (var (step, chosen) in Walk(quest))
        {
            foreach (var place in chosen)
            {
                Assert.Contains(place, step.Places.Select(At));
            }
        }
    }

    [Theory]
    [MemberData(nameof(QuestNames))]
    public void A_person_is_only_ever_passed_over_for_ground_that_outnumbers_them(string quest)
    {
        foreach (var (step, chosen) in Walk(quest))
        {
            var dropped = step.Places.Where(place => !chosen.Contains(At(place))).ToList();
            if (dropped.All(place => place.IsObject || place.ObjectId == 0))
            {
                continue;
            }

            var bare = step.Places.Count(place => place.ObjectId == 0);
            Assert.True(
                bare > step.Places.Count - bare,
                $"{quest} seq {step.Sequence} \"{step.Words}\" passed over a person without ground outnumbering them");
        }
    }

    [Theory]
    [MemberData(nameof(QuestNames))]
    public void A_step_that_names_a_thing_is_still_sent_to_it(string quest)
    {
        foreach (var (step, chosen) in Walk(quest))
        {
            foreach (var place in step.Places.Where(place => place.IsObject && StepPlaces.NamedIn(place.ObjectName, step.Words)))
            {
                Assert.True(
                    chosen.Contains(At(place)),
                    $"{quest} seq {step.Sequence} \"{step.Words}\" gave up '{place.ObjectName}', which it names");
            }
        }
    }

    [Fact]
    public void The_door_a_quest_is_shut_behind_is_never_where_a_step_sends_you()
    {
        // "Heavens Weep" hangs the cermet bulkhead on five of its six steps. It is how the player
        // gets in and out of where the quest happens, and it is nearer than the step's own places.
        foreach (var (step, chosen) in Walk("Heavens Weep"))
        {
            var door = step.Places.FirstOrDefault(place => string.Equals(place.ObjectName, "cermet bulkhead", StringComparison.Ordinal));
            if (door is null)
            {
                continue;
            }

            Assert.DoesNotContain(At(door), chosen);
        }
    }

    [Fact]
    public void A_person_on_every_step_of_a_quest_is_still_where_its_steps_send_you()
    {
        // "Sleepless in the Stable" names the same traveller on every one of its steps. A quest
        // does that on purpose, and dropping them would point at whoever else is standing about.
        var quest = Corpus.First(entry => entry.Name.Contains("Sleepless in the Stable", StringComparison.Ordinal));
        var everyStep = quest.Steps
            .SelectMany(step => step.Places)
            .Where(place => place.ObjectId != 0 && !place.IsObject)
            .GroupBy(place => place.Row)
            .Where(group => group.Count() > quest.Steps.Count / 2)
            .Select(group => group.Key)
            .ToHashSet();

        Assert.NotEmpty(everyStep);
        foreach (var (step, chosen) in Walk(quest.Name))
        {
            foreach (var place in step.Places.Where(place => everyStep.Contains(place.Row)))
            {
                Assert.Contains(At(place), chosen);
            }
        }
    }

    [Fact]
    public void One_of_a_citys_people_among_its_streets_is_not_the_errand()
    {
        // "Yes We Cant" draws three parts of the Quicksand and one of the people in them.
        var (step, chosen) = Walk("Yes We Cant").First(entry => entry.Step.Places.Count(place => place.ObjectId == 0) > 1);

        Assert.All(chosen, place => Assert.True(place.Radius > 0f));
        Assert.DoesNotContain(chosen, place => step.Places.Any(other => other.ObjectId != 0 && At(other) == place));
    }

    [Fact]
    public void An_ordinary_quest_is_left_exactly_as_the_sheet_wrote_it()
    {
        foreach (var (step, chosen) in Walk("Close to Home"))
        {
            Assert.Equal(step.Places.Select(At), chosen);
        }
    }

    private static IEnumerable<(CorpusStep Step, IReadOnlyList<Place> Chosen)> Walk(string quest)
    {
        var entry = Corpus.First(candidate => candidate.Name.Contains(quest, StringComparison.Ordinal));
        var chosen = StepPlaces.Choose([.. entry.Steps.Select(Shape)]);
        return entry.Steps.Select((step, i) => (step, chosen[i]));
    }

    private static StepShape Shape(CorpusStep step) =>
        new(step.Index, step.Sequence, step.Words, [.. step.Places.Select(place =>
            new StepPlace(place.Row, place.ObjectId, place.IsObject, place.ObjectName, At(place)))]);

    private static Place At(CorpusPlace place) =>
        new(place.Territory, place.Map, place.X, place.Y, place.Z, place.Radius);

    private static List<CorpusQuest> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "quest-corpus.json");
        return JsonSerializer.Deserialize<List<CorpusQuest>>(File.ReadAllText(path), Options) ?? [];
    }

    public sealed record CorpusPlace(uint Row, uint ObjectId, bool IsObject, string ObjectName,
        uint Territory, uint Map, float X, float Y, float Z, float Radius);

    public sealed record CorpusStep(int Index, byte Sequence, byte Qty, string Words, IReadOnlyList<CorpusPlace> Places);

    public sealed record CorpusQuest(uint QuestRow, string Name, string InternalName, IReadOnlyList<CorpusStep> Steps);
}
