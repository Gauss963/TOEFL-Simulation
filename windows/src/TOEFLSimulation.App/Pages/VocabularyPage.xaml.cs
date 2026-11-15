using System.ComponentModel;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using TOEFLSimulation.App.Controls;
using TOEFLSimulation.App.Services;
using TOEFLSimulation.Core;
using TOEFLSimulation.Core.Audio;
using Windows.System;
using Windows.UI.Core;

namespace TOEFLSimulation.App.Pages;

/// <summary>
/// Vocabulary (Mac <c>VocabularyView</c> + <c>TOEFLFlashcardStudyView</c>): Browse the word bank with search and source
/// filters, or study shuffled flash cards (fixed deck or infinite loop with spaced "Study again"), saving mastery progress.
/// Keyboard: Space reveals, V plays the pronunciation, ← Study again, → Know it.
/// </summary>
public sealed partial class VocabularyPage : Page
{
    private const double MinCardWidth = 285;
    private const double CardSpacing = 14;
    private static readonly FlashcardDeckSize[] DeckSizes = [FlashcardDeckSize.Twenty, FlashcardDeckSize.Fifty, FlashcardDeckSize.OneHundred, FlashcardDeckSize.All];
    private static readonly FlashcardMode[] Modes = [FlashcardMode.Deck, FlashcardMode.Infinite];

    private readonly UiKit ui;
    private readonly HashSet<string> revealed = [];
    private TOEFLStudyBank? bank;
    private FlashcardSession? session;
    private WordPronunciationPlayer? player;
    private IReadOnlyList<VocabularyEntry> filtered = [];
    private string browseSource = TOEFLStudyBank.AllSources;
    private int columns;
    private bool updatingControls;
    private bool flashCardsShown;

    public VocabularyPage()
    {
        InitializeComponent();
        ui = new UiKit(Resources);
        TitleText.Text = VocabularyContent.Title;
        SubtitleText.Text = VocabularyContent.Subtitle;
        SearchBox.PlaceholderText = VocabularyContent.SearchPlaceholder;
        FootnoteText.Text = VocabularyContent.Footnote;
        InfiniteNoteText.Text = FlashcardSession.InfiniteNote;
        ToolTipService.SetToolTip(FrontSpeakButton, "Hear pronunciation (V)");
        ToolTipService.SetToolTip(BackSpeakButton, "Hear pronunciation (V)");
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
            // The Practice Tests page reports a damaged installation.
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
            return;
        }
        LoadingRing.IsActive = false;
        LoadingRing.Visibility = Visibility.Collapsed;

        var core = AppServices.Core;
        bank = core.StudyBank;
        player = core.CreateWordPronunciationPlayer(AppServices.Audio);
        player.PropertyChanged += OnPlayerPropertyChanged;
        session = core.CreateFlashcardSession(player);
        session.PropertyChanged += OnSessionPropertyChanged;

        updatingControls = true;
        BrowseSourceBox.Items.Add(TOEFLStudyBank.AllSources);
        FlashSourceBox.Items.Add(TOEFLStudyBank.AllSources);
        foreach (var source in bank.SourceOptions)
        {
            BrowseSourceBox.Items.Add(source);
            FlashSourceBox.Items.Add(source);
        }
        BrowseSourceBox.SelectedIndex = 0;
        foreach (var mode in Modes) FlashModeBox.Items.Add(FlashcardSession.ModeTitle(mode));
        foreach (var size in DeckSizes) DeckSizeBox.Items.Add(FlashcardSession.DeckSizeTitle(size));
        updatingControls = false;

        ApplyFilter();
        ShowStudyMode();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        session?.Disappear();
        if (player is not null) player.PropertyChanged -= OnPlayerPropertyChanged;
        if (session is not null) session.PropertyChanged -= OnSessionPropertyChanged;
    }

    // MARK: Study mode

    private bool IsFlashCardsMode => ReferenceEquals(ModeSelector.SelectedItem, FlashCardsItem);

    private void OnStudyModeChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args) => ShowStudyMode();

    private void ShowStudyMode()
    {
        if (bank is null || session is null) return;
        var flash = IsFlashCardsMode;
        BrowsePanel.Visibility = flash ? Visibility.Collapsed : Visibility.Visible;
        FlashPanel.Visibility = flash ? Visibility.Visible : Visibility.Collapsed;
        if (flash)
        {
            flashCardsShown = true;
            session.Appear();
            RefreshFlashCards();
            RestoreKeyboardFocus();
        }
        else if (flashCardsShown)
        {
            session.Disappear();
        }
    }

    // MARK: Browse

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput || args.Reason == AutoSuggestionBoxTextChangeReason.ProgrammaticChange)
            ApplyFilter();
    }

    private void OnBrowseSourceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updatingControls) return;
        browseSource = BrowseSourceBox.SelectedItem as string ?? TOEFLStudyBank.AllSources;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (bank is null) return;
        filtered = bank.Filter(SearchBox.Text ?? "", browseSource);
        WordCountText.Text = $"{filtered.Count} words";
        NoMatchesText.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        BuildRows();
    }

    private void OnCardsRepeaterSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var count = AdaptiveGridPanel.ColumnCount(e.NewSize.Width, MinCardWidth, CardSpacing);
        if (count != columns) BuildRows();
    }

    /// <summary>Groups the filtered words into rows of as many cards as fit (LazyVGrid adaptive columns), so the
    /// repeater only realizes the rows on screen.</summary>
    private void BuildRows()
    {
        columns = AdaptiveGridPanel.ColumnCount(CardsRepeater.ActualWidth > 0 ? CardsRepeater.ActualWidth : MinCardWidth, MinCardWidth, CardSpacing);
        var rows = new List<VocabularyRow>();
        for (var start = 0; start < filtered.Count; start += columns)
            rows.Add(new VocabularyRow(this, filtered.Skip(start).Take(columns).ToList(), columns));
        CardsRepeater.ItemsSource = rows;
    }

    /// <summary>One browse card: word, pronunciation, part of speech, high-frequency mark; click to reveal the meaning.</summary>
    internal Button CreateBrowseCard(VocabularyEntry entry)
    {
        var card = new Button { Style = ui.GetStyle("CardButtonStyle"), MinHeight = 205 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(card, entry.Word);
        FillBrowseCard(card, entry);
        card.Click += (_, _) =>
        {
            if (!revealed.Remove(entry.Id)) revealed.Add(entry.Id);
            FillBrowseCard(card, entry);
        };
        return card;
    }

    private void FillBrowseCard(Button card, VocabularyEntry entry)
    {
        var isRevealed = revealed.Contains(entry.Id);
        var panel = new StackPanel { Spacing = 9 };

        var header = new Grid { ColumnSpacing = 8 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titleLine = new WrapFlowPanel { HorizontalSpacing = 8, VerticalSpacing = 2, ItemVerticalAlignment = VerticalAlignment.Bottom };
        titleLine.Children.Add(ui.Text(entry.Word, "TextNavy", 22, UiKit.Bold));
        if (entry.Pronunciation.Length > 0)
        {
            var pronunciation = ui.Text(entry.Pronunciation, "TextSecondary", 12);
            pronunciation.Margin = new Thickness(0, 0, 0, 4);
            titleLine.Children.Add(pronunciation);
        }
        var partOfSpeech = ui.Text(entry.PartOfSpeech, "TextSecondary", 12);
        partOfSpeech.Margin = new Thickness(0, 0, 0, 4);
        titleLine.Children.Add(partOfSpeech);
        header.Children.Add(titleLine);
        if (entry.IsHighFrequency)
        {
            var flame = ui.Icon(Glyphs.Star, "IconGold", 15);
            flame.VerticalAlignment = VerticalAlignment.Top;
            flame.Margin = new Thickness(0, 6, 0, 0);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(flame, "High frequency");
            ToolTipService.SetToolTip(flame, "High frequency");
            Grid.SetColumn(flame, 1);
            header.Children.Add(flame);
        }
        var eye = ui.Icon(isRevealed ? Glyphs.Eye : Glyphs.EyeHidden, "IconBlue", 15);
        eye.VerticalAlignment = VerticalAlignment.Top;
        eye.Margin = new Thickness(0, 6, 0, 0);
        Grid.SetColumn(eye, 2);
        header.Children.Add(eye);
        panel.Children.Add(header);

        if (isRevealed)
        {
            if (entry.Definition.Length > 0)
                panel.Children.Add(ui.Text(entry.Definition, "TextPrimary", 14, Microsoft.UI.Text.FontWeights.Medium));
            var chinese = ui.Text(entry.Chinese, "TextTeal", 14);
            chinese.Language = "zh-TW";
            panel.Children.Add(chinese);
            if (entry.Synonyms.Count > 0)
                panel.Children.Add(ui.Text(string.Join(" · ", entry.Synonyms), "TextSecondary", 12, UiKit.SemiBold));
            panel.Children.Add(ui.Text(entry.Example, "TextSecondary", 13, italic: true));
            panel.Children.Add(ui.Text(string.Join(" · ", entry.Sources), "TextTertiary", 11));
        }
        else
        {
            panel.Children.Add(ui.Text("Click to reveal meaning", "TextSecondary", 13));
        }
        card.Content = panel;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(card, isRevealed ? "Hide meaning" : "Reveal meaning");
    }

    // MARK: Flash cards

    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e) => OnUiThread(RefreshFlashCards);

    private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e) => OnUiThread(RefreshPronunciationButtons);

    private void OnUiThread(Action action)
    {
        if (DispatcherQueue.HasThreadAccess) action();
        else DispatcherQueue.TryEnqueue(() => action());
    }

    private void RefreshFlashCards()
    {
        if (session is null || bank is null) return;

        updatingControls = true;
        FlashSourceBox.SelectedItem = session.SelectedSource;
        FlashModeBox.SelectedIndex = Array.IndexOf(Modes, session.Mode);
        DeckSizeBox.SelectedIndex = Array.IndexOf(DeckSizes, session.DeckSize);
        UnmasteredSwitch.IsOn = session.UnmasteredOnly;
        updatingControls = false;

        var isDeck = session.Mode == FlashcardMode.Deck;
        DeckSizeBox.Visibility = isDeck ? Visibility.Visible : Visibility.Collapsed;
        UnmasteredSwitch.Visibility = isDeck ? Visibility.Visible : Visibility.Collapsed;
        AvailableText.Text = session.AvailableText;
        MasteredText.Text = session.MasteredText;
        NewDeckButton.Content = session.NewDeckButtonTitle;

        var screen = session.Screen;
        var showsCard = screen == FlashcardScreen.Card;
        DeckProgressPanel.Visibility = showsCard && isDeck ? Visibility.Visible : Visibility.Collapsed;
        InfiniteProgressPanel.Visibility = showsCard && !isDeck ? Visibility.Visible : Visibility.Collapsed;
        CardHost.Visibility = showsCard ? Visibility.Visible : Visibility.Collapsed;
        RatingPanel.Visibility = showsCard ? Visibility.Visible : Visibility.Collapsed;
        DeckCompleteCard.Visibility = screen == FlashcardScreen.DeckComplete ? Visibility.Visible : Visibility.Collapsed;
        InfiniteCompleteCard.Visibility = screen == FlashcardScreen.InfiniteComplete ? Visibility.Visible : Visibility.Collapsed;
        EmptyCard.Visibility = screen == FlashcardScreen.Empty ? Visibility.Visible : Visibility.Collapsed;

        if (showsCard && session.CurrentCard is { } card)
        {
            DeckProgressTitleText.Text = session.DeckProgressTitle;
            DeckProgressDetailText.Text = session.DeckProgressDetail;
            DeckProgressBar.Value = session.DeckProgressFraction;
            InfiniteProgressDetailText.Text = session.InfiniteProgressDetail;
            InfiniteProgressBar.Value = session.InfiniteProgressFraction;
            ShowCard(card, session.IsRevealed);
        }
        CompletionText.Text = session.CompletionText;
        ReviewMissedButton.Visibility = session.MissedThisRound.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        InfiniteCompletionText.Text = session.InfiniteCompletionText;
        EmptyText.Text = session.EmptyText;
        RefreshPronunciationButtons();
    }

    private void ShowCard(VocabularyEntry card, bool isRevealed)
    {
        FrontPanel.Visibility = isRevealed ? Visibility.Collapsed : Visibility.Visible;
        BackPanel.Visibility = isRevealed ? Visibility.Visible : Visibility.Collapsed;
        RevealButton.Visibility = isRevealed ? Visibility.Collapsed : Visibility.Visible;
        StudyAgainButton.Visibility = isRevealed ? Visibility.Visible : Visibility.Collapsed;
        KnowItButton.Visibility = isRevealed ? Visibility.Visible : Visibility.Collapsed;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(CardHost, isRevealed ? $"{card.Word}: {card.Definition} {card.Chinese}" : card.Word);

        FrontHighFrequency.Visibility = card.IsHighFrequency ? Visibility.Visible : Visibility.Collapsed;
        FrontWordText.Text = card.Word;
        FrontDetailText.Text = card.Pronunciation.Length > 0 ? $"{card.Pronunciation}  {card.PartOfSpeech}" : card.PartOfSpeech;

        BackWordText.Text = card.Word;
        BackPronunciationText.Text = card.Pronunciation;
        BackPronunciationText.Visibility = card.Pronunciation.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        BackPartOfSpeechText.Text = card.PartOfSpeech;
        BackHighFrequencyIcon.Visibility = card.IsHighFrequency ? Visibility.Visible : Visibility.Collapsed;
        BackDefinitionBlock.Visibility = card.Definition.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        BackDefinitionText.Text = card.Definition;
        BackChineseText.Text = card.Chinese;
        BackSynonymsBlock.Visibility = card.Synonyms.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        BackSynonymsText.Text = string.Join(" · ", card.Synonyms);
        BackExampleText.Text = card.Example;
        BackSourcesText.Text = string.Join(" · ", card.Sources);
    }

    private void RefreshPronunciationButtons()
    {
        var speaking = player?.IsSpeaking == true;
        var glyph = speaking ? Glyphs.Stop : Glyphs.Speaker;
        FrontSpeakIcon.Glyph = glyph;
        BackSpeakIcon.Glyph = glyph;
        FrontSpeakText.Text = speaking ? "Stop pronunciation" : "Hear pronunciation";
        var word = session?.CurrentCard?.Word ?? "";
        var name = speaking ? $"Stop pronunciation of {word}" : $"Hear pronunciation of {word}";
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(FrontSpeakButton, name);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(BackSpeakButton, name);
    }

    /// <summary>Like the Mac app's restoreKeyboardFocus: keep keyboard shortcuts going to the card after each action.</summary>
    private void RestoreKeyboardFocus()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (FlashPanel.Visibility != Visibility.Visible) return;
            if (CardHost.Visibility == Visibility.Visible) CardHost.Focus(FocusState.Programmatic);
        });
    }

    private void OnCardTapped(object sender, TappedRoutedEventArgs e)
    {
        if (session is null || session.IsRevealed || session.CurrentCard is null) return;
        // Clicks on the pronunciation button play the word without revealing the card.
        for (var element = e.OriginalSource as DependencyObject; element is not null && !ReferenceEquals(element, CardHost);
             element = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(element))
            if (element is ButtonBase) return;
        session.Reveal();
        RestoreKeyboardFocus();
    }

    private void OnRevealClick(object sender, RoutedEventArgs e)
    {
        session?.Reveal();
        RestoreKeyboardFocus();
    }

    private void OnStudyAgainClick(object sender, RoutedEventArgs e)
    {
        session?.Rate(false);
        RestoreKeyboardFocus();
    }

    private void OnKnowItClick(object sender, RoutedEventArgs e)
    {
        session?.Rate(true);
        RestoreKeyboardFocus();
    }

    private void OnSpeakClick(object sender, RoutedEventArgs e)
    {
        if (player is null || session?.CurrentCard is not { } card) return;
        _ = player.ToggleAsync(card.Word);
        RestoreKeyboardFocus();
    }

    private void OnNewDeckClick(object sender, RoutedEventArgs e)
    {
        session?.StartNewDeck();
        RestoreKeyboardFocus();
    }

    private void OnReviewMissedClick(object sender, RoutedEventArgs e)
    {
        session?.ReviewMissedCards();
        RestoreKeyboardFocus();
    }

    private void OnStartFixedDeckClick(object sender, RoutedEventArgs e)
    {
        session?.StartFixedDeck();
        RestoreKeyboardFocus();
    }

    private void OnFlashSourceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updatingControls || session is null) return;
        session.SelectedSource = FlashSourceBox.SelectedItem as string ?? TOEFLStudyBank.AllSources;
    }

    private void OnFlashModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updatingControls || session is null || FlashModeBox.SelectedIndex < 0) return;
        session.Mode = Modes[FlashModeBox.SelectedIndex]; // starts a new deck
        RestoreKeyboardFocus();
    }

    private void OnDeckSizeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updatingControls || session is null || DeckSizeBox.SelectedIndex < 0) return;
        session.DeckSize = DeckSizes[DeckSizeBox.SelectedIndex];
    }

    private void OnUnmasteredToggled(object sender, RoutedEventArgs e)
    {
        if (updatingControls || session is null) return;
        session.UnmasteredOnly = UnmasteredSwitch.IsOn;
    }

    private async void OnResetProgressClick(object sender, RoutedEventArgs e)
    {
        if (session is null) return;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Style = UiKit.AppStyle("DefaultContentDialogStyle"),
            Title = "Reset all flash-card progress?",
            Content = "Every saved mastered word will return to the study pool.",
            PrimaryButtonText = "Reset mastery progress",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) session.ResetProgress();
        RestoreKeyboardFocus();
    }

    /// <summary>Space / V / ← / → while flash cards are showing, unless a list, text box, or switch has the keyboard.</summary>
    private void OnPagePreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (session is null || !IsFlashCardsMode || e.Handled) return;
        var key = e.Key switch
        {
            VirtualKey.Space => FlashcardKey.Space,
            VirtualKey.V => FlashcardKey.V,
            VirtualKey.Left => FlashcardKey.LeftArrow,
            VirtualKey.Right => FlashcardKey.RightArrow,
            _ => (FlashcardKey?)null,
        };
        if (key is null || HasModifierKeys()) return;
        var focused = XamlRoot is null ? null : FocusManager.GetFocusedElement(XamlRoot);
        if (focused is ComboBox or ComboBoxItem or ToggleSwitch or TextBox or AutoSuggestBox or SelectorBarItem or MenuFlyoutItem
            or ToggleButton or Popup)
            return;
        if (session.HandleKey(key.Value))
        {
            e.Handled = true;
            RestoreKeyboardFocus();
        }
    }

    private static bool HasModifierKeys()
    {
        foreach (var modifier in new[] { VirtualKey.Control, VirtualKey.Menu, VirtualKey.LeftWindows, VirtualKey.RightWindows })
            if (InputKeyboardSource.GetKeyStateForCurrentThread(modifier).HasFlag(CoreVirtualKeyStates.Down)) return true;
        return false;
    }
}

/// <summary>A row of browse cards (the repeater's item).</summary>
public sealed partial class VocabularyRow(VocabularyPage owner, IReadOnlyList<VocabularyEntry> entries, int columns)
{
    internal VocabularyPage Owner { get; } = owner;
    public IReadOnlyList<VocabularyEntry> Entries { get; } = entries;
    public int Columns { get; } = columns;
}

/// <summary>Lays out one <see cref="VocabularyRow"/> as equal columns; realized by the browse repeater.</summary>
public sealed partial class VocabularyRowPresenter : Grid
{
    public VocabularyRowPresenter()
    {
        ColumnSpacing = 14;
        DataContextChanged += (_, args) => Build(args.NewValue as VocabularyRow);
    }

    private void Build(VocabularyRow? row)
    {
        Children.Clear();
        ColumnDefinitions.Clear();
        if (row is null) return;
        for (var i = 0; i < row.Columns; i++)
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var i = 0; i < row.Entries.Count; i++)
        {
            var card = row.Owner.CreateBrowseCard(row.Entries[i]);
            SetColumn(card, i);
            Children.Add(card);
        }
    }
}
