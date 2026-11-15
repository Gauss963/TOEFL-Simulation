using System.ComponentModel;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TOEFLSimulation.Core;
using TOEFLSimulation.Core.Audio;

namespace TOEFLSimulation.App.Controls.Guide;

/// <summary>
/// The Speaking guide's interview practice (SpeakingGuideView's InterviewTrainer): plays a question from the bank, then counts
/// down 45 seconds for a spoken answer and plays the end tone.
/// </summary>
public sealed partial class InterviewTrainerView : UserControl
{
    private readonly InterviewTrainer trainer;
    private readonly ExamAudioPlayer player;
    private readonly StackPanel stack = new() { Spacing = 12 };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private Button? playButton;
    private TextBlock? countdown;

    public InterviewTrainerView(InterviewTrainer trainer, ExamAudioPlayer player)
    {
        this.trainer = trainer;
        this.player = player;
        Content = stack;
        timer.Tick += (_, _) =>
        {
            trainer.Tick();
            if (trainer.Remaining is null) timer.Stop();
        };
        Render();
        Loaded += (_, _) =>
        {
            trainer.PropertyChanged += OnTrainerChanged;
            player.PropertyChanged += OnPlayerChanged;
            UpdateState();
        };
        Unloaded += (_, _) =>
        {
            trainer.PropertyChanged -= OnTrainerChanged;
            player.PropertyChanged -= OnPlayerChanged;
            timer.Stop();
            trainer.Disappear();
        };
    }

    private void OnTrainerChanged(object? sender, PropertyChangedEventArgs e) => OnUIThread(() =>
    {
        if (e.PropertyName == nameof(InterviewTrainer.Remaining)) UpdateState();
        else Render();
    });

    private void OnPlayerChanged(object? sender, PropertyChangedEventArgs e) => OnUIThread(UpdateState);

    private void OnUIThread(Action action)
    {
        if (DispatcherQueue.HasThreadAccess) action();
        else DispatcherQueue.TryEnqueue(() => action());
    }

    private void Render()
    {
        stack.Children.Clear();
        playButton = null;
        countdown = null;
        if (trainer.Question is not { } question)
        {
            stack.Children.Add(GuideTheme.Text("No interview items in the bank.", style: "GuideSecondaryText"));
            return;
        }
        stack.Children.Add(GuideTheme.Text(trainer.Caption, GuideTheme.Subheadline, FontWeights.Bold));
        if (trainer.Intro is { } intro) stack.Children.Add(GuideTheme.Secondary(intro));
        stack.Children.Add(GuideTheme.Secondary(InterviewTrainer.Instructions));

        playButton = new Button { Style = GuideTheme.AppStyle("AccentButtonStyle") };
        playButton.Click += (_, _) => _ = trainer.PlayAndCountAsync();
        var reveal = new Button { Content = trainer.Revealed ? "Hide question" : "Show question" };
        reveal.Click += (_, _) => trainer.ToggleReveal();
        var next = new Button { Content = "Next question" };
        next.Click += (_, _) => trainer.Advance();
        var newSet = new HyperlinkButton { Content = "New set" };
        newSet.Click += (_, _) => trainer.NewSet();
        var buttons = new GuideFlowPanel(10, 10);
        foreach (var button in new UIElement[] { playButton, reveal, next, newSet }) buttons.Children.Add(button);
        countdown = GuideTheme.Text("", 34, FontWeights.Bold, "GuideNavyText", wrap: false);
        stack.Children.Add(GuideTheme.SpacedRow(buttons, countdown));
        UpdateState();

        if (trainer.Revealed && question.AudioScript is { } text)
        {
            stack.Children.Add(new Border
            {
                Style = GuideTheme.Style("GuideInsetBorder"),
                Padding = new Thickness(12),
                Child = GuideTheme.Text(text, GuideTheme.Body),
            });
        }
    }

    private void UpdateState()
    {
        if (playButton is not null)
        {
            playButton.Content = player.IsPlaying ? "Playing…" : "Play question";
            playButton.IsEnabled = trainer.CanPlay;
        }
        if (countdown is not null)
        {
            if (trainer.Remaining is { } remaining)
            {
                countdown.Visibility = Visibility.Visible;
                countdown.Text = ((int)Math.Ceiling(remaining)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                countdown.Style = GuideTheme.Style(remaining <= 10 ? "GuideCriticalText" : "GuideNavyText");
            }
            else
            {
                countdown.Visibility = Visibility.Collapsed;
            }
        }
        if (trainer.Remaining is not null && !timer.IsEnabled) timer.Start();
    }
}
