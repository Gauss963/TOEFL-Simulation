using CommunityToolkit.Mvvm.ComponentModel;
using TOEFLSimulation.Core.Audio;

namespace TOEFLSimulation.Core;

/// <summary>Swift <c>SpeakingFlowView.Stage</c>.</summary>
public enum SpeakingStage { Intro, Prompt, Tone, Recording, Saving }

/// <summary>
/// The automatic sequence of one Speaking item (SpeakingFlowView.run() on the Mac): the set's intro (first item only) and a
/// 0.6 s pause, the prompt, the beep, recording for <see cref="Total"/> seconds, the end tone, scoring in the background, a
/// 1.2 s pause (so a Bluetooth headset can switch back from call mode), then the next item.
/// <para>Create one per item when it appears and await <see cref="RunAsync"/>; cancel the token when the item's view goes
/// away (exit). Bind to <see cref="Stage"/>, <see cref="Remaining"/>, <see cref="StatusTitle"/>, and <see cref="RecordingFailed"/>.</para>
/// </summary>
public sealed class SpeakingFlow : ObservableObject
{
    private readonly TOEFLExamSession session;
    private readonly ExamAudioPlayer player;
    private readonly ISpeakingRecorder recorder;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private readonly Func<DateTimeOffset> clock;
    private SpeakingStage stage = SpeakingStage.Prompt;
    private double remaining;
    private bool recordingFailed;

    public SpeakingFlow(TOEFLExamSession session, TOEFLQuestion question, ExamAudioPlayer player, ISpeakingRecorder recorder,
        Func<TimeSpan, CancellationToken, Task>? delay = null, Func<DateTimeOffset>? clock = null)
    {
        this.session = session;
        Question = question;
        this.player = player;
        this.recorder = recorder;
        this.delay = delay ?? ((span, token) => Task.Delay(span, token));
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public TOEFLQuestion Question { get; }

    public SpeakingStage Stage
    {
        get => stage;
        private set
        {
            if (SetProperty(ref stage, value)) OnPropertyChanged(nameof(StatusTitle));
        }
    }

    /// <summary>Seconds left while recording (the countdown ring shows <c>ceil(Remaining)</c> and Remaining / Total).</summary>
    public double Remaining
    {
        get => remaining;
        private set => SetProperty(ref remaining, value);
    }

    /// <summary>The recording window: responseSeconds, else 45 for interviews and 10 for Listen and Repeat.</summary>
    public double Total => Question.ResponseSeconds ?? (Question.TaskType == TOEFLTaskType.Interview ? 45 : 10);

    public bool IsFirstInSet => (Question.SetIndex ?? 0) == 0;

    /// <summary>Set when the microphone could not start (show the "Microphone unavailable" alert).</summary>
    public bool RecordingFailed
    {
        get => recordingFailed;
        private set => SetProperty(ref recordingFailed, value);
    }

    public string StatusTitle => Stage switch
    {
        SpeakingStage.Intro or SpeakingStage.Prompt => "Listen",
        SpeakingStage.Tone => "Get ready to speak",
        SpeakingStage.Recording => "Speak now",
        _ => "Response saved",
    };

    /// <summary>Runs the item. Returns when the flow has moved to the next item, or stops early when cancelled or when
    /// audio is stopped (as the Mac flow never resumes after <c>stopMedia()</c>).</summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (IsFirstInSet && Question.SetID is { } setID && Question.Intro is not null)
            {
                Stage = SpeakingStage.Intro;
                if (!await player.PlayAsync(TOEFLExamAudio.IntroKey(setID), Question.Intro, cancellationToken)) return;
                await delay(TimeSpan.FromMilliseconds(600), cancellationToken);
            }
            if (cancellationToken.IsCancellationRequested) return;
            Stage = SpeakingStage.Prompt;
            if (!await player.PlayAsync(Question.AudioKey, Question.AudioScript, cancellationToken)) return;
            if (cancellationToken.IsCancellationRequested) return;
            Stage = SpeakingStage.Tone;
            if (!await player.PlayToneAsync(TOEFLExamAudio.Beep, cancellationToken)) return;
            if (cancellationToken.IsCancellationRequested) return;
            var started = await recorder.StartAsync(Question.Id, cancellationToken);
            if (!started) RecordingFailed = true;
            Stage = SpeakingStage.Recording;
            Remaining = Total;
            var begin = clock();
            while (Remaining > 0 && !cancellationToken.IsCancellationRequested)
            {
                await delay(TimeSpan.FromMilliseconds(100), cancellationToken);
                Remaining = Math.Max(0, Total - (clock() - begin).TotalSeconds);
            }
            if (cancellationToken.IsCancellationRequested) return;
            var duration = started ? recorder.Stop() : 0;
            Stage = SpeakingStage.Saving;
            if (!await player.PlayToneAsync(TOEFLExamAudio.EndTone, cancellationToken)) return;
            _ = session.RecordSpeakingAsync(Question, duration, started ? recorder.LastRecordingPath : null);
            await delay(TimeSpan.FromMilliseconds(1_200), cancellationToken);
            if (cancellationToken.IsCancellationRequested) return;
            session.GoNext();
        }
        catch (OperationCanceledException)
        {
        }
    }
}

/// <summary>Listening stimulus playback (ListeningStimulusView's task on the Mac).</summary>
public static class ListeningStimulus
{
    /// <summary>
    /// Pauses the module clock, plays the item's recording (or reads intro + script with the system voice), then resumes the
    /// clock and marks the stimulus played so its questions appear. Returns false if playback was stopped.
    /// </summary>
    public static async Task<bool> PlayAsync(TOEFLExamSession session, TOEFLQuestion question, ExamAudioPlayer player, CancellationToken cancellationToken = default)
    {
        session.IsClockPaused = true;
        if (!await player.PlayAsync(question.AudioKey, ExamText.StimulusFallbackText(question), cancellationToken)) return false;
        session.IsClockPaused = false;
        session.MarkStimulusPlayed(question);
        return true;
    }

    /// <summary>The Mac app's <c>stopMedia()</c>: stop audio and recording and resume the clock (Exit, Back, Next, part end).</summary>
    public static void StopMedia(TOEFLExamSession session, ExamAudioPlayer player, ISpeakingRecorder? recorder, bool isRecording)
    {
        player.Stop();
        if (isRecording) recorder?.Stop();
        session.IsClockPaused = false;
    }
}
