using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace TOEFLSimulation.App.Controls.Guide;

/// <summary>Guide.phraseTable: a table with caption headers, a divider, and selectable cells (first column bold).</summary>
public sealed partial class GuidePhraseTable : UserControl
{
    public GuidePhraseTable(string[] headers, string[][] rows)
    {
        var grid = new Grid { ColumnSpacing = 14, RowSpacing = 8 };
        foreach (var _ in headers) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var column = 0; column < headers.Length; column++)
        {
            var header = GuideTheme.Text(headers[column], GuideTheme.Caption, FontWeights.Bold, "GuideSecondaryText");
            Grid.SetColumn(header, column);
            grid.Children.Add(header);
        }
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var divider = GuideTheme.Divider();
        Grid.SetRow(divider, 1);
        Grid.SetColumnSpan(divider, Math.Max(1, headers.Length));
        grid.Children.Add(divider);
        for (var row = 0; row < rows.Length; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (var column = 0; column < rows[row].Length && column < headers.Length; column++)
            {
                var cell = GuideTheme.Text(rows[row][column], GuideTheme.Subheadline, column == 0 ? FontWeights.Bold : null, selectable: true);
                Grid.SetRow(cell, row + 2);
                Grid.SetColumn(cell, column);
                grid.Children.Add(cell);
            }
        }
        Content = grid;
    }
}
