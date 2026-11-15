using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using TOEFLSimulation.App.Controls;
using TOEFLSimulation.App.Exam;
using TOEFLSimulation.App.Services;
using TOEFLSimulation.Core;

namespace TOEFLSimulation.App.Pages;

/// <summary>Practice Tests (the Mac DashboardView): hero, latest estimate, full test, section practice, blueprint, notice.</summary>
public sealed partial class DashboardPage : Page
{
    private static bool reportedLoadError;

    public DashboardPage()
    {
        InitializeComponent();
        HeroTitle.Text = DashboardContent.HeroTitle;
        HeroSubtitle.Text = DashboardContent.HeroSubtitle.Replace("for Mac and iPad", "on Windows", StringComparison.Ordinal);
        FullTestTitle.Text = DashboardContent.FullTestTitle;
        FullTestDetail.Text = DashboardContent.FullTestDetail;
        StartFullButton.Content = DashboardContent.FullTestButton;
        BlueprintTitle.Text = DashboardContent.BlueprintTitle;
        AccuracyNotice.Text = DashboardContent.AccuracyNotice;
        LatestTitle.Text = DashboardContent.LatestResultTitle;
        BuildFeatures();
        BuildPracticeCards();
        BuildBlueprint();
        StartFullButton.IsEnabled = false;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        try
        {
            await AppServices.Ready;
        }
        catch (Exception error)
        {
            await ReportLoadErrorAsync(error);
            return;
        }
        var core = AppServices.Core;
        BankSummaryText.Text = DashboardContent.BankSummary(core.QuestionBank.AllQuestions.Count, core.StudyBank.Words.Count);
        StartFullButton.IsEnabled = true;
        foreach (var button in PracticeGrid.Children.OfType<Border>().Select(card => card.Tag).OfType<Button>()) button.IsEnabled = true;
        if (core.History.Latest is { } latest)
        {
            LatestDate.Text = UiKit.NumericDateTime(latest.CompletedAt);
            OverallValue.Text = TextUtil.OneDecimal(latest.OverallBand);
            CefrValue.Text = latest.Cefr;
            ComparableValue.Text = $"{latest.ComparableTotal120}";
            LatestCard.Visibility = Visibility.Visible;
        }
        else
        {
            LatestCard.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>The bundled question bank is missing or damaged: say so and quit (nothing else can work).</summary>
    private async Task ReportLoadErrorAsync(Exception error)
    {
        var message = $"Reinstall TOEFL Simulation to restore its Resources folder. ({error.Message})";
        LoadErrorBar.Message = message;
        LoadErrorBar.IsOpen = true;
        if (reportedLoadError || XamlRoot is null) return;
        reportedLoadError = true;
        var dialog = new ContentDialog
        {
            Title = "The question bank could not be loaded",
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = "Quit",
            XamlRoot = XamlRoot,
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
        };
        try
        {
            await dialog.ShowAsync();
        }
        catch (Exception dialogError)
        {
            CrashLog.Write(dialogError);
        }
        Application.Current.Exit();
    }

    private void OnStartFullTest(object sender, RoutedEventArgs e) => ExamLauncher.Start(ExamMode.FullLength);

    private void BuildFeatures()
    {
        string[] glyphs = ["", "", "", ""];
        for (var i = 0; i < DashboardContent.FullTestFeatures.Count; i++)
        {
            var icon = new FontIcon { Glyph = glyphs[i % glyphs.Length], FontSize = 13, Style = (Style)Resources["DashSecondaryIcon"], VerticalAlignment = VerticalAlignment.Center };
            var text = new TextBlock { Text = DashboardContent.FullTestFeatures[i], FontSize = 13, Style = (Style)Resources["DashSecondaryText"], VerticalAlignment = VerticalAlignment.Center };
            FeatureRow.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { icon, text } });
        }
    }

    private void BuildPracticeCards()
    {
        foreach (var mode in DashboardContent.PracticeModes)
        {
            var kind = mode.SectionKinds()[0];
            var start = new Button { Content = "Start", Padding = new Thickness(18, 7, 18, 7), IsEnabled = false };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(start, $"Start {mode.Title()}");
            start.Click += (_, _) => ExamLauncher.Start(mode);
            var grid = new Grid { RowSpacing = 12, MinHeight = 168 };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var icon = new FontIcon { Glyph = Glyphs.Section(kind), FontSize = 22, Style = (Style)Resources["DashBlueIcon"], HorizontalAlignment = HorizontalAlignment.Left };
            var title = new TextBlock { Text = mode.Title(), FontSize = 15, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
            var subtitle = new TextBlock { Text = mode.Subtitle(), FontSize = 13, Style = (Style)Resources["DashSecondaryText"] };
            start.VerticalAlignment = VerticalAlignment.Bottom;
            Grid.SetRow(title, 1);
            Grid.SetRow(subtitle, 2);
            Grid.SetRow(start, 3);
            grid.Children.Add(icon);
            grid.Children.Add(title);
            grid.Children.Add(subtitle);
            grid.Children.Add(start);
            PracticeGrid.Children.Add(new Border { Style = (Style)Application.Current.Resources["CardStyle"], CornerRadius = new CornerRadius(14), Child = grid, Tag = start });
        }
    }

    private void BuildBlueprint()
    {
        var row = 0;
        void Add(FrameworkElement element, int column)
        {
            Grid.SetRow(element, row);
            Grid.SetColumn(element, column);
            BlueprintGrid.Children.Add(element);
        }
        void NewRow() => BlueprintGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        NewRow();
        string[] headers = ["Section", "Task types", "Items", "Base time"];
        for (var i = 0; i < headers.Length; i++)
            Add(new TextBlock { Text = headers[i], FontWeight = FontWeights.SemiBold, FontSize = 14 }, i);
        row++;
        NewRow();
        var divider = new Microsoft.UI.Xaml.Shapes.Rectangle { Height = 1, Fill = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"] };
        Grid.SetColumnSpan(divider, 4);
        Add(divider, 0);
        row++;
        foreach (var kind in TOEFLSectionKindExtensions.AllCases)
        {
            NewRow();
            var label = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 7,
                Children =
                {
                    new FontIcon { Glyph = Glyphs.Section(kind), FontSize = 13, VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = kind.Title(), FontSize = 13, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center },
                },
            };
            Add(label, 0);
            Add(new TextBlock { Text = DashboardContent.TaskSummary(kind), FontSize = 13, Style = (Style)Resources["DashSecondaryText"] }, 1);
            Add(new TextBlock { Text = $"{kind.OfficialItemCount()}", FontSize = 13 }, 2);
            Add(new TextBlock { Text = $"{kind.OfficialMinutes()} min", FontSize = 13 }, 3);
            row++;
        }
    }
}
