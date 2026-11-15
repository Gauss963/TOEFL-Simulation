using System.Text.Json;
using TOEFLSimulation.Core.Json;
using TOEFLSimulation.Core.Storage;

namespace TOEFLSimulation.Core.Tests;

public class StorageTests
{
    [Fact]
    public void KeyValueStoreRoundTripsThroughTheFile()
    {
        using var temp = new TempDirectory();
        var store = new KeyValueStore(temp.Path);
        store.SetString("toefl-simulation.flashcards.mode", "infinite");
        store.SetStringArray("list", ["b", "a"]);
        store.SetBool("flag", true);
        store.SetDouble("number", 2.5);
        store.SetJson("answer", new TOEFLAnswer { SelectedChoiceID = "c", RubricScore = 3 });
        store.SetRawJson("raw", """{"x":[1,null]}""");

        var reloaded = new KeyValueStore(temp.Path);
        Assert.Equal("infinite", reloaded.GetString("toefl-simulation.flashcards.mode"));
        Assert.Equal(["b", "a"], reloaded.GetStringArray("list"));
        Assert.True(reloaded.GetBool("flag"));
        Assert.Equal(2.5, reloaded.GetDouble("number"));
        Assert.Equal(new TOEFLAnswer { SelectedChoiceID = "c", RubricScore = 3 }, reloaded.GetJson<TOEFLAnswer>("answer"));
        Assert.Equal("""{"x":[1,null]}""", reloaded.GetRawJson("raw"));
        Assert.Null(reloaded.GetStringArray("flag"));
        Assert.Null(reloaded.GetString("missing"));
        Assert.Null(reloaded.GetJson<List<TOEFLExamResult>>("list"));

        reloaded.Remove("list");
        Assert.False(new KeyValueStore(temp.Path).Contains("list"));
        Assert.False(File.Exists(Path.Combine(temp.Path, "settings.json.tmp")));
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(temp.Path, "settings.json")));
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
    }

    [Fact]
    public void KeyValueStoreIsThreadSafe()
    {
        using var temp = new TempDirectory();
        var store = new KeyValueStore(temp.Path);
        Parallel.For(0, 200, i => store.SetString($"k{i % 20}", i.ToString()));
        var reloaded = new KeyValueStore(temp.Path);
        Assert.Equal(20, reloaded.Keys.Count);
    }

    [Fact]
    public void CorruptFileIsSetAside()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "settings.json"), "{not json");
        var store = new KeyValueStore(temp.Path);
        Assert.Empty(store.Keys);
        Assert.True(File.Exists(Path.Combine(temp.Path, "settings.json.corrupt")));
    }

    [Fact]
    public void DatesAndUuidsUseSwiftEncoding()
    {
        var result = new TOEFLExamResult
        {
            Id = Guid.Parse("e621e1f8-c36c-495a-93fc-0c247a3e6e5f"),
            CompletedAt = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
            Mode = ExamMode.ReadingPractice,
            OverallBand = 4.5,
            ComparableTotal120 = 90,
            Cefr = "B2",
            Sections = [new TOEFLSectionSummary { Kind = TOEFLSectionKind.Reading, Route = AdaptiveRoute.Upper, Band = 4.5, RubricDetail = "d", RawScore = 28, RawMaximum = 35 }],
            Reviews = [new TOEFLQuestionReview { Number = 1, Question = Shared.Bank.AllQuestions[0], Answer = new TOEFLAnswer { TextEntry = "ment" }, IsCorrect = true }],
        };
        var json = SwiftJson.Serialize(result);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("E621E1F8-C36C-495A-93FC-0C247A3E6E5F", root.GetProperty("id").GetString());
        // 2026-11-01 is 815,184,000 seconds after 2001-01-01.
        Assert.Equal(815_184_000, root.GetProperty("completedAt").GetDouble());
        Assert.Equal("readingPractice", root.GetProperty("mode").GetString());
        Assert.Equal("upper", root.GetProperty("sections")[0].GetProperty("route").GetString());
        Assert.False(root.GetProperty("sections")[0].TryGetProperty("id", out _));
        var question = root.GetProperty("reviews")[0].GetProperty("question");
        Assert.Equal("completeWords", question.GetProperty("taskType").GetString());
        Assert.False(question.TryGetProperty("audioScript", out _)); // nil optionals are omitted
        Assert.False(question.TryGetProperty("audioKey", out _));    // computed properties are not encoded
        Assert.Equal(json, SwiftJson.Serialize(SwiftJson.Deserialize<TOEFLExamResult>(json)));
        var decoded = SwiftJson.Deserialize<TOEFLExamResult>(json)!;
        Assert.Equal(result.Id, decoded.Id);
        Assert.Equal(result.CompletedAt, decoded.CompletedAt);
        Assert.Equal(result.Sections[0], decoded.Sections[0]);
    }

    [Fact]
    public void HistoryStoreReadsMacFormattedResultsNewestFirst()
    {
        var store = KeyValueStore.InMemory();
        // Two results as the Mac app encodes them (dates as reference-date seconds), oldest first.
        store.SetRawJson(StorageKeys.PracticeResults, """
            [{"id":"00000000-0000-0000-0000-000000000001","completedAt":800000000,"mode":"writingPractice","overallBand":3.5,"comparableTotal120":65,"cefr":"B1","elapsedSeconds":100,"sections":[],"reviews":[]},
             {"id":"00000000-0000-0000-0000-000000000002","completedAt":810000000.5,"mode":"fullLength","overallBand":4,"comparableTotal120":79,"cefr":"B2","elapsedSeconds":5000,"sections":[{"kind":"speaking","correct":0,"objectiveTotal":0,"answered":11,"total":11,"band":4,"rubricDetail":"x"}],"reviews":[]}]
            """);
        var history = new TOEFLHistoryStore(store);
        Assert.Equal(2, history.Results.Count);
        Assert.Equal(ExamMode.FullLength, history.Results[0].Mode);
        Assert.Null(history.Results[0].Sections[0].RawScore);
        Assert.Equal("11/11 answered", ScoreReportText.RawLine(history.Results[0].Sections[0]));
        Assert.Equal("Speaking 4.0", ScoreReportText.SectionBandsLine(history.Results[0]));

        var added = history.Results[1] with { Id = Guid.NewGuid(), CompletedAt = history.Results[0].CompletedAt.AddDays(1) };
        history.Add(added);
        Assert.Equal(added.Id, new TOEFLHistoryStore(store).Results[0].Id);
        history.Clear();
        Assert.Empty(new TOEFLHistoryStore(store).Results);
    }

    [Fact]
    public void ScoreReportTextHelpers()
    {
        Assert.Equal("12m 5s", ScoreReportText.Duration(725));
        Assert.Equal("45s", ScoreReportText.Duration(45));
        Assert.Equal("Rubric-scored", ScoreReportText.StatusBadge(null));
        var question = Shared.Bank.AllQuestions.First(q => q.TaskType == TOEFLTaskType.Interview);
        Assert.Equal("44-second recording · “hello”", ScoreReportText.AnswerText(new TOEFLQuestionReview { Question = question, Answer = new TOEFLAnswer { RecordingDuration = 44.5, Transcript = "hello" } }));
        Assert.Equal("46-second recording · (not transcribed)", ScoreReportText.AnswerText(new TOEFLQuestionReview { Question = question, Answer = new TOEFLAnswer { RecordingDuration = 45.5 } }));
        Assert.Equal("3-second recording · (no speech recognized)", ScoreReportText.AnswerText(new TOEFLQuestionReview { Question = question, Answer = new TOEFLAnswer { RecordingDuration = 3, Transcript = "" } }));
        Assert.Equal("No response", ScoreReportText.AnswerText(new TOEFLQuestionReview { Question = question, Answer = new TOEFLAnswer() }));
        Assert.Equal("Raw 28/35 · 50/50 answered", ScoreReportText.RawLine(new TOEFLSectionSummary { RawScore = 28, RawMaximum = 35, Answered = 50, Total = 50 }));
        Assert.Equal("10/12 answered · 9/10 keyed", ScoreReportText.RawLine(new TOEFLSectionSummary { Answered = 10, Total = 12, Correct = 9, ObjectiveTotal = 10 }));
    }
}
