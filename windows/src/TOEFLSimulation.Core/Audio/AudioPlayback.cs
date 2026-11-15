using CommunityToolkit.Mvvm.ComponentModel;

namespace TOEFLSimulation.Core.Audio;

/// <summary>Container format of clip bytes handed to <see cref="IAudioOutput"/>.</summary>
public enum AudioClipFormat
{
    /// <summary>AAC in an MPEG-4 container (.m4a) — every clip in the .pack files.</summary>
    M4a,
    /// <summary>16-bit PCM WAV — the generated beep and end tones.</summary>
    Wav,
}

/// <summary>How a playback request ended.</summary>
public enum PlaybackEnd
{
    /// <summary>Played to the end.</summary>
    Completed,
    /// <summary>Could not be decoded or started (the Mac app then continues as if it had finished).</summary>
    Failed,
    /// <summary>Stopped by <see cref="IAudioOutput.Stop"/> or cancellation (the Mac app then never continues the flow).</summary>
    Stopped,
}

/// <summary>
/// Platform audio output implemented by the Windows app (MediaPlayer / Windows.Media.SpeechSynthesis). Only one sound
/// plays at a time: a new request or <see cref="Stop"/> ends the current one with <see cref="PlaybackEnd.Stopped"/>.
/// </summary>
public interface IAudioOutput
{
    /// <summary>Plays clip bytes; the task completes when playback ends.</summary>
    Task<PlaybackEnd> PlayAsync(byte[] data, AudioClipFormat format, float volume, CancellationToken cancellationToken);

    /// <summary>Speaks text with the best installed US English voice (fallback when a clip is missing).
    /// <paramref name="rate"/> uses AVSpeechUtterance's scale, where 0.5 is the default speaking rate.</summary>
    Task<PlaybackEnd> SpeakAsync(string text, float rate, float volume, CancellationToken cancellationToken);

    /// <summary>Stops whatever is playing.</summary>
    void Stop();

    /// <summary>Changes the volume of the sound that is playing now.</summary>
    void SetVolume(float volume);
}

/// <summary>
/// Swift <c>ExamAudioPlayer</c>: plays exam recordings once, in order, with a test-wide volume setting. A missing clip is
/// read aloud from its fallback text with the system voice (rate 0.5).
/// </summary>
public sealed class ExamAudioPlayer : ObservableObject
{
    private readonly TOEFLExamAudio audio;
    private readonly IAudioOutput output;
    private int generation;
    private bool isPlaying;
    private float volume = 0.85f;

    public ExamAudioPlayer(TOEFLExamAudio audio, IAudioOutput output)
    {
        this.audio = audio;
        this.output = output;
    }

    public bool IsPlaying
    {
        get => isPlaying;
        private set => SetProperty(ref isPlaying, value);
    }

    /// <summary>0–1, default 0.85. The toolbar menu offers 50%, 85%, and 100% presets.</summary>
    public float Volume
    {
        get => volume;
        set
        {
            if (SetProperty(ref volume, Math.Clamp(value, 0f, 1f))) output.SetVolume(volume);
        }
    }

    /// <summary>
    /// Plays the clip for <paramref name="key"/>; if it is missing, reads <paramref name="fallbackText"/>. Returns true when
    /// the Mac app would call its completion (finished, failed to start, or nothing to play) and false when it was stopped
    /// or replaced by another request (the Mac app then never resumes the waiting flow).
    /// </summary>
    public async Task<bool> PlayAsync(string key, string? fallbackText, CancellationToken cancellationToken = default)
    {
        if (audio.Clip(key) is { } data) return await RunAsync(token => output.PlayAsync(data, AudioClipFormat.M4a, volume, token), cancellationToken);
        if (!string.IsNullOrEmpty(fallbackText)) return await RunAsync(token => output.SpeakAsync(fallbackText, 0.5f, volume, token), cancellationToken);
        return true;
    }

    /// <summary>Plays a WAV tone (<see cref="TOEFLExamAudio.Beep"/> or <see cref="TOEFLExamAudio.EndTone"/>).</summary>
    public Task<bool> PlayToneAsync(byte[] data, CancellationToken cancellationToken = default) =>
        RunAsync(token => output.PlayAsync(data, AudioClipFormat.Wav, volume, token), cancellationToken);

    private async Task<bool> RunAsync(Func<CancellationToken, Task<PlaybackEnd>> play, CancellationToken cancellationToken)
    {
        Stop();
        var mine = ++generation;
        IsPlaying = true;
        PlaybackEnd end;
        try
        {
            end = await play(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            end = PlaybackEnd.Stopped;
        }
        if (mine != generation) return false;
        IsPlaying = false;
        return end != PlaybackEnd.Stopped && !cancellationToken.IsCancellationRequested;
    }

    public void Stop()
    {
        generation++;
        output.Stop();
        IsPlaying = false;
    }
}

/// <summary>
/// Swift <c>WordPronunciationPlayer</c>: plays a word's bundled neural clip, falling back to the system voice (rate 0.45).
/// </summary>
public sealed class WordPronunciationPlayer : ObservableObject
{
    private readonly AudioClipPack pack;
    private readonly IAudioOutput output;
    private int generation;
    private bool isSpeaking;

    public WordPronunciationPlayer(AudioClipPack pronunciations, IAudioOutput output)
    {
        pack = pronunciations;
        this.output = output;
    }

    public bool IsSpeaking
    {
        get => isSpeaking;
        private set => SetProperty(ref isSpeaking, value);
    }

    /// <summary>Plays <paramref name="word"/>; completes when it finishes or is stopped.</summary>
    public async Task SpeakAsync(string word)
    {
        Stop();
        var mine = ++generation;
        IsSpeaking = true;
        try
        {
            var end = pack.Clip(word) is { } data
                ? await output.PlayAsync(data, AudioClipFormat.M4a, 1f, CancellationToken.None)
                : PlaybackEnd.Failed;
            if (end == PlaybackEnd.Failed && mine == generation)
                await output.SpeakAsync(word, 0.45f, 1f, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
        }
        if (mine == generation) IsSpeaking = false;
    }

    /// <summary>The speaker button and the V key: stops if speaking, otherwise plays the word.</summary>
    public Task ToggleAsync(string word)
    {
        if (!IsSpeaking) return SpeakAsync(word);
        Stop();
        return Task.CompletedTask;
    }

    public void Stop()
    {
        generation++;
        output.Stop();
        IsSpeaking = false;
    }
}

/// <summary>
/// Microphone recording implemented by the Windows app (AAC .m4a, 44.1 kHz mono, like the Mac recorder). Only what the
/// Speaking flow needs; the microphone check page uses the platform recorder directly.
/// </summary>
public interface ISpeakingRecorder
{
    /// <summary>Asks for permission if needed and starts recording to a new file (see <see cref="SpeakingRecording.NewFilePath"/>).
    /// Returns false when the microphone is unavailable or not allowed.</summary>
    Task<bool> StartAsync(string questionID, CancellationToken cancellationToken);

    /// <summary>Stops recording and returns the recorded duration in seconds (0 if not recording).</summary>
    double Stop();

    /// <summary>The file of the last successful recording.</summary>
    string? LastRecordingPath { get; }
}

/// <summary>Recording helpers shared with the Mac app's <c>SpeakingResponseRecorder</c>.</summary>
public static class SpeakingRecording
{
    /// <summary><c>toefl-{questionID with / replaced by -}-{UUID}.m4a</c> in <paramref name="directory"/> (the temp folder on the Mac).</summary>
    public static string NewFilePath(string directory, string questionID) =>
        Path.Combine(directory, $"toefl-{questionID.Replace("/", "-", StringComparison.Ordinal)}-{Guid.NewGuid().ToString("D").ToUpperInvariant()}.m4a");

    /// <summary>Meter level 0–1 from an average power in dBFS: <c>(power + 55) / 50</c>, clamped (sampled every 0.08 s on the Mac).</summary>
    public static float LevelFromAveragePower(float decibels) => Math.Max(0, Math.Min(1, (decibels + 55) / 50));

    /// <summary>Microphone check label for a level: "Too loud" above 0.85, "Good" above 0.35, otherwise "Too quiet".</summary>
    public static string MicrophoneCheckLabel(float level) => level > 0.85f ? "Too loud" : (level > 0.35f ? "Good" : "Too quiet");

    /// <summary>Microphone check bar color: red above 0.85, green above 0.35, otherwise orange.</summary>
    public static string MicrophoneCheckColor(float level) => level > 0.85f ? "red" : (level > 0.35f ? "green" : "orange");

    public const string MicrophoneCheckInstructions = "Press Start, then say: “I live in a city and I like to walk to work.” The bar should reach the green zone.";
    public const string SpeechReadyMessage = "On-device speech recognition is ready for scoring.";
    public const string SpeechUnavailableMessage = "Speech recognition is unavailable or not allowed, so responses cannot be scored automatically. Allow Speech Recognition in System Settings › Privacy & Security.";
    public const string MicrophoneUnavailableTitle = "Microphone unavailable";
    public const string MicrophoneUnavailableMessage = "Allow microphone access in System Settings to record Speaking responses.";
}
