using TOEFLSimulation.Core.Storage;

namespace TOEFLSimulation.Core.Tests;

public class StudyTests
{
    private static readonly IReadOnlyList<WordsDrillParagraph> Paragraphs = WordsDrillParagraph.All(Shared.Bank);
    private static readonly IReadOnlyList<ReadingDrillSet> ReadingSets = ReadingDrillSet.All(Shared.Bank);

    [Fact]
    public void WordsDrillSelfTestPasses() => Assert.Empty(WordsDrillStore.SelfTestIssues(Paragraphs));

    [Fact]
    public void WordsDrillParagraphsHaveTenBlanksAndFields()
    {
        Assert.Equal(240, Paragraphs.Count);
        Assert.All(Paragraphs, p =>
        {
            Assert.Equal(10, p.Blanks.Count);
            Assert.NotNull(p.Field);
            Assert.Equal(10, new CTestParagraph(p.Passage).Pieces.OfType<CTestParagraph.BlankPiece>().Count());
        });
        Assert.Equal("Arts & Language", WordsDrillField.Humanities.Title());
    }

    [Fact]
    public void MissedWordsLeaveAfterTwoCorrectAnswers()
    {
        var store = new WordsDrillStore(KeyValueStore.InMemory(), Paragraphs);
        var paragraph = Paragraphs[0];
        var wrong = Enumerable.Repeat(true, 10).ToList();
        wrong[0] = false;
        store.Record(paragraph, Enumerable.Repeat("x", 10).ToList(), wrong);
        var word = paragraph.Blanks[0].SampleAnswer!;
        var missed = Assert.Single(store.WordsToReview);
        Assert.Equal(word, missed.Word);
        Assert.Equal(2, missed.Misses);
        Assert.Equal(paragraph.Blanks[0].WordGloss, missed.Gloss);
        Assert.NotNull(store.LatestMiss(word.ToUpperInvariant()));
        var allRight = Enumerable.Repeat(true, 10).ToList();
        store.Record(paragraph, Enumerable.Repeat("x", 10).ToList(), allRight);
        Assert.Equal(1, Assert.Single(store.WordsToReview).Misses);
        store.Record(paragraph, Enumerable.Repeat("x", 10).ToList(), allRight);
        Assert.Empty(store.WordsToReview);
        var attempt = store.AttemptFor(paragraph)!;
        Assert.Equal(3, attempt.Attempts);
        Assert.Equal(10, attempt.BestCorrect);
        Assert.Equal(1.0, store.RecentAccuracy);
        Assert.Equal(3, store.History.Count);
    }

    [Fact]
    public void WordsDrillNextPrefersUnattemptedThenWeakest()
    {
        var times = new Queue<DateTimeOffset>(Enumerable.Range(0, 1000).Select(i => new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(i)));
        var store = new WordsDrillStore(KeyValueStore.InMemory(), Paragraphs.Take(3).ToList(), () => times.Dequeue(), new Random(1));
        var a = Paragraphs[0];
        var b = Paragraphs[1];
        var c = Paragraphs[2];
        store.Record(a, [], Enumerable.Repeat(true, 10).ToList());
        store.Record(b, [], Enumerable.Repeat(false, 10).ToList());
        Assert.Equal(c, store.Next(null, null));
        store.Record(c, [], Enumerable.Repeat(false, 10).ToList());
        // All attempted: lowest last score, then oldest.
        Assert.Equal(b, store.Next(null, null));
        Assert.Equal(c, store.Next(b.Id, null));
        Assert.Null(store.Next(null, null, new HashSet<WordsDrillField>()));
    }

    [Fact]
    public void WeakFieldsNeedThreeParagraphs()
    {
        var store = new WordsDrillStore(KeyValueStore.InMemory(), Paragraphs);
        foreach (var paragraph in Paragraphs.Where(p => p.Field == WordsDrillField.Earth).Take(3))
            store.Record(paragraph, [], Enumerable.Repeat(false, 10).ToList());
        foreach (var paragraph in Paragraphs.Where(p => p.Field == WordsDrillField.Life).Take(2))
            store.Record(paragraph, [], Enumerable.Repeat(false, 10).ToList());
        Assert.Equal([WordsDrillField.Earth], store.WeakFields);
        Assert.Equal(2, store.AccuracyByField.Count);
        var session = new WordsDrillSession(store) { Subject = WordsDrillSession.WeakSubjects };
        Assert.Equal(WordsDrillField.Earth, session.Paragraph!.Field);
        Assert.Equal("Weak areas: Earth & Space. Choose “Focus on weak areas” to practice them first.", session.WeakAreasText);
    }

    [Fact]
    public void WordsDrillSessionChecksAndRecords()
    {
        var store = new WordsDrillStore(KeyValueStore.InMemory(), Paragraphs);
        var session = new WordsDrillSession(store);
        session.Appear();
        var paragraph = session.Paragraph!;
        Assert.Equal(10, session.Entries.Count);
        for (var i = 0; i < 10; i++) session.SetEntry(i, paragraph.Blanks[i].AcceptedTextAnswers[0] + "1!", 20);
        session.SetEntry(3, "zzzzzzzzzz", 1);
        Assert.Equal("zzz", session.Entries[3]); // letters only, at most missing + 2
        session.Check();
        Assert.Equal(9, session.Results!.Count(r => r));
        Assert.Equal("Best 9/10 · 1 attempt", session.AttemptSummary);
        Assert.Equal("1 / 240", session.ParagraphsPracticedText);
        Assert.Equal("90%", session.RecentAccuracyText);
        var explanations = WordExplanation.For(paragraph, session.Entries, session.Results);
        Assert.Equal($"you typed {explanations[3].Kept}zzz", explanations[3].TypedNote);
        Assert.Equal(paragraph.Blanks[0].SampleAnswer, explanations[0].Word);
        session.TryAgain();
        Assert.Null(session.Results);
        Assert.Same(paragraph, session.Paragraph);
        session.Next();
        Assert.NotEqual(paragraph.Id, session.Paragraph!.Id);
    }

    [Fact]
    public void ReadingDrillStoreTracksTypesAndPersists()
    {
        var kv = KeyValueStore.InMemory();
        var store = new ReadingDrillStore(kv, ReadingSets);
        var set = ReadingSets.First(s => s.Questions.Count >= 4);
        var choices = set.Questions.Select(q => (string?)q.CorrectChoiceID).ToList();
        choices[0] = null;
        var results = set.Questions.Select((q, i) => choices[i] == q.CorrectChoiceID).ToList();
        store.Add(set, choices, results);
        var reloaded = new ReadingDrillStore(kv, ReadingSets);
        var record = Assert.Single(reloaded.History);
        Assert.Null(record.Choices[0]);
        Assert.Equal(set.Questions.Count - 1, record.Correct);
        Assert.Equal(1, reloaded.PracticedSets);
        Assert.Equal((double)(set.Questions.Count - 1) / set.Questions.Count, reloaded.RecentAccuracy);
        Assert.Equal(set.Questions.Count, reloaded.AccuracyByType.Sum(t => t.Total));
        Assert.Contains("\"choices\":[null", kv.GetRawJson(StorageKeys.ReadingDrill));
        Assert.NotEqual(set.Id, reloaded.Next(null, null, null, null)!.Id);
    }

    [Fact]
    public void ReadingDrillNextPrefersFocusTypes()
    {
        var store = new ReadingDrillStore(KeyValueStore.InMemory(), ReadingSets);
        var focus = new HashSet<ReadingQuestionType> { ReadingQuestionType.NegativeFactual };
        var pick = store.Next(null, TOEFLTaskType.ReadAcademicPassage, TOEFLDifficulty.Advanced, focus)!;
        var best = ReadingSets.Where(s => s.Task == TOEFLTaskType.ReadAcademicPassage && s.Difficulty == TOEFLDifficulty.Advanced)
            .Max(s => s.Questions.Count(q => q.ReadingType() == ReadingQuestionType.NegativeFactual));
        Assert.Equal(best, pick.Questions.Count(q => q.ReadingType() == ReadingQuestionType.NegativeFactual));
        Assert.All(ReadingSets.SelectMany(s => s.Questions), q => Assert.NotNull(q.ReadingType()));
        Assert.Equal("negative_factual", ReadingQuestionType.NegativeFactual.RawValue());
        Assert.Equal(ReadingQuestionType.MainIdea, ReadingQuestionTypes.FromRawValue("main_idea"));
    }

    [Fact]
    public void ReadingDrillSessionFlow()
    {
        var session = new ReadingDrillSession(new ReadingDrillStore(KeyValueStore.InMemory(), ReadingSets)) { TaskFilter = ReadingTaskFilter.Daily };
        var set = session.Current!;
        Assert.Equal(TOEFLTaskType.ReadDailyLife, set.Task);
        Assert.Equal("Read in Daily Life", session.TaskTitle);
        session.Choose(0, set.Questions[0].CorrectChoiceID!);
        Assert.Equal($"1 of {set.Questions.Count} answered", session.AnsweredText);
        session.Check();
        session.Choose(1, "ignored");
        Assert.Null(session.Choices[1]);
        Assert.Equal($"1 / {set.Questions.Count} correct", session.ScoreText);
        Assert.Equal("key", ReadingDrillSession.OptionState(set.Questions[0], set.Questions[0].CorrectChoiceID!, set.Questions[0].CorrectChoiceID, true));
        Assert.Equal($"Last time 1/{set.Questions.Count}", session.LastTimeText);
        Assert.Equal(set.Title, set.Passage.Split('\n')[0]);
    }

    [Fact]
    public void FlashcardsDeckInfiniteLoopAndPersistence()
    {
        var kv = KeyValueStore.InMemory();
        var words = Shared.StudyBank.Words;
        var session = new FlashcardSession(words, kv, random: new Random(4));
        Assert.Equal(FlashcardScreen.NotStarted, session.Screen);
        session.Appear();
        Assert.Equal(20, session.Deck.Count);
        Assert.Equal(FlashcardScreen.Card, session.Screen);
        Assert.False(session.HandleKey(FlashcardKey.RightArrow)); // not revealed yet
        Assert.True(session.HandleKey(FlashcardKey.Space));
        var first = session.CurrentCard!;
        Assert.True(session.HandleKey(FlashcardKey.RightArrow));
        Assert.Contains(first.Id, session.Progress.Mastered);
        Assert.Equal("Card 2 of 20", session.DeckProgressTitle);
        Assert.Equal(["" + first.Id], kv.GetStringArray(StorageKeys.FlashcardsMastered));
        Assert.Equal(words.Count - 1, session.EligibleWords.Count); // unmastered only
        session.Reveal();
        session.Rate(false);
        Assert.Equal("1 known · 1 review", session.DeckProgressDetail);
        for (var i = 0; i < 18; i++) { session.Reveal(); session.Rate(true); }
        Assert.Equal(FlashcardScreen.DeckComplete, session.Screen);
        session.ReviewMissedCards();
        Assert.Single(session.Deck);

        session.Mode = FlashcardMode.Infinite;
        Assert.Equal("infinite", kv.GetString(StorageKeys.FlashcardsMode));
        Assert.Equal(words.Count - 19, session.Deck.Count);
        Assert.Equal(session.Deck.Count, session.InfiniteTotal);
        var missed = session.CurrentCard!;
        session.Reveal();
        session.Rate(false);
        var position = session.Deck.ToList().IndexOf(missed);
        Assert.InRange(position, 32, 48); // remaining/4 capped at 40, ±20%
        Assert.Equal(FlashcardMode.Infinite, new FlashcardSession(words, kv).Mode);

        session.ResetProgress();
        Assert.Empty(session.Progress.Mastered);
        Assert.Null(kv.GetStringArray(StorageKeys.FlashcardsMastered));
    }

    [Fact]
    public void RequeueGapRules()
    {
        var session = new FlashcardSession([], KeyValueStore.InMemory(), random: new Random(9));
        for (var i = 0; i < 50; i++)
        {
            Assert.Equal(5, session.RequeueGap(5));                  // short queue → end
            Assert.InRange(session.RequeueGap(40), 12, 18);          // target 15
            Assert.InRange(session.RequeueGap(1000), 32, 48);        // target 40
        }
    }

    [Fact]
    public void WritingGuideLabelsTheModelEmail()
    {
        var question = WritingGuide.EmailSamples(Shared.Bank)[0];
        var bullets = WritingGuide.Bullets(question.Prompt);
        var parts = WritingGuide.LabeledEmailParts(question.SampleAnswer!, bullets);
        Assert.Equal("① 稱呼", parts[0].Label);
        Assert.Equal("② 開門見山說目的", parts[1].Label);
        Assert.Equal("⑤ 結尾敬語與署名", parts[^1].Label);
        Assert.Equal(GuideColor.Action, parts[^1].Color);
        Assert.Single(WritingGuide.LabeledEmailParts("Hi\n\nthanks"));

        var sample = "Dear Ms. Lee,\n\nI am writing about the trip. It was great.\n\nFirst point.\n\nSecond point.\n\nThank you for your help.\n\nBest,\nAna";
        var labeled = WritingGuide.LabeledEmailParts(sample, ["the trip", "first", "second"]);
        Assert.Equal(["① 稱呼", "② 開門見山說目的", "③ 要點 1：the trip", "③ 要點 2：first", "③ 要點 3：second", "④ 禮貌收尾", "⑤ 結尾敬語與署名"], labeled.Select(p => p.Label));
        Assert.Equal("I am writing about the trip.", labeled[1].Text);
        Assert.Equal("It was great.", labeled[2].Text);
        Assert.Equal("Best,\nAna", labeled[^1].Text);
    }

    [Fact]
    public void WritingGuideHighlightsNamesAndSignalPhrases()
    {
        var spans = WritingGuide.HighlightedDiscussion("I agree with Maya. However, Leo is right because data matter.", ["Maya", "Leo"]);
        Assert.Equal("I agree with Maya. However, Leo is right because data matter.", string.Concat(spans.Select(s => s.Text)));
        Assert.Contains(spans, s => s.Text == "Maya" && s.Color == GuideColor.Action);
        Assert.Contains(spans, s => s.Text == "However" && s.Color == GuideColor.Teal);
        Assert.Contains(spans, s => s.Text == "I agree" && s.Color == GuideColor.Teal);
        Assert.Equal(["Maya", "Leo"], WritingGuide.ClassmateNames("Maya: I think so.\n\n Leo : No."));
        Assert.Equal(3, WritingGuide.WordCount(" a  b\nc "));
    }

    [Fact]
    public void SpeakingTrainersCycleThroughSets()
    {
        var repeatSets = SpeakingGuide.Sets(Shared.Bank, TOEFLTaskType.ListenRepeat);
        Assert.Equal(22, repeatSets.Count); // 154 sentences in sets of 7
        Assert.All(repeatSets, s => Assert.Equal(Enumerable.Range(0, s.Count), s.Select(q => q.SetIndex ?? 0)));
        Assert.Equal(26, SpeakingGuide.Sets(Shared.Bank, TOEFLTaskType.Interview).Count);
        Assert.Equal(4, SpeakingGuide.ModelAnswers.Count);
        Assert.Equal("I live in a city / and I like / to walk to work.", SpeakingGuide.MeaningGroups("I live in a city and I like to walk to work."));
    }
}
