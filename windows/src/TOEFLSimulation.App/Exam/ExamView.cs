using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TOEFLSimulation.Core;

namespace TOEFLSimulation.App.Exam;

/// <summary>The full-window practice test (being ported from ExamView.swift).</summary>
public sealed partial class ExamView : UserControl
{
    private readonly Action onExit;

    public ExamView(ExamMode mode, Action<TOEFLExamResult> onComplete, Action onExit)
    {
        this.onExit = onExit;
        var exit = new Button { Content = "Exit" };
        exit.Click += (_, _) => this.onExit();
        Content = new StackPanel
        {
            Margin = new Thickness(34),
            Spacing = 16,
            Children = { exit, new TextBlock { Text = mode.Title() } },
        };
    }

    /// <summary>Stops audio, recording, and timers when the test closes.</summary>
    public void Shutdown()
    {
    }
}
