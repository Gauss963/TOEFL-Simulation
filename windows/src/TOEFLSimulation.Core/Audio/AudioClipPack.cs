using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace TOEFLSimulation.Core.Audio;

/// <summary>
/// Swift <c>AudioClipPack</c>: pre-rendered audio clips (Kokoro-82M, Apache-2.0) bundled as resource packs with a byte-range
/// index. Large packs are split into volumes (<c>Name.pack</c>, <c>Name-2.pack</c>, …); an index entry in <c>Name.json</c>
/// is <c>[offset, length]</c> or <c>[offset, length, volume]</c>. Clips are AAC in an MPEG-4 (.m4a) container.
/// Packs are never loaded into memory: each clip is read with a positional file read. Thread-safe.
/// </summary>
public sealed class AudioClipPack : IDisposable
{
    private readonly SafeFileHandle[] volumes;
    private readonly long[] volumeLengths;
    private readonly Dictionary<string, int[]> clips;
    private readonly bool lowercasedKeys;

    /// <summary>Opens <c>{name}.json</c> and its <c>.pack</c> volumes in <paramref name="directory"/>. Missing files give an empty pack.</summary>
    public AudioClipPack(string directory, string name, bool lowercasedKeys)
    {
        this.lowercasedKeys = lowercasedKeys;
        clips = ReadIndex(Path.Combine(directory, name + ".json"));
        var handles = new List<SafeFileHandle>();
        var lengths = new List<long>();
        while (true)
        {
            var file = Path.Combine(directory, handles.Count == 0 ? $"{name}.pack" : $"{name}-{handles.Count + 1}.pack");
            if (!File.Exists(file)) break;
            var handle = File.OpenHandle(file, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.RandomAccess);
            handles.Add(handle);
            lengths.Add(RandomAccess.GetLength(handle));
        }
        volumes = [.. handles];
        volumeLengths = [.. lengths];
    }

    private static Dictionary<string, int[]> ReadIndex(string path)
    {
        if (!File.Exists(path)) return [];
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            var result = new Dictionary<string, int[]>(StringComparer.Ordinal);
            if (!document.RootElement.TryGetProperty("clips", out var clipsElement) || clipsElement.ValueKind != JsonValueKind.Object) return [];
            foreach (var clip in clipsElement.EnumerateObject())
                result[clip.Name] = clip.Value.EnumerateArray().Select(v => v.GetInt32()).ToArray();
            return result;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException)
        {
            return [];
        }
    }

    public int VolumeCount => volumes.Length;

    /// <summary>Number of indexed clips (0 when no pack volume exists).</summary>
    public int ClipCount => volumes.Length == 0 ? 0 : clips.Count;

    /// <summary>All index keys.</summary>
    public IEnumerable<string> Keys => clips.Keys;

    private string Lookup(string key) => lowercasedKeys ? key.ToLowerInvariant().Trim() : key;

    /// <summary>Whether a valid clip exists for <paramref name="key"/> (no file read).</summary>
    public bool HasClip(string key) => Locate(key) is not null;

    private (int Volume, long Offset, int Length)? Locate(string key)
    {
        if (!clips.TryGetValue(Lookup(key), out var range) || (range.Length != 2 && range.Length != 3)) return null;
        var volume = range.Length == 3 ? range[2] : 0;
        if (volume < 0 || volume >= volumes.Length || range[0] < 0 || range[1] <= 0 || (long)range[0] + range[1] > volumeLengths[volume]) return null;
        return (volume, range[0], range[1]);
    }

    /// <summary>The clip's bytes (an .m4a file), or null if the key is unknown or its range is invalid.</summary>
    public byte[]? Clip(string key)
    {
        if (Locate(key) is not { } location) return null;
        var buffer = new byte[location.Length];
        var read = 0;
        while (read < buffer.Length)
        {
            var count = RandomAccess.Read(volumes[location.Volume], buffer.AsSpan(read), location.Offset + read);
            if (count <= 0) return null;
            read += count;
        }
        return buffer;
    }

    public void Dispose()
    {
        foreach (var handle in volumes) handle.Dispose();
    }
}

/// <summary>Swift <c>WordPronunciationLibrary</c>: natural word pronunciations for flash cards (keys are lower-cased words).</summary>
public static class WordPronunciationLibrary
{
    public const string PackName = "WordPronunciations";

    public static AudioClipPack Open(string resourcesDirectory) => new(resourcesDirectory, PackName, lowercasedKeys: true);
}

/// <summary>Swift <c>TOEFLExamAudio</c>: multi-voice recordings for Listening stimuli and Speaking prompts, plus the tones.</summary>
public sealed class TOEFLExamAudio : IDisposable
{
    public const string PackName = "ExamAudio";

    public TOEFLExamAudio(AudioClipPack pack) => Pack = pack;

    public static TOEFLExamAudio Open(string resourcesDirectory) => new(new AudioClipPack(resourcesDirectory, PackName, lowercasedKeys: false));

    public AudioClipPack Pack { get; }

    public bool HasClip(string key) => Pack.HasClip(key);

    /// <summary>The recording for a question's <see cref="TOEFLQuestion.AudioKey"/> or an intro key.</summary>
    public byte[]? Clip(string key) => Pack.Clip(key);

    /// <summary>Key of the narrator introduction for a set (played before the first Speaking item of a set).</summary>
    public static string IntroKey(string setID) => $"intro-{setID}";

    /// <summary>"Start speaking" signal: an 880 Hz, 0.32 s sine tone (WAV, 24 kHz mono 16-bit).</summary>
    public static byte[] Beep { get; } = Tone(880, 0.32);

    /// <summary>"Stop" signal: a 660 Hz, 0.22 s sine tone (WAV).</summary>
    public static byte[] EndTone { get; } = Tone(660, 0.22);

    /// <summary>A short sine tone with 20 ms fades, as a 16-bit mono PCM WAV file.</summary>
    public static byte[] Tone(double frequency, double seconds)
    {
        const double sampleRate = 24_000.0;
        var count = (int)(sampleRate * seconds);
        var samples = new byte[count * 2];
        for (var index = 0; index < count; index++)
        {
            var t = index / sampleRate;
            var envelope = Math.Min(1, Math.Min(t, seconds - t) / 0.02);
            var value = (short)(Math.Sin(2 * Math.PI * frequency * t) * envelope * 0.45 * short.MaxValue);
            System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(samples.AsSpan(index * 2), value);
        }
        using var stream = new MemoryStream(44 + samples.Length);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write((uint)(36 + samples.Length));
        writer.Write("WAVEfmt "u8);
        writer.Write(16u);
        writer.Write((ushort)1);
        writer.Write((ushort)1);
        writer.Write((uint)sampleRate);
        writer.Write((uint)(sampleRate * 2));
        writer.Write((ushort)2);
        writer.Write((ushort)16);
        writer.Write("data"u8);
        writer.Write((uint)samples.Length);
        writer.Write(samples);
        writer.Flush();
        return stream.ToArray();
    }

    public void Dispose() => Pack.Dispose();
}
