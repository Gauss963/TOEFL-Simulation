using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using TOEFLSimulation.App.Controls;
using TOEFLSimulation.App.Services;
using TOEFLSimulation.Core;

namespace TOEFLSimulation.App.Pages;

/// <summary>Official Resources (Mac <c>ResourcesView</c>): ETS references, free official practice, and the authorized local library.</summary>
public sealed partial class ResourcesPage : Page
{
    private readonly UiKit ui;

    public ResourcesPage()
    {
        InitializeComponent();
        ui = new UiKit(Resources);
        TitleText.Text = ResourcesContent.Title;
        IntroductionText.Text = ResourcesContent.Introduction;
        FreePracticeTitleText.Text = ResourcesContent.FreePracticeTitle;
        FreePracticeNoteText.Text = ResourcesContent.FreePracticeNote;
        LocalLibraryTitleText.Text = ResourcesContent.LocalLibraryTitle;
        LocalLibraryNoteText.Text = ResourcesContent.LocalLibraryNote;
        TrademarkText.Text = ResourcesContent.Trademark;
        foreach (var link in ResourcesContent.OfficialReferences) ReferencesPanel.Children.Add(LinkButton(link));
        foreach (var link in ResourcesContent.FreePractice) FreePracticePanel.Children.Add(LinkButton(link));
        foreach (var source in ResourcesContent.LocalSources)
            LocalSourcesPanel.Children.Add(ui.Label(Glyphs.Seal, source, "TextNavy", "IconNavy"));
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        try
        {
            await AppServices.Ready;
            VocabularyCountText.Text = $"{AppServices.Core.StudyBank.Words.Count} vocabulary entries";
        }
        catch (Exception)
        {
            // Keep the generic label if the vocabulary could not be loaded.
        }
    }

    /// <summary>SwiftUI <c>Link</c> with an "arrow.up.right.square" label; opens in the default browser.</summary>
    private HyperlinkButton LinkButton(ResourcesContent.Link link)
    {
        var content = new Grid { ColumnSpacing = 8 };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var icon = new FontIcon { Glyph = Glyphs.OpenLink, FontSize = 14, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 3, 0, 0) };
        content.Children.Add(icon);
        var title = new TextBlock { Text = link.Title, TextWrapping = TextWrapping.Wrap };
        Grid.SetColumn(title, 1);
        content.Children.Add(title);

        var button = new HyperlinkButton { Content = content, Padding = new Thickness(6, 5, 6, 5), Margin = new Thickness(-6, 0, 0, 0) };
        ToolTipService.SetToolTip(button, link.Url);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, link.Title);
        button.Click += async (_, _) =>
        {
            try
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri(link.Url));
            }
            catch (Exception error)
            {
                CrashLog.Write(error);
            }
        };
        return button;
    }
}
