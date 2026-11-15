using CommunityToolkit.Mvvm.ComponentModel;
using TOEFLSimulation.Core.Storage;

namespace TOEFLSimulation.Core;

/// <summary>Swift <c>WordsDrillField</c>: subject areas used to find weak fields in Complete the Words.</summary>
public enum WordsDrillField { Humanities, History, Earth, Life, Physical, Social }

public static class WordsDrillFields
{
    public static readonly IReadOnlyList<WordsDrillField> AllCases = Enum.GetValues<WordsDrillField>();

    public static string RawValue(this WordsDrillField field) => field.ToString().ToLowerInvariant();

    public static WordsDrillField? FromRawValue(string? raw) => AllCases.Cast<WordsDrillField?>().FirstOrDefault(f => f!.Value.RawValue() == raw);

    public static string Title(this WordsDrillField field) => field switch
    {
        WordsDrillField.Humanities => "Arts & Language",
        WordsDrillField.History => "History & Anthropology",
        WordsDrillField.Earth => "Earth & Space",
        WordsDrillField.Life => "Life Sciences",
        WordsDrillField.Physical => "Physical Sciences",
        _ => "Social Sciences",
    };
}

/// <summary>Swift <c>WordsDrillParagraph</c>: one Complete the Words paragraph and its 10 blank items in order.</summary>
public sealed record WordsDrillParagraph(string Id, string Title, TOEFLDifficulty Difficulty, WordsDrillField? Field, IReadOnlyList<TOEFLQuestion> Blanks)
{
    public string Passage => Blanks.Count > 0 ? Blanks[0].Passage ?? "" : "";

    /// <summary>All paragraphs of the bank, in authored order.</summary>
    public static IReadOnlyList<WordsDrillParagraph> All(TOEFLQuestionBank bank)
    {
        var order = new List<string>();
        var grouped = new Dictionary<string, List<TOEFLQuestion>>();
        foreach (var item in bank.AllQuestions.Where(q => q.TaskType == TOEFLTaskType.CompleteWords))
        {
            if (!grouped.TryGetValue(item.SetKey, out var list)) { order.Add(item.SetKey); grouped[item.SetKey] = list = []; }
            list.Add(item);
        }
        return order.Select(key =>
        {
            var blanks = grouped[key].OrderBy(q => q.SetIndex ?? 0).ToList();
            var first = blanks[0];
            return new WordsDrillParagraph(key, first.Setting ?? "Paragraph", first.Difficulty, WordsDrillFields.FromRawValue(first.Field), blanks);
        }).ToList();
    }
}

/// <summary>
/// Swift <c>WordsDrillStore</c>: saved Complete the Words progress — every checked attempt (with what was typed), per-paragraph
/// results, and the words most often missed. Stored under <see cref="StorageKeys.WordsDrill"/> in the Mac format.
/// </summary>
public sealed class WordsDrillStore : ObservableObject
{
    public sealed record Attempt
    {
        public int Attempts { get; init; }
        public int BestCorrect { get; init; }
        public int LastCorrect { get; init; }
        public DateTimeOffset LastDate { get; init; }
    }

    /// <summary>One checked paragraph with the letters typed in each blank and which blanks were right.</summary>
    public sealed record AttemptRecord
    {
        public Guid Id { get; init; }
        public string ParagraphID { get; init; } = "";
        public DateTimeOffset Date { get; init; }
        public IReadOnlyList<string>? Entries { get; init; }
        public IReadOnlyList<bool>? Results { get; init; }
        public int Correct { get; init; }
    }

    public sealed record MissedWord
    {
        public string Word { get; init; } = "";
        public string? PartOfSpeech { get; init; }
        public string? Gloss { get; init; }
        public int Misses { get; init; }
        public DateTimeOffset LastMissed { get; init; }

        [System.Text.Json.Serialization.JsonIgnore] public string Id => Word.ToLowerInvariant();
    }

    /// <summary>Accuracy of one subject area from each paragraph's most recent attempt.</summary>
    public sealed record FieldAccuracy(WordsDrillField Field, int Correct, int Total, int Paragraphs)
    {
        public double Accuracy => Total == 0 ? 0 : (double)Correct / Total;
    }

    /// <summary>The saved shape; every key is optional when decoding (the Mac custom init(from:)).</summary>
    private sealed class Saved
    {
        public Dictionary<string, Attempt>? Attempts { get; set; }
        public Dictionary<string, MissedWord>? Missed { get; set; }
        public List<AttemptRecord>? History { get; set; }
    }

    public const int HistoryLimit = 2_000;

    private readonly KeyValueStore store;
    private readonly Func<DateTimeOffset> clock;
    private readonly Random random;
    private readonly Dictionary<string, Attempt> attempts;
    private readonly Dictionary<string, MissedWord> missed;
    private readonly List<AttemptRecord> history;

    public WordsDrillStore(KeyValueStore store, IReadOnlyList<WordsDrillParagraph> paragraphs, Func<DateTimeOffset>? clock = null, Random? random = null)
    {
        this.store = store;
        Paragraphs = paragraphs;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        this.random = random ?? Random.Shared;
        var saved = store.GetJson<Saved>(StorageKeys.WordsDrill);
        attempts = saved?.Attempts ?? [];
        missed = saved?.Missed ?? [];
        history = saved?.History ?? [];
    }

    public IReadOnlyList<WordsDrillParagraph> Paragraphs { get; }

    public WordsDrillParagraph? Paragraph(string id) => Paragraphs.FirstOrDefault(p => p.Id == id);

    public Attempt? AttemptFor(WordsDrillParagraph paragraph) => attempts.GetValueOrDefault(paragraph.Id);

    /// <summary>"Paragraphs practiced".</summary>
    public int CompletedCount => attempts.Count;

    /// <summary>Accuracy across each paragraph's most recent attempt (null before any attempt).</summary>
    public double? RecentAccuracy => attempts.Count == 0 ? null : (double)attempts.Values.Sum(a => a.LastCorrect) / (attempts.Count * 10);

    /// <summary>Missed words, most misses first, then most recently missed.</summary>
    public IReadOnlyList<MissedWord> WordsToReview =>
        missed.Values.OrderByDescending(w => w.Misses).ThenByDescending(w => w.LastMissed).ToList();

    /// <summary>Checked attempts, newest first.</summary>
    public IReadOnlyList<AttemptRecord> History => Enumerable.Reverse(history).ToList();

    /// <summary>The latest attempt in which this word was answered wrong (opens the review sheet from "Words to review").</summary>
    public AttemptRecord? LatestMiss(string word) => History.FirstOrDefault(record =>
    {
        if (record.Results is not { } results || Paragraph(record.ParagraphID)?.Blanks is not { } blanks) return false;
        return blanks.Zip(results).Any(pair => !pair.Second && pair.First.SampleAnswer?.ToLowerInvariant() == word.ToLowerInvariant());
    });

    /// <summary>Saves a checked paragraph. A missed word gains 2 misses; a correct answer removes 1, so it leaves the list
    /// after it is answered correctly twice in a row.</summary>
    public AttemptRecord Record(WordsDrillParagraph paragraph, IReadOnlyList<string> entries, IReadOnlyList<bool> results)
    {
        var now = clock();
        var correct = results.Count(r => r);
        var attempt = attempts.GetValueOrDefault(paragraph.Id) ?? new Attempt { LastDate = now };
        attempts[paragraph.Id] = attempt with
        {
            Attempts = attempt.Attempts + 1,
            BestCorrect = Math.Max(attempt.BestCorrect, correct),
            LastCorrect = correct,
            LastDate = now,
        };
        var record = new AttemptRecord { Id = Guid.NewGuid(), ParagraphID = paragraph.Id, Date = now, Entries = entries.ToList(), Results = results.ToList(), Correct = correct };
        history.Add(record);
        if (history.Count > HistoryLimit) history.RemoveRange(0, history.Count - HistoryLimit);
        foreach (var (blank, isCorrect) in paragraph.Blanks.Zip(results))
        {
            var word = blank.SampleAnswer ?? "";
            var id = word.ToLowerInvariant();
            if (isCorrect)
            {
                if (missed.TryGetValue(id, out var entry))
                {
                    var misses = entry.Misses - 1;
                    if (misses <= 0) missed.Remove(id);
                    else missed[id] = entry with { Misses = misses };
                }
            }
            else
            {
                var entry = missed.GetValueOrDefault(id) ?? new MissedWord { Word = word, PartOfSpeech = blank.PartOfSpeech, Gloss = blank.WordGloss, Misses = 0, LastMissed = now };
                missed[id] = entry with { Misses = Math.Max(entry.Misses, 0) + 2, LastMissed = now };
            }
        }
        Persist();
        OnPropertyChanged(string.Empty);
        return record;
    }

    /// <summary>Accuracy per subject area from each paragraph's most recent attempt, in field order.</summary>
    public IReadOnlyList<FieldAccuracy> AccuracyByField
    {
        get
        {
            var latest = new Dictionary<string, AttemptRecord>();
            foreach (var record in history.Where(r => r.Results is not null)) latest[record.ParagraphID] = record;
            var sums = new Dictionary<WordsDrillField, (int Correct, int Total, int Paragraphs)>();
            foreach (var record in latest.Values)
            {
                if (Paragraph(record.ParagraphID)?.Field is not { } subject) continue;
                var current = sums.GetValueOrDefault(subject);
                sums[subject] = (current.Correct + record.Correct, current.Total + 10, current.Paragraphs + 1);
            }
            return WordsDrillFields.AllCases.Where(sums.ContainsKey)
                .Select(f => new FieldAccuracy(f, sums[f].Correct, sums[f].Total, sums[f].Paragraphs)).ToList();
        }
    }

    /// <summary>The two lowest-accuracy subject areas with at least three practiced paragraphs.</summary>
    public IReadOnlyList<WordsDrillField> WeakFields =>
        AccuracyByField.Where(f => f.Paragraphs >= 3).OrderBy(f => f.Accuracy).Take(2).Select(f => f.Field).ToList();

    /// <summary>Unattempted paragraphs first (random), then the weakest and least recently practiced.</summary>
    public WordsDrillParagraph? Next(string? current, TOEFLDifficulty? difficulty, IReadOnlySet<WordsDrillField>? fields = null)
    {
        var pool = Paragraphs.Where(p => (difficulty is null || p.Difficulty == difficulty) && p.Id != current
            && (fields is null || (p.Field is { } f && fields.Contains(f)))).ToList();
        var fresh = pool.Where(p => !attempts.ContainsKey(p.Id)).ToList();
        if (fresh.Count > 0) return fresh[random.Next(fresh.Count)];
        WordsDrillParagraph? best = null;
        foreach (var paragraph in pool)
        {
            if (best is null) { best = paragraph; continue; }
            var a = attempts[paragraph.Id];
            var b = attempts[best.Id];
            if (a.LastCorrect < b.LastCorrect || (a.LastCorrect == b.LastCorrect && a.LastDate < b.LastDate)) best = paragraph;
        }
        return best;
    }

    private void Persist() => store.SetJson(StorageKeys.WordsDrill, new Saved { Attempts = attempts, Missed = missed, History = history });

    /// <summary>
    /// Swift <c>selfTestIssues()</c>: scores without typed answers never enter the history, and new attempts keep what was
    /// typed, using a throwaway store.
    /// </summary>
    public static IReadOnlyList<string> SelfTestIssues(IReadOnlyList<WordsDrillParagraph> paragraphs)
    {
        if (paragraphs.Count == 0) return ["No drill test domain"];
        var paragraph = paragraphs[0];
        var defaults = KeyValueStore.InMemory();
        var legacy = $$$"""{"attempts":{"{{{paragraph.Id}}}":{"lastCorrect":7,"bestCorrect":7,"attempts":1,"lastDate":814618353.4}},"missed":{}}""";
        defaults.SetRawJson(StorageKeys.WordsDrill, legacy);
        var issues = new List<string>();
        var store = new WordsDrillStore(defaults, paragraphs);
        if (store.History.Count > 0 || store.AttemptFor(paragraph)?.Attempts != 1)
            issues.Add("Score-only drill data was turned into a history record");
        var entries = paragraph.Blanks.Select(b => b.AcceptedTextAnswers.FirstOrDefault() ?? "").ToList();
        store.Record(paragraph, entries, Enumerable.Repeat(true, entries.Count).ToList());
        var reloaded = new WordsDrillStore(defaults, paragraphs);
        if (reloaded.History.Count != 1 || !(reloaded.History[0].Entries ?? []).SequenceEqual(entries) || reloaded.AttemptFor(paragraph)?.Attempts != 2)
            issues.Add("Drill attempts are not saved with their answers");
        return issues;
    }
}

/// <summary>Swift <c>WordsDrillView.LevelFilter</c>.</summary>
public enum WordsDrillLevel { All, Intermediate, Advanced }

/// <summary>
/// The Complete the Words practice page's state (WordsDrillView): filters, the current paragraph, typed entries, and the
/// checked results. Changing <see cref="Level"/> or <see cref="Subject"/> loads the next paragraph, as on the Mac.
/// Call <see cref="Appear"/> when the page is shown.
/// </summary>
public sealed class WordsDrillSession : ObservableObject
{
    /// <summary>Subject picker tags: "all", "weak", or a field raw value.</summary>
    public const string AllSubjects = "all";
    public const string WeakSubjects = "weak";

    private WordsDrillLevel level = WordsDrillLevel.All;
    private string subject = AllSubjects;
    private bool showsAllHistory;

    public WordsDrillSession(WordsDrillStore store) => Store = store;

    public WordsDrillStore Store { get; }

    public WordsDrillLevel Level
    {
        get => level;
        set { if (SetProperty(ref level, value)) Load(NextParagraph(null)); }
    }

    public string Subject
    {
        get => subject;
        set { if (SetProperty(ref subject, value)) Load(NextParagraph(null)); }
    }

    public WordsDrillParagraph? Paragraph { get; private set; }
    public IReadOnlyList<string> Entries { get; private set; } = [];
    /// <summary>Null until Check Answers; then one flag per blank.</summary>
    public IReadOnlyList<bool>? Results { get; private set; }

    public bool ShowsAllHistory
    {
        get => showsAllHistory;
        set { if (SetProperty(ref showsAllHistory, value)) OnPropertyChanged(nameof(VisibleHistory)); }
    }

    /// <summary>The 8 newest attempts, or all of them when "Show all" is on.</summary>
    public IReadOnlyList<WordsDrillStore.AttemptRecord> VisibleHistory => ShowsAllHistory ? Store.History : Store.History.Take(8).ToList();

    public static string LevelTitle(WordsDrillLevel level) => level switch
    {
        WordsDrillLevel.All => "All levels",
        WordsDrillLevel.Intermediate => "Intermediate",
        _ => "Advanced",
    };

    private static TOEFLDifficulty? Difficulty(WordsDrillLevel level) => level switch
    {
        WordsDrillLevel.Intermediate => TOEFLDifficulty.Intermediate,
        WordsDrillLevel.Advanced => TOEFLDifficulty.Advanced,
        _ => null,
    };

    /// <summary>onAppear: loads a paragraph if none is shown.</summary>
    public void Appear()
    {
        if (Paragraph is null) Load(NextParagraph(null));
    }

    public WordsDrillParagraph? NextParagraph(string? current)
    {
        IReadOnlySet<WordsDrillField>? fields = Subject switch
        {
            AllSubjects => null,
            WeakSubjects => Store.WeakFields.Count == 0 ? null : Store.WeakFields.ToHashSet(),
            _ => WordsDrillFields.FromRawValue(Subject) is { } field ? new HashSet<WordsDrillField> { field } : null,
        };
        return Store.Next(current, Difficulty(Level), fields) ?? Store.Next(current, Difficulty(Level)) ?? Store.Paragraphs.FirstOrDefault();
    }

    /// <summary>Shows a paragraph with empty blanks ("Try Again", "Practice Again" from a review).</summary>
    public void Load(WordsDrillParagraph? next)
    {
        Paragraph = next;
        Entries = Enumerable.Repeat("", next?.Blanks.Count ?? 0).ToList();
        Results = null;
        OnPropertyChanged(string.Empty);
    }

    /// <summary>Text field binding for blank <paramref name="index"/>; input is filtered like the Mac field.</summary>
    public void SetEntry(int index, string value, int missing)
    {
        if (index < 0 || index >= Entries.Count) return;
        var entries = Entries.ToList();
        entries[index] = ExamText.SanitizeBlankEntry(value, missing);
        Entries = entries;
        OnPropertyChanged(nameof(Entries));
    }

    /// <summary>"Check Answers" (⌘Return / Ctrl+Enter).</summary>
    public void Check()
    {
        if (Paragraph is not { } paragraph) return;
        var checkedResults = paragraph.Blanks.Select((blank, index) =>
            TOEFLAnswerMatcher.IsCorrect(blank, new TOEFLAnswer { TextEntry = Entries[index] }) ?? false).ToList();
        Results = checkedResults;
        Store.Record(paragraph, Entries, checkedResults);
        OnPropertyChanged(string.Empty);
    }

    /// <summary>"Skip" before checking, "Next Paragraph" after.</summary>
    public void Next() => Load(NextParagraph(Paragraph?.Id));

    /// <summary>"Try Again".</summary>
    public void TryAgain() => Load(Paragraph);

    public string? AttemptSummary => Paragraph is { } p && Store.AttemptFor(p) is { } attempt
        ? $"Best {attempt.BestCorrect}/10 · {attempt.Attempts} attempt{(attempt.Attempts == 1 ? "" : "s")}"
        : null;

    public string HintText => Results is null ? "Return moves to the next blank · ⌘Return checks" : "⌘Return for the next paragraph";

    public string ParagraphsPracticedText => $"{Store.CompletedCount} / {Store.Paragraphs.Count}";
    public string RecentAccuracyText => Store.RecentAccuracy is { } accuracy ? TextUtil.Percent(accuracy) : "—";

    /// <summary>"Weak areas: X and Y. Choose “Focus on weak areas” to practice them first." (null without weak fields).</summary>
    public string? WeakAreasText => Store.WeakFields.Count == 0 ? null
        : $"Weak areas: {string.Join(" and ", Store.WeakFields.Select(f => f.Title()))}. Choose “Focus on weak areas” to practice them first.";

    /// <summary>Score color tiers used on the page: teal ≥ 8, gold ≥ 5, otherwise red.</summary>
    public static string ScoreTier(int correct) => correct >= 8 ? "teal" : (correct >= 5 ? "gold" : "red");

    /// <summary>Accuracy color tiers: teal ≥ 0.8, gold ≥ 0.65, otherwise red.</summary>
    public static string AccuracyTier(double accuracy) => accuracy >= 0.8 ? "teal" : (accuracy >= 0.65 ? "gold" : "red");
}

/// <summary>One row of the word explanations list (WordExplanationsView).</summary>
public sealed record WordExplanation(int Number, TOEFLQuestion Blank, string Kept, string Missing, bool? IsCorrect, string Typed)
{
    public string Word => Kept + Missing;

    /// <summary>"(blank)" or "you typed …" for a wrong answer, otherwise null.</summary>
    public string? TypedNote => IsCorrect == false ? (Typed.Length == 0 ? "(blank)" : $"you typed {Kept}{Typed}") : null;

    public static IReadOnlyList<WordExplanation> For(WordsDrillParagraph paragraph, IReadOnlyList<string>? entries, IReadOnlyList<bool>? results) =>
        paragraph.Blanks.Select((blank, index) =>
        {
            var word = blank.SampleAnswer ?? "";
            var missing = blank.AcceptedTextAnswers.FirstOrDefault() ?? "";
            var keptLength = Math.Max(0, word.Length - missing.Length);
            return new WordExplanation(index + 1, blank, word[..keptLength], missing,
                results is not null && index < results.Count ? results[index] : null,
                entries is not null && index < entries.Count ? entries[index] : "");
        }).ToList();

    /// <summary>"7 / 10 correct" heading and its note.</summary>
    public static string ScoreTitle(int correct) => $"{correct} / 10 correct";
    public static string ScoreNote(int correct) => correct == 10 ? "Perfect paragraph." : "Review the explanations for the missed words.";
}
