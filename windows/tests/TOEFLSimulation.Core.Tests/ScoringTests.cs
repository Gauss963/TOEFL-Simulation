namespace TOEFLSimulation.Core.Tests;

public class ScoringTests
{
    [Fact]
    public void ObjectiveBandsMatchTheMacApp()
    {
        foreach (var row in Golden.Get("objectiveBands").EnumerateArray())
        {
            var route = row.GetProperty("route").GetString() switch { "lower" => AdaptiveRoute.Lower, "upper" => (AdaptiveRoute?)AdaptiveRoute.Upper, _ => null };
            Assert.Equal(row.GetProperty("band").GetDouble(), TOEFLPracticeScoring.ObjectiveBand(row.GetProperty("correct").GetInt32(), 35, route));
        }
    }

    /// <summary>Values computed by hand from the Swift step tables.</summary>
    [Theory]
    [InlineData(35, "upper", 6.0)]   // 100% ≥ 0.97
    [InlineData(34, "upper", 6.0)]   // 97.1%
    [InlineData(33, "upper", 5.5)]   // 94.3% ≥ 0.90
    [InlineData(31, "upper", 5.0)]   // 88.6% ≥ 0.84
    [InlineData(23, "upper", 3.5)]   // 65.7% < 0.66, ≥ 0.55
    [InlineData(0, "upper", 2.5)]
    [InlineData(35, "lower", 4.0)]
    [InlineData(28, "lower", 3.5)]   // 80%
    [InlineData(17, "lower", 2.0)]   // 48.6% ≥ 0.34
    [InlineData(0, "lower", 1.0)]
    [InlineData(29, "none", 5.0)]    // 82.9% ≥ 0.82
    [InlineData(4, "none", 1.0)]     // 11.4% < 0.12
    public void ObjectiveBandHandValues(int correct, string route, double expected)
    {
        AdaptiveRoute? r = route switch { "upper" => AdaptiveRoute.Upper, "lower" => AdaptiveRoute.Lower, _ => null };
        var band = TOEFLPracticeScoring.ObjectiveBand(correct, 35, r);
        Assert.Equal(expected, band);
    }

    [Fact]
    public void EmptyObjectiveTotalIsBandOne() => Assert.Equal(1, TOEFLPracticeScoring.ObjectiveBand(0, 0, AdaptiveRoute.Upper));

    [Fact]
    public void HalfBandsComparableAndCefrMatchTheMacApp()
    {
        foreach (var row in Golden.Get("halfBands").EnumerateArray())
        {
            var value = row.GetProperty("value").GetDouble();
            Assert.Equal(row.GetProperty("half").GetDouble(), TOEFLPracticeScoring.HalfBand(value));
            Assert.Equal(row.GetProperty("comparable").GetInt32(), TOEFLPracticeScoring.Comparable120(value));
            Assert.Equal(row.GetProperty("cefr").GetString(), TOEFLPracticeScoring.Cefr(value));
        }
    }

    [Theory]
    [InlineData(6.0, 117, "C2")]
    [InlineData(5.5, 110, "C1")]
    [InlineData(5.0, 101, "C1")]
    [InlineData(4.5, 90, "B2")]
    [InlineData(4.0, 79, "B2")]
    [InlineData(3.5, 65, "B1")]
    [InlineData(3.0, 51, "B1")]
    [InlineData(2.5, 39, "A2")]
    [InlineData(2.0, 29, "A2")]
    [InlineData(1.5, 18, "A1")]
    [InlineData(1.0, 6, "A1")]
    [InlineData(4.25, 90, "B2")]  // 8.5 rounds away from zero → 9 → 4.5
    [InlineData(7.0, 117, "C2")]  // clamped to 6
    public void BandTable(double band, int comparable, string cefr)
    {
        Assert.Equal(comparable, TOEFLPracticeScoring.Comparable120(band));
        Assert.Equal(cefr, TOEFLPracticeScoring.Cefr(band));
    }

    [Fact]
    public void OverallIsTheHalfRoundedMean()
    {
        Assert.Equal(4.5, TOEFLPracticeScoring.Overall([4, 4.5, 4.5, 4.5]));   // 4.375 → 8.75 → 9 → 4.5
        Assert.Equal(4.0, TOEFLPracticeScoring.Overall([4, 4, 4, 4.5]));       // 4.125 → 8.25 → 8 → 4.0
        Assert.Equal(4.5, TOEFLPracticeScoring.Overall([4, 4, 4.5, 4.5]));     // 4.25 → 8.5 → 9 (away from zero) → 4.5
        Assert.Equal(1, TOEFLPracticeScoring.Overall([]));
    }

    private static TOEFLQuestion Sentence(string id, params string[] tokens) => new()
    {
        Id = id, Section = TOEFLSectionKind.Writing, TaskType = TOEFLTaskType.BuildSentence, CorrectSentenceTokens = tokens, SentenceTokens = tokens,
    };

    [Fact]
    public void WritingBandHandComputed()
    {
        var questions = Enumerable.Range(0, 10).Select(i => Sentence($"b{i}", "a", "b")).ToList<TOEFLQuestion>();
        questions.Add(new TOEFLQuestion { Id = "e", Section = TOEFLSectionKind.Writing, TaskType = TOEFLTaskType.WriteEmail });
        questions.Add(new TOEFLQuestion { Id = "d", Section = TOEFLSectionKind.Writing, TaskType = TOEFLTaskType.AcademicDiscussion });
        var answers = new Dictionary<string, TOEFLAnswer>();
        for (var i = 0; i < 7; i++) answers[$"b{i}"] = new TOEFLAnswer { OrderedTokens = ["A", "b."] };
        answers["b7"] = new TOEFLAnswer { OrderedTokens = ["b", "a"] };
        answers["e"] = new TOEFLAnswer { RubricScore = 4 };
        answers["d"] = new TOEFLAnswer { RubricScore = 3 };
        var (band, detail, raw) = TOEFLPracticeScoring.WritingBand(questions, answers);
        Assert.Equal(14, raw);
        Assert.Equal(4.5, band); // 1 + 14/20×5 = 4.5
        Assert.Equal("Raw 14/20 · Build a Sentence 7/10 · Email 4/5 · Academic Discussion 3/5 (local rubric engine).", detail);
    }

    [Fact]
    public void SpeakingBandHandComputed()
    {
        var questions = Enumerable.Range(0, 7).Select(i => new TOEFLQuestion { Id = $"r{i}", Section = TOEFLSectionKind.Speaking, TaskType = TOEFLTaskType.ListenRepeat })
            .Concat(Enumerable.Range(0, 4).Select(i => new TOEFLQuestion { Id = $"i{i}", Section = TOEFLSectionKind.Speaking, TaskType = TOEFLTaskType.Interview }))
            .ToList();
        var answers = questions.ToDictionary(q => q.Id, q => new TOEFLAnswer { RubricScore = q.TaskType == TOEFLTaskType.ListenRepeat ? 4 : 3 });
        var (band, detail, raw) = TOEFLPracticeScoring.SpeakingBand(questions, answers);
        Assert.Equal(40, raw);
        Assert.Equal(4.5, band); // 1 + 40/55×5 = 4.636 → 9.27 → 9 → 4.5
        Assert.Equal("Raw 40/55 · Listen and Repeat 28/35 · Interview 12/20 (on-device speech recognition + official rubrics).", detail);
        Assert.Equal((1.0, "No speaking items.", 0), TOEFLPracticeScoring.SpeakingBand([], answers));
    }

    [Fact]
    public void AnswerMatcherNormalizesCaseWhitespaceAndPunctuation()
    {
        var choice = new TOEFLQuestion { Id = "c", CorrectChoiceID = "b", Choices = [new TOEFLChoice { Id = "a", Text = "A" }, new TOEFLChoice { Id = "b", Text = "B" }] };
        Assert.True(TOEFLAnswerMatcher.IsCorrect(choice, new TOEFLAnswer { SelectedChoiceID = "b" }));
        Assert.False(TOEFLAnswerMatcher.IsCorrect(choice, new TOEFLAnswer()));
        var words = new TOEFLQuestion { Id = "w", AcceptedTextAnswers = ["ment", "pavement"] };
        Assert.True(TOEFLAnswerMatcher.IsCorrect(words, new TOEFLAnswer { TextEntry = " MENT. " }));
        Assert.True(TOEFLAnswerMatcher.IsCorrect(words, new TOEFLAnswer { TextEntry = "pavement" }));
        Assert.False(TOEFLAnswerMatcher.IsCorrect(words, new TOEFLAnswer { TextEntry = "men" }));
        Assert.Null(TOEFLAnswerMatcher.IsCorrect(new TOEFLQuestion { Id = "x" }, new TOEFLAnswer()));
    }

    [Fact]
    public void RepeatRubricMatchesTheMacApp()
    {
        foreach (var row in Golden.Get("repeat").EnumerateArray())
        {
            var (score, detail) = SpeakingRubric.ScoreRepeat(row.GetProperty("target").GetString()!, Golden.Transcription(row.GetProperty("transcription")));
            Assert.Equal(row.GetProperty("score").GetInt32(), score);
            Assert.Equal(row.GetProperty("detail").GetString(), detail);
        }
    }

    [Fact]
    public void InterviewRubricMatchesTheMacApp()
    {
        foreach (var row in Golden.Get("interview").EnumerateArray())
        {
            var (score, detail) = SpeakingRubric.ScoreInterview(row.GetProperty("question").GetString()!, Golden.Transcription(row.GetProperty("transcription")),
                row.GetProperty("responseSeconds").GetInt32());
            Assert.Equal(row.GetProperty("detail").GetString(), detail);
            Assert.Equal(row.GetProperty("score").GetInt32(), score);
        }
    }

    [Fact]
    public void SpeechWordsMatchTheMacApp()
    {
        foreach (var row in Golden.Get("words").EnumerateArray())
            Assert.Equal(row.GetProperty("words").Strings(), SpeakingRubric.Words(row.GetProperty("text").GetString()!));
    }

    [Fact]
    public void NumberSpellOut()
    {
        Assert.Equal("one hundred twenty-three", NumberSpeller.SpellOut(123));
        Assert.Equal("two thousand twenty-four", NumberSpeller.SpellOut(2024));
        Assert.Equal("one million", NumberSpeller.SpellOut(1_000_000));
    }

    [Fact]
    public void WritingRubricMatchesTheMacApp()
    {
        var rows = Golden.Get("writing").EnumerateArray().ToList();
        var counts = new Dictionary<string, int>();
        foreach (var row in rows) counts[row.GetProperty("response").GetString()!.Trim()] = row.GetProperty("misspellings").GetInt32();
        var checker = new RecordedSpellChecker(counts);
        foreach (var row in rows)
        {
            var question = Shared.Bank.Question(row.GetProperty("questionID").GetString()!)!;
            var (score, note) = WritingRubric.Score(question, row.GetProperty("response").GetString()!, checker);
            Assert.Equal(row.GetProperty("note").GetString(), note);
            Assert.Equal(row.GetProperty("score").GetInt32(), score);
        }
    }
}
