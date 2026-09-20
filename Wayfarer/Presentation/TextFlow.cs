using System.Globalization;
using System.Text;

namespace Wayfarer.Presentation;

/// <summary>Lays a line's words out word by word, so the keyword inside them can be drawn, lit and
/// pressed on its own while the rest of the sentence reads as ordinary words around it.
///
/// <para>The words are measured rather than guessed: the caller passes whatever measures a string
/// in the face the line is actually set in, which is the game's own text node on a surface and a
/// stub in a test. Nothing here knows about quests, items or nodes.</para></summary>
public static class TextFlow
{
    /// <summary>What is appended to the last stretch when the words did not fit. Three full stops
    /// rather than the single ellipsis character: the game's face draws that one as dots halfway up
    /// the line, which reads as a row of bullets rather than as a sentence trailing off.</summary>
    public const string Ellipsis = "...";

    private const char WordSeparator = ' ';

    /// <summary>Places the words across at most <paramref name="maxLines"/> lines.</summary>
    /// <param name="words">The whole sentence.</param>
    /// <param name="keyword">The stretch inside it that names what a press does, or null when the
    /// sentence does not name it. A keyword the sentence never says is ignored.</param>
    /// <param name="measure">What a string is wide, in the face the line is set in.</param>
    /// <param name="width">How wide the line may be.</param>
    /// <param name="lineHeight">How far down each wrapped line sits.</param>
    /// <param name="maxLines">How many lines the words may take before they are cut.</param>
    /// <param name="keywordLead">Room kept in front of the keyword for its icon, or zero.</param>
    public static IReadOnlyList<LineRun> Arrange(
        string words,
        string? keyword,
        Func<string, float> measure,
        float width,
        float lineHeight,
        int maxLines,
        float keywordLead = 0f) =>
        Arrange(words, keyword, measure, width, lineHeight, maxLines, keywordLead, out _);

    /// <inheritdoc cref="Arrange(string, string?, Func{string, float}, float, float, int, float)"/>
    /// <param name="cut">Whether the words ran out of room and were cut short, so a caller can
    /// offer the whole sentence some other way.</param>
    public static IReadOnlyList<LineRun> Arrange(
        string words,
        string? keyword,
        Func<string, float> measure,
        float width,
        float lineHeight,
        int maxLines,
        float keywordLead,
        out bool cut)
    {
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(measure);

        var flow = new Flow(measure, width, lineHeight, maxLines, keywordLead);
        foreach (var (text, isKeyword) in Tokens(words, keyword))
        {
            if (!flow.Place(text, isKeyword))
            {
                break;
            }
        }

        var runs = flow.Finish();
        cut = flow.Cut;
        return runs;
    }

    /// <summary>Whether the sentence says its keyword, so the keyword will be a stretch of its own.
    /// A caller that keeps room in front of the keyword asks this first, because there is nowhere
    /// to keep that room when the sentence never names it.</summary>
    public static bool Names(string words, string? keyword)
    {
        ArgumentNullException.ThrowIfNull(words);
        return At(words, keyword) >= 0;
    }

    /// <summary>The words in order, as the stretches they will be drawn in: what comes before the
    /// keyword word by word, the keyword whole, then what comes after word by word. A sentence that
    /// does not say the keyword is all ordinary words.</summary>
    private static IEnumerable<(string Text, bool Keyword)> Tokens(string words, string? keyword)
    {
        var at = At(words, keyword);
        if (at < 0)
        {
            return Words(words).Select(word => (word, false));
        }

        // The keyword is taken from the sentence rather than from the caller, so it keeps the
        // sentence's own capitalisation: "Use a smoke bomb", not "Use a Smoke Bomb".
        return
        [
            .. Words(words[..at]).Select(word => (word, false)),
            (words.Substring(at, keyword!.Length), true),
            .. Words(words[(at + keyword.Length)..]).Select(word => (word, false)),
        ];
    }

    private static int At(string words, string? keyword) =>
        string.IsNullOrEmpty(keyword) ? -1 : words.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);

    private static string[] Words(string text) =>
        text.Split(WordSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>One pass of placing: the pen's position, the stretch of ordinary words being
    /// gathered at it, and the stretches already placed.</summary>
    private sealed class Flow(Func<string, float> measure, float width, float lineHeight, int maxLines, float keywordLead)
    {
        /// <summary>A pair of characters with and without a space between them. The difference is
        /// what one space advances by, which no measurer reports directly.</summary>
        private const string SpacedPair = "n n";

        /// <inheritdoc cref="SpacedPair"/>
        private const string TightPair = "nn";

        private readonly float space = Math.Max(0f, measure(SpacedPair) - measure(TightPair));
        private readonly List<LineRun> runs = [];
        private readonly StringBuilder plain = new();
        private float plainLeft;
        private float plainTop;
        private int line;
        private float pen;

        /// <summary>Whether the words ran out of room and were cut short.</summary>
        public bool Cut { get; private set; }

        /// <summary>Places one stretch, and says whether there is room for another.</summary>
        public bool Place(string text, bool keyword)
        {
            var span = measure(text) + (keyword ? keywordLead : 0f);
            var gap = pen > 0f ? space : 0f;
            if (pen > 0f && pen + gap + span > width && !Wrap())
            {
                return false;
            }

            gap = pen > 0f ? gap : 0f;
            if (keyword)
            {
                FlushPlain();
                runs.Add(new LineRun(text, true, pen + gap, line * lineHeight, span));
            }
            else
            {
                Gather(text, pen + gap);
            }

            pen += gap + span;
            return true;
        }

        /// <summary>Every stretch placed, the last of them marked when the words were cut short.</summary>
        public List<LineRun> Finish()
        {
            FlushPlain();
            if (Cut && runs.Count > 0)
            {
                var last = runs[^1];
                runs[^1] = last with { Text = string.Create(CultureInfo.InvariantCulture, $"{last.Text}{Ellipsis}") };
            }

            return runs;
        }

        /// <summary>Moves the pen to the start of the next line, or reports that there is none.</summary>
        private bool Wrap()
        {
            FlushPlain();
            line++;
            pen = 0f;
            Cut = line >= maxLines;
            return !Cut;
        }

        private void Gather(string text, float left)
        {
            if (plain.Length == 0)
            {
                plainLeft = left;
                plainTop = line * lineHeight;
            }
            else
            {
                plain.Append(WordSeparator);
            }

            plain.Append(text);
        }

        private void FlushPlain()
        {
            if (plain.Length == 0)
            {
                return;
            }

            runs.Add(new LineRun(plain.ToString(), false, plainLeft, plainTop, pen - plainLeft));
            plain.Clear();
        }
    }
}
