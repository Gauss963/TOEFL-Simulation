using System.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TOEFLSimulation.App.Controls;
using TOEFLSimulation.App.Services;
using TOEFLSimulation.Core;
using TOEFLSimulation.Core.Audio;

namespace TOEFLSimulation.App.Exam;

/// <summary>
/// The full-window practice test (the Mac TOEFLExamContainerView and ExamView.swift): section directions with the
/// microphone check, the timed testing screens for every task type, the Speaking scoring wait, and the score report.
/// <para>
/// The UI is rebuilt when the screen changes (phase, section, item screen, stimulus played) and refreshed in place for
/// everything else, so text boxes keep their focus while answers are stored. Audio work that belongs to a screen (the
/// Listening stimulus, the Speaking item sequence) starts once per screen and is cancelled when the screen goes away.
/// </para>
/// </summary>
public sealed partial class ExamView : UserControl
{
    private readonly TOEFLExamSession session;
    private readonly ExamAudioPlayer player;
    private readonly SpeakingRecorder recorder;
    private readonly Action<TOEFLExamResult> onComplete;
    private readonly Action onExit;
    private readonly DispatcherTimer timer;
    private readonly DispatcherQueue dispatcher;
    private readonly ExamUi ui;
    private readonly Grid root;
    private readonly List<Action> refreshers = [];
    private readonly List<Action> cleanups = [];
    private string? screenKey;
    private CancellationTokenSource? screenWork;
    private SpeakingFlow? speakingFlow;
    private TextBlock? clockLabel;
    private bool dialogOpen;
    private bool closed;

    public ExamView(ExamMode mode, Action<TOEFLExamResult> onComplete, Action onExit)
    {
        this.onComplete = onComplete;
        this.onExit = onExit;
        dispatcher = DispatcherQueue.GetForCurrentThread();
        var styles = new ResourceDictionary { Source = new Uri("ms-appx:///Exam/ExamResources.xaml") };
        Resources.MergedDictionaries.Add(styles);
        ui = new ExamUi(styles);
        root = new Grid { Style = ui.Style("ExamRoot") };
        Content = root;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        session = AppServices.Core.CreateExamSession(mode);
        player = AppServices.Core.CreateExamAudioPlayer(AppServices.Audio);
        recorder = AppServices.Recorder;
        session.PropertyChanged += OnSessionChanged;
        player.PropertyChanged += OnPlayerChanged;
        recorder.PropertyChanged += OnRecorderChanged;

        // The Mac exam calls tick() every 0.25 s while testing.
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.25) };
        timer.Tick += (_, _) =>
        {
            if (!closed && session.Phase == TOEFLExamPhase.Testing) session.Tick();
        };
        timer.Start();
        Render();
    }

    /// <summary>The running session, for the CI smoke test (<see cref="SmokeTest"/>).</summary>
    internal TOEFLExamSession SessionForTesting => session;

    /// <summary>Stops audio, recording, and timers when the test closes (called by <see cref="ExamLauncher"/>).</summary>
    public void Shutdown()
    {
        if (closed) return;
        closed = true;
        timer.Stop();
        screenWork?.Cancel();
        RunCleanups();
        player.Stop();
        if (recorder.IsRecording) recorder.Stop();
        session.PropertyChanged -= OnSessionChanged;
        player.PropertyChanged -= OnPlayerChanged;
        recorder.PropertyChanged -= OnRecorderChanged;
    }

    // MARK: - State changes

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (closed) return;
        if (e.PropertyName is nameof(TOEFLExamSession.SecondsRemaining) or nameof(TOEFLExamSession.IsTimeVisible))
        {
            UpdateClock();
            return;
        }
        Render();
    }

    private void OnPlayerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!closed && e.PropertyName == nameof(ExamAudioPlayer.IsPlaying)) Render();
    }

    private void OnRecorderChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!closed) Render();
    }

    /// <summary>Rebuilds when the screen changed, then refreshes the dynamic parts.</summary>
    private void Render()
    {
        if (closed) return;
        var key = ScreenKey();
        if (key != screenKey)
        {
            screenKey = key;
            BeginScreen();
            Build();
        }
        foreach (var refresh in refreshers.ToList()) refresh();
    }

    private string ScreenKey() => session.Phase switch
    {
        TOEFLExamPhase.Directions => $"D{session.CurrentSectionIndex}",
        TOEFLExamPhase.Testing =>
            $"T{session.CurrentSectionIndex}|{session.CurrentScreen.LowerBound}|{session.CurrentQuestion.Id}|{session.NeedsStimulus(session.CurrentQuestion)}",
        TOEFLExamPhase.Scoring => "S",
        _ => "C",
    };

    private void RunCleanups()
    {
        foreach (var cleanup in cleanups.ToList())
        {
            try
            {
                cleanup();
            }
            catch (Exception error)
            {
                CrashLog.Write(error);
            }
        }
        cleanups.Clear();
    }

    /// <summary>Cancels the previous screen's audio work and starts this screen's (stimulus or Speaking sequence).</summary>
    private void BeginScreen()
    {
        screenWork?.Cancel();
        screenWork = new CancellationTokenSource();
        var token = screenWork.Token;
        RunCleanups();
        refreshers.Clear();
        speakingFlow = null;
        if (session.Phase != TOEFLExamPhase.Testing) return;

        var question = session.CurrentQuestion;
        if (question.TaskType is TOEFLTaskType.ListenRepeat or TOEFLTaskType.Interview)
        {
            var flow = new SpeakingFlow(session, question, player, recorder);
            speakingFlow = flow;
            void OnFlowChanged(object? s, PropertyChangedEventArgs args)
            {
                if (closed || token.IsCancellationRequested) return;
                if (args.PropertyName == nameof(SpeakingFlow.RecordingFailed) && flow.RecordingFailed)
                    _ = AlertAsync(SpeakingRecorder.MicrophoneUnavailableTitle, SpeakingRecorder.MicrophoneUnavailableMessage);
                Render();
            }
            flow.PropertyChanged += OnFlowChanged;
            cleanups.Add(() => flow.PropertyChanged -= OnFlowChanged);
            // Start after this render completes: the flow changes the session when it finishes.
            dispatcher.TryEnqueue(async () =>
            {
                if (token.IsCancellationRequested || closed) return;
                try
                {
                    await flow.RunAsync(token);
                }
                catch (Exception error)
                {
                    CrashLog.Write(error);
                }
            });
        }
        else if (session.NeedsStimulus(question))
        {
            dispatcher.TryEnqueue(async () =>
            {
                if (token.IsCancellationRequested || closed) return;
                try
                {
                    await ListeningStimulus.PlayAsync(session, question, player, token);
                }
                catch (Exception error)
                {
                    CrashLog.Write(error);
                }
            });
        }
    }

    private void Build()
    {
        root.Children.Clear();
        clockLabel = null;
        UIElement view = session.Phase switch
        {
            TOEFLExamPhase.Directions => BuildDirections(),
            TOEFLExamPhase.Testing => BuildTesting(),
            TOEFLExamPhase.Scoring => BuildScoring(),
            _ => BuildReport(),
        };
        root.Children.Add(view);
    }

    // MARK: - Media and dialogs

    /// <summary>The Mac stopMedia(): stop audio and recording and resume the clock.</summary>
    private void StopMedia() => ListeningStimulus.StopMedia(session, player, recorder, recorder.IsRecording);

    private async Task<bool> ConfirmAsync(string title, string message, string confirm, string cancel)
    {
        if (dialogOpen || XamlRoot is null) return false;
        dialogOpen = true;
        try
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                PrimaryButtonText = confirm,
                CloseButtonText = cancel,
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot,
                RequestedTheme = ActualTheme,
                Style = ExamUi.AppStyle("DefaultContentDialogStyle"),
            };
            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }
        catch (Exception error)
        {
            CrashLog.Write(error);
            return false;
        }
        finally
        {
            dialogOpen = false;
        }
    }

    private async Task AlertAsync(string title, string message)
    {
        if (dialogOpen || XamlRoot is null) return;
        dialogOpen = true;
        try
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                CloseButtonText = "OK",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot,
                RequestedTheme = ActualTheme,
                Style = ExamUi.AppStyle("DefaultContentDialogStyle"),
            };
            await dialog.ShowAsync();
        }
        catch (Exception error)
        {
            CrashLog.Write(error);
        }
        finally
        {
            dialogOpen = false;
        }
    }

    private async void ConfirmExit()
    {
        if (!await ConfirmAsync(ExamText.ExitTitle, ExamText.ExitMessage, ExamText.ExitConfirm, "Cancel") || closed) return;
        StopMedia();
        screenWork?.Cancel();
        onExit();
    }

    private async void OnNext()
    {
        if (closed) return;
        if (session.IsLastInPart)
        {
            var key = screenKey;
            if (!await ConfirmAsync(ExamText.PartEndTitle(session), ExamText.PartEndMessage, ExamText.PartEndConfirm(session), "Return")) return;
            // The part may have ended on its own (time ran out) while the dialog was open.
            if (closed || key != screenKey || session.Phase != TOEFLExamPhase.Testing) return;
            StopMedia();
            session.AdvancePart();
        }
        else
        {
            StopMedia();
            session.GoNext();
        }
    }

    // MARK: - Directions

    private UIElement BuildDirections()
    {
        var kind = session.CurrentSection.Kind;
        var stack = new StackPanel { Spacing = 24, MaxWidth = 900, Padding = new Thickness(34) };

        var exit = ExamUi.Secondary("Exit", ExamGlyphs.Cancel);
        exit.Click += (_, _) => ConfirmExit();
        stack.Children.Add(ExamUi.Spread(exit, ui.Text(session.Mode.Title(), 13, "ExamTextSecondary")));

        var title = ui.Label(ExamGlyphs.Section(kind), kind.Title(), 36, "ExamTextNavy", "ExamIconNavy", FontWeights.Bold, 14);
        stack.Children.Add(ExamUi.VStack(10, title, ui.Text(ExamText.SectionIntroduction(kind), 18, "ExamTextSecondary")));

        var facts = new Grid { ColumnSpacing = 14 };
        FrameworkElement[] factCards =
        [
            FactCard("Items", $"{kind.OfficialItemCount()}", ExamGlyphs.List),
            FactCard("Time", $"{kind.OfficialMinutes()} min", ExamGlyphs.Timer),
            FactCard("Structure", ExamText.StructureLabel(kind), ExamGlyphs.Branch),
        ];
        for (var i = 0; i < factCards.Length; i++)
        {
            facts.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(factCards[i], i);
            facts.Children.Add(factCards[i]);
        }
        stack.Children.Add(facts);

        var tasks = new StackPanel { Spacing = 13 };
        tasks.Children.Add(ui.Text("Tasks in this section", 20, "ExamText", FontWeights.Bold));
        foreach (var task in session.TaskTypesInCurrentSection)
        {
            tasks.Children.Add(ExamUi.IconRow(ui.Icon(ExamGlyphs.CheckCircle, 16, "ExamIconTeal"),
                ExamUi.VStack(3, ui.Bold(task.Title(), 15), ui.Text(task.Directions(), 13, "ExamTextSecondary"))));
        }
        stack.Children.Add(ui.Card(tasks));

        stack.Children.Add(ui.Card(ExamUi.IconRow(ui.Icon(ExamGlyphs.Info, 16, "ExamIconNavy"),
            ui.Text(ExamText.SectionRules(kind), 13, "ExamTextNavy"))));

        if (kind == TOEFLSectionKind.Speaking) stack.Children.Add(BuildMicrophoneCheck());

        var begin = ExamUi.Primary(ExamText.BeginButtonTitle(kind));
        begin.HorizontalAlignment = HorizontalAlignment.Right;
        begin.Click += (_, _) =>
        {
            if (recorder.IsRecording) return;
            session.BeginSection();
        };
        refreshers.Add(() => begin.IsEnabled = !recorder.IsRecording);
        stack.Children.Add(begin);

        return new ScrollViewer { Content = stack, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private Border FactCard(string title, string value, string glyph) =>
        ui.Card(ExamUi.VStack(8,
            ui.Icon(glyph, 18, "ExamIconBlue"),
            ui.Text(value, 22, "ExamTextNavy", FontWeights.Bold),
            ui.Text(title, 12, "ExamTextSecondary")));

    /// <summary>The Mac MicrophoneCheckView: record a test sentence and watch the level reach the green zone.</summary>
    private UIElement BuildMicrophoneCheck()
    {
        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(ui.Label(ExamGlyphs.Microphone, "Microphone check", 15, "ExamText", "ExamIcon", FontWeights.SemiBold));
        stack.Children.Add(ui.Text(SpeakingRecording.MicrophoneCheckInstructions, 13, "ExamTextSecondary"));

        var toggle = ExamUi.Secondary("Start");
        var meter = new ProgressBar { Minimum = 0, Maximum = 1, Width = 280, VerticalAlignment = VerticalAlignment.Center };
        var label = ui.Text("", 12, "ExamTextSecondary", FontWeights.SemiBold);
        label.VerticalAlignment = VerticalAlignment.Center;
        var row = new Grid { ColumnSpacing = 14 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(meter, 1);
        Grid.SetColumn(label, 2);
        row.Children.Add(toggle);
        row.Children.Add(meter);
        row.Children.Add(label);
        stack.Children.Add(row);

        var micInfo = new InfoBar
        {
            Severity = InfoBarSeverity.Error,
            Title = SpeakingRecorder.MicrophoneUnavailableTitle,
            Message = SpeakingRecorder.MicrophoneUnavailableMessage,
            IsClosable = false,
            IsOpen = false,
        };
        var speechInfo = new InfoBar { IsClosable = false, IsOpen = false };
        stack.Children.Add(micInfo);
        stack.Children.Add(speechInfo);

        var token = screenWork?.Token ?? CancellationToken.None;
        toggle.Click += async (_, _) =>
        {
            if (recorder.IsRecording)
            {
                recorder.Stop();
                DeleteMicCheckRecording();
                return;
            }
            var started = await recorder.StartAsync("mic-check", token);
            if (closed || token.IsCancellationRequested)
            {
                if (started) recorder.Stop();
                return;
            }
            micInfo.IsOpen = !started;
        };
        refreshers.Add(() =>
        {
            toggle.Content = recorder.IsRecording ? "Stop" : "Start";
            AutomationProperties.SetName(toggle, recorder.IsRecording ? "Stop microphone check" : "Start microphone check");
            meter.Value = recorder.Level;
            meter.Foreground = new SolidColorBrush(MeterColor(SpeakingRecording.MicrophoneCheckColor(recorder.Level)));
            label.Text = SpeakingRecording.MicrophoneCheckLabel(recorder.Level);
        });
        cleanups.Add(() =>
        {
            if (recorder.IsRecording) recorder.Stop();
            DeleteMicCheckRecording();
        });

        // The Mac asks for microphone and speech permission here and reports whether scoring can work.
        dispatcher.TryEnqueue(async () =>
        {
            var microphone = await recorder.RequestPermissionAsync();
            if (closed || token.IsCancellationRequested) return;
            micInfo.IsOpen = !microphone;
            var speechReady = AppServices.Transcriber.IsAvailable;
            speechInfo.Severity = speechReady ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
            speechInfo.Message = speechReady ? WhisperTranscriber.ReadyMessage : WhisperTranscriber.UnavailableMessage;
            speechInfo.IsOpen = true;
        });
        return ui.Card(stack);
    }

    private void DeleteMicCheckRecording()
    {
        if (recorder.LastRecordingPath is { } path && Path.GetFileName(path).StartsWith("toefl-mic-check-", StringComparison.Ordinal))
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception)
            {
                // A leftover temp file is harmless.
            }
        }
    }

    private static Windows.UI.Color MeterColor(string name) => name switch
    {
        "red" => Windows.UI.Color.FromArgb(255, 0xE8, 0x11, 0x23),
        "green" => Windows.UI.Color.FromArgb(255, 0x10, 0x9F, 0x3E),
        _ => Windows.UI.Color.FromArgb(255, 0xF7, 0x8A, 0x0C),
    };

    // MARK: - Testing

    private UIElement BuildTesting()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        grid.Children.Add(BuildToolbar());

        var body = new StackPanel { Spacing = 22, MaxWidth = 1080, Padding = new Thickness(34, 26, 34, 26) };
        BuildQuestionBody(body);
        var scroll = new ScrollViewer { Content = body, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1);
        grid.Children.Add(scroll);

        if (session.CurrentSection.Kind != TOEFLSectionKind.Speaking)
        {
            var footer = BuildFooter();
            Grid.SetRow(footer, 2);
            grid.Children.Add(footer);
        }
        return grid;
    }

    private UIElement BuildToolbar()
    {
        // The header is always brand navy, so its controls use the dark theme (white text) in both modes.
        var bar = new Grid { Style = ui.Style("ExamHeaderBar"), RequestedTheme = ElementTheme.Dark, ColumnSpacing = 10 };
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var subtitle = session.QuestionLabel + (session.RouteLabel is { } route ? $" · {route}" : "");
        var titles = ExamUi.VStack(2,
            ui.Text(session.CurrentSection.DisplayTitle, 15, "ExamTextOnBrand", FontWeights.SemiBold),
            ui.Text(subtitle, 12, "ExamTextOnBrand"));
        titles.Opacity = 1;
        titles.VerticalAlignment = VerticalAlignment.Center;
        ((TextBlock)titles.Children[1]).Opacity = 0.85;
        bar.Children.Add(titles);

        var volume = new Button { Content = ExamUi.ButtonContent("Volume", ExamGlyphs.Volume), VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(volume, "Volume");
        volume.Flyout = BuildVolumeFlyout();
        Grid.SetColumn(volume, 1);
        bar.Children.Add(volume);

        if (session.CurrentSection.Kind != TOEFLSectionKind.Speaking)
        {
            clockLabel = new TextBlock { FontFamily = ExamUi.Monospace, VerticalAlignment = VerticalAlignment.Center };
            var clock = new Button
            {
                Content = ExamUi.HStack(8, new FontIcon { Glyph = ExamGlyphs.Timer, FontSize = 14 }, clockLabel),
                VerticalAlignment = VerticalAlignment.Center,
            };
            ToolTipService.SetToolTip(clock, "Show or hide the time");
            AutomationProperties.SetName(clock, "Show or hide the time");
            clock.Click += (_, _) => session.IsTimeVisible = !session.IsTimeVisible;
            Grid.SetColumn(clock, 2);
            bar.Children.Add(clock);
            UpdateClock();
        }

        var exit = new Button { Content = new FontIcon { Glyph = ExamGlyphs.Cancel, FontSize = 14 }, VerticalAlignment = VerticalAlignment.Center };
        ToolTipService.SetToolTip(exit, "Exit test");
        AutomationProperties.SetName(exit, "Exit test");
        exit.Click += (_, _) => ConfirmExit();
        Grid.SetColumn(exit, 3);
        bar.Children.Add(exit);
        return bar;
    }

    private Flyout BuildVolumeFlyout()
    {
        var slider = new Slider
        {
            Header = "Volume",
            Minimum = 0,
            Maximum = 100,
            StepFrequency = 1,
            Width = 240,
            Value = Math.Round(player.Volume * 100),
        };
        slider.ValueChanged += (_, e) => player.Volume = (float)(e.NewValue / 100);
        var presets = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var level in new[] { 50, 85, 100 })
        {
            var preset = new Button { Content = $"Volume {level}%" };
            preset.Click += (_, _) =>
            {
                player.Volume = level / 100f;
                slider.Value = level;
            };
            presets.Children.Add(preset);
        }
        return new Flyout { Content = ExamUi.VStack(12, slider, presets) };
    }

    private void UpdateClock()
    {
        if (clockLabel is null) return;
        clockLabel.Text = session.IsTimeVisible ? TextUtil.ClockString(session.SecondsRemaining) : "Show Time";
    }

    private FrameworkElement BuildFooter()
    {
        var footer = new Border { Style = ui.Style("ExamFooterBar") };
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        if (session.CurrentSection.Kind == TOEFLSectionKind.Listening)
        {
            var note = ui.Label(ExamGlyphs.Back, ExamText.ListeningFooterNote, 12, "ExamTextSecondary", "ExamIconSecondary");
            note.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(note);
        }

        if (session.CurrentPart.AllowsBack)
        {
            var back = ExamUi.Secondary("Back", ExamGlyphs.ChevronLeft);
            back.Click += (_, _) =>
            {
                if (!session.CanGoBack) return;
                StopMedia();
                session.GoBack();
            };
            refreshers.Add(() => back.IsEnabled = session.CanGoBack);
            Grid.SetColumn(back, 1);
            grid.Children.Add(back);
        }

        var next = ExamUi.Primary("Next", ExamGlyphs.ChevronRight, iconAfter: true);
        next.Click += (_, _) => OnNext();
        refreshers.Add(() => next.IsEnabled = ExamText.IsNextEnabled(session, player.IsPlaying));
        Grid.SetColumn(next, 2);
        grid.Children.Add(next);

        footer.Child = grid;
        return footer;
    }

    private void BuildQuestionBody(StackPanel body)
    {
        var question = session.CurrentQuestion;
        body.Children.Add(ui.Text(question.TaskType.Title(), 12, "ExamTextBlue", FontWeights.Bold));
        switch (question.TaskType)
        {
            case TOEFLTaskType.CompleteWords:
                body.Children.Add(ui.Bold(question.TaskType.Directions(), 15));
                body.Children.Add(BuildCompleteTheWords(session.CurrentScreenQuestions));
                break;
            case TOEFLTaskType.ReadDailyLife:
            case TOEFLTaskType.ReadAcademicPassage:
                body.Children.Add(BuildReading(question));
                break;
            case TOEFLTaskType.ListenChooseResponse:
            case TOEFLTaskType.ListenConversation:
            case TOEFLTaskType.ListenAnnouncement:
            case TOEFLTaskType.ListenAcademicTalk:
                body.Children.Add(session.NeedsStimulus(question) ? BuildStimulus(question) : BuildListeningQuestion(question));
                break;
            case TOEFLTaskType.BuildSentence:
                body.Children.Add(BuildSentenceView(question));
                break;
            case TOEFLTaskType.WriteEmail:
            case TOEFLTaskType.AcademicDiscussion:
                body.Children.Add(BuildWriting(question));
                break;
            default:
                body.Children.Add(BuildSpeaking(question));
                break;
        }
    }

    // MARK: Multiple choice

    private UIElement BuildChoices(TOEFLQuestion question)
    {
        var list = new StackPanel { Spacing = 11 };
        var group = $"choices-{question.Id}";
        foreach (var choice in question.Choices)
        {
            var selected = session.AnswerFor(question.Id).SelectedChoiceID == choice.Id;
            var radio = new RadioButton
            {
                GroupName = group,
                Content = new TextBlock { Text = choice.Text, TextWrapping = TextWrapping.Wrap, FontSize = 14 },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                IsChecked = selected,
                Padding = new Thickness(8, 0, 0, 0),
            };
            radio.Checked += (_, _) =>
            {
                if (closed || session.Phase != TOEFLExamPhase.Testing || session.CurrentQuestion.Id != question.Id) return;
                if (session.AnswerFor(question.Id).SelectedChoiceID != choice.Id) session.SelectChoice(choice.Id);
            };
            var card = new Border { Style = ui.Style(selected ? "ExamChoiceSelected" : "ExamChoice"), Padding = new Thickness(14, 8, 14, 8), Child = radio };
            refreshers.Add(() =>
            {
                var isSelected = session.AnswerFor(question.Id).SelectedChoiceID == choice.Id;
                card.Style = ui.Style(isSelected ? "ExamChoiceSelected" : "ExamChoice");
                if (radio.IsChecked != isSelected) radio.IsChecked = isSelected;
            });
            list.Children.Add(card);
        }
        return list;
    }

    // MARK: Complete the Words

    private UIElement BuildCompleteTheWords(IReadOnlyList<TOEFLQuestion> questions)
    {
        var paragraph = new CTestParagraph(questions.FirstOrDefault()?.Passage ?? "");
        var flow = new FlowPanel { Spacing = 6, LineSpacing = 12 };
        var boxes = new List<TextBox>();
        foreach (var piece in paragraph.Pieces)
        {
            switch (piece)
            {
                case CTestParagraph.TextPiece text:
                    flow.Children.Add(Word(text.Text));
                    break;
                case CTestParagraph.BlankPiece blank when blank.Index < questions.Count:
                    var question = questions[blank.Index];
                    var box = BlankBox(question, blank);
                    boxes.Add(box);
                    var group = new StackPanel { Orientation = Orientation.Horizontal };
                    if (blank.Prefix.Length > 0) group.Children.Add(Word(blank.Prefix));
                    group.Children.Add(box);
                    if (blank.Suffix.Length > 0) group.Children.Add(Word(blank.Suffix));
                    AutomationProperties.SetName(box, $"Blank {blank.Index + 1}: {blank.Prefix}, {blank.Missing} missing letters");
                    flow.Children.Add(group);
                    break;
            }
        }

        // Return moves to the next blank, like the Mac onSubmit.
        for (var i = 0; i < boxes.Count; i++)
        {
            var nextIndex = i + 1;
            boxes[i].KeyDown += (_, e) =>
            {
                if (e.Key != Windows.System.VirtualKey.Enter) return;
                e.Handled = true;
                if (nextIndex < boxes.Count) boxes[nextIndex].Focus(FocusState.Keyboard);
                else Focus(FocusState.Programmatic);
            };
        }
        if (boxes.Count > 0)
        {
            var first = boxes[0];
            void FocusFirst(object sender, RoutedEventArgs e)
            {
                first.Loaded -= FocusFirst;
                first.Focus(FocusState.Programmatic);
            }
            first.Loaded += FocusFirst;
        }
        return ui.Card(flow);

        TextBlock Word(string text)
        {
            var word = ui.Text(text, 18);
            word.TextWrapping = TextWrapping.NoWrap;
            word.VerticalAlignment = VerticalAlignment.Center;
            return word;
        }
    }

    private TextBox BlankBox(TOEFLQuestion question, CTestParagraph.BlankPiece blank)
    {
        var box = new TextBox
        {
            Text = session.AnswerFor(question.Id).TextEntry,
            PlaceholderText = ExamText.BlankPlaceholder(blank.Missing),
            Width = ExamText.BlankFieldWidth(blank.Missing) + 14,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(4, 3, 4, 3),
            FontFamily = ExamUi.Monospace,
            FontSize = 17,
            IsSpellCheckEnabled = false,
            IsTextPredictionEnabled = false,
            // Wrapping (never needed at this width) keeps the clear button from covering the letters.
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(1, 0, 1, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        box.TextChanged += (_, _) =>
        {
            if (closed) return;
            var clean = ExamText.SanitizeBlankEntry(box.Text, blank.Missing);
            if (clean != box.Text)
            {
                box.Text = clean;
                box.SelectionStart = clean.Length;
                return;
            }
            if (session.AnswerFor(question.Id).TextEntry != clean) session.UpdateTextEntry(clean, question.Id);
        };
        return box;
    }

    // MARK: Reading

    private UIElement BuildReading(TOEFLQuestion question)
    {
        var passageText = new TextBlock
        {
            Style = ui.Style("ExamText"),
            Text = question.Passage ?? "",
            FontSize = 15,
            LineHeight = 25,
            IsTextSelectionEnabled = true,
        };
        var passage = ui.Card(passageText);
        var panel = ExamUi.VStack(16, ui.Text(question.Prompt, 18, "ExamText", FontWeights.Bold), BuildChoices(question));

        // Mac ViewThatFits: passage and questions side by side when they fit (440 + 420), otherwise stacked.
        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(420) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.Children.Add(passage);
        layout.Children.Add(panel);
        bool? wide = null;
        void Arrange(double width)
        {
            var isWide = width >= 440 + 420 + 22;
            if (wide == isWide) return;
            wide = isWide;
            layout.ColumnDefinitions[1].Width = isWide ? new GridLength(420) : new GridLength(0);
            layout.ColumnSpacing = isWide ? 22 : 0;
            layout.RowSpacing = isWide ? 0 : 18;
            Grid.SetColumn(panel, isWide ? 1 : 0);
            Grid.SetRow(panel, isWide ? 0 : 1);
            passage.VerticalAlignment = VerticalAlignment.Top;
            panel.VerticalAlignment = VerticalAlignment.Top;
        }
        Arrange(1080);
        layout.SizeChanged += (_, e) => Arrange(e.NewSize.Width);
        return layout;
    }

    // MARK: Listening

    private FrameworkElement BuildSpeakerStrip(IReadOnlyList<TOEFLSpeaker> speakers, bool showsPlayback)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 22, HorizontalAlignment = HorizontalAlignment.Center };
        for (var index = 0; index < speakers.Count; index++)
        {
            var name = ui.Text(speakers[index].DisplayName, 12, "ExamTextSecondary", FontWeights.SemiBold);
            name.TextAlignment = TextAlignment.Center;
            name.HorizontalAlignment = HorizontalAlignment.Center;
            var avatar = ui.Avatar(index, 74, 44);
            avatar.HorizontalAlignment = HorizontalAlignment.Center;
            row.Children.Add(ExamUi.VStack(6, avatar, name));
        }
        if (showsPlayback)
        {
            var wave = ExamUi.VStack(6,
                ui.Icon(ExamGlyphs.Volume, 30, "ExamIconBlue"),
                new ProgressBar { IsIndeterminate = true, Width = 56 });
            wave.VerticalAlignment = VerticalAlignment.Center;
            refreshers.Add(() => wave.Visibility = player.IsPlaying ? Visibility.Visible : Visibility.Collapsed);
            row.Children.Add(wave);
        }
        return row;
    }

    /// <summary>The Mac ListeningStimulusView: the recording plays once (started in <see cref="BeginScreen"/>).</summary>
    private UIElement BuildStimulus(TOEFLQuestion question)
    {
        var stack = new StackPanel { Spacing = 22, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        if (question.Intro is { } intro)
        {
            var introText = ui.Text(intro, 17, "ExamTextSecondary");
            introText.TextAlignment = TextAlignment.Center;
            introText.HorizontalAlignment = HorizontalAlignment.Center;
            stack.Children.Add(introText);
        }
        stack.Children.Add(BuildSpeakerStrip(ExamText.StimulusSpeakers(question), showsPlayback: true));
        var status = ui.Text("", 15, "ExamTextNavy", FontWeights.SemiBold);
        status.HorizontalAlignment = HorizontalAlignment.Center;
        stack.Children.Add(status);
        var note = ui.Text(ExamText.StimulusNote, 12, "ExamTextSecondary");
        note.HorizontalAlignment = HorizontalAlignment.Center;
        stack.Children.Add(note);
        refreshers.Add(() => status.Text = ExamText.StimulusStatus(player.IsPlaying));

        var card = ui.Card(stack);
        card.MinHeight = 320;
        return card;
    }

    private UIElement BuildListeningQuestion(TOEFLQuestion question)
    {
        var stack = new StackPanel { Spacing = 16 };
        if (question.Speakers is { Count: > 0 } speakers && question.TaskType != TOEFLTaskType.ListenChooseResponse)
        {
            var strip = BuildSpeakerStrip(speakers, showsPlayback: false);
            strip.HorizontalAlignment = HorizontalAlignment.Left;
            strip.Margin = new Thickness(0, 0, 0, 4);
            stack.Children.Add(strip);
        }
        stack.Children.Add(ui.Text(question.Prompt, 18, "ExamText", FontWeights.Bold));
        stack.Children.Add(BuildChoices(question));
        return stack;
    }

    // MARK: Build a Sentence

    private UIElement BuildSentenceView(TOEFLQuestion question)
    {
        var stack = new StackPanel { Spacing = 22 };
        stack.Children.Add(ui.Bold(question.TaskType.Directions(), 15));
        if (question.Context is { } context)
        {
            var bubble = new Border { Style = ui.Style("ExamBlueBubble"), Child = ui.Text(context, 17), HorizontalAlignment = HorizontalAlignment.Left };
            stack.Children.Add(ExamUi.IconRow(ui.Avatar(0, 44, 26), bubble));
        }

        var slots = new FlowPanel { Spacing = 7, LineSpacing = 10 };
        var reply = new Border
        {
            Style = ui.Style("ExamSurface"),
            Padding = new Thickness(14),
            Child = ExamUi.IconRow(ui.Avatar(1, 44, 26), slots, center: true),
        };
        stack.Children.Add(reply);

        var clear = ExamUi.Secondary("Clear");
        clear.Click += (_, _) =>
        {
            if (session.CurrentQuestion.Id == question.Id) session.ClearSentence();
        };
        stack.Children.Add(ExamUi.Spread(ui.Bold("Words", 15), clear));

        var tokens = new FlowPanel { Spacing = 9, LineSpacing = 9 };
        stack.Children.Add(tokens);

        IReadOnlyList<string>? shown = null;
        refreshers.Add(() =>
        {
            var filled = session.AnswerFor(question.Id).OrderedTokens;
            clear.IsEnabled = filled.Count > 0;
            if (shown is not null && shown.SequenceEqual(filled)) return;
            shown = filled.ToList();

            slots.Children.Clear();
            foreach (var piece in BuildSentence.TemplatePieces(question))
            {
                if (piece.Slot is not { } slot)
                {
                    var word = ui.Text(piece.Text, 17);
                    word.TextWrapping = TextWrapping.NoWrap;
                    word.VerticalAlignment = VerticalAlignment.Center;
                    slots.Children.Add(word);
                    continue;
                }
                var token = slot < filled.Count ? filled[slot] : null;
                var label = ui.Text(token is null ? " " : BuildSentence.SlotText(question, slot, token), 17);
                label.TextWrapping = TextWrapping.NoWrap;
                label.HorizontalAlignment = HorizontalAlignment.Center;
                label.VerticalAlignment = VerticalAlignment.Center;
                var box = new Border
                {
                    Style = ui.Style(token is null ? "ExamSlotEmpty" : "ExamSlotFilled"),
                    Padding = new Thickness(8, 4, 8, 4),
                    Child = label,
                };
                var button = new Button
                {
                    Content = box,
                    Padding = new Thickness(0),
                    BorderThickness = new Thickness(0),
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                };
                AutomationProperties.SetName(button, token is null ? $"Blank {slot + 1}, empty" : $"Blank {slot + 1}: {token}. Remove");
                button.Click += (_, _) =>
                {
                    if (token is not null && session.CurrentQuestion.Id == question.Id) session.RemoveSentenceToken(slot);
                };
                slots.Children.Add(button);
            }

            tokens.Children.Clear();
            foreach (var token in BuildSentence.RemainingTokens(question, filled))
            {
                var button = new Button { Content = token, FontSize = 16, Padding = new Thickness(14, 8, 14, 8) };
                button.Click += (_, _) =>
                {
                    if (session.CurrentQuestion.Id == question.Id) session.AddSentenceToken(token);
                };
                tokens.Children.Add(button);
            }
        });
        return stack;
    }

    // MARK: Writing

    private UIElement BuildWriting(TOEFLQuestion question)
    {
        var stack = new StackPanel { Spacing = 18 };
        stack.Children.Add(ui.Text(question.TaskType.Directions(), 13, "ExamTextSecondary"));
        if (question.TaskType == TOEFLTaskType.WriteEmail)
        {
            var prompt = ui.Text(question.Prompt, 14);
            prompt.LineHeight = 22;
            stack.Children.Add(ui.Card(prompt));
            if (question.Passage is { } header)
            {
                var headerText = ui.Text(header, 13, "ExamTextSecondary");
                headerText.FontFamily = ExamUi.Monospace;
                stack.Children.Add(headerText);
            }
        }
        else
        {
            var prompt = ui.Text(question.Prompt, 14);
            prompt.LineHeight = 22;
            stack.Children.Add(ui.Card(ExamUi.IconRow(ui.Icon(ExamGlyphs.Person, 32, "ExamIconNavy"), prompt)));
            foreach (var post in ExamText.DiscussionPosts(question))
            {
                var text = ui.Text(post, 13);
                text.LineHeight = 20;
                stack.Children.Add(new Border
                {
                    Style = ui.Style("ExamSurface"),
                    Padding = new Thickness(12),
                    Child = ExamUi.IconRow(ui.Icon(ExamGlyphs.Person, 24, "ExamIconTeal"), text),
                });
            }
        }

        var wordCount = ui.Text("", 12, "ExamTextSecondary");
        stack.Children.Add(ExamUi.Spread(ui.Bold("Your response", 15), wordCount));

        var editor = new TextBox
        {
            Text = session.AnswerFor(question.Id).WrittenResponse,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 280,
            FontSize = 14,
            Padding = new Thickness(10),
            // The real test has no spelling help.
            IsSpellCheckEnabled = false,
            IsTextPredictionEnabled = false,
        };
        AutomationProperties.SetName(editor, "Your response");
        ScrollViewer.SetVerticalScrollBarVisibility(editor, ScrollBarVisibility.Auto);
        editor.TextChanged += (_, _) =>
        {
            if (closed || session.CurrentQuestion.Id != question.Id) return;
            // TextBox uses \r for line breaks; store \n like the Mac editor.
            var text = editor.Text.Replace("\r\n", "\n").Replace('\r', '\n');
            if (session.AnswerFor(question.Id).WrittenResponse != text) session.UpdateWrittenResponse(text);
        };
        refreshers.Add(() => wordCount.Text = $"{ExamText.WordCount(session.AnswerFor(question.Id).WrittenResponse)} words");
        stack.Children.Add(editor);
        return stack;
    }

    // MARK: Speaking

    private UIElement BuildSpeaking(TOEFLQuestion question)
    {
        var stack = new StackPanel { Spacing = 22 };
        stack.Children.Add(ui.Text(question.TaskType.Directions(), 13, "ExamTextSecondary"));
        var isFirstInSet = (question.SetIndex ?? 0) == 0;
        stack.Children.Add(question.TaskType == TOEFLTaskType.ListenRepeat
            ? BuildRepeatIllustration(question, isFirstInSet)
            : BuildInterviewer(question, isFirstInSet));
        stack.Children.Add(BuildSpeakingStatus());
        return stack;
    }

    private UIElement BuildRepeatIllustration(TOEFLQuestion question, bool isFirstInSet)
    {
        var stack = new StackPanel { Spacing = 14 };
        if (question.Setting is { } setting)
            stack.Children.Add(ui.Label(ExamGlyphs.Map, setting, 20, "ExamTextNavy", "ExamIconNavy", FontWeights.Bold));
        if (isFirstInSet && question.Intro is { } intro)
            stack.Children.Add(ui.Text(intro, 14, "ExamTextSecondary"));
        var stops = new FlowPanel { Spacing = 8, LineSpacing = 8 };
        var current = question.SetIndex ?? 0;
        var index = 0;
        foreach (var stop in question.Stops ?? [])
        {
            var isCurrent = index == current;
            var glyph = isCurrent ? ExamGlyphs.Location : (index < current ? ExamGlyphs.CheckMark : ExamGlyphs.CircleRing);
            var label = ui.Label(glyph, stop, 13, isCurrent ? "ExamTextAction" : "ExamTextSecondary",
                isCurrent ? "ExamIconBlue" : "ExamIconSecondary", FontWeights.SemiBold, 6);
            stops.Children.Add(new Border { Style = ui.Style(isCurrent ? "ExamChipCurrent" : "ExamChip"), Child = label });
            index++;
        }
        if (stops.Children.Count > 0) stack.Children.Add(stops);
        if (question.Speakers is { Count: > 0 } speakers)
            stack.Children.Add(ui.Label(ExamGlyphs.Person, speakers[0].DisplayName, 15, "ExamTextTeal", "ExamIconTeal", FontWeights.SemiBold));
        return ui.Card(stack);
    }

    private UIElement BuildInterviewer(TOEFLQuestion question, bool isFirstInSet)
    {
        var stack = new StackPanel { Spacing = 14 };
        if (isFirstInSet && question.Intro is { } intro)
            stack.Children.Add(ui.Text(intro, 14, "ExamTextSecondary"));
        var portrait = new Border
        {
            Style = ui.Style("ExamInterviewer"),
            Child = new FontIcon
            {
                Glyph = ExamGlyphs.Person,
                FontSize = 70,
                Style = ui.Style("ExamIconOnBrand"),
                Opacity = 0.9,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        var names = ExamUi.VStack(6,
            ui.Text(ExamText.InterviewerTitle(question), 18, "ExamText", FontWeights.Bold),
            ui.Text(ExamText.InterviewQuestionLabel(question), 14, "ExamTextSecondary"));
        stack.Children.Add(ExamUi.IconRow(portrait, names, 16, center: true));
        return ui.Card(stack);
    }

    /// <summary>The Mac status card: countdown ring while recording, stage title, and the microphone level.</summary>
    private UIElement BuildSpeakingStatus()
    {
        var ring = new Grid { Width = 84, Height = 84 };
        ring.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse { Style = ui.Style("ExamRingTrack") });
        var progress = new ProgressRing
        {
            IsIndeterminate = false,
            IsActive = true,
            Minimum = 0,
            Maximum = 1,
            Width = 84,
            Height = 84,
            Foreground = new SolidColorBrush(MeterColor("red")),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
        };
        var countdown = new TextBlock
        {
            FontSize = 26,
            FontWeight = FontWeights.Bold,
            Style = ui.Style("ExamText"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var stageIcon = new FontIcon
        {
            Glyph = ExamGlyphs.Headphones,
            FontSize = 28,
            Style = ui.Style("ExamIconBlue"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ring.Children.Add(progress);
        ring.Children.Add(countdown);
        ring.Children.Add(stageIcon);

        var title = ui.Text("", 20, "ExamTextNavy", FontWeights.Bold);
        var level = new ProgressBar
        {
            Minimum = 0,
            Maximum = 1,
            Width = 220,
            HorizontalAlignment = HorizontalAlignment.Left,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x05, 0x9E, 0xAB)),
        };
        AutomationProperties.SetName(level, "Microphone level");
        var texts = ExamUi.VStack(6, title, level);
        texts.VerticalAlignment = VerticalAlignment.Center;

        var flow = speakingFlow;
        refreshers.Add(() =>
        {
            if (flow is null) return;
            var recording = flow.Stage == SpeakingStage.Recording;
            progress.Visibility = recording ? Visibility.Visible : Visibility.Collapsed;
            countdown.Visibility = recording ? Visibility.Visible : Visibility.Collapsed;
            stageIcon.Visibility = recording ? Visibility.Collapsed : Visibility.Visible;
            progress.Value = flow.Remaining / Math.Max(1, flow.Total);
            countdown.Text = $"{(int)Math.Ceiling(flow.Remaining)}";
            stageIcon.Glyph = flow.Stage == SpeakingStage.Saving ? ExamGlyphs.CheckMark : ExamGlyphs.Headphones;
            title.Text = flow.StatusTitle;
            title.Style = ui.Style(recording ? "ExamTextCritical" : "ExamTextNavy");
            level.Visibility = recording ? Visibility.Visible : Visibility.Collapsed;
            level.Value = recorder.Level;
        });
        return ui.Card(ExamUi.IconRow(ring, texts, 18, center: true));
    }

    // MARK: - Scoring and report

    private UIElement BuildScoring()
    {
        var message = ui.Text("", 14, "ExamTextSecondary");
        message.TextAlignment = TextAlignment.Center;
        var title = ui.Text(ExamText.ScoringTitle, 24, "ExamText", FontWeights.Bold);
        title.TextAlignment = TextAlignment.Center;
        var stack = new StackPanel
        {
            Spacing = 18,
            MaxWidth = 560,
            Padding = new Thickness(40),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new ProgressRing { IsActive = true, Width = 56, Height = 56, HorizontalAlignment = HorizontalAlignment.Center },
                title,
                message,
            },
        };
        refreshers.Add(() => message.Text = ExamText.ScoringMessage(session.PendingScores));
        return stack;
    }

    private UIElement BuildReport()
    {
        if (session.Result is not { } result) return new Grid();
        var report = new ScoreReportView(result);
        report.CloseRequested += (_, _) =>
        {
            if (!closed) onComplete(result);
        };
        return report;
    }
}
