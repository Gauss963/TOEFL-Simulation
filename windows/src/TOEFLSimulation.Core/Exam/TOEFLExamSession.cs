using CommunityToolkit.Mvvm.ComponentModel;
using TOEFLSimulation.Core.Audio;

namespace TOEFLSimulation.Core;

/// <summary>Swift <c>TOEFLExamPhase</c>: which screen the exam container shows.</summary>
public enum TOEFLExamPhase
{
    /// <summary>Section directions page (Begin button).</summary>
    Directions,
    /// <summary>Items are being answered.</summary>
    Testing,
    /// <summary>Waiting for Speaking transcriptions to be scored.</summary>
    Scoring,
    /// <summary>The score report (<see cref="TOEFLExamSession.Result"/>) is ready.</summary>
    Completed,
}

/// <summary>
/// Swift <c>TOEFLExamSession</c>: one practice test (full-length or a single section). Holds the form, the position, the
/// answers, the module/task clocks, adaptive routing, Speaking scoring, and the final result.
/// <para>
/// Binding: every state change raises <c>PropertyChanged</c> with an empty property name ("everything changed");
/// <see cref="Tick()"/> raises only <see cref="SecondsRemaining"/> unless a part ends. <see cref="AnswerChanged"/> names the item.
/// The UI must call <see cref="Tick()"/> about every 0.25 s while <see cref="Phase"/> is Testing (the Mac app does).
/// </para>
/// </summary>
public sealed class TOEFLExamSession : ObservableObject
{
    private readonly TOEFLQuestionBank bank;
    private readonly ISpeechTranscriber? transcriber;
    private readonly ISpellChecker spellChecker;
    private readonly Func<DateTimeOffset> clock;
    private readonly List<TOEFLExamSection> sections;
    private readonly Dictionary<string, TOEFLAnswer> answers = [];
    private readonly HashSet<string> playedStimuli = [];
    private readonly List<TOEFLSectionSummary> summaries = [];
    private readonly DateTimeOffset startedAt;
    private double partRemaining;
    private DateTimeOffset? lastTick;
    private bool awaitingSectionScores;
    private bool isTimeVisible = true;
    private int secondsRemaining;

    /// <param name="mode">Full-length or a single section.</param>
    /// <param name="bank">The question bank (its exposure history is used and updated).</param>
    /// <param name="transcriber">Speech recognition for Speaking; null scores every response 0 ("unavailable").</param>
    /// <param name="spellChecker">Spelling check for the Writing rubric; null counts no misspellings.</param>
    /// <param name="formSeed">Fixed form seed (tests); random when null, as on the Mac.</param>
    /// <param name="clock">Time source (tests); the system clock when null.</param>
    public TOEFLExamSession(ExamMode mode, TOEFLQuestionBank bank, ISpeechTranscriber? transcriber = null, ISpellChecker? spellChecker = null,
        ulong? formSeed = null, Func<DateTimeOffset>? clock = null)
    {
        Mode = mode;
        this.bank = bank;
        this.transcriber = transcriber;
        this.spellChecker = spellChecker ?? NoSpellChecker.Instance;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        FormSeed = formSeed ?? SeededGenerator.RandomSeed();
        startedAt = this.clock();
        sections = mode == ExamMode.FullLength
            ? [.. bank.FullSections(FormSeed)]
            : [bank.PracticeSection(mode.SectionKinds()[0], FormSeed)];
    }

    public ExamMode Mode { get; }
    public ulong FormSeed { get; }

    public IReadOnlyList<TOEFLExamSection> Sections => sections;
    public int CurrentSectionIndex { get; private set; }
    public int CurrentQuestionIndex { get; private set; }
    public IReadOnlyDictionary<string, TOEFLAnswer> Answers => answers;
    public IReadOnlySet<string> PlayedStimuli => playedStimuli;
    public TOEFLExamPhase Phase { get; private set; } = TOEFLExamPhase.Directions;

    /// <summary>Seconds left on the current module/task clock (shown as mm:ss, see <see cref="TextUtil.ClockString"/>).</summary>
    public int SecondsRemaining
    {
        get => secondsRemaining;
        private set => SetProperty(ref secondsRemaining, value);
    }

    /// <summary>Speaking responses still being transcribed and scored.</summary>
    public int PendingScores { get; private set; }

    /// <summary>The toolbar's Show Time / Hide Time toggle.</summary>
    public bool IsTimeVisible
    {
        get => isTimeVisible;
        set => SetProperty(ref isTimeVisible, value);
    }

    /// <summary>The finished result (when <see cref="Phase"/> is Completed). Save it with <c>TOEFLHistoryStore.Add</c>.</summary>
    public TOEFLExamResult? Result { get; private set; }

    /// <summary>Set while a recording plays, so listening time is not spent on audio.</summary>
    public bool IsClockPaused { get; set; }

    /// <summary>Raised with the question ID whenever an answer changes.</summary>
    public event EventHandler<string>? AnswerChanged;

    private void NotifyAll() => OnPropertyChanged(string.Empty);

    // MARK: - Position

    public TOEFLExamSection CurrentSection => sections[CurrentSectionIndex];
    public TOEFLQuestion CurrentQuestion => CurrentSection.Questions[CurrentQuestionIndex];
    public TOEFLAnswer CurrentAnswer => AnswerFor(CurrentQuestion.Id);

    public TOEFLAnswer AnswerFor(string questionID) => answers.TryGetValue(questionID, out var answer) ? answer : TOEFLAnswer.Empty;

    /// <summary>Replaces an answer (the Swift <c>answers</c> dictionary is publicly settable).</summary>
    public void SetAnswer(string questionID, TOEFLAnswer answer)
    {
        answers[questionID] = answer;
        AnswerChanged?.Invoke(this, questionID);
        NotifyAll();
    }

    public TOEFLTimedPart CurrentPart
    {
        get
        {
            var parts = CurrentSection.TimedParts;
            return parts.FirstOrDefault(p => p.Range.Contains(CurrentQuestionIndex)) ?? parts[0];
        }
    }

    /// <summary>Complete the Words paragraphs are answered on one screen; every other item has its own screen.</summary>
    public IntRange ScreenRange(int index)
    {
        var questions = CurrentSection.Questions;
        var question = questions[index];
        if (question.TaskType != TOEFLTaskType.CompleteWords || question.SetID is not { } setID) return new IntRange(index, index + 1);
        int lower = index, upper = index + 1;
        while (lower > 0 && questions[lower - 1].SetID == setID) lower--;
        while (upper < questions.Count && questions[upper].SetID == setID) upper++;
        return new IntRange(lower, upper);
    }

    public IntRange CurrentScreen => ScreenRange(CurrentQuestionIndex);

    public IReadOnlyList<TOEFLQuestion> CurrentScreenQuestions => CurrentSection.Questions.Skip(CurrentScreen.LowerBound).Take(CurrentScreen.Count).ToList();

    /// <summary>"Question 3 of 50" or "Questions 1–10 of 50".</summary>
    public string QuestionLabel
    {
        get
        {
            var screen = CurrentScreen;
            var total = CurrentSection.Questions.Count;
            return screen.Count > 1
                ? $"Questions {screen.LowerBound + 1}–{screen.UpperBound} of {total}"
                : $"Question {screen.LowerBound + 1} of {total}";
        }
    }

    public bool CanGoBack
    {
        get
        {
            var part = CurrentPart;
            return part.AllowsBack && CurrentScreen.LowerBound > part.Range.LowerBound;
        }
    }

    /// <summary>When true, Next should first confirm ending the part (see <see cref="ExamText.PartEndTitle"/>).</summary>
    public bool IsLastInPart => CurrentScreen.UpperBound >= CurrentPart.Range.UpperBound;

    public bool IsLastPart => Equals(CurrentSection.TimedParts[^1], CurrentPart);

    /// <summary>"Module 1"/"Module 2" for adaptive sections, otherwise null.</summary>
    public string? RouteLabel => CurrentSection.Kind.IsAdaptive() ? CurrentPart.Title : null;

    public int AnsweredCount => CurrentSection.Questions.Count(q => !AnswerFor(q.Id).IsEmpty);

    /// <summary>Distinct task types of the current section in order (the directions page list).</summary>
    public IReadOnlyList<TOEFLTaskType> TaskTypesInCurrentSection => CurrentSection.Questions.Select(q => q.TaskType).Distinct().ToList();

    // MARK: - Flow

    /// <summary>The directions page's Begin button.</summary>
    public void BeginSection()
    {
        CurrentQuestionIndex = 0;
        StartPart(CurrentSection.TimedParts[0]);
        Phase = TOEFLExamPhase.Testing;
        NotifyAll();
    }

    private void StartPart(TOEFLTimedPart part)
    {
        CurrentQuestionIndex = part.Range.LowerBound;
        partRemaining = part.Seconds;
        SecondsRemaining = part.Seconds;
        lastTick = clock();
    }

    /// <summary>Advances the clock to now. Call about every 0.25 s while testing.</summary>
    public void Tick() => Tick(clock());

    /// <summary>Advances the clock to <paramref name="now"/>; ends the part when its time runs out. Speaking has no visible clock.</summary>
    public void Tick(DateTimeOffset now)
    {
        if (Phase != TOEFLExamPhase.Testing) return;
        var delta = lastTick is { } last ? (now - last).TotalSeconds : 0;
        lastTick = now;
        if (IsClockPaused || CurrentSection.Kind == TOEFLSectionKind.Speaking) return;
        partRemaining = Math.Max(0, partRemaining - delta);
        SecondsRemaining = (int)Math.Ceiling(partRemaining);
        if (partRemaining <= 0) AdvancePart();
    }

    /// <summary>The Next button (after confirming when <see cref="IsLastInPart"/>), and the Speaking flow after each response.</summary>
    public void GoNext()
    {
        if (IsLastInPart)
        {
            AdvancePart();
        }
        else
        {
            CurrentQuestionIndex = CurrentScreen.UpperBound;
            NotifyAll();
        }
    }

    public void GoBack()
    {
        if (!CanGoBack) return;
        CurrentQuestionIndex = ScreenRange(CurrentScreen.LowerBound - 1).LowerBound;
        NotifyAll();
    }

    /// <summary>Ends the current module or task; time left on its clock is forfeited, as in the real test.</summary>
    public void AdvancePart()
    {
        var parts = CurrentSection.TimedParts;
        var part = CurrentPart;
        if (CurrentSection.Kind.IsAdaptive() && CurrentSection.Route is null && Equals(part, parts[0]))
            ConfigureAdaptiveModule();
        var refreshed = CurrentSection.TimedParts;
        var index = refreshed.ToList().FindIndex(p => p.Range.LowerBound == part.Range.LowerBound);
        if (index >= 0 && index + 1 < refreshed.Count)
        {
            StartPart(refreshed[index + 1]);
            NotifyAll();
        }
        else
        {
            FinishSection();
        }
    }

    // MARK: - Answers

    public void SelectChoice(string choiceID) => SetAnswer(CurrentQuestion.Id, CurrentAnswer with { SelectedChoiceID = choiceID });

    /// <summary>Complete the Words: store the letters typed in a blank (sanitize with <see cref="ExamText.SanitizeBlankEntry"/> first).</summary>
    public void UpdateTextEntry(string value, string questionID) => SetAnswer(questionID, AnswerFor(questionID) with { TextEntry = value });

    /// <summary>Places a chunk in the next empty blank. Chunks may repeat (for example two "to"), so placed chunks are
    /// removed by blank position rather than by text.</summary>
    public void AddSentenceToken(string token)
    {
        var answer = CurrentAnswer;
        if (answer.OrderedTokens.Count >= CurrentQuestion.CorrectSentenceTokens.Count) return;
        SetAnswer(CurrentQuestion.Id, answer with { OrderedTokens = [.. answer.OrderedTokens, token] });
    }

    public void RemoveSentenceToken(int slot)
    {
        var answer = CurrentAnswer;
        if (slot < 0 || slot >= answer.OrderedTokens.Count) return;
        var tokens = answer.OrderedTokens.ToList();
        tokens.RemoveAt(slot);
        SetAnswer(CurrentQuestion.Id, answer with { OrderedTokens = tokens });
    }

    public void ClearSentence() => SetAnswer(CurrentQuestion.Id, CurrentAnswer with { OrderedTokens = [] });

    public void UpdateWrittenResponse(string value) => SetAnswer(CurrentQuestion.Id, CurrentAnswer with { WrittenResponse = value });

    // MARK: - Listening

    /// <summary>True when the item's recording must play before its question is shown (first item of an unplayed stimulus).</summary>
    public bool NeedsStimulus(TOEFLQuestion question) =>
        question.TaskType.IsListeningStimulus() && (question.SetIndex ?? 0) == 0 && !playedStimuli.Contains(question.AudioKey);

    public void MarkStimulusPlayed(TOEFLQuestion question)
    {
        playedStimuli.Add(question.AudioKey);
        NotifyAll();
    }

    // MARK: - Speaking

    /// <summary>
    /// Stores a finished recording and scores it (transcription, then the rubric). The Speaking flow starts this without
    /// awaiting it, as the Mac app does; await it in tests. Continuations run on the caller's synchronization context.
    /// </summary>
    public async Task RecordSpeakingAsync(TOEFLQuestion question, double duration, string? audioFilePath, CancellationToken cancellationToken = default)
    {
        SetAnswer(question.Id, AnswerFor(question.Id) with { RecordingDuration = duration });
        PendingScores++;
        NotifyAll();
        SpeechTranscription? transcription = null;
        if (audioFilePath is not null && transcriber is not null)
        {
            try
            {
                IReadOnlyList<string> context = question.TaskType == TOEFLTaskType.ListenRepeat ? [question.AudioScript ?? ""] : [];
                transcription = await transcriber.TranscribeAsync(audioFilePath, context, cancellationToken);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                transcription = null;
            }
        }
        var scored = question.TaskType == TOEFLTaskType.ListenRepeat
            ? SpeakingRubric.ScoreRepeat(question.AudioScript ?? "", transcription)
            : SpeakingRubric.ScoreInterview(question.AudioScript ?? "", transcription, question.ResponseSeconds ?? 45);
        SetAnswer(question.Id, AnswerFor(question.Id) with { Transcript = transcription?.Text, RubricScore = scored.Score, RubricNote = scored.Detail });
        PendingScores--;
        if (awaitingSectionScores && PendingScores == 0)
        {
            awaitingSectionScores = false;
            SummarizeSection();
        }
        NotifyAll();
    }

    // MARK: - Scoring

    public void FinishSection()
    {
        if (Phase != TOEFLExamPhase.Testing) return;
        lastTick = null;
        if (CurrentSection.RouterCount is not null && CurrentSection.Route is null) ConfigureAdaptiveModule();
        if (CurrentSection.Kind == TOEFLSectionKind.Speaking && PendingScores > 0)
        {
            awaitingSectionScores = true;
            Phase = TOEFLExamPhase.Scoring;
            NotifyAll();
            return;
        }
        SummarizeSection();
        NotifyAll();
    }

    private void SummarizeSection()
    {
        var section = CurrentSection;
        bank.RememberExposure(section.Questions);
        if (section.Kind == TOEFLSectionKind.Writing)
        {
            foreach (var question in section.Questions.Where(q => q.TaskType is TOEFLTaskType.WriteEmail or TOEFLTaskType.AcademicDiscussion))
            {
                var answer = AnswerFor(question.Id);
                var scored = WritingRubric.Score(question, answer.WrittenResponse, spellChecker);
                answers[question.Id] = answer with { RubricScore = scored.Score, RubricNote = scored.Note };
                AnswerChanged?.Invoke(this, question.Id);
            }
        }
        var scoredQuestions = section.Questions.Where(q => q.CountsTowardScore).ToList();
        var objective = scoredQuestions.Select(q => TOEFLAnswerMatcher.IsCorrect(q, AnswerFor(q.Id))).OfType<bool>().ToList();
        var correct = objective.Count(c => c);
        var answered = section.Questions.Count(q => !AnswerFor(q.Id).IsEmpty);
        double band;
        string detail;
        int raw;
        switch (section.Kind)
        {
            case TOEFLSectionKind.Reading:
            case TOEFLSectionKind.Listening:
                band = TOEFLPracticeScoring.ObjectiveBand(correct, objective.Count, section.Route);
                raw = correct;
                var pretest = section.Questions.Count - scoredQuestions.Count;
                detail = $"Raw {correct}/{objective.Count} scored items · {section.Route?.Title() ?? "Linear"} · {pretest} unscored pretest items not counted.";
                break;
            case TOEFLSectionKind.Writing:
                (band, detail, raw) = TOEFLPracticeScoring.WritingBand(section.Questions, answers);
                break;
            default:
                (band, detail, raw) = TOEFLPracticeScoring.SpeakingBand(section.Questions, answers);
                break;
        }
        summaries.Add(new TOEFLSectionSummary
        {
            Kind = section.Kind,
            Correct = correct,
            ObjectiveTotal = objective.Count,
            Answered = answered,
            Total = section.Questions.Count,
            Route = section.Route,
            Band = band,
            RubricDetail = detail,
            RawScore = raw,
            RawMaximum = section.Kind.RawMaximum(),
        });

        if (CurrentSectionIndex + 1 < sections.Count)
        {
            CurrentSectionIndex++;
            CurrentQuestionIndex = 0;
            Phase = TOEFLExamPhase.Directions;
        }
        else
        {
            CompleteExam();
        }
    }

    /// <summary>Routes to the upper module at 65% or better on the router's scored items, and draws that module.</summary>
    private void ConfigureAdaptiveModule()
    {
        if (CurrentSection.RouterCount is not { } routerCount) return;
        var routerQuestions = CurrentSection.Questions.Take(routerCount).Where(q => q.CountsTowardScore).ToList();
        var objective = routerQuestions.Select(q => TOEFLAnswerMatcher.IsCorrect(q, AnswerFor(q.Id))).OfType<bool>().ToList();
        var accuracy = objective.Count == 0 ? 0 : (double)objective.Count(c => c) / objective.Count;
        var route = accuracy >= 0.65 ? AdaptiveRoute.Upper : AdaptiveRoute.Lower;
        var router = CurrentSection.Questions.Take(routerCount).ToList();
        var module = bank.ModuleQuestions(CurrentSection.Kind, route, FormSeed, router.Select(q => q.Id).ToHashSet());
        sections[CurrentSectionIndex].Questions = [.. router, .. module];
        sections[CurrentSectionIndex].Route = route;
    }

    private void CompleteExam()
    {
        Phase = TOEFLExamPhase.Completed;
        var overall = TOEFLPracticeScoring.Overall(summaries.Select(s => s.Band).ToList());
        var reviews = sections.SelectMany(section => section.Questions.Select((question, index) => new TOEFLQuestionReview
        {
            Number = index + 1,
            Question = question,
            Answer = AnswerFor(question.Id),
            IsCorrect = TOEFLAnswerMatcher.IsCorrect(question, AnswerFor(question.Id)),
        })).ToList();
        var now = clock();
        Result = new TOEFLExamResult
        {
            Id = Guid.NewGuid(),
            CompletedAt = now,
            Mode = Mode,
            OverallBand = overall,
            ComparableTotal120 = TOEFLPracticeScoring.Comparable120(overall),
            Cefr = TOEFLPracticeScoring.Cefr(overall),
            ElapsedSeconds = Math.Max(0, (int)(now - startedAt).TotalSeconds),
            Sections = summaries.ToList(),
            Reviews = reviews,
        };
    }

    // MARK: - Testing support (the Mac app's DEBUG -completePractice)

    /// <summary>Answers every item with its key (speaking items rubric 4) and finishes all sections.</summary>
    public void CompleteWithSampleAnswersForTesting()
    {
        var guardCount = 0;
        while (Phase != TOEFLExamPhase.Completed && guardCount < 10)
        {
            guardCount++;
            if (Phase == TOEFLExamPhase.Directions) BeginSection();
            if (CurrentSection.RouterCount is not null && CurrentSection.Route is null)
            {
                foreach (var question in CurrentSection.Questions.Take(CurrentSection.RouterCount ?? 0)) answers[question.Id] = SampleAnswer(question);
                ConfigureAdaptiveModule();
            }
            foreach (var question in CurrentSection.Questions) answers[question.Id] = SampleAnswer(question);
            FinishSection();
        }
        NotifyAll();
    }

    private static TOEFLAnswer SampleAnswer(TOEFLQuestion question)
    {
        var answer = new TOEFLAnswer
        {
            SelectedChoiceID = question.CorrectChoiceID,
            TextEntry = question.AcceptedTextAnswers.FirstOrDefault() ?? "",
            OrderedTokens = question.CorrectSentenceTokens,
        };
        if (question.TaskType is TOEFLTaskType.WriteEmail or TOEFLTaskType.AcademicDiscussion)
            answer = answer with
            {
                WrittenResponse = question.SampleAnswer ?? "Dear Professor, I am writing because the schedule changed. However, I would appreciate your help. For example, could we meet on Friday? Thank you. Best regards.",
            };
        if (question.Section == TOEFLSectionKind.Speaking)
            answer = answer with
            {
                RecordingDuration = question.ResponseSeconds ?? 10,
                RubricScore = 4,
                Transcript = question.TaskType == TOEFLTaskType.ListenRepeat ? question.AudioScript : "Sample response.",
            };
        return answer;
    }
}
