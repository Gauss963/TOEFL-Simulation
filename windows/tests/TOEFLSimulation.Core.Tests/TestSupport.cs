using System.Text.Json;
using TOEFLSimulation.Core.Audio;

namespace TOEFLSimulation.Core.Tests;

/// <summary>Resources loaded once for all tests.</summary>
public static class Shared
{
    private static readonly Lazy<TOEFLQuestionBank> bank = new(() => TOEFLQuestionBank.Load(RepoPaths.Resources));
    private static readonly Lazy<TOEFLExamAudio> examAudio = new(() => TOEFLExamAudio.Open(RepoPaths.Resources));
    private static readonly Lazy<AudioClipPack> words = new(() => WordPronunciationLibrary.Open(RepoPaths.Resources));
    private static readonly Lazy<TOEFLStudyBank> study = new(() => TOEFLStudyBank.Load(RepoPaths.Resources));

    /// <summary>A bank without exposure storage (never reads or writes a history).</summary>
    public static TOEFLQuestionBank Bank => bank.Value;
    public static TOEFLExamAudio ExamAudio => examAudio.Value;
    public static AudioClipPack WordPronunciations => words.Value;
    public static TOEFLStudyBank StudyBank => study.Value;
}

/// <summary>Reference data recorded from the Mac app's Swift code by Golden/Harness/generate.sh.</summary>
public static class Golden
{
    private static readonly Lazy<JsonDocument> document = new(() =>
        JsonDocument.Parse(File.ReadAllBytes(Path.Combine(RepoPaths.Root, "windows", "tests", "TOEFLSimulation.Core.Tests", "Golden", "golden.json"))));

    public static JsonElement Root => document.Value.RootElement;

    public static JsonElement Get(string name) => Root.GetProperty(name);

    public static List<string> Strings(this JsonElement element) => element.EnumerateArray().Select(e => e.GetString()!).ToList();

    public static SpeechTranscription? Transcription(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null) return null;
        var segments = element.GetProperty("segments").EnumerateArray()
            .Select(s => new SpeechTranscription.Segment(s.GetProperty("text").GetString()!, s.GetProperty("start").GetDouble(), s.GetProperty("duration").GetDouble()))
            .ToList();
        return new SpeechTranscription(element.GetProperty("text").GetString()!, segments);
    }
}

/// <summary>A spell checker that returns counts recorded from the macOS spell checker.</summary>
public sealed class RecordedSpellChecker(Dictionary<string, int> counts) : ISpellChecker
{
    public int CountMisspellings(string text) => counts.TryGetValue(text, out var count) ? count : throw new KeyNotFoundException(text);
}

/// <summary>A temporary data directory deleted after the test.</summary>
public sealed class TempDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "toefl-core-tests-" + Guid.NewGuid().ToString("N"));

    public TempDirectory() => Directory.CreateDirectory(Path);

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch (IOException) { }
    }
}
