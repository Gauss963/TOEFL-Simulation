namespace TOEFLSimulation.Core;

/// <summary>Swift <c>TOEFLPracticeScoring</c>: raw-to-band conversion, overall band, comparable 0–120 score, and CEFR.</summary>
public static class TOEFLPracticeScoring
{
    private static readonly (double Accuracy, double Band)[] UpperSteps =
        [(0.97, 6), (0.90, 5.5), (0.84, 5), (0.76, 4.5), (0.66, 4), (0.55, 3.5), (0.43, 3), (0, 2.5)];
    private static readonly (double Accuracy, double Band)[] LowerSteps =
        [(0.92, 4), (0.80, 3.5), (0.66, 3), (0.50, 2.5), (0.34, 2), (0.18, 1.5), (0, 1)];
    private static readonly (double Accuracy, double Band)[] LinearSteps =
        [(0.97, 6), (0.90, 5.5), (0.82, 5), (0.72, 4.5), (0.62, 4), (0.52, 3.5), (0.42, 3), (0.32, 2.5), (0.22, 2), (0.12, 1.5), (0, 1)];

    /// <summary>
    /// Reading and Listening band from scored accuracy on the route taken (35 scored items across the router
    /// and the second module). The upper route needs about 90% for 5.5 and 97% for 6; the lower route tops out at 4.0.
    /// </summary>
    public static double ObjectiveBand(int correct, int total, AdaptiveRoute? route)
    {
        if (total <= 0) return 1;
        var accuracy = (double)correct / total;
        var steps = route switch
        {
            AdaptiveRoute.Upper => UpperSteps,
            AdaptiveRoute.Lower => LowerSteps,
            _ => LinearSteps,
        };
        foreach (var (threshold, band) in steps)
            if (accuracy >= threshold) return band;
        return 1;
    }

    /// <summary>Writing raw score 0–20: Build a Sentence (10 × 1) + Email (0–5) + Academic Discussion (0–5).</summary>
    public static (double Band, string Detail, int Raw) WritingBand(IReadOnlyList<TOEFLQuestion> questions, IReadOnlyDictionary<string, TOEFLAnswer> answers)
    {
        TOEFLAnswer AnswerFor(TOEFLQuestion q) => answers.TryGetValue(q.Id, out var a) ? a : TOEFLAnswer.Empty;
        var sentenceCorrect = questions
            .Where(q => q.TaskType == TOEFLTaskType.BuildSentence && q.CountsTowardScore)
            .Count(q => TOEFLAnswerMatcher.IsCorrect(q, AnswerFor(q)) == true);
        var emailQuestion = questions.FirstOrDefault(q => q.TaskType == TOEFLTaskType.WriteEmail);
        var email = emailQuestion is not null && answers.TryGetValue(emailQuestion.Id, out var e) ? e.RubricScore ?? 0 : 0;
        var discussionQuestion = questions.FirstOrDefault(q => q.TaskType == TOEFLTaskType.AcademicDiscussion);
        var discussion = discussionQuestion is not null && answers.TryGetValue(discussionQuestion.Id, out var d) ? d.RubricScore ?? 0 : 0;
        var raw = sentenceCorrect + email + discussion;
        var band = HalfBand(1 + raw / 20.0 * 5);
        var detail = $"Raw {raw}/20 · Build a Sentence {sentenceCorrect}/10 · Email {email}/5 · Academic Discussion {discussion}/5 (local rubric engine).";
        return (band, detail, raw);
    }

    /// <summary>Speaking raw score 0–55: eleven items scored 0–5 on the official rubrics.</summary>
    public static (double Band, string Detail, int Raw) SpeakingBand(IReadOnlyList<TOEFLQuestion> questions, IReadOnlyDictionary<string, TOEFLAnswer> answers)
    {
        if (questions.Count == 0) return (1, "No speaking items.", 0);
        int ScoreOf(TOEFLQuestion q) => answers.TryGetValue(q.Id, out var a) ? a.RubricScore ?? 0 : 0;
        var repeatScores = questions.Where(q => q.TaskType == TOEFLTaskType.ListenRepeat).Select(ScoreOf).ToList();
        var interviewScores = questions.Where(q => q.TaskType == TOEFLTaskType.Interview).Select(ScoreOf).ToList();
        var raw = repeatScores.Sum() + interviewScores.Sum();
        var maximum = questions.Count * 5;
        var band = HalfBand(1 + (double)raw / Math.Max(1, maximum) * 5);
        var detail = $"Raw {raw}/{maximum} · Listen and Repeat {repeatScores.Sum()}/{repeatScores.Count * 5} · Interview {interviewScores.Sum()}/{interviewScores.Count * 5} (on-device speech recognition + official rubrics).";
        return (band, detail, raw);
    }

    /// <summary>The average of the section bands, rounded to the nearest half band.</summary>
    public static double Overall(IReadOnlyList<double> bands) => bands.Count == 0 ? 1 : HalfBand(bands.Sum() / bands.Count);

    /// <summary>Midpoint of the ETS comparable 0–120 range for each band (score scale update, 2026).</summary>
    public static int Comparable120(double band) => HalfBand(band) switch
    {
        6 => 117,
        5.5 => 110,
        5 => 101,
        4.5 => 90,
        4 => 79,
        3.5 => 65,
        3 => 51,
        2.5 => 39,
        2 => 29,
        1.5 => 18,
        _ => 6,
    };

    public static string Cefr(double band) => HalfBand(band) switch
    {
        6 => "C2",
        5 or 5.5 => "C1",
        4 or 4.5 => "B2",
        3 or 3.5 => "B1",
        2 or 2.5 => "A2",
        _ => "A1",
    };

    /// <summary>Rounds to the nearest half band (halves away from zero) and clamps to 1–6.</summary>
    public static double HalfBand(double value) => Math.Min(6, Math.Max(1, TextUtil.SwiftRound(value * 2) / 2));
}

/// <summary>Swift <c>TOEFLAnswerMatcher</c>: keyed scoring for choices, Complete the Words, and Build a Sentence.</summary>
public static class TOEFLAnswerMatcher
{
    /// <summary>True/false for keyed items; null when the item has no key (constructed responses).</summary>
    public static bool? IsCorrect(TOEFLQuestion question, TOEFLAnswer answer)
    {
        if (question.CorrectChoiceID is { } correctChoiceID) return answer.SelectedChoiceID == correctChoiceID;
        if (question.AcceptedTextAnswers.Count > 0)
        {
            var candidate = Normalize(answer.TextEntry);
            return question.AcceptedTextAnswers.Any(accepted => Normalize(accepted) == candidate);
        }
        if (question.CorrectSentenceTokens.Count > 0)
            return answer.OrderedTokens.Select(Normalize).SequenceEqual(question.CorrectSentenceTokens.Select(Normalize));
        return null;
    }

    private static string Normalize(string value) => TextUtil.TrimWhitespaceAndPunctuation(value).ToLowerInvariant();
}
