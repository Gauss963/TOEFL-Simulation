using System.Text.RegularExpressions;

namespace TOEFLSimulation.Core;

/// <summary>User-visible text and small pure helpers from the Mac exam screens (ExamView.swift).</summary>
public static class ExamText
{
    /// <summary>Directions page subtitle.</summary>
    public static string SectionIntroduction(TOEFLSectionKind kind) => kind switch
    {
        TOEFLSectionKind.Reading => "Read everyday and academic texts, use context, and restore partially missing words.",
        TOEFLSectionKind.Listening => "Listen to people speaking in campus and academic settings, then answer questions.",
        TOEFLSectionKind.Writing => "Build sentences, write an email, and contribute to an academic discussion.",
        _ => "Repeat what you hear, then answer an interviewer's questions.",
    };

    /// <summary>Directions page rules card.</summary>
    public static string SectionRules(TOEFLSectionKind kind) => kind switch
    {
        TOEFLSectionKind.Reading => "Module 1 has 21 minutes and Module 2 has 9 minutes. Use Next and Back to move within a module. Your Module 1 performance determines Module 2, and you cannot return to Module 1. Some items are unscored.",
        TOEFLSectionKind.Listening => "Module 1 has 18 minutes; Module 2 has 7 minutes on the lower path or 11 minutes on the upper path. The clock pauses while recordings play. Each recording plays only once. You can take notes. You cannot return to previous questions. Some items are unscored.",
        TOEFLSectionKind.Writing => "Build a Sentence has 6 minutes, the email 7 minutes, and the academic discussion 10 minutes. Each task ends when its clock runs out.",
        _ => "Each prompt plays once. Speak after the tone; recording starts and stops automatically (8–12 seconds for each repeated sentence, 45 seconds for each interview answer). There is no preparation time.",
    };

    /// <summary>Directions page "Structure" fact card.</summary>
    public static string StructureLabel(TOEFLSectionKind kind) => kind switch
    {
        TOEFLSectionKind.Reading or TOEFLSectionKind.Listening => "2 modules",
        TOEFLSectionKind.Writing => "3 timed tasks",
        _ => "Auto-timed",
    };

    /// <summary>Title of the confirmation shown when Next would end a part.</summary>
    public static string PartEndTitle(TOEFLExamSession session) =>
        session.IsLastPart ? $"End the {session.CurrentSection.Kind.Title()} section?" : $"End {session.CurrentPart.Title}?";

    /// <summary>Confirm button of that dialog ("Return" cancels).</summary>
    public static string PartEndConfirm(TOEFLExamSession session) => session.IsLastPart ? "End section" : "Continue";

    public const string PartEndMessage = "You will not be able to return to this part once you continue.";
    public const string ExitTitle = "Exit this test?";
    public const string ExitConfirm = "Exit without saving";
    public const string ExitMessage = "Answers and recordings from this session will be discarded.";
    public const string ListeningFooterNote = "You cannot return to previous questions.";
    public const string ScoringTitle = "Scoring your Speaking responses…";
    public const string StimulusNote = "The recording plays once. You may take notes.";

    /// <summary>The scoring screen's message.</summary>
    public static string ScoringMessage(int pendingScores) =>
        $"Recordings are transcribed on this device and scored with the official 0–5 rubrics. {pendingScores} remaining.";

    /// <summary>Stimulus screen status: "Listening…" while playing, otherwise "Preparing audio…".</summary>
    public static string StimulusStatus(bool isPlaying) => isPlaying ? "Listening…" : "Preparing audio…";

    /// <summary>Begin button title: "Begin Reading", ….</summary>
    public static string BeginButtonTitle(TOEFLSectionKind kind) => $"Begin {kind.Title()}";

    /// <summary>The Next button is disabled while a stimulus must still play or audio is playing.</summary>
    public static bool IsNextEnabled(TOEFLExamSession session, bool isAudioPlaying) =>
        !session.NeedsStimulus(session.CurrentQuestion) && !isAudioPlaying;

    /// <summary>Speakers shown on the stimulus screen; one female "speaker" when the item lists none.</summary>
    public static IReadOnlyList<TOEFLSpeaker> StimulusSpeakers(TOEFLQuestion question) =>
        question.Speakers ?? [new TOEFLSpeaker { Role = "speaker", Gender = "female" }];

    /// <summary>The text read by the system voice when a stimulus clip is missing: intro and script joined by a space.</summary>
    public static string StimulusFallbackText(TOEFLQuestion question) =>
        string.Join(" ", new[] { question.Intro, question.AudioScript }.OfType<string>());

    /// <summary>Complete the Words input filter: letters and apostrophes only, at most <c>missing + 2</c> characters.</summary>
    public static string SanitizeBlankEntry(string value, int missing) =>
        TextUtil.PrefixCharacters(new string(value.Where(c => char.IsLetter(c) || c == '\'').ToArray()), missing + 2);

    /// <summary>Width hint used by the Mac blank field: <c>missing × 15 + 12</c> points.</summary>
    public static double BlankFieldWidth(int missing) => missing * 15 + 12;

    /// <summary>Placeholder for a blank: one underscore per missing letter.</summary>
    public static string BlankPlaceholder(int missing) => new('_', missing);

    /// <summary>Writing task word counter (split on whitespace and newlines).</summary>
    public static int WordCount(string text) => TextUtil.WordCount(text);

    /// <summary>Academic Discussion classmate posts: the passage split on blank lines.</summary>
    public static IReadOnlyList<string> DiscussionPosts(TOEFLQuestion question) =>
        (question.Passage ?? "").Split("\n\n").Where(p => p.Length > 0).ToList();

    /// <summary>Interview screen: "Question n of 4".</summary>
    public static string InterviewQuestionLabel(TOEFLQuestion question) => $"Question {(question.SetIndex ?? 0) + 1} of 4";

    /// <summary>Interviewer name: the first speaker's role capitalized, or "Interviewer".</summary>
    public static string InterviewerTitle(TOEFLQuestion question) =>
        question.Speakers is { Count: > 0 } speakers ? TextUtil.Capitalized(speakers[0].Role) : "Interviewer";
}

/// <summary>Build a Sentence helpers (BuildSentenceView in the Mac app).</summary>
public static class BuildSentence
{
    /// <summary>A word of the reply template, or a blank slot (<see cref="Slot"/> is the blank index).</summary>
    public sealed record Piece(string Text, int? Slot);

    /// <summary>The template split into words and blank slots ("___").</summary>
    public static IReadOnlyList<Piece> TemplatePieces(TOEFLQuestion question)
    {
        var parts = (question.SentenceTemplate ?? "").Split("___");
        var pieces = new List<Piece>();
        for (var index = 0; index < parts.Length; index++)
        {
            pieces.AddRange(parts[index].Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(word => new Piece(word, null)));
            if (index < parts.Length - 1) pieces.Add(new Piece("", index));
        }
        return pieces;
    }

    /// <summary>The chunks not yet placed, removing one occurrence per placed chunk so repeated chunks work.</summary>
    public static IReadOnlyList<string> RemainingTokens(TOEFLQuestion question, IReadOnlyList<string> filled)
    {
        var remaining = question.SentenceTokens.ToList();
        foreach (var token in filled)
        {
            var index = remaining.IndexOf(token);
            if (index >= 0) remaining.RemoveAt(index);
        }
        return remaining;
    }

    /// <summary>The reply begins with a blank (no given words), so the first placed chunk is shown capitalized.</summary>
    public static bool StartsWithBlank(TOEFLQuestion question) => (question.SentenceTemplate ?? "").Trim(' ', '\t').StartsWith("___", StringComparison.Ordinal);

    /// <summary>Text shown in a filled slot.</summary>
    public static string SlotText(TOEFLQuestion question, int slot, string token) =>
        slot == 0 && StartsWithBlank(question) ? CapitalizingFirstLetter(token) : token;

    public static string CapitalizingFirstLetter(string text) =>
        text.Length == 0 ? text : TextUtil.PrefixCharacters(text, 1).ToUpperInvariant() + text[TextUtil.PrefixCharacters(text, 1).Length..];
}

/// <summary>
/// Swift <c>CTestParagraph</c>: a Complete the Words paragraph whose blanks are written as <c>{kept letters|missing letters}</c>,
/// split into words and blanks for layout. Punctuation written directly after a blank stays attached to it (<c>Suffix</c>).
/// </summary>
public sealed class CTestParagraph
{
    public abstract record Piece;
    public sealed record TextPiece(string Text) : Piece;
    public sealed record BlankPiece(int Index, string Prefix, int Missing, string Suffix) : Piece;

    private static readonly Regex Pattern = new(@"\{([^|}]*)\|([^}]*)\}", RegexOptions.CultureInvariant);

    public CTestParagraph(string passage)
    {
        var pieces = new List<Piece>();
        var index = 0;
        var cursor = 0;
        void AppendText(string text)
        {
            var words = text.Split(' ').ToList();
            if (words.Count > 0 && words[0].Length > 0 && pieces.Count > 0 && pieces[^1] is BlankPiece blank)
            {
                pieces[^1] = blank with { Suffix = blank.Suffix + words[0] };
                words.RemoveAt(0);
            }
            pieces.AddRange(words.Where(w => w.Length > 0).Select(w => new TextPiece(w)));
        }
        foreach (Match match in Pattern.Matches(passage))
        {
            AppendText(passage[cursor..match.Index]);
            pieces.Add(new BlankPiece(index, match.Groups[1].Value, TextUtil.CharacterCount(match.Groups[2].Value), ""));
            index++;
            cursor = match.Index + match.Length;
        }
        AppendText(passage[cursor..]);
        Pieces = pieces;
    }

    public IReadOnlyList<Piece> Pieces { get; }
}
