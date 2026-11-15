using System.ComponentModel;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TOEFLSimulation.Core;
using TOEFLSimulation.Core.Audio;

namespace TOEFLSimulation.App.Controls.Guide;

/// <summary>
/// The Speaking guide's Listen and Repeat practice (SpeakingGuideView's ListenRepeatTrainer): plays one sentence at a time
/// from the bank; the sentence, split into meaning groups, can be shown after trying to repeat it.
/// </summary>
public sealed partial class ListenRepeatTrainerView : UserControl
{
    private readonly ListenRepeatTrainer trainer;
    private readonly ExamAudioPlayer player;
    private readonly StackPanel stack = new() { Spacing = 12 };
    private Button? playButton;

    public ListenRepeatTrainerView(ListenRepeatTrainer trainer, ExamAudioPlayer player)
    {
        this.trainer = trainer;
        this.player = player;
        Content = stack;
        Render();
        Loaded += (_, _) =>
        {
            trainer.PropertyChanged += OnTrainerChanged;
            player.PropertyChanged += OnPlayerChanged;
            UpdatePlayButton();
        };
        Unloaded += (_, _) =>
        {
            trainer.PropertyChanged -= OnTrainerChanged;
            player.PropertyChanged -= OnPlayerChanged;
            trainer.Disappear();
        };
    }

    private void OnTrainerChanged(object? sender, PropertyChangedEventArgs e) => OnUIThread(Render);

    private void OnPlayerChanged(object? sender, PropertyChangedEventArgs e) => OnUIThread(UpdatePlayButton);

    private void OnUIThread(Action action)
    {
        if (DispatcherQueue.HasThreadAccess) action();
        else DispatcherQueue.TryEnqueue(() => action());
    }

    private void Render()
    {
        stack.Children.Clear();
        playButton = null;
        if (trainer.Sentence is not { } sentence)
        {
            stack.Children.Add(GuideTheme.Text("No Listen and Repeat items in the bank.", style: "GuideSecondaryText"));
            return;
        }
        if (trainer.Caption is { } caption)
            stack.Children.Add(GuideTheme.Text(caption, GuideTheme.Subheadline, FontWeights.Bold));
        stack.Children.Add(GuideTheme.Secondary(trainer.Instructions));

        playButton = new Button { Style = GuideTheme.AppStyle("AccentButtonStyle") };
        playButton.Click += (_, _) => _ = trainer.PlayAsync();
        var reveal = new Button { Content = trainer.Revealed ? "Hide sentence" : "Show sentence" };
        reveal.Click += (_, _) => trainer.ToggleReveal();
        var next = new Button { Content = "Next sentence" };
        next.Click += (_, _) => trainer.Advance();
        var newSet = new HyperlinkButton { Content = "New set" };
        newSet.Click += (_, _) => trainer.NewSet();
        var buttons = new GuideFlowPanel(10, 10);
        foreach (var button in new UIElement[] { playButton, reveal, next, newSet }) buttons.Children.Add(button);
        stack.Children.Add(buttons);
        UpdatePlayButton();

        if (trainer.GroupedText is { } grouped)
        {
            stack.Children.Add(new Border
            {
                Style = GuideTheme.Style("GuideInsetBorder"),
                Padding = new Thickness(12),
                Child = GuideTheme.Text(grouped, GuideTheme.Title3, selectable: true),
            });
            stack.Children.Add(GuideTheme.Secondary(SpeakingGuide.MeaningGroupsNote));
        }
    }

    private void UpdatePlayButton()
    {
        if (playButton is null) return;
        playButton.Content = player.IsPlaying ? "Playing…" : "Play sentence";
        playButton.IsEnabled = !player.IsPlaying;
    }
}
