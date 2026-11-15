using TOEFLSimulation.Core;
using TOEFLSimulation.Core.Audio;

namespace TOEFLSimulation.App.Services;

/// <summary>
/// The app's shared services: the loaded <see cref="TOEFLCore"/> and the Windows implementations of Core's platform
/// interfaces. Every page uses this class instead of creating its own copies.
/// <para>
/// Usage from a page (the core loads off the UI thread when the app starts, so always await <see cref="Ready"/> first):
/// <code>
/// await AppServices.Ready;                 // throws if the bundled question bank could not be loaded
/// var core = AppServices.Core;             // question bank, history, drills, factories (see Core/README.md)
/// var player = core.CreateExamAudioPlayer(AppServices.Audio);
/// var words = core.CreateWordPronunciationPlayer(AppServices.Audio);
/// </code>
/// </para>
/// <list type="bullet">
/// <item><see cref="Audio"/> — <see cref="IAudioOutput"/> (MediaPlayer for .m4a/.wav clips, Windows speech synthesis as the
/// fallback voice). One sound at a time across the whole app; stop it when a page goes away.</item>
/// <item><see cref="Recorder"/> — <see cref="ISpeakingRecorder"/> with a live <see cref="SpeakingRecorder.Level"/> (0–1) for
/// microphone meters. Records 16 kHz mono WAV, which <see cref="Transcriber"/> reads.</item>
/// <item><see cref="Transcriber"/> — offline Whisper (<c>Models\ggml-base.en-q5_1.bin</c> next to the exe);
/// <see cref="WhisperTranscriber.IsAvailable"/> is false when the model is missing (Speaking then scores "unavailable").</item>
/// <item><see cref="SpellChecker"/> — Windows spell checking (en-US) for the Writing rubric.</item>
/// </list>
/// All members are safe to read from the UI thread at any time; only <see cref="Core"/> requires <see cref="Ready"/>.
/// </summary>
public static class AppServices
{
    private static TOEFLCore? core;

    static AppServices()
    {
        Audio = new WindowsAudioOutput();
        Recorder = new SpeakingRecorder();
        Transcriber = new WhisperTranscriber(Path.Combine(AppContext.BaseDirectory, "Models", "ggml-base.en-q5_1.bin"));
        SpellChecker = new WindowsSpellChecker();
        Ready = Task.Run(() =>
        {
            try
            {
                core = TOEFLCore.Load(AppPaths.ResourcesDirectory, AppPaths.DataDirectory, Transcriber, SpellChecker);
            }
            catch (Exception error)
            {
                CrashLog.Write(error);
                throw;
            }
        });
        AppDomain.CurrentDomain.ProcessExit += (_, _) => core?.Dispose();
    }

    /// <summary>Starts loading (the first access to this class does that too). Called once when the app launches.</summary>
    public static void Start() => _ = Ready;

    /// <summary>The loaded core. Await <see cref="Ready"/> before reading it.</summary>
    public static TOEFLCore Core => core ?? throw new InvalidOperationException("AppServices.Core was used before AppServices.Ready completed.");

    /// <summary>Completes when <see cref="Core"/> is loaded; faults (InvalidDataException/IOException) if the bundled
    /// question bank or vocabulary is missing or damaged. The Practice Tests page reports that and quits.</summary>
    public static Task Ready { get; }

    /// <summary>Clip playback and the fallback system voice (one sound at a time).</summary>
    public static IAudioOutput Audio { get; }

    /// <summary>Microphone recorder used by Speaking items and the microphone checks.</summary>
    public static SpeakingRecorder Recorder { get; }

    /// <summary>Offline speech recognition for Speaking scores.</summary>
    public static WhisperTranscriber Transcriber { get; }

    /// <summary>en-US spell checker for the Writing rubric.</summary>
    public static ISpellChecker SpellChecker { get; }
}
