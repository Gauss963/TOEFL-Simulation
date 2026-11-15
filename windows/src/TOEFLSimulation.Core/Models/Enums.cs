namespace TOEFLSimulation.Core;

/// <summary>Swift <c>TOEFLSectionKind</c>. JSON raw values: reading, listening, writing, speaking.</summary>
public enum TOEFLSectionKind { Reading, Listening, Writing, Speaking }

/// <summary>Swift <c>TOEFLTaskType</c>. JSON raw values are the camelCase names (completeWords, …).</summary>
public enum TOEFLTaskType
{
    CompleteWords,
    ReadDailyLife,
    ReadAcademicPassage,
    ListenChooseResponse,
    ListenConversation,
    ListenAnnouncement,
    ListenAcademicTalk,
    BuildSentence,
    WriteEmail,
    AcademicDiscussion,
    ListenRepeat,
    Interview,
}

/// <summary>Swift <c>TOEFLDifficulty</c>.</summary>
public enum TOEFLDifficulty { Foundational, Intermediate, Advanced }

/// <summary>Swift <c>AdaptiveRoute</c>: the second-stage module of Reading or Listening.</summary>
public enum AdaptiveRoute { Lower, Upper }

/// <summary>Swift <c>ExamMode</c>: what the user starts from the Practice Tests page or a guide.</summary>
public enum ExamMode { FullLength, ReadingPractice, ListeningPractice, WritingPractice, SpeakingPractice }

public static class TOEFLSectionKindExtensions
{
    public static readonly IReadOnlyList<TOEFLSectionKind> AllCases =
        [TOEFLSectionKind.Reading, TOEFLSectionKind.Listening, TOEFLSectionKind.Writing, TOEFLSectionKind.Speaking];

    public static string RawValue(this TOEFLSectionKind kind) => kind switch
    {
        TOEFLSectionKind.Reading => "reading",
        TOEFLSectionKind.Listening => "listening",
        TOEFLSectionKind.Writing => "writing",
        _ => "speaking",
    };

    public static string Title(this TOEFLSectionKind kind) => kind switch
    {
        TOEFLSectionKind.Reading => "Reading",
        TOEFLSectionKind.Listening => "Listening",
        TOEFLSectionKind.Writing => "Writing",
        _ => "Speaking",
    };

    /// <summary>SF Symbol name used by the Mac app (map to a Windows glyph in the UI).</summary>
    public static string Icon(this TOEFLSectionKind kind) => kind switch
    {
        TOEFLSectionKind.Reading => "book.pages",
        TOEFLSectionKind.Listening => "headphones",
        TOEFLSectionKind.Writing => "keyboard",
        _ => "waveform.and.mic",
    };

    /// <summary>Items delivered on every path, including unscored (pretest) items, per the ETS 2026 blueprint.</summary>
    public static int OfficialItemCount(this TOEFLSectionKind kind) => kind switch
    {
        TOEFLSectionKind.Reading => 50,
        TOEFLSectionKind.Listening => 47,
        TOEFLSectionKind.Writing => 12,
        _ => 11,
    };

    public static int OfficialMinutes(this TOEFLSectionKind kind) => kind switch
    {
        TOEFLSectionKind.Reading => 30,
        TOEFLSectionKind.Listening => 29,
        TOEFLSectionKind.Writing => 23,
        _ => 8,
    };

    /// <summary>Raw score points published in the ETS 2026 specification.</summary>
    public static int RawMaximum(this TOEFLSectionKind kind) => kind switch
    {
        TOEFLSectionKind.Reading or TOEFLSectionKind.Listening => 35,
        TOEFLSectionKind.Writing => 20,
        _ => 55,
    };

    public static bool IsAdaptive(this TOEFLSectionKind kind) => kind is TOEFLSectionKind.Reading or TOEFLSectionKind.Listening;
}

public static class TOEFLTaskTypeExtensions
{
    public static readonly IReadOnlyList<TOEFLTaskType> AllCases = Enum.GetValues<TOEFLTaskType>();

    public static string RawValue(this TOEFLTaskType task) => char.ToLowerInvariant(task.ToString()[0]) + task.ToString()[1..];

    public static string Title(this TOEFLTaskType task) => task switch
    {
        TOEFLTaskType.CompleteWords => "Complete the Words",
        TOEFLTaskType.ReadDailyLife => "Read in Daily Life",
        TOEFLTaskType.ReadAcademicPassage => "Read an Academic Passage",
        TOEFLTaskType.ListenChooseResponse => "Listen and Choose a Response",
        TOEFLTaskType.ListenConversation => "Listen to a Conversation",
        TOEFLTaskType.ListenAnnouncement => "Listen to an Announcement",
        TOEFLTaskType.ListenAcademicTalk => "Listen to an Academic Talk",
        TOEFLTaskType.BuildSentence => "Build a Sentence",
        TOEFLTaskType.WriteEmail => "Write an Email",
        TOEFLTaskType.AcademicDiscussion => "Write for an Academic Discussion",
        TOEFLTaskType.ListenRepeat => "Listen and Repeat",
        _ => "Take an Interview",
    };

    public static string Directions(this TOEFLTaskType task) => task switch
    {
        TOEFLTaskType.CompleteWords => "Fill in the missing letters in the paragraph.",
        TOEFLTaskType.ReadDailyLife or TOEFLTaskType.ReadAcademicPassage => "Read the text and answer the questions.",
        TOEFLTaskType.ListenChooseResponse => "You will hear a person speak. Choose the best response.",
        TOEFLTaskType.ListenConversation => "Listen to a conversation. Then answer the questions.",
        TOEFLTaskType.ListenAnnouncement => "Listen to an announcement. Then answer the questions.",
        TOEFLTaskType.ListenAcademicTalk => "Listen to an academic talk. Then answer the questions.",
        TOEFLTaskType.BuildSentence => "Move the words in the boxes to create a grammatical sentence.",
        TOEFLTaskType.WriteEmail => "You will read some information and use it to write an email. You will have 7 minutes to write the email.",
        TOEFLTaskType.AcademicDiscussion => "Your professor is teaching a class. Write a post responding to the professor's question. You will have 10 minutes to write.",
        TOEFLTaskType.ListenRepeat => "You will listen as someone speaks to you. Listen carefully and then repeat what you have heard. You will not have time to prepare.",
        _ => "An interviewer will ask you questions. Answer each question as fully as you can. You will have 45 seconds to answer each question.",
    };

    public static bool IsListeningStimulus(this TOEFLTaskType task) => task is TOEFLTaskType.ListenChooseResponse
        or TOEFLTaskType.ListenConversation or TOEFLTaskType.ListenAnnouncement or TOEFLTaskType.ListenAcademicTalk;
}

public static class TOEFLDifficultyExtensions
{
    public static readonly IReadOnlyList<TOEFLDifficulty> AllCases = [TOEFLDifficulty.Foundational, TOEFLDifficulty.Intermediate, TOEFLDifficulty.Advanced];

    public static string RawValue(this TOEFLDifficulty difficulty) => difficulty.ToString().ToLowerInvariant();
    public static string Title(this TOEFLDifficulty difficulty) => difficulty.ToString();

    public static TOEFLDifficulty? FromRawValue(string? raw) => raw switch
    {
        "foundational" => TOEFLDifficulty.Foundational,
        "intermediate" => TOEFLDifficulty.Intermediate,
        "advanced" => TOEFLDifficulty.Advanced,
        _ => null,
    };
}

public static class AdaptiveRouteExtensions
{
    public static string RawValue(this AdaptiveRoute route) => route == AdaptiveRoute.Upper ? "upper" : "lower";
    public static string Title(this AdaptiveRoute route) => route == AdaptiveRoute.Upper ? "Upper module" : "Lower module";
}

public static class ExamModeExtensions
{
    public static readonly IReadOnlyList<ExamMode> AllCases =
        [ExamMode.FullLength, ExamMode.ReadingPractice, ExamMode.ListeningPractice, ExamMode.WritingPractice, ExamMode.SpeakingPractice];

    public static string RawValue(this ExamMode mode) => char.ToLowerInvariant(mode.ToString()[0]) + mode.ToString()[1..];

    public static string Title(this ExamMode mode) => mode switch
    {
        ExamMode.FullLength => "Full-length 2026 Practice Test",
        ExamMode.ReadingPractice => "Reading Section",
        ExamMode.ListeningPractice => "Listening Section",
        ExamMode.WritingPractice => "Writing Section",
        _ => "Speaking Section",
    };

    public static string Subtitle(this ExamMode mode) => mode switch
    {
        ExamMode.FullLength => "Reading → Listening → Writing → Speaking · 120 items · about 90 min",
        ExamMode.ReadingPractice => "50 items · 30 min · two adaptive modules",
        ExamMode.ListeningPractice => "47 items · 25–29 min · two adaptive modules",
        ExamMode.WritingPractice => "12 items · 23 min · three timed tasks",
        _ => "11 items · about 8 min · automatic timing",
    };

    public static IReadOnlyList<TOEFLSectionKind> SectionKinds(this ExamMode mode) => mode switch
    {
        ExamMode.FullLength => TOEFLSectionKindExtensions.AllCases,
        ExamMode.ReadingPractice => [TOEFLSectionKind.Reading],
        ExamMode.ListeningPractice => [TOEFLSectionKind.Listening],
        ExamMode.WritingPractice => [TOEFLSectionKind.Writing],
        _ => [TOEFLSectionKind.Speaking],
    };
}
