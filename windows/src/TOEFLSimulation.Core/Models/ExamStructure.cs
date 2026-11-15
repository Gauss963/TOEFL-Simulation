using System.Text.Json.Serialization;

namespace TOEFLSimulation.Core;

/// <summary>A half-open index range <c>[LowerBound, UpperBound)</c>, Swift <c>Range&lt;Int&gt;</c>.</summary>
public readonly record struct IntRange(int LowerBound, int UpperBound)
{
    public int Count => UpperBound - LowerBound;
    public bool Contains(int index) => index >= LowerBound && index < UpperBound;
    public IEnumerable<int> Indices => Enumerable.Range(LowerBound, Math.Max(0, Count));
}

/// <summary>Swift <c>TOEFLTimedPart</c>: a block of items with its own clock (module or writing task).</summary>
public sealed record TOEFLTimedPart(string Title, IntRange Range, int Seconds, bool AllowsBack);

/// <summary>Swift <c>TOEFLExamSection</c>: one section of a form. <see cref="Questions"/> and <see cref="Route"/>
/// change when the adaptive second module is chosen.</summary>
public sealed class TOEFLExamSection
{
    public TOEFLExamSection(string id, TOEFLSectionKind kind, int durationSeconds, IReadOnlyList<TOEFLQuestion> questions, int? routerCount, AdaptiveRoute? route)
    {
        Id = id;
        Kind = kind;
        DurationSeconds = durationSeconds;
        Questions = questions;
        RouterCount = routerCount;
        Route = route;
    }

    public string Id { get; }
    public TOEFLSectionKind Kind { get; }
    public int DurationSeconds { get; }
    public IReadOnlyList<TOEFLQuestion> Questions { get; set; }
    public int? RouterCount { get; }
    public AdaptiveRoute? Route { get; set; }

    public string DisplayTitle => Kind.Title();

    public IReadOnlyList<TOEFLTimedPart> TimedParts
    {
        get
        {
            switch (Kind)
            {
                case TOEFLSectionKind.Reading:
                case TOEFLSectionKind.Listening:
                {
                    var router = RouterCount ?? Questions.Count;
                    var first = Kind == TOEFLSectionKind.Reading ? 21 * 60 : 18 * 60;
                    // Blueprint: Reading Module 2 is 9 min; Listening Module 2 is 7 min (lower) or 11 min (upper).
                    var second = Kind == TOEFLSectionKind.Reading ? 9 * 60 : (Route == AdaptiveRoute.Upper ? 11 * 60 : 7 * 60);
                    var parts = new List<TOEFLTimedPart> { new("Module 1", new IntRange(0, router), first, Kind == TOEFLSectionKind.Reading) };
                    if (router < Questions.Count)
                        parts.Add(new TOEFLTimedPart("Module 2", new IntRange(router, Questions.Count), second, Kind == TOEFLSectionKind.Reading));
                    return parts;
                }
                case TOEFLSectionKind.Writing:
                {
                    var parts = new List<TOEFLTimedPart>();
                    var index = 0;
                    while (index < Questions.Count)
                    {
                        var task = Questions[index].TaskType;
                        var end = index;
                        while (end < Questions.Count && Questions[end].TaskType == task) end++;
                        var seconds = task switch
                        {
                            TOEFLTaskType.WriteEmail => 7 * 60,
                            TOEFLTaskType.AcademicDiscussion => 10 * 60,
                            _ => 6 * 60,
                        };
                        parts.Add(new TOEFLTimedPart(task.Title(), new IntRange(index, end), seconds, task == TOEFLTaskType.BuildSentence));
                        index = end;
                    }
                    return parts;
                }
                default:
                    return [new TOEFLTimedPart("Speaking", new IntRange(0, Questions.Count), DurationSeconds, false)];
            }
        }
    }
}

/// <summary>Swift <c>TOEFLSectionSummary</c> (stored in saved results).</summary>
public sealed record TOEFLSectionSummary
{
    public TOEFLSectionKind Kind { get; init; }
    public int Correct { get; init; }
    public int ObjectiveTotal { get; init; }
    public int Answered { get; init; }
    public int Total { get; init; }
    public AdaptiveRoute? Route { get; init; }
    public double Band { get; init; }
    public string RubricDetail { get; init; } = "";
    public int? RawScore { get; init; }
    public int? RawMaximum { get; init; }

    [JsonIgnore] public string Id => Kind.RawValue();
}

/// <summary>Swift <c>TOEFLQuestionReview</c>: one answered item in a score report.</summary>
public sealed record TOEFLQuestionReview
{
    public int Number { get; init; }
    public TOEFLQuestion Question { get; init; } = new();
    public TOEFLAnswer Answer { get; init; } = new();
    /// <summary>True/false for keyed items; null for rubric-scored items.</summary>
    public bool? IsCorrect { get; init; }

    [JsonIgnore] public string Id => Question.Id;
}

/// <summary>Swift <c>TOEFLExamResult</c>: a completed practice test, as saved in Score History.</summary>
public sealed record TOEFLExamResult
{
    public Guid Id { get; init; }
    public DateTimeOffset CompletedAt { get; init; }
    public ExamMode Mode { get; init; }
    public double OverallBand { get; init; }
    public int ComparableTotal120 { get; init; }
    public string Cefr { get; init; } = "";
    public int ElapsedSeconds { get; init; }
    public IReadOnlyList<TOEFLSectionSummary> Sections { get; init; } = [];
    public IReadOnlyList<TOEFLQuestionReview> Reviews { get; init; } = [];
}
