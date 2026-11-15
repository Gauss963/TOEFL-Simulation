using TOEFLSimulation.Core.Audio;
using TOEFLSimulation.Core.Storage;

namespace TOEFLSimulation.Core.Tests;

public class ExamSessionTests
{
    private sealed class ManualClock(DateTimeOffset start)
    {
        public DateTimeOffset Now { get; set; } = start;
        public void Advance(double seconds) => Now = Now.AddSeconds(seconds);
    }

    private static readonly DateTimeOffset Start = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    private static TOEFLQuestionBank NewBank(KeyValueStore? store = null) => new(Shared.Bank.AllQuestions, store ?? KeyValueStore.InMemory());

    [Fact]
    public void FullLengthWithKeyedAnswersScoresTopBands()
    {
        var store = KeyValueStore.InMemory();
        var clock = new ManualClock(Start);
        var session = new TOEFLExamSession(ExamMode.FullLength, NewBank(store), formSeed: 2026, clock: () => clock.Now);
        Assert.Equal([50, 47, 12, 11], session.Sections.Select(s => s.Questions.Count));
        clock.Advance(3600);
        session.CompleteWithSampleAnswersForTesting();
        Assert.Equal(TOEFLExamPhase.Completed, session.Phase);
        var result = session.Result!;
        Assert.Equal(ExamMode.FullLength, result.Mode);
        Assert.Equal(3600, result.ElapsedSeconds);
        Assert.Equal(4, result.Sections.Count);
        var reading = result.Sections[0];
        Assert.Equal(AdaptiveRoute.Upper, reading.Route);
        Assert.Equal(35, reading.Correct);
        Assert.Equal(35, reading.ObjectiveTotal);
        Assert.Equal(6, reading.Band);
        Assert.Equal("Raw 35/35 scored items · Upper module · 15 unscored pretest items not counted.", reading.RubricDetail);
        Assert.Equal(35, reading.RawMaximum);
        var listening = result.Sections[1];
        Assert.Equal("Raw 35/35 scored items · Upper module · 12 unscored pretest items not counted.", listening.RubricDetail);
        var speaking = result.Sections[3];
        Assert.Equal(44, speaking.RawScore); // 11 × 4
        Assert.Equal(TOEFLPracticeScoring.HalfBand(1 + 44.0 / 55 * 5), speaking.Band);
        Assert.Equal(120, result.Reviews.Count);
        Assert.Equal(TOEFLPracticeScoring.Comparable120(result.OverallBand), result.ComparableTotal120);
        // Exposure was remembered for every section.
        Assert.Equal(session.Sections.SelectMany(s => s.Questions).Select(q => q.SetKey).Distinct().Count(), store.GetStringArray(StorageKeys.QuestionExposure)!.Count);
    }

    [Fact]
    public void RouterAccuracyBelow65PercentRoutesLower()
    {
        var session = new TOEFLExamSession(ExamMode.ReadingPractice, NewBank(), formSeed: 11);
        session.BeginSection();
        var scored = session.CurrentSection.Questions.Take(35).Where(q => q.CountsTowardScore).ToList();
        // 12 of 20 correct = 60% → lower; answer through the public API.
        foreach (var question in scored.Take(12))
            session.SetAnswer(question.Id, new TOEFLAnswer { SelectedChoiceID = question.CorrectChoiceID, TextEntry = question.AcceptedTextAnswers.FirstOrDefault() ?? "" });
        session.AdvancePart();
        Assert.Equal(AdaptiveRoute.Lower, session.CurrentSection.Route);
        Assert.Equal("Module 2", session.RouteLabel);
        Assert.Equal(35, session.CurrentQuestionIndex);
        Assert.Equal(9 * 60, session.SecondsRemaining);

        var upper = new TOEFLExamSession(ExamMode.ReadingPractice, NewBank(), formSeed: 11);
        upper.BeginSection();
        foreach (var question in upper.CurrentSection.Questions.Take(35).Where(q => q.CountsTowardScore).Take(13))
            upper.SetAnswer(question.Id, new TOEFLAnswer { SelectedChoiceID = question.CorrectChoiceID, TextEntry = question.AcceptedTextAnswers.FirstOrDefault() ?? "" });
        upper.AdvancePart(); // 13/20 = 65% → upper
        Assert.Equal(AdaptiveRoute.Upper, upper.CurrentSection.Route);
        // The module drawn after routing is the one the bank draws for that route with the form seed.
        var router = upper.CurrentSection.Questions.Take(35).Select(q => q.Id).ToHashSet();
        Assert.Equal(NewBank().ModuleQuestions(TOEFLSectionKind.Reading, AdaptiveRoute.Upper, 11, router).Select(q => q.Id), upper.CurrentSection.Questions.Skip(35).Select(q => q.Id));
    }

    [Fact]
    public void ClockCountsDownPausesAndEndsTheModule()
    {
        var clock = new ManualClock(Start);
        var session = new TOEFLExamSession(ExamMode.ListeningPractice, NewBank(), formSeed: 3, clock: () => clock.Now);
        Assert.Equal(TOEFLExamPhase.Directions, session.Phase);
        session.BeginSection();
        Assert.Equal(18 * 60, session.SecondsRemaining);
        Assert.Equal("00:00", TextUtil.ClockString(0));
        Assert.Equal("18:00", TextUtil.ClockString(session.SecondsRemaining));
        clock.Advance(10.2);
        session.Tick(clock.Now);
        Assert.Equal(18 * 60 - 10, session.SecondsRemaining); // ceil(1069.8)
        session.IsClockPaused = true;
        clock.Advance(100);
        session.Tick(clock.Now);
        Assert.Equal(18 * 60 - 10, session.SecondsRemaining);
        session.IsClockPaused = false;
        clock.Advance(18 * 60);
        session.Tick(clock.Now);
        Assert.Equal("Module 2", session.CurrentPart.Title);
        Assert.Equal(AdaptiveRoute.Lower, session.CurrentSection.Route); // no answers → lower
        Assert.Equal(7 * 60, session.SecondsRemaining);
        clock.Advance(7 * 60 + 1);
        session.Tick(clock.Now);
        Assert.Equal(TOEFLExamPhase.Completed, session.Phase);
        Assert.Equal(1, session.Result!.OverallBand);
    }

    [Fact]
    public void SpeakingHasNoClock()
    {
        var clock = new ManualClock(Start);
        var session = new TOEFLExamSession(ExamMode.SpeakingPractice, NewBank(), formSeed: 3, clock: () => clock.Now);
        session.BeginSection();
        clock.Advance(10_000);
        session.Tick(clock.Now);
        Assert.Equal(TOEFLExamPhase.Testing, session.Phase);
        Assert.Equal(8 * 60, session.SecondsRemaining);
    }

    [Fact]
    public void CompleteTheWordsScreensAndBackNavigation()
    {
        var session = new TOEFLExamSession(ExamMode.ReadingPractice, NewBank(), formSeed: 5);
        session.BeginSection();
        Assert.Equal(TOEFLTaskType.CompleteWords, session.CurrentQuestion.TaskType);
        Assert.Equal(new IntRange(0, 10), session.CurrentScreen);
        Assert.Equal("Questions 1–10 of 50", session.QuestionLabel);
        Assert.Equal(10, session.CurrentScreenQuestions.Count);
        Assert.False(session.CanGoBack);
        session.GoNext();
        Assert.Equal(10, session.CurrentQuestionIndex);
        Assert.True(session.CanGoBack);
        session.GoNext();
        Assert.Equal(20, session.CurrentQuestionIndex);
        Assert.Equal("Question 21 of 50", session.QuestionLabel);
        session.GoBack();
        Assert.Equal(10, session.CurrentQuestionIndex);
        Assert.Equal(session.TaskTypesInCurrentSection, [TOEFLTaskType.CompleteWords, TOEFLTaskType.ReadDailyLife, TOEFLTaskType.ReadAcademicPassage]);
        Assert.Equal("End Module 1?", ExamText.PartEndTitle(session));
        Assert.Equal("Continue", ExamText.PartEndConfirm(session));
    }

    [Fact]
    public void ListeningCannotGoBackAndNeedsStimulus()
    {
        var session = new TOEFLExamSession(ExamMode.ListeningPractice, NewBank(), formSeed: 5);
        session.BeginSection();
        var first = session.CurrentQuestion;
        Assert.True(session.NeedsStimulus(first));
        Assert.False(ExamText.IsNextEnabled(session, isAudioPlaying: false));
        session.MarkStimulusPlayed(first);
        Assert.False(session.NeedsStimulus(first));
        Assert.True(ExamText.IsNextEnabled(session, isAudioPlaying: false));
        session.GoNext();
        Assert.False(session.CanGoBack);
    }

    [Fact]
    public void BuildSentenceAnswerEditing()
    {
        var session = new TOEFLExamSession(ExamMode.WritingPractice, NewBank(), formSeed: 8);
        session.BeginSection();
        var question = session.CurrentQuestion;
        Assert.Equal(TOEFLTaskType.BuildSentence, question.TaskType);
        foreach (var token in question.CorrectSentenceTokens) session.AddSentenceToken(token);
        session.AddSentenceToken("extra"); // ignored: every blank is full
        Assert.Equal(question.CorrectSentenceTokens, session.CurrentAnswer.OrderedTokens);
        Assert.True(TOEFLAnswerMatcher.IsCorrect(question, session.CurrentAnswer));
        Assert.Empty(BuildSentence.RemainingTokens(question, session.CurrentAnswer.OrderedTokens).Except(question.SentenceTokens.Except(question.CorrectSentenceTokens)));
        session.RemoveSentenceToken(0);
        Assert.Equal(question.CorrectSentenceTokens.Count - 1, session.CurrentAnswer.OrderedTokens.Count);
        session.ClearSentence();
        Assert.True(session.CurrentAnswer.IsEmpty);
        Assert.Equal(question.CorrectSentenceTokens.Count, BuildSentence.TemplatePieces(question).Count(p => p.Slot is not null));
        Assert.Equal("Hello world", BuildSentence.CapitalizingFirstLetter("hello world"));
    }

    [Fact]
    public void WritingSectionScoresResponsesWithTheRubric()
    {
        var session = new TOEFLExamSession(ExamMode.WritingPractice, NewBank(), formSeed: 8);
        session.BeginSection();
        session.AdvancePart(); // Build a Sentence ends
        Assert.Equal(TOEFLTaskType.WriteEmail, session.CurrentQuestion.TaskType);
        Assert.Equal(7 * 60, session.SecondsRemaining);
        var email = session.CurrentQuestion;
        session.UpdateWrittenResponse(email.SampleAnswer!);
        session.AdvancePart();
        Assert.Equal(10 * 60, session.SecondsRemaining);
        Assert.True(session.IsLastPart);
        Assert.Equal("End the Writing section?", ExamText.PartEndTitle(session));
        session.AdvancePart();
        Assert.Equal(TOEFLExamPhase.Completed, session.Phase);
        var expected = WritingRubric.Score(email, email.SampleAnswer!);
        Assert.Equal(expected.Score, session.AnswerFor(email.Id).RubricScore);
        Assert.Equal(expected.Note, session.AnswerFor(email.Id).RubricNote);
        Assert.StartsWith($"Raw {expected.Score}/20 · Build a Sentence 0/10 · Email {expected.Score}/5 · Academic Discussion 0/5", session.Result!.Sections[0].RubricDetail);
    }

    private sealed class FakeTranscriber(Func<string, SpeechTranscription?> reply) : ISpeechTranscriber
    {
        public List<(string Path, IReadOnlyList<string> Context)> Calls { get; } = [];
        public TaskCompletionSource Gate { get; } = new();

        public async Task<SpeechTranscription?> TranscribeAsync(string audioFilePath, IReadOnlyList<string> contextualStrings, CancellationToken cancellationToken)
        {
            Calls.Add((audioFilePath, contextualStrings));
            await Gate.Task;
            return reply(audioFilePath);
        }
    }

    [Fact]
    public async Task SpeakingWaitsForPendingScoresBeforeSummarizing()
    {
        var transcriber = new FakeTranscriber(_ => null);
        var session = new TOEFLExamSession(ExamMode.SpeakingPractice, NewBank(), transcriber, formSeed: 4);
        session.BeginSection();
        var question = session.CurrentQuestion;
        Assert.Equal(TOEFLTaskType.ListenRepeat, question.TaskType);
        var scoring = session.RecordSpeakingAsync(question, 9.5, "/tmp/a.m4a", TestContext.Current.CancellationToken);
        Assert.Equal(1, session.PendingScores);
        Assert.Equal([question.AudioScript!], transcriber.Calls[0].Context);
        session.FinishSection();
        Assert.Equal(TOEFLExamPhase.Scoring, session.Phase);
        Assert.Equal("Recordings are transcribed on this device and scored with the official 0–5 rubrics. 1 remaining.", ExamText.ScoringMessage(1));
        transcriber.Gate.SetResult();
        await scoring;
        Assert.Equal(TOEFLExamPhase.Completed, session.Phase);
        var answer = session.AnswerFor(question.Id);
        Assert.Equal(9.5, answer.RecordingDuration);
        Assert.Equal(0, answer.RubricScore);
        Assert.Equal("Speech recognition was unavailable, so this response could not be scored.", answer.RubricNote);
    }

    [Fact]
    public async Task SpeakingScoresTheTranscription()
    {
        var session = new TOEFLExamSession(ExamMode.SpeakingPractice, NewBank(), null, formSeed: 4);
        var repeat = session.Sections[0].Questions[0];
        var transcriber = new FakeTranscriber(_ => new SpeechTranscription(repeat.AudioScript!, []));
        transcriber.Gate.SetResult();
        session = new TOEFLExamSession(ExamMode.SpeakingPractice, NewBank(), transcriber, formSeed: 4);
        session.BeginSection();
        await session.RecordSpeakingAsync(repeat, 8, "/tmp/r.m4a", TestContext.Current.CancellationToken);
        Assert.Equal(5, session.AnswerFor(repeat.Id).RubricScore);
        Assert.Equal(repeat.AudioScript, session.AnswerFor(repeat.Id).Transcript);
        var interview = session.Sections[0].Questions.First(q => q.TaskType == TOEFLTaskType.Interview);
        await session.RecordSpeakingAsync(interview, 45, null, TestContext.Current.CancellationToken); // no recording file → not transcribed
        Assert.Single(transcriber.Calls);
        Assert.Equal(0, session.AnswerFor(interview.Id).RubricScore);
    }

    private sealed class FakeOutput : IAudioOutput
    {
        public List<string> Log { get; } = [];
        public PlaybackEnd Result { get; set; } = PlaybackEnd.Completed;
        public Task<PlaybackEnd> PlayAsync(byte[] data, AudioClipFormat format, float volume, CancellationToken cancellationToken)
        {
            Log.Add($"play {format} {data.Length}");
            return Task.FromResult(Result);
        }
        public Task<PlaybackEnd> SpeakAsync(string text, float rate, float volume, CancellationToken cancellationToken)
        {
            Log.Add($"speak {rate}");
            return Task.FromResult(Result);
        }
        public void Stop() => Log.Add("stop");
        public void SetVolume(float volume) => Log.Add($"volume {volume}");
    }

    private sealed class FakeRecorder(bool works) : ISpeakingRecorder
    {
        public string? LastRecordingPath { get; private set; }
        public int Starts { get; private set; }
        public Task<bool> StartAsync(string questionID, CancellationToken cancellationToken)
        {
            Starts++;
            if (works) LastRecordingPath = $"/tmp/{questionID}.m4a";
            return Task.FromResult(works);
        }
        public double Stop() => 7.5;
    }

    [Fact]
    public async Task SpeakingFlowRunsIntroPromptToneRecordingAndAdvances()
    {
        var clock = new ManualClock(Start);
        var delays = new List<double>();
        Task Delay(TimeSpan span, CancellationToken token)
        {
            delays.Add(span.TotalMilliseconds);
            clock.Advance(span.TotalSeconds);
            return Task.CompletedTask;
        }
        var transcriber = new FakeTranscriber(_ => SpeechTranscription.Empty);
        transcriber.Gate.SetResult();
        var session = new TOEFLExamSession(ExamMode.SpeakingPractice, NewBank(), transcriber, formSeed: 4, clock: () => clock.Now);
        session.BeginSection();
        var output = new FakeOutput();
        var player = new ExamAudioPlayer(Shared.ExamAudio, output);
        var recorder = new FakeRecorder(works: true);
        var question = session.CurrentQuestion;
        var flow = new SpeakingFlow(session, question, player, recorder, Delay, () => clock.Now);
        Assert.Equal("Listen", flow.StatusTitle);
        await flow.RunAsync(TestContext.Current.CancellationToken);
        Assert.False(flow.RecordingFailed);
        Assert.Equal(0, flow.Remaining);
        Assert.Equal(SpeakingStage.Saving, flow.Stage);
        Assert.Equal(1, session.CurrentQuestionIndex);
        var hasIntro = question.Intro is not null && question.SetID is not null;
        Assert.Equal(hasIntro ? 600 : 100, delays[0]);
        Assert.Equal(1200, delays[^1]);
        Assert.Equal((int)Math.Ceiling(flow.Total / 0.1), delays.Count(d => d == 100));
        Assert.Contains(output.Log, l => l == $"play Wav {TOEFLExamAudio.Beep.Length}");
        Assert.Contains(output.Log, l => l == $"play Wav {TOEFLExamAudio.EndTone.Length}");
        Assert.Equal(7.5, session.AnswerFor(question.Id).RecordingDuration);
        Assert.Equal(0, session.AnswerFor(question.Id).RubricScore); // empty transcription → "No speech was detected."
        Assert.Equal("No speech was detected.", session.AnswerFor(question.Id).RubricNote);
    }

    [Fact]
    public async Task SpeakingFlowStopsWhenAudioIsStoppedAndFlagsMicrophoneFailure()
    {
        var session = new TOEFLExamSession(ExamMode.SpeakingPractice, NewBank(), null, formSeed: 4);
        session.BeginSection();
        var output = new FakeOutput { Result = PlaybackEnd.Stopped };
        var player = new ExamAudioPlayer(Shared.ExamAudio, output);
        var recorder = new FakeRecorder(works: false);
        var flow = new SpeakingFlow(session, session.CurrentQuestion, player, recorder, (_, _) => Task.CompletedTask);
        await flow.RunAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, recorder.Starts);
        Assert.Equal(0, session.CurrentQuestionIndex);

        output.Result = PlaybackEnd.Failed; // failed playback continues, as on the Mac
        var clockTime = Start;
        var flow2 = new SpeakingFlow(session, session.CurrentQuestion, player, recorder, (span, _) => { clockTime = clockTime.Add(span); return Task.CompletedTask; }, () => clockTime);
        await flow2.RunAsync(TestContext.Current.CancellationToken);
        Assert.True(flow2.RecordingFailed);
        Assert.Equal(1, session.CurrentQuestionIndex);
        Assert.Equal(0, session.AnswerFor(flow2.Question.Id).RecordingDuration);
    }

    [Fact]
    public async Task ListeningStimulusPausesTheClockAndMarksPlayed()
    {
        var session = new TOEFLExamSession(ExamMode.ListeningPractice, NewBank(), formSeed: 6);
        session.BeginSection();
        var output = new FakeOutput();
        var player = new ExamAudioPlayer(Shared.ExamAudio, output);
        Assert.True(await ListeningStimulus.PlayAsync(session, session.CurrentQuestion, player, TestContext.Current.CancellationToken));
        Assert.False(session.IsClockPaused);
        Assert.False(session.NeedsStimulus(session.CurrentQuestion));
        Assert.StartsWith("play M4a", output.Log.Last(l => l.StartsWith("play", StringComparison.Ordinal)));

        // A missing clip falls back to the system voice at rate 0.5.
        var missing = session.CurrentQuestion with { Id = "missing", SetID = null, TaskType = TOEFLTaskType.ListenChooseResponse };
        Assert.True(await ListeningStimulus.PlayAsync(session, missing, player, TestContext.Current.CancellationToken));
        Assert.Equal("speak 0.5", output.Log[^1]);

        player.Volume = 0.5f;
        Assert.Equal("volume 0.5", output.Log[^1]);
    }
}
