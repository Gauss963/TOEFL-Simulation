using System.Globalization;

namespace TOEFLSimulation.Core;

/// <summary>Swift <c>SpeechTranscription</c>: the recognized text and its word segments.</summary>
public sealed record SpeechTranscription(string Text, IReadOnlyList<SpeechTranscription.Segment> Segments)
{
    /// <summary>A recognized word: its text, start time, and duration in seconds from the start of the recording.</summary>
    public sealed record Segment(string Text, double Start, double Duration);

    /// <summary>"No speech detected" is a valid, empty response (what the Mac app returns on recognizer errors).</summary>
    public static SpeechTranscription Empty { get; } = new("", []);
}

/// <summary>
/// Speech-to-text used to score Speaking responses (the Mac app uses on-device Apple Speech; the Windows app
/// implements this with Whisper). Return <c>null</c> when recognition is unavailable (no model, not allowed);
/// return <see cref="SpeechTranscription.Empty"/> when no speech was detected.
/// </summary>
public interface ISpeechTranscriber
{
    /// <param name="audioFilePath">The recorded response.</param>
    /// <param name="contextualStrings">Hints (the target sentence for Listen and Repeat; empty for interviews).</param>
    /// <param name="cancellationToken">Cancels recognition (for example when the user exits the test).</param>
    Task<SpeechTranscription?> TranscribeAsync(string audioFilePath, IReadOnlyList<string> contextualStrings, CancellationToken cancellationToken);
}

/// <summary>
/// Swift <c>SpeakingRubric</c>: scores Speaking responses on the official 0–5 TOEFL iBT 2026 rubrics from a
/// transcription (Listen and Repeat by word alignment; Take an Interview by development, fluency, language, relevance).
/// </summary>
public static class SpeakingRubric
{
    private static readonly HashSet<string> FunctionWords =
    [
        "a", "an", "the", "of", "to", "in", "on", "at", "for", "with", "by", "from", "and", "or", "but", "so",
        "is", "are", "was", "were", "be", "been", "am", "do", "does", "did", "have", "has", "had", "will", "would",
        "can", "could", "should", "may", "might", "must", "it", "its", "this", "that", "these", "those", "there",
        "here", "as", "if", "than", "then", "your", "you", "our", "we", "they", "their", "he", "she", "his", "her",
        "i", "my", "me", "us", "them", "all", "some", "any", "each", "every", "not", "no", "up", "out", "into",
    ];

    private static readonly string[] Connectors =
    [
        "because", "so", "for example", "for instance", "however", "although", "though", "also", "first", "second",
        "finally", "then", "in addition", "on the other hand", "i think", "i believe", "in my opinion", "that's why",
        "which", "while", "since", "as a result", "especially", "actually", "but",
    ];

    private const string Unavailable = "Speech recognition was unavailable, so this response could not be scored.";

    // MARK: Listen and Repeat

    public static (int Score, string Detail) ScoreRepeat(string target, SpeechTranscription? transcription)
    {
        if (transcription is null) return (0, Unavailable);
        var expected = Words(target);
        var spoken = Words(transcription.Text);
        if (spoken.Count == 0) return (0, "No speech was detected.");

        var alignment = Align(expected, spoken);
        var missingContent = alignment.Missing.Count(w => !FunctionWords.Contains(w));
        var missingFunction = alignment.Missing.Count - missingContent;
        // A changed ending on the same word (open/opens, study/studied) is a tense or number slip.
        static bool IsInflection((string Expected, string Spoken) s) =>
            Stem(s.Expected) == Stem(s.Spoken) || s.Expected.StartsWith(s.Spoken, StringComparison.Ordinal) || s.Spoken.StartsWith(s.Expected, StringComparison.Ordinal);
        var inflections = alignment.Substituted.Count(IsInflection);
        var changed = alignment.Substituted.Where(s => !IsInflection(s)).ToList();
        var wrongContent = changed.Count(s => !FunctionWords.Contains(s.Expected));
        var wrongFunction = changed.Count - wrongContent + inflections;
        var coverage = (double)(alignment.Matched + inflections) / Math.Max(1, expected.Count);

        var sameWordsReordered = new HashSet<string>(expected).SetEquals(spoken) && expected.Count == spoken.Count;

        int score;
        string reason;
        if (alignment.Matched == expected.Count && alignment.Extra == 0)
        {
            score = 5; reason = "Exact repetition.";
        }
        else if (sameWordsReordered || (missingContent + wrongContent <= 1 && missingFunction + wrongFunction + alignment.Extra <= 2 && coverage >= 0.8))
        {
            score = 4; reason = "Meaning captured with minor changes (a function word, tense/number, or word order).";
        }
        else if (coverage >= 0.65 && missingContent + wrongContent <= 3)
        {
            score = 3; reason = "Essentially full sentence, but the meaning is not captured accurately.";
        }
        else if (coverage >= 0.35)
        {
            score = 2; reason = "A significant part of the sentence is missing or changed.";
        }
        else
        {
            score = 1; reason = "Very little of the sentence was reproduced.";
        }
        return (score, $"{reason} Words matched: {alignment.Matched}/{expected.Count}.");
    }

    // MARK: Take an Interview

    public static (int Score, string Detail) ScoreInterview(string question, SpeechTranscription? transcription, int responseSeconds)
    {
        if (transcription is null) return (0, Unavailable);
        var spoken = Words(transcription.Text);
        if (spoken.Count < 3) return (0, "No meaningful speech was detected.");

        var segments = transcription.Segments;
        var speakingStart = segments.Count > 0 ? segments[0].Start : 0;
        var speakingEnd = segments.Count > 0 ? segments[^1].Start + segments[^1].Duration : responseSeconds;
        var speakingSpan = Math.Max(1, speakingEnd - speakingStart);
        var rate = spoken.Count / speakingSpan * 60;
        var longPauses = 0;
        for (var i = 0; i + 1 < segments.Count; i++)
            if (segments[i + 1].Start - (segments[i].Start + segments[i].Duration) > 1.2) longPauses++;
        var fillers = spoken.Count(w => w is "um" or "uh" or "er" or "hmm");

        var content = spoken.Where(w => !FunctionWords.Contains(w) && TextUtil.CharacterCount(w) > 2).ToList();
        var diversity = (double)content.Distinct().Count() / Math.Max(1, content.Count);
        var lowered = transcription.Text.ToLowerInvariant();
        var connectorCount = Connectors.Count(c => lowered.Contains(c, StringComparison.Ordinal));
        var questionKeys = Words(question).Where(w => !FunctionWords.Contains(w) && TextUtil.CharacterCount(w) > 3).Select(Stem).ToHashSet();
        var overlap = content.Select(Stem).ToHashSet().Intersect(questionKeys).Count();

        var development = Math.Min(1, spoken.Count / 85.0);
        double fluency;
        {
            var value = rate >= 95 && rate <= 175 ? 1 : Math.Max(0, 1 - Math.Abs(rate - 135) / 110);
            value -= longPauses * 0.08 + fillers * 0.03;
            fluency = Math.Max(0, Math.Min(1, value));
        }
        var language = Math.Min(1, 0.5 * Math.Min(1, diversity / 0.75) + 0.5 * Math.Min(1, connectorCount / 3.0));
        var relevance = Math.Min(1, overlap / 2.0);

        var raw = 5 * (0.35 * development + 0.25 * fluency + 0.2 * language + 0.2 * relevance);
        if (overlap == 0) raw = Math.Min(raw, 2.4);
        if (spoken.Count < 10) raw = Math.Min(raw, 1.4);
        else if (spoken.Count < 25) raw = Math.Min(raw, 2.4);
        else if (spoken.Count < 45) raw = Math.Min(raw, 3.4);
        var score = Math.Max(1, Math.Min(5, (int)TextUtil.SwiftRound(raw)));

        var descriptor = score switch
        {
            5 => "Fully addresses the question; clear, fluent, and well elaborated.",
            4 => "Addresses the question; reasonably clear with adequate elaboration.",
            3 => "Limited elaboration or clarity; choppy pace or limited range.",
            2 => "Minimally connected to the question with little elaboration.",
            _ => "Vague or very limited response.",
        };
        var detail = $"{descriptor} {spoken.Count} words · {(long)TextUtil.SwiftRound(rate)} words/min · {longPauses} long pauses · {connectorCount} connectors.";
        return (score, detail);
    }

    // MARK: Helpers

    /// <summary>Lower-cased words with contractions expanded and digits spelled out ("21" → "twenty", "one").</summary>
    public static List<string> Words(string text)
    {
        var expanded = text.ToLowerInvariant()
            .Replace("’", "'", StringComparison.Ordinal)
            .Replace("won't", "will not", StringComparison.Ordinal)
            .Replace("can't", "cannot", StringComparison.Ordinal)
            .Replace("n't", " not", StringComparison.Ordinal)
            .Replace("'re", " are", StringComparison.Ordinal)
            .Replace("'ll", " will", StringComparison.Ordinal)
            .Replace("'ve", " have", StringComparison.Ordinal)
            .Replace("'m", " am", StringComparison.Ordinal)
            .Replace("it's", "it is", StringComparison.Ordinal)
            .Replace("that's", "that is", StringComparison.Ordinal)
            .Replace("there's", "there is", StringComparison.Ordinal)
            .Replace("let's", "let us", StringComparison.Ordinal);
        var result = new List<string>();
        foreach (var token in TextUtil.Split(expanded, c => !char.IsLetter(c) && !char.IsNumber(c) && c != '\''))
        {
            var word = token.Trim('\'');
            if (word.Length == 0) continue;
            if (word.All(char.IsAsciiDigit) && long.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                result.AddRange(Words(NumberSpeller.SpellOut(number)));
            else
                result.Add(word);
        }
        return result;
    }

    private static string Stem(string word)
    {
        foreach (var suffix in (string[])["ing", "ed", "es", "s", "ly"])
            if (TextUtil.CharacterCount(word) > suffix.Length + 3 && word.EndsWith(suffix, StringComparison.Ordinal))
                return word[..^suffix.Length];
        return word;
    }

    private sealed class Alignment
    {
        public int Matched;
        public readonly List<(string Expected, string Spoken)> Substituted = [];
        public readonly List<string> Missing = [];
        public int Extra;
    }

    /// <summary>Word-level Levenshtein alignment between the target and the spoken words.</summary>
    private static Alignment Align(IReadOnlyList<string> expected, IReadOnlyList<string> spoken)
    {
        int n = expected.Count, m = spoken.Count;
        var cost = new int[n + 1, m + 1];
        for (var i = 0; i <= n; i++) cost[i, 0] = i;
        for (var j = 0; j <= m; j++) cost[0, j] = j;
        for (var i = 1; i <= n; i++)
        for (var j = 1; j <= m; j++)
        {
            var same = expected[i - 1] == spoken[j - 1] ? 0 : 1;
            cost[i, j] = Math.Min(Math.Min(cost[i - 1, j] + 1, cost[i, j - 1] + 1), cost[i - 1, j - 1] + same);
        }
        var result = new Alignment();
        int a = n, b = m;
        while (a > 0 || b > 0)
        {
            if (a > 0 && b > 0 && cost[a, b] == cost[a - 1, b - 1] + (expected[a - 1] == spoken[b - 1] ? 0 : 1))
            {
                if (expected[a - 1] == spoken[b - 1]) result.Matched++;
                else result.Substituted.Add((expected[a - 1], spoken[b - 1]));
                a--; b--;
            }
            else if (a > 0 && cost[a, b] == cost[a - 1, b] + 1)
            {
                result.Missing.Add(expected[a - 1]); a--;
            }
            else
            {
                result.Extra++; b--;
            }
        }
        return result;
    }
}

/// <summary>English number words as produced by Apple's <c>NumberFormatter</c> with <c>.spellOut</c> (en_US).</summary>
public static class NumberSpeller
{
    private static readonly string[] Ones =
    [
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
        "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen",
    ];
    private static readonly string[] Tens = ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];
    private static readonly (long Value, string Name)[] Scales =
    [
        (1_000_000_000_000_000, "quadrillion"), (1_000_000_000_000, "trillion"),
        (1_000_000_000, "billion"), (1_000_000, "million"), (1_000, "thousand"),
    ];

    public static string SpellOut(long number)
    {
        if (number < 0) return "minus " + SpellOut(-number);
        if (number < 20) return Ones[number];
        if (number < 100) return Tens[number / 10] + (number % 10 == 0 ? "" : "-" + Ones[number % 10]);
        if (number < 1000) return Ones[number / 100] + " hundred" + (number % 100 == 0 ? "" : " " + SpellOut(number % 100));
        // ICU's English rules stop below one quintillion and fall back to grouped digits ("9,223,372,…").
        if (number >= 1_000_000_000_000_000_000) return number.ToString("#,##0", CultureInfo.InvariantCulture);
        foreach (var (value, name) in Scales)
        {
            if (number < value) continue;
            return SpellOut(number / value) + " " + name + (number % value == 0 ? "" : " " + SpellOut(number % value));
        }
        return number.ToString(CultureInfo.InvariantCulture);
    }
}
