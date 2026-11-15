using TOEFLSimulation.Core.Audio;
using TOEFLSimulation.Core.Storage;

namespace TOEFLSimulation.Core;

/// <summary>
/// The composition root for the Windows app: loads the bundled resources once and owns the saved-progress stores.
/// Create it at startup with the Resources folder (next to the exe) and a per-user data folder
/// (for example <c>%LOCALAPPDATA%\TOEFL Simulation</c>). Dispose it on exit to close the audio packs.
/// </summary>
public sealed class TOEFLCore : IDisposable
{
    private TOEFLCore(string resourcesDirectory, string dataDirectory, ISpeechTranscriber? transcriber, ISpellChecker? spellChecker)
    {
        ResourcesDirectory = resourcesDirectory;
        DataDirectory = dataDirectory;
        Store = new KeyValueStore(dataDirectory);
        QuestionBank = TOEFLQuestionBank.Load(resourcesDirectory, Store);
        StudyBank = TOEFLStudyBank.Load(resourcesDirectory);
        ExamAudio = TOEFLExamAudio.Open(resourcesDirectory);
        WordPronunciations = WordPronunciationLibrary.Open(resourcesDirectory);
        History = new TOEFLHistoryStore(Store);
        WordsDrillParagraphs = WordsDrillParagraph.All(QuestionBank);
        ReadingDrillSets = ReadingDrillSet.All(QuestionBank);
        WordsDrill = new WordsDrillStore(Store, WordsDrillParagraphs);
        ReadingDrill = new ReadingDrillStore(Store, ReadingDrillSets);
        Transcriber = transcriber;
        SpellChecker = spellChecker ?? NoSpellChecker.Instance;
    }

    /// <summary>Loads everything. Throws <see cref="InvalidDataException"/> or <see cref="IOException"/> if the bundled
    /// question bank or vocabulary is missing or does not decode (missing audio packs only leave audio empty).</summary>
    /// <param name="resourcesDirectory">The copied Mac <c>Resources</c> folder.</param>
    /// <param name="dataDirectory">Where <c>settings.json</c> (all saved progress) is kept; created if needed.</param>
    /// <param name="transcriber">Whisper-based speech recognition for Speaking scores (null: responses score 0, "unavailable").</param>
    /// <param name="spellChecker">Windows spell checker for the Writing rubric (null: no misspellings are counted).</param>
    public static TOEFLCore Load(string resourcesDirectory, string dataDirectory, ISpeechTranscriber? transcriber = null, ISpellChecker? spellChecker = null) =>
        new(resourcesDirectory, dataDirectory, transcriber, spellChecker);

    public string ResourcesDirectory { get; }
    public string DataDirectory { get; }

    /// <summary>Saved settings and progress (the Mac UserDefaults keys).</summary>
    public KeyValueStore Store { get; }
    public TOEFLQuestionBank QuestionBank { get; }
    public TOEFLStudyBank StudyBank { get; }
    public TOEFLExamAudio ExamAudio { get; }
    public AudioClipPack WordPronunciations { get; }
    public TOEFLHistoryStore History { get; }
    public IReadOnlyList<WordsDrillParagraph> WordsDrillParagraphs { get; }
    public IReadOnlyList<ReadingDrillSet> ReadingDrillSets { get; }
    public WordsDrillStore WordsDrill { get; }
    public ReadingDrillStore ReadingDrill { get; }
    public ISpeechTranscriber? Transcriber { get; }
    public ISpellChecker SpellChecker { get; }

    /// <summary>A new practice test (Dashboard "Start", guides' "Practice the … section").</summary>
    public TOEFLExamSession CreateExamSession(ExamMode mode) => new(mode, QuestionBank, Transcriber, SpellChecker);

    public ExamAudioPlayer CreateExamAudioPlayer(IAudioOutput output) => new(ExamAudio, output);

    public WordPronunciationPlayer CreateWordPronunciationPlayer(IAudioOutput output) => new(WordPronunciations, output);

    public FlashcardSession CreateFlashcardSession(WordPronunciationPlayer? pronunciation = null) => new(StudyBank.Words, Store, pronunciation);

    public WordsDrillSession CreateWordsDrillSession() => new(WordsDrill);

    public ReadingDrillSession CreateReadingDrillSession() => new(ReadingDrill);

    public ListenRepeatTrainer CreateListenRepeatTrainer(ExamAudioPlayer player) => new(QuestionBank, player);

    public InterviewTrainer CreateInterviewTrainer(ExamAudioPlayer player) => new(QuestionBank, player);

    /// <summary>The Mac app's DEBUG self-test (validation, Listening rotation, drill storage). Empty means passed.</summary>
    public IReadOnlyList<string> SelfTest()
    {
        var issues = new List<string>();
        issues.AddRange(QuestionBank.Validate(ExamAudio));
        issues.AddRange(StudyBank.Validate(WordPronunciations));
        issues.AddRange(QuestionBank.RotationIssues());
        issues.AddRange(WordsDrillStore.SelfTestIssues(WordsDrillParagraphs));
        return issues;
    }

    public void Dispose()
    {
        ExamAudio.Dispose();
        WordPronunciations.Dispose();
    }
}
