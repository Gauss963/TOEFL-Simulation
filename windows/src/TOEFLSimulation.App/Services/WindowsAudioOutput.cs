using System.Runtime.InteropServices.WindowsRuntime;
using TOEFLSimulation.Core.Audio;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;
using Windows.Storage.Streams;

namespace TOEFLSimulation.App.Services;

/// <summary>
/// <see cref="IAudioOutput"/> for Windows (the Mac app's AVAudioPlayer and AVSpeechSynthesizer): plays clip bytes from memory
/// with <see cref="MediaPlayer"/> and reads fallback text with the best installed US English voice. Only one sound plays at a
/// time; a new request or <see cref="Stop"/> ends the previous one with <see cref="PlaybackEnd.Stopped"/>.
/// Returned tasks complete asynchronously, so an awaiting UI-thread caller continues on the UI thread.
/// </summary>
public sealed class WindowsAudioOutput : IAudioOutput
{
    private readonly object gate = new();
    private int generation;
    private MediaPlayer? player;
    private IDisposable? source;
    private TaskCompletionSource<PlaybackEnd>? pending;
    private CancellationTokenRegistration registration;
    private SpeechSynthesizer? synthesizer;

    public Task<PlaybackEnd> PlayAsync(byte[] data, AudioClipFormat format, float volume, CancellationToken cancellationToken) =>
        Start(cancellationToken, async () =>
        {
            var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(data.AsBuffer());
            stream.Seek(0);
            return (stream, format == AudioClipFormat.M4a ? "audio/mp4" : "audio/wav");
        }, volume);

    public Task<PlaybackEnd> SpeakAsync(string text, float rate, float volume, CancellationToken cancellationToken) =>
        Start(cancellationToken, async () =>
        {
            var voice = Synthesizer();
            voice.Options.SpeakingRate = SpeakingRate(rate);
            var stream = await voice.SynthesizeTextToStreamAsync(text);
            return ((IRandomAccessStream)stream, string.IsNullOrEmpty(stream.ContentType) ? "audio/wav" : stream.ContentType);
        }, volume);

    public void Stop()
    {
        TaskCompletionSource<PlaybackEnd>? ended;
        lock (gate)
        {
            generation++;
            ended = pending;
            pending = null;
            ReleasePlayer();
        }
        registration.Dispose();
        ended?.TrySetResult(PlaybackEnd.Stopped);
    }

    public void SetVolume(float volume)
    {
        lock (gate)
        {
            if (player is not null) player.Volume = Math.Clamp(volume, 0, 1);
        }
    }

    /// <summary>AVSpeechUtterance's rate scale (0–1, 0.5 = normal) mapped to Windows' SpeakingRate (0.5–6, 1 = normal).</summary>
    public static double SpeakingRate(float avSpeechRate)
    {
        var rate = Math.Clamp(avSpeechRate, 0f, 1f);
        return rate <= 0.5f ? 0.5 + rate : 1 + (rate - 0.5) * 4;
    }

    private Task<PlaybackEnd> Start(CancellationToken cancellationToken, Func<Task<(IRandomAccessStream Stream, string ContentType)>> open, float volume)
    {
        Stop();
        if (cancellationToken.IsCancellationRequested) return Task.FromResult(PlaybackEnd.Stopped);
        var completion = new TaskCompletionSource<PlaybackEnd>(TaskCreationOptions.RunContinuationsAsynchronously);
        int mine;
        lock (gate)
        {
            mine = generation;
            pending = completion;
        }
        registration = cancellationToken.Register(() => StopIfCurrent(mine));
        _ = RunAsync(mine, completion, open, volume);
        return completion.Task;
    }

    private async Task RunAsync(int mine, TaskCompletionSource<PlaybackEnd> completion,
        Func<Task<(IRandomAccessStream Stream, string ContentType)>> open, float volume)
    {
        try
        {
            var (stream, contentType) = await open();
            lock (gate)
            {
                if (mine != generation)
                {
                    stream.Dispose();
                    return;
                }
                var media = MediaSource.CreateFromStream(stream, contentType);
                var newPlayer = new MediaPlayer
                {
                    AutoPlay = false,
                    Volume = Math.Clamp(volume, 0, 1),
                    AudioCategory = MediaPlayerAudioCategory.Speech,
                };
                newPlayer.CommandManager.IsEnabled = false;
                newPlayer.MediaEnded += (_, _) => Finish(mine, PlaybackEnd.Completed);
                newPlayer.MediaFailed += (_, _) => Finish(mine, PlaybackEnd.Failed);
                newPlayer.Source = media;
                player = newPlayer;
                source = new CompositeDisposable(media, stream);
                newPlayer.Play();
            }
        }
        catch (Exception error)
        {
            CrashLog.Write(error);
            Finish(mine, PlaybackEnd.Failed);
        }
    }

    private void Finish(int mine, PlaybackEnd end)
    {
        TaskCompletionSource<PlaybackEnd>? ended;
        lock (gate)
        {
            if (mine != generation) return;
            ended = pending;
            pending = null;
        }
        ended?.TrySetResult(end);
    }

    private void StopIfCurrent(int mine)
    {
        bool current;
        lock (gate) current = mine == generation;
        if (current) Stop();
    }

    private void ReleasePlayer()
    {
        var oldPlayer = player;
        var oldSource = source;
        player = null;
        source = null;
        if (oldPlayer is null && oldSource is null) return;
        try
        {
            if (oldPlayer is not null)
            {
                oldPlayer.Pause();
                oldPlayer.Source = null;
                oldPlayer.Dispose();
            }
            oldSource?.Dispose();
        }
        catch (Exception error)
        {
            CrashLog.Write(error);
        }
    }

    private SpeechSynthesizer Synthesizer()
    {
        lock (gate)
        {
            if (synthesizer is not null) return synthesizer;
            synthesizer = new SpeechSynthesizer();
            if (BestEnglishVoice() is { } voice) synthesizer.Voice = voice;
            return synthesizer;
        }
    }

    /// <summary>The best installed US English voice (natural/neural voices first), else any English voice.</summary>
    public static VoiceInformation? BestEnglishVoice()
    {
        try
        {
            var voices = SpeechSynthesizer.AllVoices;
            static int Rank(VoiceInformation voice)
            {
                var name = voice.DisplayName;
                if (name.Contains("Natural", StringComparison.OrdinalIgnoreCase) || name.Contains("Online", StringComparison.OrdinalIgnoreCase)) return 0;
                if (name.Contains("Aria", StringComparison.OrdinalIgnoreCase) || name.Contains("Jenny", StringComparison.OrdinalIgnoreCase)) return 1;
                if (name.Contains("Zira", StringComparison.OrdinalIgnoreCase)) return 2;
                return 3;
            }
            var us = voices.Where(v => string.Equals(v.Language, "en-US", StringComparison.OrdinalIgnoreCase)).OrderBy(Rank).FirstOrDefault();
            if (us is not null) return us;
            if (SpeechSynthesizer.DefaultVoice is { } fallback && fallback.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return fallback;
            return voices.Where(v => v.Language.StartsWith("en-", StringComparison.OrdinalIgnoreCase)).OrderBy(Rank).FirstOrDefault();
        }
        catch (Exception error)
        {
            CrashLog.Write(error);
            return null;
        }
    }

    private sealed class CompositeDisposable(params IDisposable[] items) : IDisposable
    {
        public void Dispose()
        {
            foreach (var item in items) item.Dispose();
        }
    }
}
