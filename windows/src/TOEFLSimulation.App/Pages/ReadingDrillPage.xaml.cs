using System.ComponentModel;
using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using TOEFLSimulation.App.Controls.Guide;
using TOEFLSimulation.App.Services;
using TOEFLSimulation.Core;
using Windows.System;
using static TOEFLSimulation.App.Controls.Guide.GuideTheme;

namespace TOEFLSimulation.App.Pages;

/// <summary>Reading practice with immediate feedback, accuracy by question type, and an answer history (ReadingDrillView).</summary>
public sealed partial class ReadingDrillPage : Page
{
    private ReadingDrillSession? session;
    private readonly StackPanel body = new() { Spacing = 22 };
    private SelectorBar? taskBar;
    private ComboBox? levelBox;
    private ToggleSwitch? focusSwitch;
    private TextBlock? practicedText, accuracyText, attemptsText;
    private StackPanel? typeAccuracyPanel;
    private ReadingSetView? setView;
    private TextBlock? answeredText;
    private Border? historyCard;
    private bool updatingFilters;

    public ReadingDrillPage()
    {
        InitializeComponent();
        // ⌘Return on the Mac: Check Answers, then Next Set.
        var accelerator = new KeyboardAccelerator { Key = VirtualKey.Enter, Modifiers = VirtualKeyModifiers.Control };
        accelerator.Invoked += (_, args) =>
        {
            args.Handled = true;
            if (session?.Current is null) return;
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
        session = AppServices.Core.CreateReadingDrillSession();
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
            case nameof(ReadingDrillSession.Choices):
                // Picking an option recolors that question and updates "n of m answered".
                if (session is { Current: { } set } s)
                {
                    for (var i = 0; i < set.Questions.Count; i++) setView?.UpdateOptions(i, i < s.Choices.Count ? s.Choices[i] : null);
                    if (answeredText is not null) answeredText.Text = s.AnsweredText;
                }
                break;
            case nameof(ReadingDrillSession.ShowsAllHistory):
                if (historyCard is not null && body.Children.IndexOf(historyCard) is var index and >= 0)
                {
                    historyCard = HistoryList();
                    body.Children[index] = historyCard;
                }
                break;
            case "" or null:
                Render();
                break;
        }
    }

    // MARK: - Header

    private UIElement Header()
    {
        var s = session!;
        taskBar = new SelectorBar();
        foreach (var filter in new[] { ReadingTaskFilter.All, ReadingTaskFilter.Daily, ReadingTaskFilter.Academic })
            taskBar.Items.Add(new SelectorBarItem { Text = ReadingDrillSession.TaskFilterTitle(filter), Tag = filter, IsSelected = filter == s.TaskFilter });
        taskBar.SelectionChanged += (sender, _) =>
        {
            if (!updatingFilters && sender.SelectedItem?.Tag is ReadingTaskFilter filter) s.TaskFilter = filter;
        };

        levelBox = new ComboBox { Width = 200 };
        levelBox.Items.Add(new ComboBoxItem { Content = "All levels", Tag = ReadingDrillSession.AllLevels });
        levelBox.Items.Add(new ComboBoxItem { Content = "Intermediate", Tag = TOEFLDifficulty.Intermediate.RawValue() });
        levelBox.Items.Add(new ComboBoxItem { Content = "Advanced", Tag = TOEFLDifficulty.Advanced.RawValue() });
        levelBox.SelectedItem = levelBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == s.Level);
        levelBox.SelectionChanged += (_, _) =>
        {
            if (!updatingFilters && levelBox.SelectedItem is ComboBoxItem { Tag: string level }) s.Level = level;
        };

        focusSwitch = new ToggleSwitch { OnContent = "Focus on weak types", OffContent = "Focus on weak types", IsOn = s.FocusWeak, MinWidth = 0 };
        focusSwitch.Toggled += (_, _) =>
        {
            if (!updatingFilters) s.FocusWeak = focusSwitch.IsOn;
        };

        var levelLabel = Text("Level", Body, style: "GuideSecondaryText", wrap: false);
        levelLabel.VerticalAlignment = VerticalAlignment.Center;
        var levelRow = HStack(12, levelLabel, levelBox, focusSwitch);
        var filters = VStack(8, taskBar, levelRow);
        filters.HorizontalAlignment = HorizontalAlignment.Right;
        foreach (var child in filters.Children.OfType<FrameworkElement>()) child.HorizontalAlignment = HorizontalAlignment.Right;

        var titleBlock = VStack(6,
            Text("Reading practice", PageTitle, FontWeights.Bold, "GuideNavyText"),
            Text("Answer one passage at a time, then check it to see the correct answers, the explanations, and the question type of each item.",
                style: "GuideSecondaryText"));
        var stats = HStack(22,
            Stat("Sets practiced", out practicedText),
            Stat("Recent accuracy", out accuracyText),
            Stat("Attempts", out attemptsText));
        typeAccuracyPanel = new StackPanel { Spacing = 6 };
        return Card(VStack(14, SpacedRow(titleBlock, filters, 16, VerticalAlignment.Top), stats, typeAccuracyPanel));
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
        if (taskBar is not null)
            foreach (var item in taskBar.Items) item.IsSelected = item.Tag is ReadingTaskFilter filter && filter == s.TaskFilter;
        if (levelBox is not null)
            levelBox.SelectedItem = levelBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == s.Level);
        if (focusSwitch is not null) focusSwitch.IsOn = s.FocusWeak;
        updatingFilters = false;

        practicedText!.Text = s.SetsPracticedText;
        accuracyText!.Text = s.RecentAccuracyText;
        attemptsText!.Text = store.History.Count.ToString(CultureInfo.InvariantCulture);

        typeAccuracyPanel!.Children.Clear();
        var types = store.AccuracyByType.OrderBy(t => t.Accuracy).ToList();
        typeAccuracyPanel.Visibility = types.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (types.Count == 0) return;
        var weak = store.WeakTypes.ToHashSet();
        typeAccuracyPanel.Children.Add(Secondary("Accuracy by question type (latest attempt of each set)"));
        var chips = new GuideFlowPanel(8, 8);
        foreach (var entry in types)
        {
            var row = HStack(6,
                Text(entry.Type.Title(), Caption, FontWeights.SemiBold, wrap: false),
                Text(TextUtil.Percent(entry.Accuracy), Caption, FontWeights.Bold, TierStyle(WordsDrillSession.AccuracyTier(entry.Accuracy)), wrap: false),
                Text($"· {entry.Total}", Caption2, style: "GuideSecondaryText", wrap: false));
            chips.Children.Add(new Border { Style = Style(weak.Contains(entry.Type) ? "GuideWeakChip" : "GuideChip"), Child = row });
        }
        typeAccuracyPanel.Children.Add(chips);
        if (s.WeakestText is { } weakest)
            typeAccuracyPanel.Children.Add(GuideMistakes.IconRow(GuideIcons.Lightbulb, "GuideSecondaryIcon", Secondary(weakest), Caption));
    }

    // MARK: - Body

    private void Render()
    {
        if (session is not { } s) return;
        UpdateHeader();
        body.Children.Clear();
        setView = null;
        answeredText = null;
        if (s.Current is { } set)
        {
            var chip = new Border { Style = Style("GuideAquaChip"), Child = Text(set.Difficulty.Title(), Caption, FontWeights.Bold, wrap: false) };
            var titleRow = HStack(8, Text(s.TaskTitle ?? "", Headline, FontWeights.SemiBold, wrap: false), chip);
            foreach (var child in titleRow.Children.OfType<FrameworkElement>()) child.VerticalAlignment = VerticalAlignment.Center;
            body.Children.Add(SpacedRow(titleRow, s.LastTimeText is { } last ? Secondary(last) : null));
            setView = new ReadingSetView(set, s.Choices, s.Results, (index, choice) => s.Choose(index, choice));
            body.Children.Add(setView);
            body.Children.Add(Actions(s));
        }
        historyCard = s.Store.History.Count > 0 ? HistoryList() : null;
        if (historyCard is not null) body.Children.Add(historyCard);
    }

    private static Border Actions(ReadingDrillSession s)
    {
        UIElement leading;
        StackPanel buttons;
        if (s.Results is not null)
        {
            leading = Text(s.ScoreText ?? "", Title2, FontWeights.Bold, TierStyle(s.ScoreTier ?? "red"), wrap: false);
            var again = new Button { Content = "Try Again" };
            again.Click += (_, _) => s.TryAgain();
            var next = new Button { Content = "Next Set", Style = AppStyle("AccentButtonStyle") };
            next.Click += (_, _) => s.Next();
            buttons = HStack(12, again, next);
        }
        else
        {
            leading = Secondary(s.AnsweredText);
            var skip = new Button { Content = "Skip" };
            skip.Click += (_, _) => s.Next();
            var check = new Button { Content = "Check Answers", Style = AppStyle("AccentButtonStyle") };
            check.Click += (_, _) => s.Check();
            buttons = HStack(12, skip, check);
        }
        return Card(SpacedRow(leading, buttons));
    }

    // MARK: - History

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
        foreach (var record in s.VisibleHistory)
        {
            var set = store.Set(record.SetID);
            var grid = new Grid { ColumnSpacing = 12 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var allCorrect = record.Results.All(r => r);
            var score = Text($"{record.Correct}/{record.Results.Count}", Headline, FontWeights.SemiBold, allCorrect ? "GuideTealText" : "GuideGoldText", wrap: false);
            score.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(score);
            var info = VStack(2,
                Text(set?.Title ?? "Set", Subheadline, FontWeights.SemiBold),
                Secondary($"{s.HistoryTaskLabel(record)} · {ShortDateTime(record.Date)}"));
            Grid.SetColumn(info, 1);
            grid.Children.Add(info);
            var chevron = Icon(GuideIcons.ChevronRight, Caption, "GuideSecondaryIcon");
            chevron.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(chevron, 2);
            grid.Children.Add(chevron);
            var button = new Button
            {
                Content = grid,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(10),
            };
            button.Click += (_, _) => _ = ShowReview(record);
            stack.Children.Add(button);
        }
        return Card(stack);
    }

    /// <summary>The review sheet: the set as it was answered, with "Practice Again" and "Done".</summary>
    private async Task ShowReview(ReadingDrillStore.Record record)
    {
        if (session is not { } s || XamlRoot is null) return;
        var set = s.Store.Set(record.SetID);
        var header = Text(ShortDateTime(record.Date), Headline, FontWeights.SemiBold);
        UIElement details = set is not null ? new ReadingSetView(set, record.Choices, record.Results, null) : new Grid();
        var result = await ReviewDialog.ShowAsync(XamlRoot, header, details, set is not null ? "Practice Again" : null);
        if (result == ContentDialogResult.Primary && set is not null) s.Load(set);
    }
}
