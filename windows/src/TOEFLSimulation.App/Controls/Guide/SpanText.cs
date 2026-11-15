using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using TOEFLSimulation.Core;

namespace TOEFLSimulation.App.Controls.Guide;

/// <summary>One run of <see cref="SpanText"/>: plain text, or text in a brand color (optionally bold).</summary>
public sealed record TextSpan(string Text, GuideColor? Color = null, bool Bold = false);

/// <summary>
/// Text made of differently colored runs (SwiftUI <c>Text + Text</c> or an AttributedString). Runs cannot take styles, so the
/// colors are resolved for the current light/dark theme and rebuilt when the theme changes.
/// </summary>
public sealed partial class SpanText : UserControl
{
    private readonly TextBlock block;
    private readonly IReadOnlyList<TextSpan> spans;

    public SpanText(IReadOnlyList<TextSpan> spans, double size = GuideTheme.Body, FontFamily? font = null, bool selectable = false, double lineSpacing = 0)
    {
        this.spans = spans;
        block = new TextBlock { FontSize = size, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = selectable };
        if (font is not null) block.FontFamily = font;
        if (lineSpacing > 0) block.LineHeight = size * 1.33 + lineSpacing;
        Content = block;
        Build(Application.Current.RequestedTheme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light);
        Loaded += (_, _) => Build(ActualTheme);
        ActualThemeChanged += (_, _) => Build(ActualTheme);
    }

    private void Build(ElementTheme theme)
    {
        block.Inlines.Clear();
        foreach (var span in spans)
        {
            var run = new Run { Text = span.Text };
            if (span.Color is { } color) run.Foreground = new SolidColorBrush(GuideTheme.RunColor(color, theme));
            if (span.Bold) run.FontWeight = FontWeights.Bold;
            block.Inlines.Add(run);
        }
    }
}
