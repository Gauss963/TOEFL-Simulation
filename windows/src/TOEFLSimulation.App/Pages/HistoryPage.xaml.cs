using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using TOEFLSimulation.App.Controls;
using TOEFLSimulation.App.Services;
using TOEFLSimulation.Core;

namespace TOEFLSimulation.App.Pages;

/// <summary>Score History (Mac <c>HistoryView</c>): saved practice results, newest first; each reopens its score report.</summary>
public sealed partial class HistoryPage : Page
{
    private readonly UiKit ui;
    private TOEFLHistoryStore? store;

    public HistoryPage()
    {
        InitializeComponent();
        ui = new UiKit(Resources);
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        try
        {
            await AppServices.Ready;
        }
        catch (Exception)
        {
            // The Practice Tests page reports a damaged installation; nothing can be shown here.
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
            return;
        }
        LoadingRing.IsActive = false;
        LoadingRing.Visibility = Visibility.Collapsed;
        store = AppServices.Core.History;
        ((INotifyCollectionChanged)store.Results).CollectionChanged += OnResultsChanged;
        Rebuild();
        // A result passed in (e.g. right after a test) opens straight away.
        if (e.Parameter is TOEFLExamResult result) ShowReport(result);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        if (store is not null) ((INotifyCollectionChanged)store.Results).CollectionChanged -= OnResultsChanged;
    }

    private void OnResultsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DispatcherQueue.HasThreadAccess) Rebuild();
        else DispatcherQueue.TryEnqueue(Rebuild);
    }

    private void Rebuild()
    {
        if (store is null) return;
        var results = store.Results;
        ClearButton.Visibility = results.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyCard.Visibility = results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ResultsPanel.Children.Clear();
        foreach (var result in results) ResultsPanel.Children.Add(HistoryCard(result));
    }

    private Button HistoryCard(TOEFLExamResult result)
    {
        var grid = new Grid { ColumnSpacing = 18 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel { Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(ui.Text(result.Mode.Title(), "TextPrimary", 16, UiKit.SemiBold));
        left.Children.Add(ui.Text(UiKit.NumericDateTime(result.CompletedAt), "TextSecondary", 12));
        left.Children.Add(ui.Text(ScoreReportText.SectionBandsLine(result), "TextSecondary", 14));
        grid.Children.Add(left);

        var right = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        var band = ui.Text(ScoreReportText.Band(result.OverallBand), "TextNavy", 28, UiKit.Bold);
        band.HorizontalAlignment = HorizontalAlignment.Right;
        right.Children.Add(band);
        var cefr = ui.Text(ScoreReportText.HistoryCefrLine(result), "TextSecondary", 12);
        cefr.HorizontalAlignment = HorizontalAlignment.Right;
        cefr.TextWrapping = TextWrapping.NoWrap;
        right.Children.Add(cefr);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);

        var chevron = ui.Icon(Glyphs.ChevronRight, "IconTertiary", 14);
        chevron.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(chevron, 2);
        grid.Children.Add(chevron);

        var card = new Button { Style = ui.GetStyle("CardButtonStyle"), Content = grid };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(card,
            $"{result.Mode.Title()}, {UiKit.NumericDateTime(result.CompletedAt)}, overall {ScoreReportText.Band(result.OverallBand)}");
        card.Click += (_, _) => ShowReport(result);
        return card;
    }

    private void ShowReport(TOEFLExamResult result)
    {
        var report = new ScoreReportView(result, ScoreReportText.SavedHeading, ScoreReportText.SavedDoneTitle);
        report.CloseRequested += (_, _) => HideReport();
        ReportHost.Children.Clear();
        ReportHost.Children.Add(report);
        ReportHost.Visibility = Visibility.Visible;
        ListScroller.Visibility = Visibility.Collapsed;
    }

    private void HideReport()
    {
        ReportHost.Children.Clear();
        ReportHost.Visibility = Visibility.Collapsed;
        ListScroller.Visibility = Visibility.Visible;
    }

    private async void OnClearClick(object sender, RoutedEventArgs e)
    {
        if (store is null) return;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Style = UiKit.AppStyle("DefaultContentDialogStyle"),
            Title = "Delete every saved result?",
            Content = "Every saved practice report on this device will be removed. This can't be undone.",
            PrimaryButtonText = "Delete all results",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) store.Clear();
    }
}
