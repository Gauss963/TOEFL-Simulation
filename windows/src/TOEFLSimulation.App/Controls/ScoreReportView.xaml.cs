using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TOEFLSimulation.Core;
using Windows.Foundation;

namespace TOEFLSimulation.App.Controls;

/// <summary>
/// The score report (Mac <c>TOEFLScoreReportView</c>): overall band ring, CEFR and comparable 0–120 score, section cards
/// with raw scores, routes and rubric detail, the methodology note, and the per-question answer review.
/// Shown after a practice test ("Save &amp; close") and from Score History ("Close").
/// </summary>
public sealed partial class ScoreReportView : UserControl
{
    private readonly UiKit ui;
    private readonly Dictionary<TOEFLSectionKind, UIElement> rubricDetails = [];
    private string heading = ScoreReportText.CompletedHeading;
    private string doneTitle = ScoreReportText.CompletedDoneTitle;

    public ScoreReportView(TOEFLExamResult result)
    {
        Result = result;
        InitializeComponent();
        ui = new UiKit(Resources);
        HeadingText.Text = heading;
        DoneButton.Content = doneTitle;
        Populate();
    }

    /// <summary>A report with a custom heading and button title (Score History uses
    /// <see cref="ScoreReportText.SavedHeading"/> and <see cref="ScoreReportText.SavedDoneTitle"/>).</summary>
    public ScoreReportView(TOEFLExamResult result, string heading, string doneTitle) : this(result)
    {
        Heading = heading;
        DoneTitle = doneTitle;
    }

    /// <summary>Raised by the Done / Close button.</summary>
    public event EventHandler? CloseRequested;

    public TOEFLExamResult Result { get; }

    /// <summary>Header title; defaults to "Practice score report".</summary>
    public string Heading
    {
        get => heading;
        set { heading = value; HeadingText.Text = value; }
    }

    /// <summary>Done button title; defaults to "Save &amp; close".</summary>
    public string DoneTitle
    {
        get => doneTitle;
        set { doneTitle = value; DoneButton.Content = value; }
    }

    private void OnDoneClick(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void Populate()
    {
        var result = Result;
        ModeText.Text = result.Mode.Title();
        OverallBandText.Text = ScoreReportText.Band(result.OverallBand);
        CefrText.Text = ScoreReportText.CefrLine(result);
        OverallExplanationText.Text = ScoreReportText.OverallExplanation;
        CompletedText.Text = $"Completed {UiKit.AbbreviatedDateTime(result.CompletedAt)} · {ScoreReportText.Duration(result.ElapsedSeconds)}";
        MethodologyTitleText.Text = ScoreReportText.MethodologyTitle;
        MethodologyText.Text = ScoreReportText.Methodology;
        DisclaimerText.Text = ScoreReportText.Disclaimer;
        DrawScoreArc(result.OverallBand / 6);
        BuildSectionCards();
        BuildReviewGroups();
    }

    /// <summary>The ring's progress arc, clockwise from 12 o'clock (Circle().trim(from: 0, to: band / 6)).</summary>
    private void DrawScoreArc(double fraction)
    {
        fraction = Math.Clamp(double.IsNaN(fraction) ? 0 : fraction, 0, 1);
        if (fraction <= 0)
        {
            ScoreArc.Visibility = Visibility.Collapsed;
            return;
        }
        const double center = 75, radius = 68;
        var angle = Math.Min(fraction, 0.9999) * 2 * Math.PI;
        var start = new Point(center, center - radius);
        var end = new Point(center + radius * Math.Sin(angle), center - radius * Math.Cos(angle));
        var figure = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };
        figure.Segments.Add(new ArcSegment
        {
            Point = end,
            Size = new Size(radius, radius),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = angle > Math.PI,
        });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        ScoreArc.Data = geometry;
    }

    private void BuildSectionCards()
    {
        SectionGrid.Children.Clear();
        rubricDetails.Clear();
        foreach (var section in Result.Sections)
        {
            var content = new StackPanel { Spacing = 10 };

            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var title = ui.Label(Glyphs.Section(section.Kind), section.Kind.Title(), "TextPrimary", "IconPrimary", 16, UiKit.SemiBold);
            title.VerticalAlignment = VerticalAlignment.Center;
            header.Children.Add(title);
            var band = ui.Text(ScoreReportText.Band(section.Band), "TextNavy", 22, UiKit.Bold);
            band.TextWrapping = TextWrapping.NoWrap;
            Grid.SetColumn(band, 1);
            header.Children.Add(band);
            content.Children.Add(header);

            content.Children.Add(new ProgressBar { Minimum = 0, Maximum = 6, Value = section.Band });
            content.Children.Add(ui.Text(ScoreReportText.RawLine(section), "TextSecondary", 12));
            if (section.Route is { } route)
                content.Children.Add(ui.Text(route.Title(), "TextTeal", 12, UiKit.Bold));

            var detail = new StackPanel { Spacing = 10, Visibility = Visibility.Collapsed };
            detail.Children.Add(new Border { Style = ui.GetStyle("DividerStyle") });
            detail.Children.Add(ui.Text(section.RubricDetail, "TextSecondary", 13));
            content.Children.Add(detail);
            rubricDetails[section.Kind] = detail;

            var card = new Button { Style = ui.GetStyle("CardButtonStyle"), Content = content, MinHeight = 120 };
            AutomationProperties_SetName(card, $"{section.Kind.Title()} band {ScoreReportText.Band(section.Band)}. {ScoreReportText.RawLine(section)}");
            var kind = section.Kind;
            card.Click += (_, _) =>
            {
                // Like the Mac app, only one section's rubric detail is open at a time.
                var opening = rubricDetails[kind].Visibility != Visibility.Visible;
                foreach (var (other, panel) in rubricDetails)
                    panel.Visibility = opening && other == kind ? Visibility.Visible : Visibility.Collapsed;
            };
            SectionGrid.Children.Add(card);
        }
    }

    private static void AutomationProperties_SetName(DependencyObject element, string name) =>
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(element, name);

    private void BuildReviewGroups()
    {
        ReviewGroupsPanel.Children.Clear();
        foreach (var group in ScoreReportText.ReviewGroups(Result))
        {
            var records = group.Records;
            var body = new StackPanel { Spacing = 12, Padding = new Thickness(0, 4, 0, 4) };
            var expander = new Expander
            {
                Header = ui.Label(Glyphs.Section(group.Kind), group.Title, "TextPrimary", "IconPrimary", 15, UiKit.SemiBold),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Content = body,
            };
            // Building every review card up front is slow for a full test, so each section fills in when first opened.
            var built = false;
            expander.Expanding += (_, _) =>
            {
                if (built) return;
                built = true;
                foreach (var record in records) body.Children.Add(ReviewCard(record));
            };
            ReviewGroupsPanel.Children.Add(expander);
        }
    }

    private Border ReviewCard(TOEFLQuestionReview record)
    {
        var panel = new StackPanel { Spacing = 8 };

        var top = new Grid { ColumnSpacing = 12 };
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleColumn = new StackPanel { Spacing = 3 };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        titleRow.Children.Add(ui.Text(ScoreReportText.ReviewTitle(record), "TextPrimary", 14, UiKit.SemiBold));
        if (!record.Question.CountsTowardScore)
        {
            var pill = new Border { Style = ui.GetStyle("PillStyle"), VerticalAlignment = VerticalAlignment.Center };
            pill.Child = ui.Text("Unscored", "TextSecondary", 11, UiKit.Bold);
            titleRow.Children.Add(pill);
        }
        titleColumn.Children.Add(titleRow);
        titleColumn.Children.Add(ui.Text(record.Question.Prompt, "TextSecondary", 14));
        top.Children.Add(titleColumn);

        var badge = StatusBadge(record.IsCorrect);
        badge.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(badge, 1);
        top.Children.Add(badge);
        panel.Children.Add(top);

        if (ScoreReportText.TranscriptLine(record) is { } transcript)
            panel.Children.Add(ui.Text(transcript, "TextSecondary", 13));
        panel.Children.Add(ui.Text($"Your answer: {ScoreReportText.AnswerText(record)}", "TextPrimary", 13));
        if (ScoreReportText.RubricLine(record) is { } rubric)
            panel.Children.Add(ui.Text(rubric, "TextBlue", 13, UiKit.SemiBold));
        if (ScoreReportText.SampleLine(record) is { } sample)
            panel.Children.Add(ui.Text(sample, "TextTeal", 13));
        panel.Children.Add(ui.Text(record.Question.Explanation, "TextSecondary", 12));
        panel.Children.Add(ui.Text(record.Question.SourceNote, "TextTertiary", 11));

        foreach (var text in panel.Children.OfType<TextBlock>()) text.IsTextSelectionEnabled = true;
        return new Border { Style = ui.GetStyle("InsetStyle"), Child = panel };
    }

    private StackPanel StatusBadge(bool? isCorrect)
    {
        var (glyph, style) = isCorrect switch
        {
            true => (Glyphs.CheckCircle, "Teal"),
            false => (Glyphs.ErrorCircle, "Critical"),
            null => (Glyphs.Checklist, "Blue"),
        };
        var label = ui.Label(glyph, ScoreReportText.StatusBadge(isCorrect), "Text" + style, "Icon" + style, 12, UiKit.Bold);
        foreach (var text in label.Children.OfType<TextBlock>()) text.TextWrapping = TextWrapping.NoWrap;
        return label;
    }
}
