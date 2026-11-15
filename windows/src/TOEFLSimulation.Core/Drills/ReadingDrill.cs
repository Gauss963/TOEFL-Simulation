using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using TOEFLSimulation.Core.Storage;

namespace TOEFLSimulation.Core;

/// <summary>Swift <c>ReadingDrillSet</c>: one Read in Daily Life or Read an Academic Passage set.</summary>
public sealed record ReadingDrillSet(string Id, TOEFLTaskType Task, TOEFLDifficulty Difficulty, string Passage, IReadOnlyList<TOEFLQuestion> Questions)
{
    /// <summary>The passage's first line.</summary>
    public string Title => Passage.Split('\n')[0];

    /// <summary>The passage without its first line, trimmed.</summary>
    public string Body => string.Join("\n", Passage.Split('\n').Skip(1)).Trim();

    /// <summary>All reading sets of the bank, in authored order.</summary>
    public static IReadOnlyList<ReadingDrillSet> All(TOEFLQuestionBank bank)
    {
        var order = new List<string>();
        var grouped = new Dictionary<string, List<TOEFLQuestion>>();
        foreach (var item in bank.AllQuestions.Where(q => q.TaskType is TOEFLTaskType.ReadDailyLife or TOEFLTaskType.ReadAcademicPassage))
        {
            if (!grouped.TryGetValue(item.SetKey, out var list)) { order.Add(item.SetKey); grouped[item.SetKey] = list = []; }
            list.Add(item);
        }
        return order.Select(key =>
        {
            var questions = grouped[key].OrderBy(q => q.SetIndex ?? 0).ToList();
            return new ReadingDrillSet(key, questions[0].TaskType, questions[0].Difficulty, questions[0].Passage ?? "", questions);
        }).ToList();
    }
}

/// <summary>
/// Swift <c>ReadingDrillStore</c>: every checked Reading practice set with the options chosen, accuracy by question type, and
/// the next-set rule. Stored under <see cref="StorageKeys.ReadingDrill"/>.
/// </summary>
public sealed class ReadingDrillStore : ObservableObject
{
    public sealed record Record
    {
        public Guid Id { get; init; }
        public string SetID { get; init; } = "";
        public DateTimeOffset Date { get; init; }
        /// <summary>The chosen option per question (null when unanswered; encoded as JSON null like Swift).</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public IReadOnlyList<string?> Choices { get; init; } = [];
        public IReadOnlyList<bool> Results { get; init; } = [];

        [JsonIgnore] public int Correct => Results.Count(r => r);
    }

    public sealed record TypeAccuracy(ReadingQuestionType Type, int Correct, int Total)
    {
        public double Accuracy => Total == 0 ? 0 : (double)Correct / Total;
    }

    public const int HistoryLimit = 2_000;

    private readonly KeyValueStore store;
    private readonly Func<DateTimeOffset> clock;
    private readonly Random random;
    private readonly List<Record> records;

    public ReadingDrillStore(KeyValueStore store, IReadOnlyList<ReadingDrillSet> sets, Func<DateTimeOffset>? clock = null, Random? random = null)
    {
        this.store = store;
        Sets = sets;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        this.random = random ?? Random.Shared;
        records = store.GetJson<List<Record>>(StorageKeys.ReadingDrill) ?? [];
    }

    public IReadOnlyList<ReadingDrillSet> Sets { get; }

    public ReadingDrillSet? Set(string id) => Sets.FirstOrDefault(s => s.Id == id);

    /// <summary>Newest first.</summary>
    public IReadOnlyList<Record> History => Enumerable.Reverse(records).ToList();

    public int PracticedSets => records.Select(r => r.SetID).Distinct().Count();

    private Dictionary<string, Record> LatestBySet()
    {
        var latest = new Dictionary<string, Record>();
        foreach (var record in records) latest[record.SetID] = record;
        return latest;
    }

    public double? RecentAccuracy
    {
        get
        {
            var latest = LatestBySet().Values.ToList();
            var total = latest.Sum(r => r.Results.Count);
            return total == 0 ? null : (double)latest.Sum(r => r.Correct) / total;
        }
    }

    /// <summary>Accuracy per question type from each set's most recent attempt, in type order.</summary>
    public IReadOnlyList<TypeAccuracy> AccuracyByType
    {
        get
        {
            var sums = new Dictionary<ReadingQuestionType, (int Correct, int Total)>();
            foreach (var record in LatestBySet().Values)
            {
                if (Set(record.SetID) is not { } set) continue;
                foreach (var (question, isCorrect) in set.Questions.Zip(record.Results))
                {
                    if (question.ReadingType() is not { } type) continue;
                    var current = sums.GetValueOrDefault(type);
                    sums[type] = (current.Correct + (isCorrect ? 1 : 0), current.Total + 1);
                }
            }
            return ReadingQuestionTypes.AllCases.Where(sums.ContainsKey).Select(t => new TypeAccuracy(t, sums[t].Correct, sums[t].Total)).ToList();
        }
    }

    /// <summary>The two lowest-accuracy question types with at least four answered questions.</summary>
    public IReadOnlyList<ReadingQuestionType> WeakTypes =>
        AccuracyByType.Where(t => t.Total >= 4).OrderBy(t => t.Accuracy).Take(2).Select(t => t.Type).ToList();

    public Record Add(ReadingDrillSet set, IReadOnlyList<string?> choices, IReadOnlyList<bool> results)
    {
        var record = new Record { Id = Guid.NewGuid(), SetID = set.Id, Date = clock(), Choices = choices.ToList(), Results = results.ToList() };
        records.Add(record);
        if (records.Count > HistoryLimit) records.RemoveRange(0, records.Count - HistoryLimit);
        store.SetJson(StorageKeys.ReadingDrill, records);
        OnPropertyChanged(string.Empty);
        return record;
    }

    public Record? LastResult(ReadingDrillSet set) => LatestBySet().GetValueOrDefault(set.Id);

    /// <summary>Unpracticed sets first (those with the most questions of <paramref name="focus"/> types first), then the
    /// weakest and least recently practiced.</summary>
    public ReadingDrillSet? Next(string? current, TOEFLTaskType? task, TOEFLDifficulty? difficulty, IReadOnlySet<ReadingQuestionType>? focus)
    {
        var pool = Sets.Where(s => s.Id != current && (task is null || s.Task == task) && (difficulty is null || s.Difficulty == difficulty)).ToList();
        var latest = LatestBySet();
        int FocusCount(ReadingDrillSet set) => focus is null ? 0 : set.Questions.Count(q => q.ReadingType() is { } t && focus.Contains(t));
        var fresh = pool.Where(s => !latest.ContainsKey(s.Id)).ToList();
        Shuffle(fresh);
        ReadingDrillSet? pick = null;
        foreach (var set in fresh)
            if (pick is null || FocusCount(pick) < FocusCount(set)) pick = set;
        if (pick is not null) return pick;
        foreach (var set in pool)
        {
            if (pick is null) { pick = set; continue; }
            var a = latest[set.Id];
            var b = latest[pick.Id];
            var left = (double)a.Correct / Math.Max(1, a.Results.Count);
            var right = (double)b.Correct / Math.Max(1, b.Results.Count);
            if (left < right || (left == right && a.Date < b.Date)) pick = set;
        }
        return pick;
    }

    private void Shuffle<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

/// <summary>Swift <c>ReadingDrillView.TaskFilter</c>.</summary>
public enum ReadingTaskFilter { All, Daily, Academic }

/// <summary>
/// The Reading Practice page's state (ReadingDrillView): filters, the current set, chosen options, and checked results.
/// Changing <see cref="TaskFilter"/>, <see cref="Level"/>, or <see cref="FocusWeak"/> loads the next set. Call <see cref="Appear"/>.
/// </summary>
public sealed class ReadingDrillSession : ObservableObject
{
    /// <summary>Level picker tags: "all", "intermediate", "advanced".</summary>
    public const string AllLevels = "all";

    private ReadingTaskFilter taskFilter = ReadingTaskFilter.All;
    private string level = AllLevels;
    private bool focusWeak;
    private bool showsAllHistory;

    public ReadingDrillSession(ReadingDrillStore store) => Store = store;

    public ReadingDrillStore Store { get; }

    public ReadingTaskFilter TaskFilter
    {
        get => taskFilter;
        set { if (SetProperty(ref taskFilter, value)) Load(NextSet(null)); }
    }

    public string Level
    {
        get => level;
        set { if (SetProperty(ref level, value)) Load(NextSet(null)); }
    }

    public bool FocusWeak
    {
        get => focusWeak;
        set { if (SetProperty(ref focusWeak, value)) Load(NextSet(null)); }
    }

    public bool ShowsAllHistory
    {
        get => showsAllHistory;
        set { if (SetProperty(ref showsAllHistory, value)) OnPropertyChanged(nameof(VisibleHistory)); }
    }

    public IReadOnlyList<ReadingDrillStore.Record> VisibleHistory => ShowsAllHistory ? Store.History : Store.History.Take(8).ToList();

    public ReadingDrillSet? Current { get; private set; }
    public IReadOnlyList<string?> Choices { get; private set; } = [];
    public IReadOnlyList<bool>? Results { get; private set; }

    public static string TaskFilterTitle(ReadingTaskFilter filter) => filter switch
    {
        ReadingTaskFilter.All => "All tasks",
        ReadingTaskFilter.Daily => "Daily Life",
        _ => "Academic Passage",
    };

    private static TOEFLTaskType? Task(ReadingTaskFilter filter) => filter switch
    {
        ReadingTaskFilter.Daily => TOEFLTaskType.ReadDailyLife,
        ReadingTaskFilter.Academic => TOEFLTaskType.ReadAcademicPassage,
        _ => null,
    };

    public void Appear()
    {
        if (Current is null) Load(NextSet(null));
    }

    public ReadingDrillSet? NextSet(string? current)
    {
        IReadOnlySet<ReadingQuestionType>? focus = FocusWeak && Store.WeakTypes.Count > 0 ? Store.WeakTypes.ToHashSet() : null;
        var difficulty = TOEFLDifficultyExtensions.FromRawValue(Level);
        return Store.Next(current, Task(TaskFilter), difficulty, focus)
            ?? Store.Next(current, Task(TaskFilter), null, focus)
            ?? Store.Sets.FirstOrDefault();
    }

    public void Load(ReadingDrillSet? set)
    {
        Current = set;
        Choices = Enumerable.Repeat<string?>(null, set?.Questions.Count ?? 0).ToList();
        Results = null;
        OnPropertyChanged(string.Empty);
    }

    /// <summary>Selecting an option (ignored after checking).</summary>
    public void Choose(int questionIndex, string choiceID)
    {
        if (Results is not null || questionIndex < 0 || questionIndex >= Choices.Count) return;
        var choices = Choices.ToList();
        choices[questionIndex] = choiceID;
        Choices = choices;
        OnPropertyChanged(nameof(Choices));
        OnPropertyChanged(nameof(AnsweredText));
    }

    /// <summary>"Check Answers".</summary>
    public void Check()
    {
        if (Current is not { } set) return;
        var checkedResults = set.Questions.Select((q, i) => Choices[i] == q.CorrectChoiceID).ToList();
        Results = checkedResults;
        Store.Add(set, Choices, checkedResults);
        OnPropertyChanged(string.Empty);
    }

    /// <summary>"Skip" / "Next Set".</summary>
    public void Next() => Load(NextSet(Current?.Id));

    /// <summary>"Try Again".</summary>
    public void TryAgain() => Load(Current);

    public string? TaskTitle => Current is null ? null : Current.Task == TOEFLTaskType.ReadDailyLife ? "Read in Daily Life" : "Read an Academic Passage";

    public string? LastTimeText => Current is { } set && Store.LastResult(set) is { } last ? $"Last time {last.Correct}/{last.Results.Count}" : null;

    public string AnsweredText => $"{Choices.Count(c => c is not null)} of {Current?.Questions.Count ?? 0} answered";

    public string? ScoreText => Results is { } results ? $"{results.Count(r => r)} / {results.Count} correct" : null;

    /// <summary>Score color: teal when all correct, gold at ≥ 60%, otherwise red.</summary>
    public string? ScoreTier => Results is { Count: > 0 } results
        ? (results.All(r => r) ? "teal" : ((double)results.Count(r => r) / results.Count >= 0.6 ? "gold" : "red"))
        : null;

    public string SetsPracticedText => $"{Store.PracticedSets} / {Store.Sets.Count}";
    public string RecentAccuracyText => Store.RecentAccuracy is { } accuracy ? TextUtil.Percent(accuracy) : "—";

    /// <summary>"Weakest: {title} — {tip}" for the weakest type, or null.</summary>
    public string? WeakestText => Store.WeakTypes.Count > 0 ? $"Weakest: {Store.WeakTypes[0].Title()} — {Store.WeakTypes[0].Tip()}" : null;

    /// <summary>History row subtitle prefix: "Daily Life" or "Academic".</summary>
    public string HistoryTaskLabel(ReadingDrillStore.Record record) => Store.Set(record.SetID)?.Task == TOEFLTaskType.ReadDailyLife ? "Daily Life" : "Academic";

    /// <summary>Option background after checking: "key" (correct option), "wrong" (selected and wrong), "selected" (before checking), or "none".</summary>
    public static string OptionState(TOEFLQuestion question, string choiceID, string? selected, bool isChecked)
    {
        var isSelected = selected == choiceID;
        if (!isChecked) return isSelected ? "selected" : "none";
        if (choiceID == question.CorrectChoiceID) return "key";
        return isSelected ? "wrong" : "none";
    }
}
