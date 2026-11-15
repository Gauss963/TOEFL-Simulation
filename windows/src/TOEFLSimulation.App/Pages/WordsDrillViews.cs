using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TOEFLSimulation.App.Controls.Guide;
using TOEFLSimulation.Core;
using Windows.UI.Text;
using static TOEFLSimulation.App.Controls.Guide.GuideTheme;

namespace TOEFLSimulation.App.Pages;

/// <summary>
/// CheckedParagraphView: a checked paragraph. Each blank shows the letters typed, marked right or wrong, with the correct
/// letters beside wrong answers. Without results, the answers are shown.
/// </summary>
public sealed partial class CheckedParagraphView : UserControl
{
    public CheckedParagraphView(WordsDrillParagraph paragraph, IReadOnlyList<string>? entries, IReadOnlyList<bool>? results)
    {
        var flow = new GuideFlowPanel(6, 12);
        foreach (var piece in new CTestParagraph(paragraph.Passage).Pieces)
        {
            switch (piece)
            {
                case CTestParagraph.TextPiece text:
                    flow.Children.Add(Text(text.Text, Title3, wrap: false));
                    break;
                case CTestParagraph.BlankPiece blank:
                    var group = HStack(0, Text(blank.Prefix, Title3, wrap: false), Blank(paragraph, blank.Index, entries, results));
                    if (blank.Suffix.Length > 0) group.Children.Add(Text(blank.Suffix, Title3, wrap: false));
                    foreach (var child in group.Children.OfType<FrameworkElement>()) child.VerticalAlignment = VerticalAlignment.Center;
                    flow.Children.Add(group);
                    break;
            }
        }
        Content = flow;
    }

    private static Border Blank(WordsDrillParagraph paragraph, int index, IReadOnlyList<string>? entries, IReadOnlyList<bool>? results)
    {
        var answer = index < paragraph.Blanks.Count ? paragraph.Blanks[index].AcceptedTextAnswers.FirstOrDefault() ?? "" : "";
        var typed = entries is not null && index < entries.Count ? entries[index] : "";
        bool? isCorrect = results is not null && index < results.Count ? results[index] : null;
        switch (isCorrect)
        {
            case false:
                var row = HStack(4);
                if (typed.Length > 0)
                {
                    var wrong = Text(typed, Title3, style: "GuideCriticalText", wrap: false, font: Monospaced);
                    wrong.TextDecorations = TextDecorations.Strikethrough;
                    row.Children.Add(wrong);
                }
                row.Children.Add(Text(answer, Title3, FontWeights.Bold, "GuideTealText", wrap: false, font: Monospaced));
                return new Border { Style = Style("GuideWrongBox"), Child = row };
            case true:
                return new Border { Style = Style("GuideCorrectBox"), Child = Text(typed, Title3, FontWeights.Bold, "GuideTealText", wrap: false, font: Monospaced) };
            default:
                return new Border { Style = Style("GuideAnswerBox"), Child = Text(answer, Title3, FontWeights.Bold, "GuideNavyText", wrap: false, font: Monospaced) };
        }
    }
}

/// <summary>WordExplanationsView: the score and, for every blank, the word, its part of speech, its meaning, and why it fits.</summary>
public sealed partial class WordExplanationsView : UserControl
{
    public WordExplanationsView(WordsDrillParagraph paragraph, IReadOnlyList<string>? entries, IReadOnlyList<bool>? results, int correct)
    {
        var stack = new StackPanel { Spacing = 14 };
        var score = Text(WordExplanation.ScoreTitle(correct), 28, FontWeights.Bold, TierStyle(WordsDrillSession.ScoreTier(correct)), wrap: false);
        var note = Text(WordExplanation.ScoreNote(correct), Subheadline, style: "GuideSecondaryText");
        note.HorizontalAlignment = HorizontalAlignment.Right;
        note.TextAlignment = TextAlignment.Right;
        stack.Children.Add(SpacedRow(score, note, alignment: VerticalAlignment.Bottom));
        var rows = WordExplanation.For(paragraph, entries, results);
        for (var i = 0; i < rows.Count; i++)
        {
            stack.Children.Add(Row(rows[i]));
            if (i < rows.Count - 1) stack.Children.Add(Divider());
        }
        Content = stack;
    }

    private static Grid Row(WordExplanation row)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var icon = row.IsCorrect switch
        {
            null => Icon(GuideIcons.Circle, Title3, "GuideSecondaryIcon"),
            true => Icon(GuideIcons.CheckmarkCircleFill, Title3, "GuideTealIcon"),
            false => Icon(GuideIcons.XmarkCircleFill, Title3, "GuideCriticalIcon"),
        };
        icon.VerticalAlignment = VerticalAlignment.Top;
        icon.Margin = new Thickness(0, 4, 0, 0);
        grid.Children.Add(icon);

        var line = new GuideFlowPanel(8, 4);
        line.Children.Add(Text($"{row.Number}.", Body, style: "GuideSecondaryText", wrap: false));
        line.Children.Add(new SpanText(new[] { new TextSpan(row.Kept), new TextSpan(row.Missing, GuideColor.Teal, Bold: true) }, Title3, Monospaced));
        if (row.Blank.PartOfSpeech is { } pos) line.Children.Add(Text(pos, Subheadline, style: "GuideSecondaryText", wrap: false, italic: true));
        if (row.Blank.WordGloss is { } gloss) line.Children.Add(Text(gloss, Subheadline, FontWeights.SemiBold));
        if (row.TypedNote is { } typed) line.Children.Add(Text(typed, Caption, style: "GuideCriticalText", wrap: false));
        var body = VStack(5, line);
        if (row.Blank.BlankReason is { } reason) body.Children.Add(Text(reason, Subheadline, style: "GuideSecondaryText"));
        Grid.SetColumn(body, 1);
        grid.Children.Add(body);
        return grid;
    }
}

/// <summary>WordsDrillReviewView: a past attempt, shown as it was answered, with the explanations (in a dialog).</summary>
public static class WordsDrillReview
{
    /// <summary>Shows the attempt; "Practice Again" calls <paramref name="practiceAgain"/> with its paragraph.</summary>
    public static async Task ShowAsync(XamlRoot root, WordsDrillStore store, WordsDrillStore.AttemptRecord record, Action<WordsDrillParagraph> practiceAgain)
    {
        var paragraph = store.Paragraph(record.ParagraphID);
        var header = VStack(3,
            Text(paragraph?.Title ?? "Paragraph", Title2, FontWeights.Bold),
            Text($"{paragraph?.Difficulty.Title() ?? ""} · {ShortDateTime(record.Date)}", Subheadline, style: "GuideSecondaryText"));
        UIElement details;
        if (paragraph is not null)
        {
            details = VStack(20,
                Card(new CheckedParagraphView(paragraph, record.Entries, record.Results)),
                Card(new WordExplanationsView(paragraph, record.Entries, record.Results, record.Correct)));
        }
        else
        {
            var unavailable = VStack(8,
                Icon(GuideIcons.Document, 40, "GuideSecondaryIcon"),
                Text("Paragraph unavailable", Title2, FontWeights.Bold),
                Text("This paragraph is no longer in the question bank.", Body, style: "GuideSecondaryText"));
            unavailable.HorizontalAlignment = HorizontalAlignment.Center;
            unavailable.Padding = new Thickness(40);
            foreach (var child in unavailable.Children.OfType<FrameworkElement>()) child.HorizontalAlignment = HorizontalAlignment.Center;
            details = unavailable;
        }
        var result = await ReviewDialog.ShowAsync(root, header, details, paragraph is not null ? "Practice Again" : null);
        if (result == ContentDialogResult.Primary && paragraph is not null) practiceAgain(paragraph);
    }
}

/// <summary>The Mac review sheet as a large ContentDialog: a header, scrolling details, "Practice Again", and "Done" (Esc).</summary>
public static class ReviewDialog
{
    private static bool isOpen;

    public static async Task<ContentDialogResult> ShowAsync(XamlRoot root, UIElement header, UIElement details, string? primaryButton)
    {
        if (isOpen) return ContentDialogResult.None;
        isOpen = true;
        try
        {
            var width = Math.Max(480, Math.Min(920, root.Size.Width - 96));
            if (details is FrameworkElement element) element.Width = width;
            var dialog = new ContentDialog
            {
                XamlRoot = root,
                Style = AppStyle("DefaultContentDialogStyle"),
                Title = header,
                Content = details,
                CloseButtonText = "Done",
                DefaultButton = ContentDialogButton.Close,
            };
            if (primaryButton is not null) dialog.PrimaryButtonText = primaryButton;
            dialog.Resources["ContentDialogMaxWidth"] = width + 48;
            dialog.Resources["ContentDialogMaxHeight"] = Math.Max(480, root.Size.Height - 48);
            return await dialog.ShowAsync();
        }
        finally
        {
            isOpen = false;
        }
    }
}
