namespace TOEFLSimulation.Core;

/// <summary>Swift <c>WritingGuideView.Topic</c>.</summary>
public enum WritingGuideTopic { Email, Discussion, Sentence }

/// <summary>Color role of an annotated part (the Mac colors: action blue, teal, navy, gold, secondary gray).</summary>
public enum GuideColor { Action, Teal, Navy, Gold, Secondary }

/// <summary>
/// The non-prose logic of the Writing Guide (WritingGuideView): sample selection, the labeled model email, classmate names,
/// discussion highlighting, and word counts. The guide's explanatory text stays in the UI.
/// </summary>
public static class WritingGuide
{
    public static string Title(this WritingGuideTopic topic) => topic switch
    {
        WritingGuideTopic.Email => "Write an Email",
        WritingGuideTopic.Discussion => "Academic Discussion",
        _ => "Build a Sentence",
    };

    /// <summary>Email items with a sample answer (the "範例 Email" navigator).</summary>
    public static IReadOnlyList<TOEFLQuestion> EmailSamples(TOEFLQuestionBank bank) =>
        bank.AllQuestions.Where(q => q.TaskType == TOEFLTaskType.WriteEmail && !string.IsNullOrEmpty(q.SampleAnswer)).ToList();

    /// <summary>Academic Discussion items with a sample answer.</summary>
    public static IReadOnlyList<TOEFLQuestion> DiscussionSamples(TOEFLQuestionBank bank) =>
        bank.AllQuestions.Where(q => q.TaskType == TOEFLTaskType.AcademicDiscussion && !string.IsNullOrEmpty(q.SampleAnswer)).ToList();

    /// <summary>The sample navigator caption: "Sample 3 of 100".</summary>
    public static string SampleCaption(int index, int count) => $"Sample {index % count + 1} of {count}";

    /// <summary>Previous / next with wrap-around, as the navigator's chevrons.</summary>
    public static int PreviousIndex(int index, int count) => (index - 1 + count) % count;
    public static int NextIndex(int index, int count) => (index + 1) % count;

    public sealed record LabeledPart(string Label, string Text, GuideColor Color);

    /// <summary>The task's required points ("• …" lines in the prompt).</summary>
    public static IReadOnlyList<string> Bullets(string prompt) =>
        prompt.Split('\n').Where(line => line.StartsWith("• ", StringComparison.Ordinal)).Select(line => line[2..]).ToList();

    /// <summary>
    /// Splits a model email into greeting, purpose, body, closing, and sign-off. When the body has one paragraph per required
    /// point, each paragraph is labeled with the point it answers.
    /// </summary>
    public static IReadOnlyList<LabeledPart> LabeledEmailParts(string email, IReadOnlyList<string>? bullets = null)
    {
        bullets ??= [];
        var paragraphs = email.Split("\n\n").Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
        if (paragraphs.Count < 3) return [new LabeledPart("Email", email, GuideColor.Secondary)];
        var parts = new List<LabeledPart>();
        var body = paragraphs.ToList();
        var greeting = body[0];
        body.RemoveAt(0);
        parts.Add(new LabeledPart("① 稱呼", greeting, GuideColor.Action));
        var signOff = body[^1];
        body.RemoveAt(body.Count - 1);
        // The purpose is the first sentence of the first paragraph; the rest of it belongs to the body.
        var first = body[0];
        body.RemoveAt(0);
        var stop = first.IndexOfAny(['.', '!', '?']);
        var purposeEnd = stop >= 0 ? stop + 1 : first.Length;
        parts.Add(new LabeledPart("② 開門見山說目的", TrimSpaces(first[..purposeEnd]), GuideColor.Teal));
        var restOfFirst = TrimSpaces(first[purposeEnd..]);
        var bodyParagraphs = (restOfFirst.Length == 0 ? new List<string>() : [restOfFirst]).Concat(body).ToList();
        string? closing = null;
        if (bodyParagraphs.Count > 1)
        {
            closing = bodyParagraphs[^1];
            bodyParagraphs.RemoveAt(bodyParagraphs.Count - 1);
        }
        var matchesPoints = bullets.Count > 0 && bodyParagraphs.Count == bullets.Count;
        for (var index = 0; index < bodyParagraphs.Count; index++)
        {
            var label = matchesPoints ? $"③ 要點 {index + 1}：{bullets[index]}" : "③ 主體：回應要點";
            parts.Add(new LabeledPart(label, bodyParagraphs[index], GuideColor.Navy));
        }
        if (closing is not null) parts.Add(new LabeledPart("④ 禮貌收尾", closing, GuideColor.Gold));
        parts.Add(new LabeledPart("⑤ 結尾敬語與署名", signOff, GuideColor.Action));
        return parts;
    }

    private static string TrimSpaces(string text) => text.Trim(' ', '\t', ' ');

    /// <summary>Classmate names: the text before the first colon of each post.</summary>
    public static IReadOnlyList<string> ClassmateNames(string posts) =>
        posts.Split("\n\n").Select(post => TextUtil.Split(post, c => c == ':').FirstOrDefault())
            .OfType<string>().Select(TrimSpaces).ToList();

    /// <summary>Transition, example, and stance phrases highlighted in teal.</summary>
    public static readonly IReadOnlyList<string> SignalPhrases =
    [
        "In my view", "In my opinion", "I believe", "I agree", "I disagree", "I would argue", "I think",
        "However", "although", "Although", "While", "That said", "Admittedly", "On the other hand",
        "For example", "For instance", "Another", "In addition", "Moreover", "Furthermore", "Overall",
        "For these reasons", "neither", "Neither", "because", "Because", "As a result", "In contrast",
    ];

    /// <summary>A run of text in the highlighted discussion; <see cref="Color"/> is null for plain text (bold when set).</summary>
    public sealed record Span(string Text, GuideColor? Color);

    /// <summary>
    /// The model discussion response with classmate names (action blue) and signal phrases (teal) marked in bold, as runs of
    /// text. Case-sensitive matches; a later phrase recolors overlapping text, as on the Mac.
    /// </summary>
    public static IReadOnlyList<Span> HighlightedDiscussion(string text, IReadOnlyList<string> classmates)
    {
        var colors = new GuideColor?[text.Length];
        void Mark(string phrase, GuideColor color)
        {
            var start = 0;
            while (start <= text.Length - phrase.Length)
            {
                var found = text.IndexOf(phrase, start, StringComparison.Ordinal);
                if (found < 0) break;
                for (var i = found; i < found + phrase.Length; i++) colors[i] = color;
                start = found + phrase.Length;
            }
        }
        foreach (var name in classmates.Where(n => n.Length > 0)) Mark(name, GuideColor.Action);
        foreach (var phrase in SignalPhrases) Mark(phrase, GuideColor.Teal);
        var spans = new List<Span>();
        var runStart = 0;
        for (var i = 1; i <= text.Length; i++)
        {
            if (i < text.Length && colors[i] == colors[runStart]) continue;
            spans.Add(new Span(text[runStart..i], colors[runStart]));
            runStart = i;
        }
        return spans;
    }

    /// <summary>Words separated by whitespace ("163 words").</summary>
    public static int WordCount(string text) => TextUtil.WordCount(text);
}
