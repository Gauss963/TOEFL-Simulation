using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI.Text;

namespace TOEFLSimulation.App.Controls.Guide;

/// <summary>Guide.mistakes: a struck-through mistake, its correction, and why.</summary>
public sealed partial class GuideMistakes : UserControl
{
    public GuideMistakes((string Wrong, string Right, string Why)[] rows)
    {
        var stack = new StackPanel { Spacing = 10 };
        foreach (var row in rows)
        {
            var wrong = GuideTheme.Text(row.Wrong, GuideTheme.Subheadline);
            wrong.TextDecorations = TextDecorations.Strikethrough;
            var right = GuideTheme.Text(row.Right, GuideTheme.Subheadline, FontWeights.Bold);
            var why = GuideTheme.Secondary(row.Why);
            why.Margin = new Thickness(24, 0, 0, 0);
            stack.Children.Add(GuideTheme.VStack(3,
                IconRow(GuideIcons.XmarkCircleFill, "GuideCriticalIcon", wrong),
                IconRow(GuideIcons.CheckmarkCircleFill, "GuideTealIcon", right),
                why));
        }
        Content = stack;
    }

    internal static Grid IconRow(string glyph, string? iconStyle, TextBlock text, double iconSize = 14)
    {
        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var icon = GuideTheme.Icon(glyph, iconSize, iconStyle);
        icon.VerticalAlignment = VerticalAlignment.Top;
        icon.Margin = new Thickness(0, 3, 0, 0);
        grid.Children.Add(icon);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        return grid;
    }
}

/// <summary>Guide.checklist: checkbox-style items.</summary>
public sealed partial class GuideChecklist : UserControl
{
    public GuideChecklist(string[] items)
    {
        var stack = new StackPanel { Spacing = 6 };
        foreach (var item in items)
            stack.Children.Add(GuideMistakes.IconRow(GuideIcons.CheckSquare, "GuidePrimaryIcon", GuideTheme.Text(item, GuideTheme.Subheadline)));
        Content = stack;
    }
}

/// <summary>Guide.note: a small secondary note with an info icon.</summary>
public sealed partial class GuideNote : UserControl
{
    public GuideNote(string text) =>
        Content = GuideMistakes.IconRow(GuideIcons.Info, "GuideSecondaryIcon", GuideTheme.Secondary(text), GuideTheme.Caption);
}

/// <summary>Guide.legend: a colored dot and a caption.</summary>
public sealed partial class GuideLegend : UserControl
{
    /// <param name="color">"Action" or "Teal".</param>
    public GuideLegend(string text, string color)
    {
        var dot = new Microsoft.UI.Xaml.Shapes.Ellipse { Width = 8, Height = 8, Style = GuideTheme.Style($"Guide{color}Dot"), VerticalAlignment = VerticalAlignment.Center };
        var label = GuideTheme.Secondary(text);
        label.TextWrapping = TextWrapping.NoWrap;
        label.VerticalAlignment = VerticalAlignment.Center;
        Content = GuideTheme.HStack(5, dot, label);
    }
}
