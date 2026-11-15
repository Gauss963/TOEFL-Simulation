namespace TOEFLSimulation.Core;

/// <summary>Swift <c>TOEFLAppPage</c>: the sidebar pages, in order.</summary>
public enum TOEFLAppPage { Dashboard, WordsDrill, ReadingDrill, WritingGuide, SpeakingGuide, Study, History, Resources }

public static class TOEFLAppPages
{
    public static readonly IReadOnlyList<TOEFLAppPage> AllCases = Enum.GetValues<TOEFLAppPage>();

    public static string Title(this TOEFLAppPage page) => page switch
    {
        TOEFLAppPage.Dashboard => "Practice Tests",
        TOEFLAppPage.WordsDrill => "Complete the Words",
        TOEFLAppPage.ReadingDrill => "Reading Practice",
        TOEFLAppPage.WritingGuide => "Writing Guide",
        TOEFLAppPage.SpeakingGuide => "Speaking Guide",
        TOEFLAppPage.Study => "Vocabulary",
        TOEFLAppPage.History => "Score History",
        _ => "Official Resources",
    };

    /// <summary>SF Symbol name used by the Mac app (map to a Windows glyph).</summary>
    public static string Icon(this TOEFLAppPage page) => page switch
    {
        TOEFLAppPage.Dashboard => "rectangle.and.pencil.and.ellipsis",
        TOEFLAppPage.WordsDrill => "character.cursor.ibeam",
        TOEFLAppPage.ReadingDrill => "book",
        TOEFLAppPage.WritingGuide => "envelope.open",
        TOEFLAppPage.SpeakingGuide => "mic",
        TOEFLAppPage.Study => "text.book.closed",
        TOEFLAppPage.History => "chart.line.uptrend.xyaxis",
        _ => "checkmark.seal",
    };

    public const string SidebarFooter = "Independent TOEFL® practice simulator";
    public const string BrandTitle = "TOEFL Simulator";
}

/// <summary>Text and data of the Practice Tests page (DashboardView).</summary>
public static class DashboardContent
{
    public const string HeroTitle = "Practice the TOEFL iBT 2026 format";
    public const string HeroSubtitle = "Two-stage adaptive Reading and Listening, current task types, timed delivery, local recording, and transparent practice scoring for Mac and iPad.";
    public const string FullTestTitle = "Full-length simulation";
    public const string FullTestDetail = "Reading → Listening → Writing → Speaking · 120 items · about 90 minutes of section time; allow approximately 2 hours overall";
    public const string FullTestButton = "Start full test";
    public static readonly IReadOnlyList<string> FullTestFeatures = ["Two-stage adaptive", "Timed sections", "Microphone recording", "Score report"];
    public const string BlueprintTitle = "Official 2026 section blueprint";
    public const string LatestResultTitle = "Latest practice estimate";
    public const string AccuracyNotice = "Practice estimates are not official TOEFL scores. ETS uses secure calibrated forms plus AI and certified raters; this app uses answer keys, official rubrics, and on-device speech recognition.";

    /// <summary>The four section practice cards, in order.</summary>
    public static readonly IReadOnlyList<ExamMode> PracticeModes = [ExamMode.ReadingPractice, ExamMode.ListeningPractice, ExamMode.WritingPractice, ExamMode.SpeakingPractice];

    /// <summary>"5671 questions · 1375 vocabulary entries".</summary>
    public static string BankSummary(int questionCount, int vocabularyCount) => $"{questionCount} questions · {vocabularyCount} vocabulary entries";

    /// <summary>The blueprint table's task-type column.</summary>
    public static string TaskSummary(TOEFLSectionKind kind) => kind switch
    {
        TOEFLSectionKind.Reading => "Complete Words · Daily Life · Academic Passage",
        TOEFLSectionKind.Listening => "Response · Conversation · Announcement · Academic Talk",
        TOEFLSectionKind.Writing => "Build Sentence · Email · Academic Discussion",
        _ => "Listen & Repeat · Interview",
    };
}

/// <summary>Content of the Official Resources page (ResourcesView).</summary>
public static class ResourcesContent
{
    public sealed record Link(string Title, string Url);

    public const string Title = "Official resources";
    public const string Introduction = "The app uses the post-January 21, 2026 TOEFL iBT blueprint. Older authorized books inform skill coverage, interface conventions, and study vocabulary; their retired task structure is not presented as the current exam.";

    public static readonly IReadOnlyList<Link> OfficialReferences =
    [
        new("ETS: Test content and structure", "https://www.ets.org/toefl/test-takers/ibt/about/content.html"),
        new("ETS: 2026 blueprint and specifications", "https://www.ets.org/content/dam/ets-india/pdfs/toefl/toefl-ibt-test-specifications-2026.pdf"),
        new("ETS: Understanding 1–6 scores", "https://www.ets.org/toefl/test-takers/ibt/scores/understand-scores.html"),
        new("ETS: Reading task types", "https://www.ets.org/toefl/test-takers/ibt/about/content/reading.html"),
        new("ETS: Listening task types", "https://www.ets.org/toefl/test-takers/ibt/about/content/listening.html"),
        new("ETS: Writing task types", "https://www.ets.org/toefl/test-takers/ibt/about/content/writing.html"),
        new("ETS: Speaking task types", "https://www.ets.org/toefl/test-takers/ibt/about/content/speaking.html"),
    ];

    public const string FreePracticeTitle = "Official free practice";
    public const string FreePracticeNote = "Real ETS practice material for the current format. Use it alongside this app's original items.";

    public static readonly IReadOnlyList<Link> FreePractice =
    [
        new("ETS TOEFL TestReady: free full-length practice test with scoring, section samplers, and daily activities", "https://testready.ets.org"),
        new("ETS: TOEFL iBT Practice Test 1 (PDF, aligned to tests from January 21, 2026)", "https://www.ets.org/content/dam/ets-india/pdfs/toefl/toefl-ibt-full-length-practice-test-1.pdf"),
        new("ETS: All official TOEFL iBT preparation", "https://www.ets.org/toefl/test-takers/ibt/prepare.html"),
    ];

    public const string LocalLibraryTitle = "Authorized local reference library";

    public static readonly IReadOnlyList<string> LocalSources =
    [
        "The Official Guide to the TOEFL iBT Test",
        "Official TOEFL iBT Tests, Volumes 1 and 2",
        "TOEFL iBT Premium with 8 Online Practice Tests",
        "Princeton Review TOEFL iBT Prep with Audio-Listening Tracks",
        "Essential Words for the TOEFL (authorized editions)",
        "托福中階500單字、托福進階500單字、高階500英文難字",
        "TOEFL 托福分類字彙、Toefl 詞彙紅寶書",
        "TOEFL Idioms Quiz Book",
        "Authorized reading-comprehension collection and practice test",
    ];

    public const string LocalLibraryNote = "The large source files remain outside the app bundle and Git repository.";
    public const string Trademark = "TOEFL® is a registered trademark of ETS. This independent simulator is not endorsed or approved by ETS.";
}

/// <summary>Text of the Vocabulary page (VocabularyView).</summary>
public static class VocabularyContent
{
    public const string Title = "High-frequency vocabulary lab";
    public const string Subtitle = "Browse the authorized offline word bank or work through shuffled flash-card sessions with persistent mastery progress.";
    public static readonly IReadOnlyList<string> StudyModes = ["Browse", "Flash Cards"];
    public const string SearchPlaceholder = "Search words, meanings, or synonyms";
    public const string Footnote = "Entries retain their authorized source tags after deduplication. Chinese definitions use Taiwan Traditional Chinese. English glosses and examples are compacted for card display; consult the authorized source material for complete study notes.";
    public const string TapToReveal = "Tap to reveal meaning";
}
