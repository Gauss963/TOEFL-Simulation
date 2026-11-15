using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace TOEFLSimulation.App.Controls.Guide;

/// <summary>Guide.facts (and Guide.phrases with selectable text): a two-column grid of teal labels and values.</summary>
public sealed partial class GuideFacts : UserControl
{
    public GuideFacts((string Label, string Text)[] rows) : this(rows, selectable: false) { }

    internal GuideFacts((string Label, string Text)[] rows, bool selectable)
    {
        var grid = new Grid { ColumnSpacing = 16, RowSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var i = 0; i < rows.Length; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var label = GuideTheme.Text(rows[i].Label, GuideTheme.Subheadline, FontWeights.Bold, "GuideTealText", wrap: false);
            var value = GuideTheme.Text(rows[i].Text, GuideTheme.Subheadline, selectable: selectable);
            Grid.SetRow(label, i);
            Grid.SetRow(value, i);
            Grid.SetColumn(value, 1);
            grid.Children.Add(label);
            grid.Children.Add(value);
        }
        Content = grid;
    }
}

/// <summary>Guide.phrases: labeled example sentences that can be selected and copied.</summary>
public sealed partial class GuidePhrases : UserControl
{
    public GuidePhrases((string Label, string Text)[] rows) => Content = new GuideFacts(rows, selectable: true);
}
