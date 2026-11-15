using System.Text.Json;
using TOEFLSimulation.Core.Storage;

namespace TOEFLSimulation.Core.Tests;

public class QuestionBankTests
{
    [Fact]
    public void BankDecodesEveryItem()
    {
        Assert.Equal(Golden.Get("questionCount").GetInt32(), Shared.Bank.AllQuestions.Count);
        Assert.Equal(5671, Shared.Bank.AllQuestions.Count);
        var counts = Shared.Bank.AllQuestions.GroupBy(q => q.TaskType).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(2400, counts[TOEFLTaskType.CompleteWords]);
        Assert.Equal(400, counts[TOEFLTaskType.BuildSentence]);
        Assert.Equal(154, counts[TOEFLTaskType.ListenRepeat]);
        Assert.Equal(104, counts[TOEFLTaskType.Interview]);
    }

    [Fact]
    public void DecodingRequiresTheKeysSwiftRequires()
    {
        var missingPrompt = """[{"id":"x","section":"reading","taskType":"completeWords","difficulty":"advanced","choices":[],"acceptedTextAnswers":[],"sentenceTokens":[],"correctSentenceTokens":[],"explanation":"","sourceNote":""}]"""u8.ToArray();
        Assert.Throws<InvalidDataException>(() => TOEFLQuestionBank.Decode(missingPrompt));
        var unknownEnum = """[{"id":"x","section":"math","taskType":"completeWords","difficulty":"advanced","prompt":"","choices":[],"acceptedTextAnswers":[],"sentenceTokens":[],"correctSentenceTokens":[],"explanation":"","sourceNote":""}]"""u8.ToArray();
        Assert.Throws<InvalidDataException>(() => TOEFLQuestionBank.Decode(unknownEnum));
        var minimal = """[{"id":"x","section":"reading","taskType":"readDailyLife","difficulty":"advanced","prompt":"p","passage":null,"choices":[{"id":"a","text":"A"}],"correctChoiceID":"a","acceptedTextAnswers":[],"sentenceTokens":[],"correctSentenceTokens":[],"explanation":"","sourceNote":"","isScored":false}]"""u8;
        var decoded = Assert.Single(TOEFLQuestionBank.Decode(minimal));
        Assert.False(decoded.CountsTowardScore);
        Assert.Null(decoded.SetID);
        Assert.Equal("x", decoded.SetKey);
    }

    [Fact]
    public void ValidationPassesWithZeroIssues()
    {
        Assert.Empty(Golden.Get("validate").EnumerateArray());
        Assert.Empty(Shared.Bank.Validate(Shared.ExamAudio));
    }

    [Fact]
    public void ListeningRotationHasNoEarlyRepeats()
    {
        var issues = Shared.Bank.RotationIssues(out var summary);
        Assert.Empty(issues);
        Assert.Equal(Golden.Get("rotationIssues").Strings(), issues);
        Assert.Equal("20 Listening sessions → Listen and Choose a Response: 340 of 598 sets; Listen to a Conversation: 100 of 120 sets; Listen to an Announcement: 80 of 140 sets; Listen to an Academic Talk: 60 of 180 sets", summary);
    }

    public static TheoryData<int> FormCases()
    {
        var data = new TheoryData<int>();
        for (var i = 0; i < Golden.Get("forms").GetArrayLength(); i++) data.Add(i);
        return data;
    }

    private static void AssertForm(JsonElement expected, IReadOnlyList<string>? recent)
    {
        var kind = Enum.Parse<TOEFLSectionKind>(expected.GetProperty("kind").GetString()!, ignoreCase: true);
        var seed = ulong.Parse(expected.GetProperty("seed").GetString()!);
        var section = Shared.Bank.PracticeSection(kind, seed, recent ?? []);
        Assert.Equal(expected.GetProperty("questions").Strings(), section.Questions.Select(q => q.Id).ToList());
        Assert.Equal(expected.GetProperty("scored").EnumerateArray().Select(e => e.GetBoolean()), section.Questions.Select(q => q.CountsTowardScore));
        Assert.Equal(expected.GetProperty("routerCount").GetInt32(), section.RouterCount ?? -1);
        if (kind.IsAdaptive())
        {
            var router = section.Questions.Take(section.RouterCount!.Value).Select(q => q.Id).ToHashSet();
            foreach (var route in new[] { AdaptiveRoute.Lower, AdaptiveRoute.Upper })
            {
                var module = Shared.Bank.ModuleQuestions(kind, route, seed, router, recent ?? []);
                Assert.Equal(expected.GetProperty(route.RawValue()).Strings(), module.Select(q => q.Id).ToList());
            }
        }
    }

    /// <summary>The same seed builds the same form as the Mac app (Swift SplitMix64, Lemire bounds, Swift shuffle).</summary>
    [Theory]
    [MemberData(nameof(FormCases))]
    public void FormsMatchTheMacAppForTheSameSeed(int index) => AssertForm(Golden.Get("forms")[index], null);

    [Theory]
    [MemberData(nameof(FormCases))]
    public void FormsWithExposureHistoryMatchTheMacApp(int index) =>
        AssertForm(Golden.Get("formsWithHistory")[index], Golden.Get("history").Strings());

    [Fact]
    public void ReadingBlueprintIs35RouterPlus15()
    {
        for (ulong seed = 100; seed < 110; seed++)
        {
            var section = Shared.Bank.PracticeSection(TOEFLSectionKind.Reading, seed, []);
            Assert.Equal(35, section.RouterCount);
            var router = section.Questions.Take(35).ToList();
            // Router: 2 Complete the Words paragraphs (one unscored), Daily Life 2 + 3, 2 academic passages (one unscored).
            Assert.Equal(20, router.Count(q => q.TaskType == TOEFLTaskType.CompleteWords));
            Assert.Equal(10, router.Count(q => q.TaskType == TOEFLTaskType.CompleteWords && !q.CountsTowardScore));
            Assert.Equal(5, router.Count(q => q.TaskType == TOEFLTaskType.ReadDailyLife));
            Assert.Equal(10, router.Count(q => q.TaskType == TOEFLTaskType.ReadAcademicPassage));
            Assert.Equal(20, router.Count(q => q.CountsTowardScore));
            Assert.All(router, q => Assert.Equal(TOEFLSectionKind.Reading, q.Section));
            var ids = router.Select(q => q.Id).ToHashSet();
            var lower = Shared.Bank.ModuleQuestions(TOEFLSectionKind.Reading, AdaptiveRoute.Lower, seed, ids, []);
            var upper = Shared.Bank.ModuleQuestions(TOEFLSectionKind.Reading, AdaptiveRoute.Upper, seed, ids, []);
            Assert.Equal(15, lower.Count);
            Assert.Equal(15, upper.Count);
            Assert.Equal(10, lower.Count(q => q.TaskType == TOEFLTaskType.CompleteWords));
            Assert.Equal(5, lower.Count(q => q.TaskType == TOEFLTaskType.ReadDailyLife));
            Assert.Equal(5, upper.Count(q => q.TaskType == TOEFLTaskType.ReadAcademicPassage));
            Assert.All(upper, q => Assert.Equal(TOEFLDifficulty.Advanced, q.Difficulty));
            Assert.All(lower.Concat(upper), q => Assert.True(q.CountsTowardScore));
            Assert.Empty(lower.Select(q => q.Id).Intersect(ids));
        }
    }

    [Fact]
    public void ListeningBlueprintIs32RouterPlus15()
    {
        var section = Shared.Bank.PracticeSection(TOEFLSectionKind.Listening, 5, []);
        Assert.Equal(32, section.RouterCount);
        var router = section.Questions.Take(32).ToList();
        Assert.Equal(12, router.Count(q => q.TaskType == TOEFLTaskType.ListenChooseResponse));
        Assert.Equal(20, router.Count(q => q.CountsTowardScore));
        Assert.Equal(47, section.Questions.Count);
        var ids = router.Select(q => q.Id).ToHashSet();
        var lower = Shared.Bank.ModuleQuestions(TOEFLSectionKind.Listening, AdaptiveRoute.Lower, 5, ids, []);
        Assert.Equal(15, lower.Count);
        Assert.Equal(7, lower.Count(q => q.TaskType == TOEFLTaskType.ListenChooseResponse));
        Assert.DoesNotContain(lower, q => q.TaskType == TOEFLTaskType.ListenAcademicTalk);
        var upper = Shared.Bank.ModuleQuestions(TOEFLSectionKind.Listening, AdaptiveRoute.Upper, 5, ids, []);
        Assert.Equal(15, upper.Count);
        Assert.Equal(3, upper.Count(q => q.TaskType == TOEFLTaskType.ListenChooseResponse));
        Assert.DoesNotContain(upper, q => q.TaskType == TOEFLTaskType.ListenAnnouncement);
    }

    [Fact]
    public void WritingAndSpeakingFormsAreLinear()
    {
        var writing = Shared.Bank.PracticeSection(TOEFLSectionKind.Writing, 9, []);
        Assert.Equal(12, writing.Questions.Count);
        Assert.Equal(Enumerable.Repeat(TOEFLTaskType.BuildSentence, 10).Append(TOEFLTaskType.WriteEmail).Append(TOEFLTaskType.AcademicDiscussion),
            writing.Questions.Select(q => q.TaskType));
        Assert.Null(writing.RouterCount);
        var parts = writing.TimedParts;
        Assert.Equal([("Build a Sentence", 0, 10, 360, true), ("Write an Email", 10, 11, 420, false), ("Write for an Academic Discussion", 11, 12, 600, false)],
            parts.Select(p => (p.Title, p.Range.LowerBound, p.Range.UpperBound, p.Seconds, p.AllowsBack)).ToList());

        var speaking = Shared.Bank.PracticeSection(TOEFLSectionKind.Speaking, 9, []);
        Assert.Equal(11, speaking.Questions.Count);
        Assert.Equal(7, speaking.Questions.Count(q => q.TaskType == TOEFLTaskType.ListenRepeat));
        Assert.Equal(4, speaking.Questions.Count(q => q.TaskType == TOEFLTaskType.Interview));
        // Speaking items keep their authored order.
        Assert.Equal(Enumerable.Range(0, 7), speaking.Questions.Take(7).Select(q => q.SetIndex ?? 0));
        var part = Assert.Single(speaking.TimedParts);
        Assert.Equal(8 * 60, part.Seconds);
        Assert.False(part.AllowsBack);
    }

    [Fact]
    public void TimedPartsFollowTheBlueprintClocks()
    {
        var reading = Shared.Bank.PracticeSection(TOEFLSectionKind.Reading, 3, []);
        Assert.Equal([("Module 1", 21 * 60, true), ("Module 2", 9 * 60, true)], reading.TimedParts.Select(p => (p.Title, p.Seconds, p.AllowsBack)).ToList());
        var listening = Shared.Bank.PracticeSection(TOEFLSectionKind.Listening, 3, []);
        Assert.Equal([("Module 1", 18 * 60, false), ("Module 2", 7 * 60, false)], listening.TimedParts.Select(p => (p.Title, p.Seconds, p.AllowsBack)).ToList());
        listening.Route = AdaptiveRoute.Upper;
        Assert.Equal(11 * 60, listening.TimedParts[1].Seconds);
    }

    [Fact]
    public void SeenSetsReturnOnlyAfterUnseenSetsAreUsed()
    {
        // Mark every Build a Sentence item but ten as seen: a writing form must use exactly the ten unseen ones.
        var all = Shared.Bank.Sets.Where(s => s[0].TaskType == TOEFLTaskType.BuildSentence).Select(s => s[0].SetKey).ToList();
        var unseen = all.Take(10).ToHashSet();
        var history = all.Skip(10).ToList();
        var form = Shared.Bank.PracticeSection(TOEFLSectionKind.Writing, 77, history);
        Assert.Equal(unseen, form.Questions.Where(q => q.TaskType == TOEFLTaskType.BuildSentence).Select(q => q.SetKey).ToHashSet());

        // With every set seen, the least recently seen come back first.
        var everything = all.ToList();
        var form2 = Shared.Bank.PracticeSection(TOEFLSectionKind.Writing, 78, everything);
        Assert.Equal(everything.Take(10).ToHashSet(), form2.Questions.Where(q => q.TaskType == TOEFLTaskType.BuildSentence).Select(q => q.SetKey).ToHashSet());
    }

    [Fact]
    public void ExposureHistoryIsRememberedAndCapped()
    {
        var store = KeyValueStore.InMemory();
        var bank = new TOEFLQuestionBank(Shared.Bank.AllQuestions, store);
        var section = bank.PracticeSection(TOEFLSectionKind.Writing, 1);
        bank.RememberExposure(section.Questions);
        var history = store.GetStringArray(StorageKeys.QuestionExposure)!;
        Assert.Equal(section.Questions.Select(q => q.SetKey).Distinct(), history);

        // Re-remembering moves keys to the newest end without duplicates.
        bank.RememberExposure(section.Questions.Take(1));
        history = store.GetStringArray(StorageKeys.QuestionExposure)!;
        Assert.Equal(section.Questions[0].SetKey, history[^1]);
        Assert.Equal(history.Count, history.Distinct().Count());

        bank.RememberExposure(Shared.Bank.AllQuestions);
        Assert.Equal(TOEFLQuestionBank.ExposureLimit, store.GetStringArray(StorageKeys.QuestionExposure)!.Count);

        // With the history off nothing is read or written.
        bank.UsesExposureHistory = false;
        Assert.Empty(bank.RecentSetKeys);
    }

    [Fact]
    public void MixedSeedMatchesFnv()
    {
        // FNV-1a of "a" from the FNV offset basis when the seed is 0.
        Assert.Equal(0xaf63dc4c8601ec8cUL, TOEFLQuestionBank.MixedSeed(0, "a"));
    }

    [Fact]
    public void SeededGeneratorMatchesSplitMix64()
    {
        // SplitMix64 with state 0 + 2×golden gamma produces this first output (the Swift generator adds the gamma at init).
        var generator = new SeededGenerator(0);
        Assert.Equal(0x6E789E6AA1B965F4UL, generator.Next());
    }
}
