using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using TOEFLSimulation.Core.Storage;

namespace TOEFLSimulation.Core;

/// <summary>
/// Swift <c>TOEFLHistoryStore</c>: saved practice results, newest first (Score History page, Dashboard's latest result).
/// Stored under <see cref="StorageKeys.PracticeResults"/>.
/// </summary>
public sealed class TOEFLHistoryStore : ObservableObject
{
    private readonly KeyValueStore store;
    private readonly ObservableCollection<TOEFLExamResult> results;

    public TOEFLHistoryStore(KeyValueStore store)
    {
        this.store = store;
        var decoded = store.GetJson<List<TOEFLExamResult>>(StorageKeys.PracticeResults) ?? [];
        results = new ObservableCollection<TOEFLExamResult>(decoded.OrderByDescending(r => r.CompletedAt));
        Results = new ReadOnlyObservableCollection<TOEFLExamResult>(results);
    }

    /// <summary>Newest first. Bindable collection.</summary>
    public ReadOnlyObservableCollection<TOEFLExamResult> Results { get; }

    public TOEFLExamResult? Latest => results.Count > 0 ? results[0] : null;

    /// <summary>Called when the user closes a completed test's score report ("Save &amp; close").</summary>
    public void Add(TOEFLExamResult result)
    {
        results.Insert(0, result);
        Save();
    }

    /// <summary>"Clear history" → confirm "Delete every saved result?" → "Delete all results".</summary>
    public void Clear()
    {
        results.Clear();
        Save();
    }

    private void Save()
    {
        store.SetJson(StorageKeys.PracticeResults, results.ToList());
        OnPropertyChanged(nameof(Latest));
    }
}

/// <summary>Text shown in score reports and history cards (ScoreReportView / HistoryView / DashboardView).</summary>
public static class ScoreReportText
{
    public const string MethodologyTitle = "How this estimate works";

    public const string Methodology = "Reading and Listening count only the 35 scored items on your adaptive path; unscored pretest items are marked below. Build a Sentence uses answer keys (1 point each). Email and Academic Discussion are scored 0–5 by a local engine that measures the features ETS lists for its AI scoring (task completion, elaboration, language range, cohesion, conventions, and spelling). Every Speaking response is transcribed on this device and scored 0–5 on the official rubric: Listen and Repeat by word-level accuracy against the sentence, the interview by relevance, elaboration, speaking rate, pauses, vocabulary, and cohesion. Raw totals follow the ETS scale (Reading/Listening 35, Writing 20, Speaking 55) and convert to the 1–6 bands.";

    public const string Disclaimer = "ETS combines proprietary AI scoring engines with certified human raters and IRT-equated forms; those models are not public. These results estimate practice performance and are not official TOEFL scores.";

    public const string OverallExplanation = "The 2026 TOEFL overall score is the average of four section bands rounded to the nearest half band. For a single-section practice, this card summarizes that section only.";

    /// <summary>Heading/done button when opened after a test vs. from history.</summary>
    public const string CompletedHeading = "Practice score report";
    public const string CompletedDoneTitle = "Save & close";
    public const string SavedHeading = "Saved practice report";
    public const string SavedDoneTitle = "Close";

    /// <summary>"CEFR B2 · comparable overall score ~79/120".</summary>
    public static string CefrLine(TOEFLExamResult result) => $"CEFR {result.Cefr} · comparable overall score ~{result.ComparableTotal120}/120";

    /// <summary>History card: "CEFR B2 · ~79/120".</summary>
    public static string HistoryCefrLine(TOEFLExamResult result) => $"CEFR {result.Cefr} · ~{result.ComparableTotal120}/120";

    /// <summary>History card: "Reading 4.5 · Listening 4.0 · …".</summary>
    public static string SectionBandsLine(TOEFLExamResult result) =>
        string.Join(" · ", result.Sections.Select(s => $"{s.Kind.Title()} {TextUtil.OneDecimal(s.Band)}"));

    /// <summary>Band formatted with one decimal ("4.5").</summary>
    public static string Band(double band) => TextUtil.OneDecimal(band);

    /// <summary>Section card line: "Raw 28/35 · 50/50 answered", or the answered/keyed fallback for old results.</summary>
    public static string RawLine(TOEFLSectionSummary section)
    {
        if (section.RawScore is { } raw && section.RawMaximum is { } maximum)
            return $"Raw {raw}/{maximum} · {section.Answered}/{section.Total} answered";
        return $"{section.Answered}/{section.Total} answered" + (section.ObjectiveTotal > 0 ? $" · {section.Correct}/{section.ObjectiveTotal} keyed" : "");
    }

    /// <summary>"12m 5s" or "45s".</summary>
    public static string Duration(int seconds)
    {
        var minutes = seconds / 60;
        return minutes > 0 ? $"{minutes}m {seconds % 60}s" : $"{seconds}s";
    }

    /// <summary>Review badge: "Correct", "Review" (wrong), or "Rubric-scored" (no key).</summary>
    public static string StatusBadge(bool? isCorrect) => isCorrect switch
    {
        true => "Correct",
        false => "Review",
        null => "Rubric-scored",
    };

    /// <summary>Review card header: "Question 3 · Read an Academic Passage".</summary>
    public static string ReviewTitle(TOEFLQuestionReview record) => $"Question {record.Number} · {record.Question.TaskType.Title()}";

    /// <summary>"Rubric score 4/5 · note", or null when the item has no rubric score.</summary>
    public static string? RubricLine(TOEFLQuestionReview record) =>
        record.Answer.RubricScore is { } score ? $"Rubric score {score}/5 · {record.Answer.RubricNote ?? ""}" : null;

    /// <summary>"Key: …" for keyed items or "Sample / target: …" for rubric items; null when there is no sample.</summary>
    public static string? SampleLine(TOEFLQuestionReview record) =>
        record.Question.SampleAnswer is { Length: > 0 } sample ? (record.IsCorrect is null ? "Sample / target: " : "Key: ") + sample : null;

    /// <summary>"Transcript: …" for audio items.</summary>
    public static string? TranscriptLine(TOEFLQuestionReview record) =>
        record.Question.AudioScript is { } audio ? $"Transcript: {audio}" : null;

    /// <summary>The "Your answer:" text.</summary>
    public static string AnswerText(TOEFLQuestionReview record)
    {
        var answer = record.Answer;
        if (answer.SelectedChoiceID is { } id && record.Question.Choices.FirstOrDefault(c => c.Id == id) is { } choice) return choice.Text;
        if (answer.TextEntry.Length > 0) return answer.TextEntry;
        if (answer.OrderedTokens.Count > 0) return string.Join(" ", answer.OrderedTokens);
        if (answer.WrittenResponse.Length > 0) return answer.WrittenResponse;
        if (answer.RecordingDuration > 0)
        {
            var transcript = answer.Transcript is { } t ? (t.Length == 0 ? "(no speech recognized)" : $"“{t}”") : "(not transcribed)";
            // %.0f rounds half to even, like printf.
            return Math.Round(answer.RecordingDuration, MidpointRounding.ToEven).ToString("0", CultureInfo.InvariantCulture) + "-second recording · " + transcript;
        }
        return "No response";
    }

    /// <summary>The answer-review groups: each section in order with its records ("Reading · 50 items").</summary>
    public static IReadOnlyList<(TOEFLSectionKind Kind, string Title, IReadOnlyList<TOEFLQuestionReview> Records)> ReviewGroups(TOEFLExamResult result) =>
        TOEFLSectionKindExtensions.AllCases
            .Select(kind => (Kind: kind, Records: (IReadOnlyList<TOEFLQuestionReview>)result.Reviews.Where(r => r.Question.Section == kind).ToList()))
            .Where(group => group.Records.Count > 0)
            .Select(group => (group.Kind, $"{group.Kind.Title()} · {group.Records.Count} items", group.Records))
            .ToList();
}
