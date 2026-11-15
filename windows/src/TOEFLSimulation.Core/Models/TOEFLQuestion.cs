using System.Text.Json.Serialization;

namespace TOEFLSimulation.Core;

/// <summary>Swift <c>TOEFLChoice</c>.</summary>
public sealed record TOEFLChoice
{
    [JsonRequired] public string Id { get; init; } = "";
    [JsonRequired] public string Text { get; init; } = "";
}

/// <summary>Swift <c>TOEFLSpeaker</c>: a voice in a Listening or Speaking recording.</summary>
public sealed record TOEFLSpeaker
{
    [JsonRequired] public string Role { get; init; } = "";
    public string? Name { get; init; }
    /// <summary>"female" or "male".</summary>
    [JsonRequired] public string Gender { get; init; } = "";

    /// <summary>"Name · Role" (role capitalized), or just the capitalized role.</summary>
    [JsonIgnore]
    public string DisplayName => Name is { } name ? $"{name} · {TextUtil.Capitalized(Role)}" : TextUtil.Capitalized(Role);
}

/// <summary>Swift <c>TOEFLQuestion</c>: one item of the bundled bank (TOEFL2026Bank.json).</summary>
public sealed record TOEFLQuestion
{
    [JsonRequired] public string Id { get; init; } = "";
    [JsonRequired] public TOEFLSectionKind Section { get; init; }
    [JsonRequired] public TOEFLTaskType TaskType { get; init; }
    [JsonRequired] public TOEFLDifficulty Difficulty { get; init; }
    [JsonRequired] public string Prompt { get; init; } = "";
    public string? Passage { get; init; }
    public string? AudioScript { get; init; }
    [JsonRequired] public IReadOnlyList<TOEFLChoice> Choices { get; init; } = [];
    public string? CorrectChoiceID { get; init; }
    [JsonRequired] public IReadOnlyList<string> AcceptedTextAnswers { get; init; } = [];
    [JsonRequired] public IReadOnlyList<string> SentenceTokens { get; init; } = [];
    [JsonRequired] public IReadOnlyList<string> CorrectSentenceTokens { get; init; } = [];
    public string? ResponseGuidance { get; init; }
    public string? SampleAnswer { get; init; }
    [JsonRequired] public string Explanation { get; init; } = "";
    [JsonRequired] public string SourceNote { get; init; } = "";
    /// <summary>Items that share one text or recording form a set (a Complete the Words paragraph, a conversation, …).</summary>
    public string? SetID { get; init; }
    public int? SetIndex { get; init; }
    /// <summary>Unscored pretest items are delivered like any other item but excluded from the score.</summary>
    public bool? IsScored { get; init; }
    /// <summary>Narrator line or scenario introduction, shown and played before the first item of a set.</summary>
    public string? Intro { get; init; }
    public IReadOnlyList<TOEFLSpeaker>? Speakers { get; init; }
    /// <summary>Build a Sentence: the first speaker's line.</summary>
    public string? Context { get; init; }
    /// <summary>Build a Sentence: the reply with ___ blanks.</summary>
    public string? SentenceTemplate { get; init; }
    /// <summary>Speaking: recording window in seconds.</summary>
    public int? ResponseSeconds { get; init; }
    /// <summary>Listen and Repeat illustration: the setting (also the paragraph title for Complete the Words).</summary>
    public string? Setting { get; init; }
    /// <summary>Listen and Repeat illustration: the places visited in order.</summary>
    public IReadOnlyList<string>? Stops { get; init; }
    /// <summary>Complete the Words study notes: part of speech.</summary>
    public string? PartOfSpeech { get; init; }
    /// <summary>Complete the Words study notes: meaning in context (Traditional Chinese).</summary>
    public string? WordGloss { get; init; }
    /// <summary>Complete the Words study notes: how the context and spelling lead to the answer.</summary>
    public string? BlankReason { get; init; }
    /// <summary>Complete the Words subject area: humanities, history, earth, life, physical, or social.</summary>
    public string? Field { get; init; }
    /// <summary>Reading question type raw value (see <see cref="ReadingQuestionType"/>).</summary>
    public string? QuestionType { get; init; }

    [JsonIgnore] public bool CountsTowardScore => IsScored ?? true;

    [JsonIgnore]
    public bool IsConstructedResponse => TaskType is TOEFLTaskType.WriteEmail or TOEFLTaskType.AcademicDiscussion
        or TOEFLTaskType.ListenRepeat or TOEFLTaskType.Interview;

    [JsonIgnore] public bool IsAudioTask => AudioScript is not null;

    /// <summary>Key of the pre-rendered recording that belongs to this item (ExamAudio pack).</summary>
    [JsonIgnore]
    public string AudioKey => TaskType switch
    {
        TOEFLTaskType.ListenConversation or TOEFLTaskType.ListenAnnouncement or TOEFLTaskType.ListenAcademicTalk => SetID ?? Id,
        _ => Id,
    };

    /// <summary>The set key used for grouping and exposure history (<c>setID ?? id</c>).</summary>
    [JsonIgnore] public string SetKey => SetID ?? Id;
}

/// <summary>Swift <c>TOEFLAnswer</c>. Immutable; use <c>with</c> to change it (the Swift type is a value type).</summary>
public sealed record TOEFLAnswer
{
    public static readonly TOEFLAnswer Empty = new();

    public string? SelectedChoiceID { get; init; }
    public string TextEntry { get; init; } = "";
    public IReadOnlyList<string> OrderedTokens { get; init; } = [];
    public string WrittenResponse { get; init; } = "";
    public double RecordingDuration { get; init; }
    public int SpeakingSelfRating { get; init; }
    public string? Transcript { get; init; }
    /// <summary>Rubric score (0–5) for Email, Academic Discussion, and Speaking items.</summary>
    public int? RubricScore { get; init; }
    public string? RubricNote { get; init; }

    [JsonIgnore]
    public bool IsEmpty => SelectedChoiceID is null
        && TextEntry.Trim().Length == 0
        && OrderedTokens.Count == 0
        && WrittenResponse.Trim().Length == 0
        && RecordingDuration <= 0;

    public bool Equals(TOEFLAnswer? other) => other is not null
        && SelectedChoiceID == other.SelectedChoiceID && TextEntry == other.TextEntry
        && OrderedTokens.SequenceEqual(other.OrderedTokens) && WrittenResponse == other.WrittenResponse
        && RecordingDuration.Equals(other.RecordingDuration) && SpeakingSelfRating == other.SpeakingSelfRating
        && Transcript == other.Transcript && RubricScore == other.RubricScore && RubricNote == other.RubricNote;

    public override int GetHashCode() => HashCode.Combine(SelectedChoiceID, TextEntry, OrderedTokens.Count, WrittenResponse, RecordingDuration, RubricScore);
}
