using System.Text.Json;
using TOEFLSimulation.Core.Audio;
using TOEFLSimulation.Core.Json;
using TOEFLSimulation.Core.Storage;

namespace TOEFLSimulation.Core;

/// <summary>
/// Swift <c>TOEFLQuestionBank</c>: the bundled items and the form assembler that follows the ETS TOEFL iBT 2026 blueprint.
/// Reading and Listening are two-stage adaptive: a router module, then a lower or upper module. Scored counts per module
/// follow the ETS technical manual (Reading 20 + 15, Listening 20 + 15); unscored pretest items bring every path to the
/// published 50 and 47 items. Sets the user has seen (exposure history) return only after the pool rotates.
/// </summary>
public sealed class TOEFLQuestionBank
{
    public const string ResourceFileName = "TOEFL2026Bank.json";

    /// <summary>Remembers enough sets that a full rotation of each task pool passes before an item returns.</summary>
    public const int ExposureLimit = 1_500;

    private readonly Dictionary<string, TOEFLQuestion> byID;
    private readonly KeyValueStore? exposureStore;

    public TOEFLQuestionBank(IReadOnlyList<TOEFLQuestion> questions, KeyValueStore? exposureStore = null)
    {
        AllQuestions = questions;
        this.exposureStore = exposureStore;
        byID = [];
        foreach (var question in questions) byID.TryAdd(question.Id, question);
        Sets = BuildSets(questions);
    }

    /// <summary>Loads <c>TOEFL2026Bank.json</c> from the Resources directory. Throws if it is missing or does not decode.</summary>
    public static TOEFLQuestionBank Load(string resourcesDirectory, KeyValueStore? exposureStore = null) =>
        new(Decode(File.ReadAllBytes(Path.Combine(resourcesDirectory, ResourceFileName))), exposureStore);

    /// <summary>Decodes the bank exactly as the Swift <c>Codable</c> types do (all non-optional keys required).</summary>
    public static IReadOnlyList<TOEFLQuestion> Decode(ReadOnlySpan<byte> json)
    {
        try
        {
            return SwiftJson.Deserialize<List<TOEFLQuestion>>(json) ?? throw new InvalidDataException("TOEFL2026Bank.json is empty.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException($"Could not decode TOEFL2026Bank.json: {error.Message}", error);
        }
    }

    public IReadOnlyList<TOEFLQuestion> AllQuestions { get; }

    /// <summary>Items grouped by shared stimulus (<c>setID ?? id</c>), in authored order, each sorted by setIndex.</summary>
    public IReadOnlyList<IReadOnlyList<TOEFLQuestion>> Sets { get; }

    public TOEFLQuestion? Question(string id) => byID.GetValueOrDefault(id);

    /// <summary>Validation turns this off so sampled forms do not depend on, or change, the user's history.</summary>
    public bool UsesExposureHistory { get; set; } = true;

    private static List<IReadOnlyList<TOEFLQuestion>> BuildSets(IReadOnlyList<TOEFLQuestion> questions)
    {
        var order = new List<string>();
        var grouped = new Dictionary<string, List<TOEFLQuestion>>();
        foreach (var question in questions)
        {
            var key = question.SetKey;
            if (!grouped.TryGetValue(key, out var list)) { order.Add(key); grouped[key] = list = []; }
            list.Add(question);
        }
        return order.Select(key => (IReadOnlyList<TOEFLQuestion>)grouped[key].OrderBy(q => q.SetIndex ?? 0).ToList()).ToList();
    }

    // MARK: - Blueprint

    /// <summary>A block of sets to draw: <c>Sizes</c> restricts set sizes (e.g. 2- and 3-item Daily Life sets).</summary>
    internal sealed record Draw(TOEFLTaskType Task, IReadOnlyList<int?> Sizes, bool Unscored = false)
    {
        public static Draw Sets(TOEFLTaskType task, int sets, bool unscored = false) => new(task, Enumerable.Repeat<int?>(null, sets).ToList(), unscored);
        public static Draw WithSizes(TOEFLTaskType task, params int[] sizes) => new(task, sizes.Select(s => (int?)s).ToList());
    }

    internal enum Stage { Router, Lower, Upper, Linear }

    // Unscored items sit in the router (Module 1): the technical manual (RR-25-12, II-7) says the router module may
    // include unscored tryout questions (Reading 15, Listening 12), and the blueprint's second-module timings
    // (Reading 9 min; Listening 7 min lower, 11 min upper) and per-task item ranges only fit 15 scored items there.
    internal static IReadOnlyList<Draw> Plan(TOEFLSectionKind kind, Stage stage) => (kind, stage) switch
    {
        (TOEFLSectionKind.Reading, Stage.Router) =>
        [
            Draw.Sets(TOEFLTaskType.CompleteWords, 1), Draw.Sets(TOEFLTaskType.CompleteWords, 1, unscored: true),
            Draw.WithSizes(TOEFLTaskType.ReadDailyLife, 2, 3),
            Draw.Sets(TOEFLTaskType.ReadAcademicPassage, 1), Draw.Sets(TOEFLTaskType.ReadAcademicPassage, 1, unscored: true),
        ],
        (TOEFLSectionKind.Reading, Stage.Lower) =>
        [
            Draw.Sets(TOEFLTaskType.CompleteWords, 1),
            Draw.WithSizes(TOEFLTaskType.ReadDailyLife, 2, 3),
        ],
        (TOEFLSectionKind.Reading, Stage.Upper) =>
        [
            Draw.Sets(TOEFLTaskType.CompleteWords, 1),
            Draw.Sets(TOEFLTaskType.ReadAcademicPassage, 1),
        ],
        (TOEFLSectionKind.Listening, Stage.Router) =>
        [
            Draw.Sets(TOEFLTaskType.ListenChooseResponse, 8), Draw.Sets(TOEFLTaskType.ListenChooseResponse, 4, unscored: true),
            Draw.Sets(TOEFLTaskType.ListenConversation, 2), Draw.Sets(TOEFLTaskType.ListenConversation, 1, unscored: true),
            Draw.Sets(TOEFLTaskType.ListenAnnouncement, 2), Draw.Sets(TOEFLTaskType.ListenAnnouncement, 1, unscored: true),
            Draw.Sets(TOEFLTaskType.ListenAcademicTalk, 1), Draw.Sets(TOEFLTaskType.ListenAcademicTalk, 1, unscored: true),
        ],
        (TOEFLSectionKind.Listening, Stage.Lower) =>
        [
            Draw.Sets(TOEFLTaskType.ListenChooseResponse, 7),
            Draw.Sets(TOEFLTaskType.ListenConversation, 2),
            Draw.Sets(TOEFLTaskType.ListenAnnouncement, 2),
        ],
        (TOEFLSectionKind.Listening, Stage.Upper) =>
        [
            Draw.Sets(TOEFLTaskType.ListenChooseResponse, 3),
            Draw.Sets(TOEFLTaskType.ListenConversation, 2),
            Draw.Sets(TOEFLTaskType.ListenAcademicTalk, 2),
        ],
        (TOEFLSectionKind.Writing, _) =>
            [Draw.Sets(TOEFLTaskType.BuildSentence, 10), Draw.Sets(TOEFLTaskType.WriteEmail, 1), Draw.Sets(TOEFLTaskType.AcademicDiscussion, 1)],
        (TOEFLSectionKind.Speaking, _) =>
            [Draw.Sets(TOEFLTaskType.ListenRepeat, 1), Draw.Sets(TOEFLTaskType.Interview, 1)],
        _ => [],
    };

    private static HashSet<TOEFLDifficulty> Difficulties(Stage stage) => stage switch
    {
        Stage.Router or Stage.Linear => [TOEFLDifficulty.Intermediate],
        Stage.Lower => [TOEFLDifficulty.Foundational, TOEFLDifficulty.Intermediate],
        _ => [TOEFLDifficulty.Advanced],
    };

    // MARK: - Forms

    /// <summary>All four sections of a full-length test (Reading and Listening start with a provisional lower module).</summary>
    public IReadOnlyList<TOEFLExamSection> FullSections(ulong? seed = null)
    {
        var value = seed ?? SeededGenerator.RandomSeed();
        return TOEFLSectionKindExtensions.AllCases.Select(kind => Section(kind, value, null)).ToList();
    }

    /// <param name="kind">The section.</param>
    /// <param name="seed">Form seed (random when null).</param>
    /// <param name="recent">Overrides the stored exposure history (used by the self-test).</param>
    public TOEFLExamSection PracticeSection(TOEFLSectionKind kind, ulong? seed = null, IReadOnlyList<string>? recent = null) =>
        Section(kind, seed ?? SeededGenerator.RandomSeed(), recent);

    private TOEFLExamSection Section(TOEFLSectionKind kind, ulong seed, IReadOnlyList<string>? recent)
    {
        var id = $"{kind.RawValue()}-section";
        if (kind.IsAdaptive())
        {
            var router = Questions(Plan(kind, Stage.Router), Stage.Router, MixedSeed(seed, $"{kind.RawValue()}-router"), null, recent);
            // A provisional lower module keeps the item count visible until routing decides the real one.
            var provisional = ModuleQuestions(kind, AdaptiveRoute.Lower, seed, router.Select(q => q.Id).ToHashSet(), recent);
            return new TOEFLExamSection(id, kind, kind.OfficialMinutes() * 60, [.. router, .. provisional], router.Count, null);
        }
        return new TOEFLExamSection(id, kind, kind.OfficialMinutes() * 60,
            Questions(Plan(kind, Stage.Linear), Stage.Linear, MixedSeed(seed, $"{kind.RawValue()}-linear"), null, recent), null, null);
    }

    /// <summary>The second module for <paramref name="route"/>, avoiding the sets of <paramref name="excluding"/>.</summary>
    public IReadOnlyList<TOEFLQuestion> ModuleQuestions(TOEFLSectionKind kind, AdaptiveRoute route, ulong? seed = null,
        IReadOnlySet<string>? excluding = null, IReadOnlyList<string>? recent = null)
    {
        var stage = route == AdaptiveRoute.Lower ? Stage.Lower : Stage.Upper;
        return Questions(Plan(kind, stage), stage, MixedSeed(seed ?? SeededGenerator.RandomSeed(), $"{kind.RawValue()}-{route.RawValue()}"), excluding, recent);
    }

    private List<TOEFLQuestion> Questions(IReadOnlyList<Draw> plan, Stage stage, ulong seed, IReadOnlySet<string>? excludedIDs, IReadOnlyList<string>? recentOverride)
    {
        // Set key → position in the history (higher is more recent).
        var recent = new Dictionary<string, int>();
        var history = recentOverride ?? RecentSetKeys;
        for (var i = 0; i < history.Count; i++)
            recent[history[i]] = recent.TryGetValue(history[i], out var existing) ? Math.Max(existing, i) : i;
        var generator = new SeededGenerator(seed);
        var usedSets = new HashSet<string>();
        foreach (var id in excludedIDs ?? new HashSet<string>())
            if (byID.TryGetValue(id, out var excluded)) usedSets.Add(excluded.SetKey);
        var usedTopics = new HashSet<string>();
        // Draws of the same task are collected first, then interleaved so unscored sets are not predictable.
        var blocks = new List<(TOEFLTaskType Task, List<IReadOnlyList<TOEFLQuestion>> Drawn)>();
        var preferred = Difficulties(stage);
        foreach (var draw in plan)
        {
            var drawn = new List<IReadOnlyList<TOEFLQuestion>>();
            foreach (var size in draw.Sizes)
            {
                var pool = Sets.Where(set => set.Count > 0 && set[0].TaskType == draw.Task
                    && !usedSets.Contains(set[0].SetKey) && (size is null || set.Count == size)).ToList();
                // Unseen sets come first (the module's difficulty, then neighboring levels); seen sets return least
                // recently seen first, so an item reappears only after the pool rotates.
                bool IsPreferred(IReadOnlyList<TOEFLQuestion> set) => preferred.Contains(set[0].Difficulty);
                var unseen = pool.Where(set => !recent.ContainsKey(set[0].SetKey)).ToList();
                var seen = generator.Shuffled(pool.Where(set => recent.ContainsKey(set[0].SetKey)))
                    .OrderBy(set => recent[set[0].SetKey]).ToList();
                var ranked = LongFirst(generator.Shuffled(unseen.Where(IsPreferred)))
                    .Concat(LongFirst(generator.Shuffled(unseen.Where(set => !IsPreferred(set)))))
                    .Concat(seen.Where(IsPreferred))
                    .Concat(seen.Where(set => !IsPreferred(set)))
                    .ToList();
                var chosen = ranked.FirstOrDefault(set => !usedTopics.Contains(TopicKey(set[0]))) ?? ranked.FirstOrDefault()
                    ?? throw new InvalidOperationException($"Not enough {draw.Task.Title()} sets in the bank");
                if (draw.Unscored) chosen = chosen.Select(q => q with { IsScored = false }).ToList();
                usedSets.Add(chosen[0].SetKey);
                usedTopics.Add(TopicKey(chosen[0]));
                drawn.Add(chosen);
            }
            var index = blocks.FindIndex(b => b.Task == draw.Task);
            if (index >= 0) blocks[index].Drawn.AddRange(drawn);
            else blocks.Add((draw.Task, drawn));
        }
        var result = new List<TOEFLQuestion>();
        foreach (var (task, drawn) in blocks)
        {
            var ordered = task is TOEFLTaskType.ListenRepeat or TOEFLTaskType.Interview ? drawn : generator.Shuffled(drawn);
            foreach (var set in ordered) result.AddRange(set);
        }
        return result;
    }

    // MARK: - Exposure

    /// <summary>Exposure history, oldest first (empty when <see cref="UsesExposureHistory"/> is off).</summary>
    public IReadOnlyList<string> RecentSetKeys =>
        UsesExposureHistory ? exposureStore?.GetStringArray(StorageKeys.QuestionExposure) ?? [] : [];

    /// <summary>Records the sets a test taker actually worked through, newest last (called when a section is scored).</summary>
    public void RememberExposure(IEnumerable<TOEFLQuestion> questions)
    {
        if (!UsesExposureHistory || exposureStore is null) return;
        var keys = questions.Select(q => q.SetKey).ToList();
        var keySet = keys.ToHashSet();
        var history = (exposureStore.GetStringArray(StorageKeys.QuestionExposure) ?? []).Where(k => !keySet.Contains(k)).ToList();
        var seen = new HashSet<string>();
        history.AddRange(keys.Where(seen.Add));
        exposureStore.SetStringArray(StorageKeys.QuestionExposure, history.Skip(Math.Max(0, history.Count - ExposureLimit)));
    }

    /// <summary>
    /// Debug self-test: simulates 20 consecutive Listening sessions with an in-memory history. For every Listening task,
    /// no set may repeat until all of that task's sets have been used.
    /// </summary>
    public IReadOnlyList<string> RotationIssues() => RotationIssues(out _);

    public IReadOnlyList<string> RotationIssues(out string summary)
    {
        TOEFLTaskType[] tasks = [TOEFLTaskType.ListenChooseResponse, TOEFLTaskType.ListenConversation, TOEFLTaskType.ListenAnnouncement, TOEFLTaskType.ListenAcademicTalk];
        var poolSizes = tasks.ToDictionary(task => task, task => Sets.Count(set => set[0].TaskType == task));
        var history = new List<string>();
        var seen = tasks.ToDictionary(task => task, _ => new HashSet<string>());
        var issues = new HashSet<string>();
        for (var session = 0; session < 20; session++)
        {
            var section = PracticeSection(TOEFLSectionKind.Listening, unchecked((ulong)session * 104_729), history.ToList());
            var router = section.Questions.Take(section.RouterCount ?? 0).ToList();
            var module = ModuleQuestions(TOEFLSectionKind.Listening, session % 2 == 0 ? AdaptiveRoute.Lower : AdaptiveRoute.Upper,
                (ulong)session, router.Select(q => q.Id).ToHashSet(), history.ToList());
            var added = new HashSet<string>();
            var keys = router.Concat(module).Select(q => (q.TaskType, Key: q.SetKey)).Where(k => added.Add(k.Key)).ToList();
            foreach (var (task, key) in keys)
            {
                if (!seen.TryGetValue(task, out var taskSeen)) continue;
                if (taskSeen.Contains(key) && taskSeen.Count < poolSizes[task])
                    issues.Add($"{task.Title()} set {key} repeated in session {session + 1} before the pool rotated");
                taskSeen.Add(key);
            }
            history.AddRange(keys.Select(k => k.Key));
        }
        summary = "20 Listening sessions → " + string.Join("; ", tasks.Select(t => $"{t.Title()}: {seen[t].Count} of {poolSizes[t]} sets"));
        return issues.Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// A learner who scored 5.5 on live Listening found the real talks and announcements longer than the app's, so among
    /// unseen sets the long ones (talks of 235+ words, announcements of 88+ words) are delivered first.
    /// </summary>
    private static List<IReadOnlyList<TOEFLQuestion>> LongFirst(List<IReadOnlyList<TOEFLQuestion>> sets)
    {
        static bool IsLong(IReadOnlyList<TOEFLQuestion> set)
        {
            var first = set.Count > 0 ? set[0] : null;
            var words = TextUtil.Split(first?.AudioScript ?? "", char.IsWhiteSpace).Count;
            return first?.TaskType switch
            {
                TOEFLTaskType.ListenAcademicTalk => words >= 235,
                TOEFLTaskType.ListenAnnouncement => words >= 88,
                _ => false,
            };
        }
        return [.. sets.Where(IsLong), .. sets.Where(set => !IsLong(set))];
    }

    /// <summary>Avoids, for example, a Complete the Words paragraph and an academic passage on the same subject.</summary>
    private static string TopicKey(TOEFLQuestion question) => question.Setting ?? question.SetID ?? question.Id;

    // MARK: - Validation

    private static readonly HashSet<string> CompleteWordsFields = ["humanities", "history", "earth", "life", "physical", "social"];

    /// <summary>
    /// Swift <c>validate()</c>: bank integrity (keys, recordings, study notes, question types, blanks) and the blueprint of
    /// 30 sampled forms per section on both routes. Returns sorted, de-duplicated issues; empty means valid.
    /// </summary>
    public IReadOnlyList<string> Validate(TOEFLExamAudio audio)
    {
        var issues = new HashSet<string>();
        var previous = UsesExposureHistory;
        UsesExposureHistory = false;
        try
        {
            if (AllQuestions.Select(q => q.Id).Distinct().Count() != AllQuestions.Count) issues.Add("Duplicate question IDs.");
            foreach (var question in AllQuestions)
            {
                if (question.Choices.Count > 0 && question.CorrectChoiceID is null) issues.Add($"{question.Id} has no key.");
                if (question.TaskType.IsListeningStimulus() || question.Section == TOEFLSectionKind.Speaking)
                    if (!audio.HasClip(question.AudioKey)) issues.Add($"{question.Id} has no recording.");
                if (question.TaskType == TOEFLTaskType.CompleteWords && (question.WordGloss is null || question.BlankReason is null || question.PartOfSpeech is null))
                    issues.Add($"{question.Id} has no study notes.");
                if (question.TaskType is TOEFLTaskType.ReadDailyLife or TOEFLTaskType.ReadAcademicPassage
                    && ReadingQuestionTypes.FromRawValue(question.QuestionType ?? "") is null)
                    issues.Add($"{question.Id} has no question type.");
                if (question.TaskType == TOEFLTaskType.CompleteWords && !CompleteWordsFields.Contains(question.Field ?? ""))
                    issues.Add($"{question.Id} has no subject area.");
                if (question.TaskType == TOEFLTaskType.BuildSentence)
                {
                    var blanks = (question.SentenceTemplate ?? "").Split("___").Length - 1;
                    if (blanks != question.CorrectSentenceTokens.Count) issues.Add($"{question.Id} blank count mismatch.");
                }
            }
            for (ulong seed = 1; seed <= 30; seed++)
            {
                foreach (var kind in TOEFLSectionKindExtensions.AllCases)
                {
                    var section = PracticeSection(kind, unchecked(seed * 7919));
                    if (kind.IsAdaptive())
                    {
                        foreach (var route in (AdaptiveRoute[])[AdaptiveRoute.Lower, AdaptiveRoute.Upper])
                        {
                            var router = section.Questions.Take(section.RouterCount ?? 0).ToList();
                            section.Questions = [.. router, .. ModuleQuestions(kind, route, seed, router.Select(q => q.Id).ToHashSet())];
                            var routeName = route.RawValue();
                            var scored = section.Questions.Count(q => q.CountsTowardScore);
                            if (section.Questions.Count != kind.OfficialItemCount()) issues.Add($"{kind.Title()} {routeName} has {section.Questions.Count} items.");
                            if (scored != 35) issues.Add($"{kind.Title()} {routeName} has {scored} scored items.");
                            var expectedRouter = kind == TOEFLSectionKind.Reading ? 35 : 32;
                            if (router.Count != expectedRouter || section.Questions.Count - router.Count != 15
                                || section.Questions.Skip(router.Count).Any(q => !q.CountsTowardScore))
                                issues.Add($"{kind.Title()} modules should be {expectedRouter} + 15 scored items; found {router.Count} + {section.Questions.Count - router.Count}.");
                            if (section.Questions.Select(q => q.Id).Distinct().Count() != section.Questions.Count) issues.Add($"{kind.Title()} {routeName} repeats an item.");
                        }
                    }
                    else if (section.Questions.Count != kind.OfficialItemCount())
                    {
                        issues.Add($"{kind.Title()} form has {section.Questions.Count} items.");
                    }
                }
            }
        }
        finally
        {
            UsesExposureHistory = previous;
        }
        return issues.Order(StringComparer.Ordinal).ToList();
    }

    // MARK: - Seeding

    /// <summary>FNV-1a style mix of the form seed and a label, so each module draws from its own stream.</summary>
    internal static ulong MixedSeed(ulong seed, string label)
    {
        unchecked
        {
            var hash = seed ^ 0xcbf29ce484222325UL;
            foreach (var b in System.Text.Encoding.UTF8.GetBytes(label))
            {
                hash ^= b;
                hash *= 0x100000001b3UL;
            }
            return hash;
        }
    }
}
