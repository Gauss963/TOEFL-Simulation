using System.ComponentModel;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using TOEFLSimulation.App.Controls.Guide;
using TOEFLSimulation.App.Services;
using TOEFLSimulation.Core;
using Windows.System;
using Windows.UI.Core;
using static TOEFLSimulation.App.Controls.Guide.GuideTheme;

namespace TOEFLSimulation.App.Pages;

/// <summary>Complete the Words practice with immediate feedback after each paragraph (WordsDrillView).</summary>
public sealed partial class WordsDrillPage : Page
{
    private WordsDrillSession? session;
    private readonly StackPanel body = new() { Spacing = 22 };
    private readonly List<TextBox> blanks = [];
    private SelectorBar? levelBar;
    private ComboBox? subjectBox;
    private TextBlock? practicedText, accuracyText, attemptsText, reviewCountText;
    private StackPanel? fieldAccuracyPanel;
    private Button? checkButton;
    private Border? historyCard;
    private bool updatingFilters;

    public WordsDrillPage()
    {
        InitializeComponent();
        // ⌘Return on the Mac: Check Answers, then Next Paragraph.
        var accelerator = new KeyboardAccelerator { Key = VirtualKey.Enter, Modifiers = VirtualKeyModifiers.Control };
        accelerator.Invoked += (_, args) =>
        {
            args.Handled = true;
            if (session?.Paragraph is null) return;
            if (session.Results is null) session.Check();
            else session.Next();
        };
        KeyboardAccelerators.Add(accelerator);
        KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Root.Children.Clear();
        Root.Children.Add(new ProgressRing { IsActive = true, HorizontalAlignment = HorizontalAlignment.Left });
        try
        {
            await AppServices.Ready;
        }
        catch (Exception exception)
        {
            CrashLog.Write(exception);
            return;
        }
        session = AppServices.Core.CreateWordsDrillSession();
        session.Appear();
        session.PropertyChanged += OnSessionChanged;
        Root.Children.Clear();
        Root.Children.Add(Header());
        Root.Children.Add(body);
        Render();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        if (session is not null) session.PropertyChanged -= OnSessionChanged;
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(WordsDrillSession.ShowsAllHistory):
                // "Show all" / "Show recent" only changes the history list.
                if (historyCard is not null && body.Children.IndexOf(historyCard) is var index and >= 0)
                {
                    historyCard = HistoryList();
                    body.Children[index] = historyCard;
                }
                break;
            case "" or null:
                Render();
                break;
            // Typing updates the entries only (the text boxes already show them); a filter change is followed by a
            // full reload ("").
        }
    }

    // MARK: - Header

    private UIElement Header()
    {
        var s = session!;
        levelBar = new SelectorBar();
        foreach (var level in new[] { WordsDrillLevel.All, WordsDrillLevel.Intermediate, WordsDrillLevel.Advanced })
            levelBar.Items.Add(new SelectorBarItem { Text = WordsDrillSession.LevelTitle(level), Tag = level, IsSelected = level == s.Level });
        levelBar.SelectionChanged += (sender, _) =>
        {
            if (!updatingFilters && sender.SelectedItem?.Tag is WordsDrillLevel level) s.Level = level;
        };

        subjectBox = new ComboBox { Width = 320 };
        subjectBox.Items.Add(new ComboBoxItem { Content = "All subjects", Tag = WordsDrillSession.AllSubjects });
        subjectBox.Items.Add(new ComboBoxItem { Content = "Focus on weak areas", Tag = WordsDrillSession.WeakSubjects });
        foreach (var field in WordsDrillFields.AllCases)
            subjectBox.Items.Add(new ComboBoxItem { Content = field.Title(), Tag = field.RawValue() });
        subjectBox.SelectedItem = subjectBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == s.Subject);
        subjectBox.SelectionChanged += (_, _) =>
        {
            if (!updatingFilters && subjectBox.SelectedItem is ComboBoxItem { Tag: string subject }) s.Subject = subject;
        };

        var titleBlock = VStack(6,
            Text("Complete the Words practice", PageTitle, FontWeights.Bold, "GuideNavyText"),
            Text("Type the missing letters, then check the paragraph to see which blanks are right, what each word means, and why it fits.",
                style: "GuideSecondaryText"));
        var filters = VStack(8,
            levelBar,
            HStack(8, Text("Subject", Body, style: "GuideSecondaryText", wrap: false), subjectBox));
        filters.HorizontalAlignment = HorizontalAlignment.Right;
        foreach (var child in filters.Children.OfType<FrameworkElement>()) child.HorizontalAlignment = HorizontalAlignment.Right;
        ((StackPanel)filters.Children[1]).Children[0].SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);

        var stats = HStack(22,
            Stat("Paragraphs practiced", out practicedText),
            Stat("Recent accuracy", out accuracyText),
            Stat("Attempts", out attemptsText),
            Stat("Words to review", out reviewCountText));
        fieldAccuracyPanel = new StackPanel { Spacing = 6 };
        return Card(VStack(14, SpacedRow(titleBlock, filters, 16, VerticalAlignment.Top), stats, fieldAccuracyPanel));
    }

    private static StackPanel Stat(string title, out TextBlock value)
    {
        value = Text("", Title2, FontWeights.Bold, wrap: false);
        return VStack(2, Secondary(title), value);
    }

    private void UpdateHeader()
    {
        var s = session!;
        var store = s.Store;
        updatingFilters = true;
        if (levelBar is not null)
            foreach (var item in levelBar.Items) item.IsSelected = item.Tag is WordsDrillLevel level && level == s.Level;
        if (subjectBox is not null)
            subjectBox.SelectedItem = subjectBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == s.Subject);
        updatingFilters = false;

        practicedText!.Text = s.ParagraphsPracticedText;
        accuracyText!.Text = s.RecentAccuracyText;
        attemptsText!.Text = store.History.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        reviewCountText!.Text = store.WordsToReview.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // Accuracy per subject area, weakest first, so weak fields are visible at a glance.
        fieldAccuracyPanel!.Children.Clear();
        var fields = store.AccuracyByField.OrderBy(f => f.Accuracy).ToList();
        fieldAccuracyPanel.Visibility = fields.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (fields.Count == 0) return;
        var weak = store.WeakFields.ToHashSet();
        fieldAccuracyPanel.Children.Add(Secondary("Accuracy by subject (latest attempt of each paragraph)"));
        var chips = new GuideFlowPanel(8, 8);
        foreach (var entry in fields)
        {
            var row = HStack(6,
                Text(entry.Field.Title(), Caption, FontWeights.SemiBold, wrap: false),
                Text(TextUtil.Percent(entry.Accuracy), Caption, FontWeights.Bold, TierStyle(WordsDrillSession.AccuracyTier(entry.Accuracy)), wrap: false),
                Text($"· {entry.Paragraphs}", Caption2, style: "GuideSecondaryText", wrap: false));
            chips.Children.Add(new Border { Style = Style(weak.Contains(entry.Field) ? "GuideWeakChip" : "GuideChip"), Child = row });
        }
        fieldAccuracyPanel.Children.Add(chips);
        if (s.WeakAreasText is { } weakText) fieldAccuracyPanel.Children.Add(Secondary(weakText));
    }

    // MARK: - Body

    private void Render()
    {
        if (session is null) return;
        UpdateHeader();
        body.Children.Clear();
        blanks.Clear();
        checkButton = null;
        if (session.Paragraph is { } paragraph)
        {
            body.Children.Add(Exercise(paragraph));
            if (session.Results is { } results)
                body.Children.Add(Card(new WordExplanationsView(paragraph, session.Entries, results, results.Count(r => r))));
        }
        historyCard = session.Store.History.Count > 0 ? HistoryList() : null;
        if (historyCard is not null) body.Children.Add(historyCard);
        if (session.Store.WordsToReview.Count > 0) body.Children.Add(ReviewList());
        if (blanks.Count > 0)
        {
            var first = blanks[0];
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => first.Focus(FocusState.Programmatic));
        }
    }

    private Border Exercise(WordsDrillParagraph paragraph)
    {
        var s = session!;
        var chip = new Border { Style = Style("GuideAquaChip"), Child = Text(paragraph.Difficulty.Title(), Caption, FontWeights.Bold, wrap: false) };
        var titleRow = HStack(8, Text(paragraph.Title, Headline, FontWeights.SemiBold), chip);
        foreach (var child in titleRow.Children.OfType<FrameworkElement>()) child.VerticalAlignment = VerticalAlignment.Center;
        var stack = new StackPanel { Spacing = 16 };
        stack.Children.Add(SpacedRow(titleRow, s.AttemptSummary is { } summary ? Secondary(summary) : null));
        stack.Children.Add(Text("Fill in the missing letters in the paragraph.", Subheadline, style: "GuideSecondaryText"));

        if (s.Results is { } results)
        {
            stack.Children.Add(new CheckedParagraphView(paragraph, s.Entries, results));
        }
        else
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
                        var group = HStack(0, Text(blank.Prefix, Title3, wrap: false), Input(blank));
                        if (blank.Suffix.Length > 0) group.Children.Add(Text(blank.Suffix, Title3, wrap: false));
                        foreach (var child in group.Children.OfType<FrameworkElement>()) child.VerticalAlignment = VerticalAlignment.Center;
                        flow.Children.Add(group);
                        break;
                }
            }
            stack.Children.Add(flow);
        }

        var buttons = HStack(12);
        if (s.Results is null)
        {
            checkButton = new Button { Content = "Check Answers", Style = AppStyle("AccentButtonStyle") };
            checkButton.Click += (_, _) => s.Check();
            var skip = new Button { Content = "Skip" };
            skip.Click += (_, _) => s.Next();
            buttons.Children.Add(checkButton);
            buttons.Children.Add(skip);
        }
        else
        {
            var next = new Button { Content = "Next Paragraph", Style = AppStyle("AccentButtonStyle") };
            next.Click += (_, _) => s.Next();
            var again = new Button { Content = "Try Again" };
            again.Click += (_, _) => s.TryAgain();
            buttons.Children.Add(next);
            buttons.Children.Add(again);
        }
        var hint = Secondary(s.Results is null ? "Enter moves to the next blank · Ctrl+Enter checks" : "Ctrl+Enter for the next paragraph");
        stack.Children.Add(SpacedRow(buttons, hint));
        return Card(stack);
    }

    private TextBox Input(CTestParagraph.BlankPiece blank)
    {
        var s = session!;
        var index = blank.Index;
        var missing = blank.Missing;
        var box = new TextBox
        {
            Text = index < s.Entries.Count ? s.Entries[index] : "",
            PlaceholderText = ExamText.BlankPlaceholder(missing),
            FontFamily = Monospaced,
            FontSize = Title3,
            Width = ExamText.BlankFieldWidth(missing) + 12,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(4, 1, 4, 1),
            IsSpellCheckEnabled = false,
            IsTextPredictionEnabled = false,
            AcceptsReturn = false,
        };
        AutomationProperties.SetName(box, $"Blank {index + 1}: {blank.Prefix}, {missing} missing letters");
        box.TextChanged += (_, _) =>
        {
            if (s.Results is not null) return;
            s.SetEntry(index, box.Text, missing);
            var clean = index < s.Entries.Count ? s.Entries[index] : "";
            if (box.Text != clean)
            {
                box.Text = clean;
                box.SelectionStart = clean.Length;
            }
        };
        // Return moves to the next blank (Ctrl+Return is the page's Check / Next accelerator).
        box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != VirtualKey.Enter) return;
            if (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down)) return;
            e.Handled = true;
            var position = blanks.IndexOf(box);
            if (position >= 0 && position + 1 < blanks.Count) blanks[position + 1].Focus(FocusState.Keyboard);
            else checkButton?.Focus(FocusState.Keyboard);
        };
        box.Loaded += (_, _) => HideClearButton(box);
        blanks.Add(box);
        return box;
    }

    /// <summary>The blanks are only a few letters wide; the TextBox's clear button would cover them.</summary>
    private static void HideClearButton(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement { Name: "DeleteButton" } button)
            {
                button.MaxWidth = 0;
                button.Margin = new Thickness(0);
                return;
            }
            HideClearButton(child);
        }
    }

    // MARK: - History and review

    private Border HistoryList()
    {
        var s = session!;
        var store = s.Store;
        var stack = new StackPanel { Spacing = 10 };
        HyperlinkButton? toggle = null;
        if (store.History.Count > 8)
        {
            toggle = new HyperlinkButton { Content = s.ShowsAllHistory ? "Show recent" : $"Show all {store.History.Count}" };
            toggle.Click += (_, _) => s.ShowsAllHistory = !s.ShowsAllHistory;
        }
        stack.Children.Add(SpacedRow(Text("Answer history", Headline, FontWeights.SemiBold), toggle));
        stack.Children.Add(Secondary("Open an attempt to see your answers and the explanation for every word."));
        foreach (var record in s.VisibleHistory)
        {
            var paragraph = store.Paragraph(record.ParagraphID);
            var grid = new Grid { ColumnSpacing = 12 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var score = Text($"{record.Correct}/10", Headline, FontWeights.SemiBold, TierStyle(WordsDrillSession.ScoreTier(record.Correct)), wrap: false);
            score.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(score);
            var info = VStack(2,
                Text(paragraph?.Title ?? "Paragraph", Subheadline, FontWeights.SemiBold),
                Secondary($"{paragraph?.Difficulty.Title() ?? ""} · {ShortDateTime(record.Date)}"));
            Grid.SetColumn(info, 1);
            grid.Children.Add(info);
            var chevron = Icon(GuideIcons.ChevronRight, Caption, "GuideSecondaryIcon");
            chevron.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(chevron, 2);
            grid.Children.Add(chevron);
            stack.Children.Add(RowButton(grid, () => _ = ShowReview(record)));
        }
        return Card(stack);
    }

    private Border ReviewList()
    {
        var store = session!.Store;
        var words = store.WordsToReview;
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(Text($"Words to review ({words.Count})", Headline, FontWeights.SemiBold));
        stack.Children.Add(Secondary("All the words you missed, most frequent first. Select one to open the attempt where you missed it. A word leaves the list after you get it right twice."));
        var grid = new GuideGridPanel(220, 10);
        foreach (var entry in words)
        {
            var miss = store.LatestMiss(entry.Word);
            var title = HStack(6, Text(entry.Word, Headline, FontWeights.SemiBold, wrap: false, font: Monospaced));
            if (entry.PartOfSpeech is { } pos)
            {
                var posText = Text(pos, Caption, style: "GuideSecondaryText", wrap: false, italic: true);
                posText.VerticalAlignment = VerticalAlignment.Bottom;
                title.Children.Add(posText);
            }
            var content = VStack(2, title);
            if (entry.Gloss is { } gloss) content.Children.Add(Text(gloss, Caption));
            var button = RowButton(content, () => { if (miss is not null) _ = ShowReview(miss); });
            button.IsEnabled = miss is not null;
            button.VerticalAlignment = VerticalAlignment.Stretch;
            button.VerticalContentAlignment = VerticalAlignment.Top;
            grid.Children.Add(button);
        }
        stack.Children.Add(grid);
        return Card(stack);
    }

    private static Button RowButton(UIElement content, Action click)
    {
        var button = new Button
        {
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10),
        };
        button.Click += (_, _) => click();
        return button;
    }

    private async Task ShowReview(WordsDrillStore.AttemptRecord record)
    {
        if (session is not { } s || XamlRoot is null) return;
        await WordsDrillReview.ShowAsync(XamlRoot, s.Store, record, paragraph => s.Load(paragraph));
    }
}
