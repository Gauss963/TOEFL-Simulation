using NAudio.Wave;
using TOEFLSimulation.Core;
using Whisper.net;

namespace TOEFLSimulation.App.Services;

/// <summary>
/// <see cref="ISpeechTranscriber"/> using offline Whisper (whisper.cpp through Whisper.net) in place of the Mac app's
/// on-device Apple Speech recognizer. English only; the Listen and Repeat target sentence is passed as the initial prompt
/// (Apple's contextualStrings). Word timings come from Whisper's one-word segments. Returns null when the model file is
/// missing or Whisper cannot load, so responses score as "unavailable" exactly like a Mac without Speech permission.
/// Recognition runs on a background thread, one recording at a time.
/// </summary>
public sealed class WhisperTranscriber : ISpeechTranscriber
{
    private readonly string modelPath;
    private readonly SemaphoreSlim busy = new(1, 1);
    private readonly Lazy<WhisperFactory?> factory;

    public WhisperTranscriber(string modelPath)
    {
        this.modelPath = modelPath;
        factory = new Lazy<WhisperFactory?>(LoadFactory, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>True when the Whisper model is installed (the microphone check shows the "ready" message).</summary>
    public bool IsAvailable => File.Exists(modelPath);

    public const string ReadyMessage = "Offline speech recognition (Whisper) is ready for scoring.";
    public const string UnavailableMessage =
        "Speech recognition is unavailable because the Whisper model (Models\\ggml-base.en-q5_1.bin) is missing, so responses cannot be scored automatically. Reinstall the app to restore it.";

    public Task<SpeechTranscription?> TranscribeAsync(string audioFilePath, IReadOnlyList<string> contextualStrings, CancellationToken cancellationToken) =>
        Task.Run(async () =>
        {
            if (!IsAvailable || factory.Value is not { } whisper) return null;
            var samples = ReadSamples(audioFilePath);
            if (samples is null) return SpeechTranscription.Empty;
            // Whisper tends to "hear" its prompt in silence; a silent recording is no speech, as on the Mac.
            if (!ContainsSpeechEnergy(samples)) return SpeechTranscription.Empty;

            await busy.WaitAsync(cancellationToken);
            try
            {
                var builder = whisper.CreateBuilder()
                    .WithLanguage("en")
                    .WithThreads(Math.Max(1, Math.Min(4, Environment.ProcessorCount)))
                    .WithTokenTimestamps()
                    .WithMaxSegmentLength(1)
                    .SplitOnWord();
                var prompt = string.Join(" ", contextualStrings.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (prompt.Length > 0) builder = builder.WithPrompt(prompt);
                await using var processor = builder.Build();

                var segments = new List<SpeechTranscription.Segment>();
                await foreach (var data in processor.ProcessAsync(samples, cancellationToken))
                {
                    var word = data.Text.Trim();
                    if (word.Length == 0 || IsNonSpeechMarker(word)) continue;
                    var start = Math.Max(0, data.Start.TotalSeconds);
                    var duration = Math.Max(0, data.End.TotalSeconds - start);
                    segments.Add(new SpeechTranscription.Segment(word, start, duration));
                }
                if (segments.Count == 0) return SpeechTranscription.Empty;
                var text = string.Join(" ", segments.Select(s => s.Text));
                return new SpeechTranscription(text, segments);
            }
            finally
            {
                busy.Release();
            }
        }, cancellationToken);

    private WhisperFactory? LoadFactory()
    {
        try
        {
            return File.Exists(modelPath) ? WhisperFactory.FromPath(modelPath) : null;
        }
        catch (Exception error)
        {
            CrashLog.Write(error);
            return null;
        }
    }

    /// <summary>Reads a recording as 16 kHz mono float samples (what Whisper expects).</summary>
    private static float[]? ReadSamples(string path)
    {
        try
        {
            using var reader = new AudioFileReader(path);
            ISampleProvider provider = reader;
            if (provider.WaveFormat.Channels > 1) provider = new NAudio.Wave.SampleProviders.StereoToMonoSampleProvider(provider);
            if (provider.WaveFormat.SampleRate != SpeakingRecorder.SampleRate)
                provider = new NAudio.Wave.SampleProviders.WdlResamplingSampleProvider(provider, SpeakingRecorder.SampleRate);
            var samples = new List<float>(SpeakingRecorder.SampleRate * 50);
            var buffer = new float[SpeakingRecorder.SampleRate];
            int read;
            while ((read = provider.Read(buffer.AsSpan())) > 0)
                samples.AddRange(buffer.AsSpan(0, read).ToArray());
            return samples.ToArray();
        }
        catch (Exception error)
        {
            CrashLog.Write(error);
            return null;
        }
    }

    /// <summary>True when some 50 ms window is louder than about −45 dBFS (quiet speech; room noise stays below).</summary>
    private static bool ContainsSpeechEnergy(float[] samples)
    {
        const int window = SpeakingRecorder.SampleRate / 20;
        var threshold = Math.Pow(10, -45 / 20.0);
        for (var start = 0; start < samples.Length; start += window)
        {
            var end = Math.Min(samples.Length, start + window);
            double sum = 0;
            for (var i = start; i < end; i++) sum += samples[i] * samples[i];
            if (end > start && Math.Sqrt(sum / (end - start)) > threshold) return true;
        }
        return false;
    }

    /// <summary>Whisper's annotations for non-speech, such as [BLANK_AUDIO] or (music).</summary>
    private static bool IsNonSpeechMarker(string word) =>
        (word.StartsWith('[') && word.EndsWith(']')) || (word.StartsWith('(') && word.EndsWith(')')) || word.All(c => !char.IsLetterOrDigit(c));
}
