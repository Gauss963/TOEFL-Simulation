using TOEFLSimulation.Core.Audio;

namespace TOEFLSimulation.Core.Tests;

public class ResourceTests
{
    [Fact]
    public void AudioPacksIndexEveryClip()
    {
        Assert.Equal(Golden.Get("examClipCount").GetInt32(), Shared.ExamAudio.Pack.ClipCount);
        Assert.Equal(1344, Shared.ExamAudio.Pack.ClipCount);
        Assert.Equal(3, Shared.ExamAudio.Pack.VolumeCount);
        Assert.Equal(Golden.Get("wordClipCount").GetInt32(), Shared.WordPronunciations.ClipCount);
        Assert.Equal(1375, Shared.WordPronunciations.ClipCount);
    }

    [Fact]
    public void ExamClipsMatchTheMacByteRanges()
    {
        foreach (var entry in Golden.Get("examClipSizes").EnumerateObject())
        {
            var clip = Shared.ExamAudio.Clip(entry.Name);
            Assert.Equal(entry.Value.GetInt32(), clip?.Length ?? -1);
            if (clip is not null) AssertIsM4a(clip);
        }
    }

    [Fact]
    public void WordClipsUseLowercasedTrimmedKeys()
    {
        foreach (var entry in Golden.Get("wordClipSizes").EnumerateObject())
        {
            var clip = Shared.WordPronunciations.Clip(entry.Name);
            Assert.Equal(entry.Value.GetInt32(), clip?.Length ?? -1);
            if (clip is not null) AssertIsM4a(clip);
        }
        Assert.NotNull(Shared.WordPronunciations.Clip("  ADAPT\n"));
        Assert.Null(Shared.ExamAudio.Clip("AN-01")); // exam keys are case-sensitive
    }

    [Fact]
    public void EveryListeningAndSpeakingItemHasARecording()
    {
        var keys = Shared.Bank.AllQuestions.Where(q => q.TaskType.IsListeningStimulus() || q.Section == TOEFLSectionKind.Speaking).Select(q => q.AudioKey).Distinct().ToList();
        Assert.All(keys, key => Assert.True(Shared.ExamAudio.HasClip(key), key));
        // Clips from all three volumes decode as MPEG-4.
        foreach (var key in keys.Where((_, i) => i % 50 == 0)) AssertIsM4a(Shared.ExamAudio.Clip(key)!);
    }

    [Fact]
    public void IntroKeysExistForSpeakingSetsWithIntros()
    {
        Assert.Equal("intro-lr-01", TOEFLExamAudio.IntroKey("lr-01"));
        var withIntro = Shared.Bank.AllQuestions.Where(q => q.Section == TOEFLSectionKind.Speaking && q.Intro is not null && (q.SetIndex ?? 0) == 0 && q.SetID is not null).ToList();
        Assert.NotEmpty(withIntro);
        var present = withIntro.Count(q => Shared.ExamAudio.HasClip(TOEFLExamAudio.IntroKey(q.SetID!)));
        Assert.True(present > 0);
    }

    private static void AssertIsM4a(byte[] clip)
    {
        Assert.True(clip.Length > 12);
        Assert.Equal("ftyp"u8.ToArray(), clip[4..8]);
    }

    [Fact]
    public void MissingPackIsEmpty()
    {
        using var temp = new TempDirectory();
        using var pack = new AudioClipPack(temp.Path, "ExamAudio", lowercasedKeys: false);
        Assert.Equal(0, pack.ClipCount);
        Assert.Null(pack.Clip("an-01"));
    }

    [Fact]
    public void PackRejectsInvalidRanges()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "T.json"), """{"clips":{"ok":[0,4],"second":[2,3,1],"bad":[3,10],"neg":[-1,2],"vol":[0,1,5],"short":[1]}}""");
        File.WriteAllBytes(Path.Combine(temp.Path, "T.pack"), [1, 2, 3, 4, 5]);
        File.WriteAllBytes(Path.Combine(temp.Path, "T-2.pack"), [9, 8, 7, 6, 5]);
        using var pack = new AudioClipPack(temp.Path, "T", lowercasedKeys: true);
        Assert.Equal(2, pack.VolumeCount);
        Assert.Equal(6, pack.ClipCount);
        Assert.Equal([1, 2, 3, 4], pack.Clip("OK"));
        Assert.Equal([7, 6, 5], pack.Clip("second"));
        Assert.Null(pack.Clip("bad"));
        Assert.Null(pack.Clip("neg"));
        Assert.Null(pack.Clip("vol"));
        Assert.Null(pack.Clip("short"));
    }

    [Fact]
    public void TonesMatchTheMacWavBytes()
    {
        AssertSameWav(Convert.FromBase64String(Golden.Get("beep").GetString()!), TOEFLExamAudio.Beep);
        AssertSameWav(Convert.FromBase64String(Golden.Get("endTone").GetString()!), TOEFLExamAudio.EndTone);
        Assert.Equal(44 + 7680 * 2, TOEFLExamAudio.Beep.Length);
        Assert.Equal(44 + 5280 * 2, TOEFLExamAudio.EndTone.Length);
    }

    /// <summary>Headers identical; samples within ±1 (libm sin may differ by an ulp before truncation).</summary>
    private static void AssertSameWav(byte[] expected, byte[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        Assert.Equal(expected[..44], actual[..44]);
        for (var i = 44; i < expected.Length; i += 2)
            Assert.InRange(BitConverter.ToInt16(actual, i) - BitConverter.ToInt16(expected, i), -1, 1);
    }

    [Fact]
    public void VocabularyMergesCuratedWordsLikeTheMacApp()
    {
        var words = Shared.StudyBank.Words;
        Assert.Equal(Golden.Get("vocabularyIds").Strings(), words.Select(w => w.Id).ToList());
        var sources = Golden.Get("vocabularySources").EnumerateArray().Select(e => e.Strings()).ToList();
        Assert.Equal(sources, words.Select(w => w.Sources.ToList()).ToList());
        Assert.Equal(Golden.Get("vocabularyHighFrequency").EnumerateArray().Select(e => e.GetBoolean()), words.Select(w => w.IsHighFrequency));
        Assert.Equal(Golden.Get("sourceOptions").Strings(), Shared.StudyBank.SourceOptions);
        Assert.Equal(36, TOEFLStudyBank.CuratedWords.Count);
        var adapt = words.Single(w => w.Id == "adapt");
        Assert.Equal("適應；調整；改編", adapt.Chinese);
        Assert.True(adapt.IsHighFrequency);
    }

    [Fact]
    public void VocabularyValidationPasses()
    {
        Assert.Empty(Golden.Get("vocabularyValidate").EnumerateArray());
        Assert.Empty(Shared.StudyBank.Validate(Shared.WordPronunciations));
    }

    [Fact]
    public void VocabularyFilterMatchesWordDefinitionChineseAndSynonyms()
    {
        var bank = Shared.StudyBank;
        Assert.Equal(bank.Words.Count, bank.Filter("  ").Count);
        Assert.Contains(bank.Filter("MITIGATE"), w => w.Id == "mitigate");
        Assert.Contains(bank.Filter("減輕"), w => w.Id == "mitigate");
        var source = bank.SourceOptions[0];
        Assert.All(bank.Filter("", source), w => Assert.Contains(source, w.Sources));
    }

    [Fact]
    public void CompleteTheWordsParagraphsSplitLikeTheMacApp()
    {
        foreach (var row in Golden.Get("ctest").EnumerateArray())
        {
            var pieces = new CTestParagraph(row.GetProperty("passage").GetString()!).Pieces;
            var expected = row.GetProperty("pieces").EnumerateArray().ToList();
            Assert.Equal(expected.Count, pieces.Count);
            for (var i = 0; i < pieces.Count; i++)
            {
                if (expected[i].TryGetProperty("text", out var text))
                {
                    Assert.Equal(new CTestParagraph.TextPiece(text.GetString()!), pieces[i]);
                }
                else
                {
                    Assert.Equal(new CTestParagraph.BlankPiece(expected[i].GetProperty("index").GetInt32(), expected[i].GetProperty("prefix").GetString()!,
                        expected[i].GetProperty("missing").GetInt32(), expected[i].GetProperty("suffix").GetString()!), pieces[i]);
                }
            }
        }
    }

    [Fact]
    public void MeaningGroupsMatchTheMacApp()
    {
        foreach (var row in Golden.Get("meaningGroups").EnumerateArray())
            Assert.Equal(row.GetProperty("groups").GetString(), SpeakingGuide.MeaningGroups(row.GetProperty("text").GetString()!));
    }

    [Fact]
    public void CoreLoadsAndPassesTheMacSelfTest()
    {
        using var temp = new TempDirectory();
        using var core = TOEFLCore.Load(RepoPaths.Resources, temp.Path);
        Assert.Empty(core.SelfTest());
        Assert.Equal(240, core.WordsDrillParagraphs.Count);
        Assert.Equal(Shared.Bank.Sets.Count(s => s[0].TaskType is TOEFLTaskType.ReadDailyLife or TOEFLTaskType.ReadAcademicPassage), core.ReadingDrillSets.Count);
        Assert.Equal("5671 questions · 1375 vocabulary entries", DashboardContent.BankSummary(core.QuestionBank.AllQuestions.Count, core.StudyBank.Words.Count));
    }

    [Fact]
    public void MissingResourcesThrow()
    {
        using var temp = new TempDirectory();
        Assert.ThrowsAny<IOException>(() => TOEFLCore.Load(Path.Combine(temp.Path, "nope"), temp.Path));
    }
}
