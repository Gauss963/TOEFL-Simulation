using CommunityToolkit.Mvvm.ComponentModel;
using TOEFLSimulation.Core.Audio;

namespace TOEFLSimulation.Core;

/// <summary>Swift <c>SpeakingGuideView.Topic</c>.</summary>
public enum SpeakingGuideTopic { RepeatTask, Interview, Delivery }

/// <summary>Data and helpers of the Speaking Guide (SpeakingGuideView). The explanatory prose stays in the UI.</summary>
public static class SpeakingGuide
{
    public static string Title(this SpeakingGuideTopic topic) => topic switch
    {
        SpeakingGuideTopic.RepeatTask => "Listen and Repeat",
        SpeakingGuideTopic.Interview => "Take an Interview",
        _ => "Pronunciation & Fluency",
    };

    public sealed record ModelAnswer(string Question, IReadOnlyList<(string Label, string Text)> Parts, string Note);

    /// <summary>Original model answers for interview set iv-01 (study habits of university students).</summary>
    public static readonly IReadOnlyList<ModelAnswer> ModelAnswers =
    [
        new("Could you tell me where you usually study and what a typical study session looks like for you?",
        [
            ("直接回答", "I usually study on the third floor of the library, because it's quiet and I don't get distracted there."),
            ("細節", "A typical session starts with a quick plan: I write down two or three tasks I want to finish. Then I work in blocks of about forty minutes and take a short break in between, maybe to get some water or walk around."),
            ("具體做法", "I also put my phone in my bag, because otherwise I'd keep checking messages."),
            ("收尾", "At the end, I note what I finished and what's next, so it's easier to start the following day."),
        ], "現在式描述習慣；用 first / then / at the end 讓步驟清楚。"),
        new("Think about a time when you had to prepare for a really important exam. How did you get ready, and how did it turn out?",
        [
            ("背景", "Last year, I had to prepare for my organic chemistry final, which was worth forty percent of my grade."),
            ("你做了什麼", "About three weeks before the exam, I made a schedule and reviewed one chapter every day. The most helpful thing was doing old practice problems, because they showed me exactly which reactions I kept mixing up. I also met a classmate twice a week, and we explained the mechanisms to each other out loud."),
            ("結果與收穫", "In the end, it turned out really well. I got an A minus, much higher than my midterm score, and it taught me that steady practice works better than cramming."),
        ], "過去式講一個具體事件：背景 → 行動 → 結果。"),
        new("Some students like to study in groups, while others prefer to work alone. Which do you prefer, and why do you think it works better for you?",
        [
            ("直接回答", "I definitely prefer studying alone, at least for most subjects."),
            ("主要理由", "The main reason is that I can control my own pace. In a group, we often spend a long time on topics I already understand, or the conversation drifts to something else."),
            ("具體例子", "For example, last semester my economics study group met for three hours, but we only finished one problem set."),
            ("讓步與收尾", "That said, I think groups are useful right before an exam, when you can quiz each other. So overall, I study alone for most of the work and join a group only for the final review."),
        ], "選一邊 → 理由 → 例子，再用 That said 承認另一邊，顯得成熟。"),
        new("Do you think schools should require all first-year students to take a course on how to study effectively? Why or why not?",
        [
            ("表明立場", "Yes, I think universities should require a course like that for first-year students."),
            ("主要理由", "The biggest reason is that studying in college is very different from high school. In high school, teachers check your progress all the time, but in college you have to manage long projects on your own, and many students don't know how."),
            ("具體例子", "For instance, several of my friends did badly on their first midterms simply because they started reviewing the night before."),
            ("收尾", "A short required course could teach skills like planning and active recall early, before students fall behind. That's why I think it would save a lot of people a stressful first year."),
        ], "一個理由說完整 + 現實例子，比列三個理由更有說服力。"),
    ];

    /// <summary>Sets of a task, each sorted by setIndex, ordered by their first item's ID (the trainers' set list).</summary>
    public static IReadOnlyList<IReadOnlyList<TOEFLQuestion>> Sets(TOEFLQuestionBank bank, TOEFLTaskType task) =>
        bank.AllQuestions.Where(q => q.TaskType == task)
            .GroupBy(q => q.SetKey)
            .Select(g => (IReadOnlyList<TOEFLQuestion>)g.OrderBy(q => q.SetIndex ?? 0).ToList())
            .OrderBy(set => set.Count > 0 ? set[0].Id : "", StringComparer.Ordinal)
            .ToList();

    private static readonly HashSet<string> Breakers =
    [
        "and", "but", "so", "or", "because", "if", "when", "where", "while", "which", "who", "that",
        "to", "at", "for", "in", "on", "with", "from", "behind", "near", "during", "before", "after", "until",
    ];

    /// <summary>Marks likely meaning-group boundaries with "/": after commas, and before a conjunction, relative word, or
    /// preposition once the current group has at least three words.</summary>
    public static string MeaningGroups(string sentence)
    {
        var output = new List<string>();
        var groupLength = 0;
        foreach (var word in sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var bare = TextUtil.TrimPunctuation(word.ToLowerInvariant());
            if (groupLength >= 3 && Breakers.Contains(bare))
            {
                output.Add("/");
                groupLength = 0;
            }
            output.Add(word);
            groupLength++;
            if (word.EndsWith(','))
            {
                output.Add("/");
                groupLength = 0;
            }
        }
        return string.Join(" ", output).Replace("/ /", "/", StringComparison.Ordinal);
    }

    public const string MeaningGroupsNote = "「/」標示建議的意群切分；字尾 -s、-ed 與冠詞要照說。";
}

/// <summary>
/// ListenRepeatTrainer: plays Listen and Repeat sentences one at a time; the text, split into meaning groups, is shown after
/// the learner has tried repeating it.
/// </summary>
public sealed class ListenRepeatTrainer : ObservableObject
{
    private readonly ExamAudioPlayer player;
    private readonly Random random;
    private int setIndex;
    private int sentenceIndex;
    private bool revealed;

    public ListenRepeatTrainer(TOEFLQuestionBank bank, ExamAudioPlayer player, Random? random = null)
    {
        this.player = player;
        this.random = random ?? Random.Shared;
        Sets = SpeakingGuide.Sets(bank, TOEFLTaskType.ListenRepeat);
        setIndex = this.random.Next(Math.Max(1, Sets.Count));
    }

    public IReadOnlyList<IReadOnlyList<TOEFLQuestion>> Sets { get; }
    public int SetIndex => setIndex;
    public int SentenceIndex => sentenceIndex;

    public bool Revealed
    {
        get => revealed;
        set => SetProperty(ref revealed, value);
    }

    public TOEFLQuestion? Sentence => setIndex < Sets.Count && sentenceIndex < Sets[setIndex].Count ? Sets[setIndex][sentenceIndex] : null;

    /// <summary>"{setting} · sentence 2 of 7" (null without a setting).</summary>
    public string? Caption => Sentence?.Setting is { } setting ? $"{setting} · sentence {sentenceIndex + 1} of {Sets[setIndex].Count}" : null;

    public string Instructions => $"播放後，在心裡切意群，然後大聲複述一次（實際考試約有 {Sentence?.ResponseSeconds ?? 10} 秒）。說完再按「顯示原句」核對。";

    public string? GroupedText => Revealed && Sentence?.AudioScript is { } text ? SpeakingGuide.MeaningGroups(text) : null;

    public Task<bool> PlayAsync() => Sentence is { } s ? player.PlayAsync(s.AudioKey, s.AudioScript) : Task.FromResult(true);

    public void ToggleReveal() => Revealed = !Revealed;

    public void Advance()
    {
        player.Stop();
        Revealed = false;
        if (sentenceIndex + 1 < Sets[setIndex].Count)
        {
            sentenceIndex++;
        }
        else
        {
            setIndex = (setIndex + 1) % Sets.Count;
            sentenceIndex = 0;
        }
        OnPropertyChanged(string.Empty);
    }

    public void NewSet()
    {
        setIndex = random.Next(Sets.Count);
        sentenceIndex = 0;
        Revealed = false;
        player.Stop();
        OnPropertyChanged(string.Empty);
    }

    public void Disappear() => player.Stop();
}

/// <summary>
/// InterviewTrainer: plays an interview question, then counts down 45 seconds for a spoken answer and plays the end tone.
/// Call <see cref="Tick(DateTimeOffset)"/> about every 0.1 s while <see cref="Remaining"/> is not null.
/// </summary>
public sealed class InterviewTrainer : ObservableObject
{
    public const double AnswerSeconds = 45;

    private readonly ExamAudioPlayer player;
    private readonly Random random;
    private readonly Func<DateTimeOffset> clock;
    private int setIndex;
    private int questionIndex;
    private bool revealed;
    private double? remaining;
    private DateTimeOffset? countdownStart;
    private int generation;

    public InterviewTrainer(TOEFLQuestionBank bank, ExamAudioPlayer player, Random? random = null, Func<DateTimeOffset>? clock = null)
    {
        this.player = player;
        this.random = random ?? Random.Shared;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        Sets = SpeakingGuide.Sets(bank, TOEFLTaskType.Interview);
        setIndex = this.random.Next(Math.Max(1, Sets.Count));
    }

    public IReadOnlyList<IReadOnlyList<TOEFLQuestion>> Sets { get; }
    public int SetIndex => setIndex;
    public int QuestionIndex => questionIndex;

    public bool Revealed
    {
        get => revealed;
        set => SetProperty(ref revealed, value);
    }

    /// <summary>Seconds left in the answer countdown (shown as ceil; red at 10 or less), or null when not counting.</summary>
    public double? Remaining
    {
        get => remaining;
        private set => SetProperty(ref remaining, value);
    }

    public TOEFLQuestion? Question => setIndex < Sets.Count && questionIndex < Sets[setIndex].Count ? Sets[setIndex][questionIndex] : null;

    public string Caption => $"Question {questionIndex + 1} of {(setIndex < Sets.Count ? Sets[setIndex].Count : 0)}";

    /// <summary>The set's intro, shown above the first question only.</summary>
    public string? Intro => questionIndex == 0 ? Question?.Intro : null;

    public const string Instructions = "題目只用聽的（和正式考試一樣）。播完後開始 45 秒倒數，請用「直接回答 → 理由 → 例子 → 收尾」說滿時間。";

    /// <summary>The Play button is disabled while playing or counting down.</summary>
    public bool CanPlay => !player.IsPlaying && Remaining is null;

    /// <summary>"Play question": plays it, then starts the 45-second countdown.</summary>
    public async Task PlayAndCountAsync()
    {
        if (Question is not { } question) return;
        var mine = ++generation;
        if (!await player.PlayAsync(question.AudioKey, question.AudioScript) || mine != generation) return;
        countdownStart = clock();
        Remaining = AnswerSeconds;
    }

    /// <summary>Updates the countdown; at zero it plays the end tone and clears the countdown.</summary>
    public void Tick(DateTimeOffset now)
    {
        if (countdownStart is not { } start || Remaining is null) return;
        Remaining = Math.Max(0, AnswerSeconds - (now - start).TotalSeconds);
        if (Remaining > 0) return;
        countdownStart = null;
        _ = player.PlayToneAsync(TOEFLExamAudio.EndTone);
        Remaining = null;
    }

    public void Tick() => Tick(clock());

    public void ToggleReveal() => Revealed = !Revealed;

    private void Reset()
    {
        generation++;
        countdownStart = null;
        Remaining = null;
        Revealed = false;
        player.Stop();
    }

    public void Advance()
    {
        Reset();
        if (questionIndex + 1 < Sets[setIndex].Count)
        {
            questionIndex++;
        }
        else
        {
            setIndex = (setIndex + 1) % Sets.Count;
            questionIndex = 0;
        }
        OnPropertyChanged(string.Empty);
    }

    public void NewSet()
    {
        setIndex = random.Next(Sets.Count);
        questionIndex = 0;
        Reset();
        OnPropertyChanged(string.Empty);
    }

    public void Disappear() => Reset();
}
