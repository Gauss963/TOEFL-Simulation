namespace TOEFLSimulation.Core;

/// <summary>
/// Counts misspelled words in a response. The Mac app uses the system spell checker (NSSpellChecker, en_US);
/// the Windows app should implement this with the Windows spell-checking API. Count the way the Mac loop does:
/// each flagged range counts once, scanning left to right.
/// </summary>
public interface ISpellChecker
{
    int CountMisspellings(string text);
}

/// <summary>A spell checker that never flags anything (used when no platform checker is available).</summary>
public sealed class NoSpellChecker : ISpellChecker
{
    public static NoSpellChecker Instance { get; } = new();
    public int CountMisspellings(string text) => 0;
}

/// <summary>
/// Swift <c>WritingRubric</c>: scores Write an Email and Write for an Academic Discussion on the official 0–5 rubrics
/// from observable features (task completion, elaboration, language range, mechanics, conventions, relevance).
/// </summary>
public static class WritingRubric
{
    private static readonly HashSet<string> StopWords =
    [
        "the", "a", "an", "and", "or", "but", "of", "to", "in", "on", "at", "for", "with", "is", "are", "was", "were",
        "be", "been", "it", "this", "that", "these", "those", "i", "you", "we", "they", "he", "she", "my", "your",
        "our", "their", "as", "by", "from", "so", "if", "not", "do", "does", "did", "have", "has", "had", "will",
        "would", "can", "could", "should", "about", "what", "which", "who", "how", "why", "more", "most", "some",
    ];

    private static readonly string[] Connectors =
    [
        "because", "however", "therefore", "for example", "for instance", "although", "while", "also", "in addition",
        "moreover", "as a result", "on the other hand", "first", "second", "finally", "since", "unless", "instead",
        "in contrast", "consequently", "that is why", "for this reason",
    ];

    public static (int Score, string Note) Score(TOEFLQuestion question, string response, ISpellChecker? spellChecker = null)
    {
        spellChecker ??= NoSpellChecker.Instance;
        var text = TextUtil.TrimWhitespace(response);
        var words = Tokens(text);
        if (words.Count < 5) return (0, "Blank or too short to evaluate.");

        var promptText = string.Join(" ", question.Prompt, question.Passage ?? "", question.Context ?? "");
        if (CopiedRatio(words, Tokens(promptText)) > 0.6) return (0, "The response mostly repeats the prompt.");

        var isEmail = question.TaskType == TOEFLTaskType.WriteEmail;
        var target = isEmail ? 110.0 : 120.0;
        var sentences = TextUtil.Split(text, c => c is '.' or '!' or '?').Select(s => Tokens(s).Count).Where(n => n > 2).ToList();
        var content = words.Where(w => !StopWords.Contains(w) && TextUtil.CharacterCount(w) > 2).ToList();
        var diversity = (double)content.Distinct().Count() / Math.Max(1, content.Count);
        var averageLength = (double)content.Sum(TextUtil.CharacterCount) / Math.Max(1, content.Count);
        var lowered = text.ToLowerInvariant();
        var connectorCount = Connectors.Count(c => lowered.Contains(c, StringComparison.Ordinal));
        double variety;
        if (sentences.Count <= 2)
        {
            variety = 0.3;
        }
        else
        {
            var mean = (double)sentences.Sum() / sentences.Count;
            var deviation = Math.Sqrt(sentences.Select(n => Math.Pow(n - mean, 2)).Sum() / sentences.Count);
            variety = Math.Min(1, deviation / 6);
        }
        var misspelled = spellChecker.CountMisspellings(text);
        var errorRate = (double)misspelled / words.Count * 100;

        var promptKeys = Tokens(promptText).Where(w => !StopWords.Contains(w) && TextUtil.CharacterCount(w) > 3).ToHashSet();
        var relevance = Math.Min(1, content.ToHashSet().Intersect(promptKeys).Count() / 4.0);

        double taskCompletion;
        double conventions;
        if (isEmail)
        {
            var hasGreeting = new[] { "dear", "hello", "hi ", "good morning", "good afternoon" }.Any(g =>
                lowered.StartsWith(g, StringComparison.Ordinal) || lowered.Contains("\n" + g, StringComparison.Ordinal)
                || TextUtil.PrefixCharacters(lowered, 40).Contains(g, StringComparison.Ordinal));
            var ending = TextUtil.SuffixCharacters(lowered, 120);
            var hasClosing = new[] { "sincerely", "best", "regards", "thank you", "thanks", "warmly" }.Any(c => ending.Contains(c, StringComparison.Ordinal));
            conventions = (hasGreeting ? 0.5 : 0) + (hasClosing ? 0.5 : 0);
            var politeness = new[] { "please", "could you", "would you", "i would appreciate", "thank" }.Count(p => lowered.Contains(p, StringComparison.Ordinal));
            taskCompletion = Math.Min(1, sentences.Count / 6.0) * 0.7 + Math.Min(1, politeness / 2.0) * 0.3;
        }
        else
        {
            var engagesPeers = question.Passage is { } passage && TextUtil.Split(passage, c => c == '\n')
                .Select(line => TextUtil.Split(line, c => c == ':').FirstOrDefault())
                .OfType<string>()
                .Any(name => TextUtil.CharacterCount(name) < 20 && lowered.Contains(name.ToLowerInvariant(), StringComparison.Ordinal));
            var stance = new[] { "i agree", "i disagree", "i believe", "i think", "in my opinion", "i support", "i would argue" }
                .Any(s => lowered.Contains(s, StringComparison.Ordinal));
            taskCompletion = (stance ? 0.5 : 0.2) + (engagesPeers ? 0.3 : 0) + Math.Min(0.2, sentences.Count / 25.0);
            conventions = 1;
        }

        var elaboration = Math.Min(1, words.Count / target);
        var language = Math.Min(1, 0.35 * Math.Min(1, diversity / 0.7) + 0.25 * Math.Min(1, (averageLength - 4) / 2.5)
            + 0.2 * variety + 0.2 * Math.Min(1, connectorCount / 3.0));
        var mechanics = Math.Max(0, 1 - errorRate / 8);

        var raw = 5 * (0.25 * taskCompletion + 0.25 * elaboration + 0.25 * language + 0.15 * mechanics + 0.05 * conventions + 0.05 * relevance);
        if (words.Count < 30) raw = Math.Min(raw, 1.5);
        else if (words.Count < 60) raw = Math.Min(raw, 2.5);
        else if (words.Count < 90) raw = Math.Min(raw, 3.5);
        if (errorRate > 10) raw = Math.Min(raw, 2.5);
        else if (errorRate > 5) raw = Math.Min(raw, 3.5);
        if (relevance == 0) raw = Math.Min(raw, 2);
        var score = Math.Max(1, Math.Min(5, (int)TextUtil.SwiftRound(raw)));

        var descriptor = score switch
        {
            5 => "Fully successful: well elaborated, varied, and nearly error-free.",
            4 => "Generally successful: adequate elaboration with few errors.",
            3 => "Partially successful: some elaboration missing or unclear; noticeable errors.",
            2 => "Mostly unsuccessful: limited elaboration and accumulating errors.",
            _ => "Unsuccessful: very limited, unclear language.",
        };
        return (score, $"{descriptor} {words.Count} words · {sentences.Count} sentences · {misspelled} spelling errors · {connectorCount} connectors.");
    }

    private static List<string> Tokens(string text) => TextUtil.Split(text.ToLowerInvariant(), c => !char.IsLetter(c) && c != '\'');

    private static double CopiedRatio(IReadOnlyList<string> response, IReadOnlyList<string> prompt)
    {
        if (response.Count < 4) return 0;
        static HashSet<string> Grams(IReadOnlyList<string> words)
        {
            var set = new HashSet<string>();
            for (var i = 0; i + 4 <= words.Count; i++) set.Add(string.Join(" ", words.Skip(i).Take(4)));
            return set;
        }
        var responseGrams = Grams(response);
        var promptGrams = Grams(prompt);
        return (double)responseGrams.Count(promptGrams.Contains) / Math.Max(1, responseGrams.Count);
    }
}
