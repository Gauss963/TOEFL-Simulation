using System.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.Win32;
using NAudio.Wave;
using TOEFLSimulation.Core.Audio;

namespace TOEFLSimulation.App.Services;

/// <summary>
/// Microphone recorder (the Mac app's SpeakingResponseRecorder) built on NAudio. Records 16 kHz mono 16-bit WAV, the format
/// Whisper reads directly, to <c>%TEMP%\toefl-{id}-{UUID}.wav</c>, and meters the input level every 80 ms like the Mac
/// recorder (<see cref="Level"/>, 0–1, for the meters). <see cref="IsRecording"/> and <see cref="Level"/> raise
/// <see cref="PropertyChanged"/> on the UI thread that started the recording.
/// </summary>
public sealed class SpeakingRecorder : ISpeakingRecorder, INotifyPropertyChanged
{
    public const int SampleRate = 16_000;

    public const string MicrophoneUnavailableTitle = SpeakingRecording.MicrophoneUnavailableTitle;

    /// <summary>The Mac message adapted to Windows privacy settings.</summary>
    public const string MicrophoneUnavailableMessage =
        "Allow microphone access in Windows Settings › Privacy & security › Microphone (turn on “Let desktop apps access your microphone”), and make sure a microphone is connected, to record Speaking responses.";

    private readonly object gate = new();
    private WaveIn? waveIn;
    private WaveFileWriter? writer;
    private string? currentPath;
    private long bytesRecorded;
    private DispatcherQueue? dispatcher;
    private bool isRecording;
    private float level;
    private bool permissionDenied;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsRecording
    {
        get => isRecording;
        private set
        {
            if (isRecording == value) return;
            isRecording = value;
            Raise(nameof(IsRecording));
        }
    }

    /// <summary>Input level 0–1 from the average power: <see cref="SpeakingRecording.LevelFromAveragePower"/>.</summary>
    public float Level
    {
        get => level;
        private set
        {
            if (level == value) return;
            level = value;
            Raise(nameof(Level));
        }
    }

    /// <summary>Set when the last start failed because Windows blocks the microphone or none is connected.</summary>
    public bool PermissionDenied
    {
        get => permissionDenied;
        private set
        {
            if (permissionDenied == value) return;
            permissionDenied = value;
            Raise(nameof(PermissionDenied));
        }
    }

    public string? LastRecordingPath { get; private set; }

    /// <summary>True when Windows privacy settings allow this desktop app to use a microphone and one is connected.</summary>
    public static bool IsMicrophoneAvailable()
    {
        try
        {
            if (WaveIn.DeviceCount == 0) return false;
        }
        catch (Exception)
        {
            return false;
        }
        return !IsDeniedByPrivacySettings();
    }

    private static bool IsDeniedByPrivacySettings()
    {
        const string consent = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";
        static bool Denied(RegistryKey root, string path)
        {
            try
            {
                using var key = root.OpenSubKey(path);
                return string.Equals(key?.GetValue("Value") as string, "Deny", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }
        return Denied(Registry.LocalMachine, consent) || Denied(Registry.CurrentUser, consent)
            || Denied(Registry.CurrentUser, consent + @"\NonPackaged");
    }

    /// <summary>The Mac requestPermission(): Windows has no prompt for desktop apps, so this checks the privacy settings.</summary>
    public Task<bool> RequestPermissionAsync() => Task.Run(IsMicrophoneAvailable);

    public async Task<bool> StartAsync(string questionID, CancellationToken cancellationToken)
    {
        Stop();
        dispatcher = DispatcherQueue.GetForCurrentThread() ?? dispatcher;
        var allowed = await RequestPermissionAsync();
        if (cancellationToken.IsCancellationRequested) return false;
        if (!allowed)
        {
            PermissionDenied = true;
            return false;
        }
        var path = Path.ChangeExtension(SpeakingRecording.NewFilePath(Path.GetTempPath(), questionID), ".wav");
        try
        {
            // WaveIn posts RecordingStopped to the creating thread's context; create it without one so the
            // UI thread is never needed to finish a recording.
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            WaveIn capture;
            try
            {
                capture = new WaveIn
                {
                    WaveFormat = new WaveFormat(SampleRate, 16, 1),
                    BufferMilliseconds = 80,
                    NumberOfBuffers = 3,
                };
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
            var file = new WaveFileWriter(path, capture.WaveFormat);
            lock (gate)
            {
                waveIn = capture;
                writer = file;
                currentPath = path;
                bytesRecorded = 0;
            }
            capture.DataAvailable += OnDataAvailable;
            capture.RecordingStopped += (_, _) => capture.Dispose();
            capture.StartRecording();
            PermissionDenied = false;
            LastRecordingPath = path;
            IsRecording = true;
            return true;
        }
        catch (Exception error)
        {
            CrashLog.Write(error);
            Abandon();
            PermissionDenied = true;
            return false;
        }
    }

    /// <summary>Stops recording, closes the WAV file, and returns its duration in seconds (0 if not recording).</summary>
    public double Stop()
    {
        WaveIn? capture;
        double duration;
        lock (gate)
        {
            capture = waveIn;
            waveIn = null;
            duration = bytesRecorded / (double)(SampleRate * 2);
            try
            {
                writer?.Dispose();
            }
            catch (Exception error)
            {
                CrashLog.Write(error);
            }
            writer = null;
            currentPath = null;
            bytesRecorded = 0;
        }
        if (capture is null)
        {
            Level = 0;
            return 0;
        }
        try
        {
            capture.DataAvailable -= OnDataAvailable;
            capture.StopRecording();
        }
        catch (Exception error)
        {
            CrashLog.Write(error);
        }
        IsRecording = false;
        Level = 0;
        return duration;
    }

    private void Abandon()
    {
        string? path;
        lock (gate)
        {
            path = currentPath;
            try { writer?.Dispose(); } catch (Exception) { }
            try { waveIn?.Dispose(); } catch (Exception) { }
            writer = null;
            waveIn = null;
            currentPath = null;
        }
        if (path is not null)
        {
            try { File.Delete(path); } catch (Exception) { }
        }
        LastRecordingPath = null;
        IsRecording = false;
        Level = 0;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        double sum = 0;
        var samples = e.BytesRecorded / 2;
        lock (gate)
        {
            if (writer is null || !ReferenceEquals(sender, waveIn)) return;
            writer.Write(e.Buffer, 0, e.BytesRecorded);
            bytesRecorded += e.BytesRecorded;
        }
        for (var i = 0; i + 1 < e.BytesRecorded; i += 2)
        {
            var sample = BitConverter.ToInt16(e.Buffer, i) / 32768.0;
            sum += sample * sample;
        }
        var rms = samples > 0 ? Math.Sqrt(sum / samples) : 0;
        var decibels = rms > 0 ? (float)(20 * Math.Log10(rms)) : -160f;
        var newLevel = SpeakingRecording.LevelFromAveragePower(decibels);
        Post(() =>
        {
            if (IsRecording) Level = newLevel;
        });
    }

    private void Raise(string name) => Post(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)));

    private void Post(Action action)
    {
        var queue = dispatcher;
        if (queue is null || queue.HasThreadAccess) action();
        else queue.TryEnqueue(() => action());
    }
}
