using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace TOEFLSimulation.App.Controls.Guide;

/// <summary>Guide.structure: numbered steps, each with a title, an explanation, and an optional example.</summary>
public sealed partial class GuideStructure : UserControl
{
    public GuideStructure((string Number, string Title, string Detail, string Example)[] steps)
    {
        var stack = new StackPanel { Spacing = 14 };
        foreach (var step in steps)
        {
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var number = GuideTheme.Text(step.Number, GuideTheme.Headline, FontWeights.SemiBold, "GuideOnAccentText", wrap: false);
            number.HorizontalAlignment = HorizontalAlignment.Center;
            number.VerticalAlignment = VerticalAlignment.Center;
            var circle = new Border
            {
                Style = GuideTheme.Style("GuideActionCircle"),
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                VerticalAlignment = VerticalAlignment.Top,
                Child = number,
            };
            row.Children.Add(circle);
            var body = new StackPanel { Spacing = 4 };
            body.Children.Add(GuideTheme.Text(step.Title, GuideTheme.Headline, FontWeights.SemiBold));
            body.Children.Add(GuideTheme.Text(step.Detail, GuideTheme.Subheadline, style: "GuideSecondaryText"));
            if (step.Example.Length > 0)
            {
                body.Children.Add(new Border
                {
                    Style = GuideTheme.Style("GuideInsetBorder"),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(8),
                    Child = GuideTheme.Text(step.Example, GuideTheme.Subheadline, italic: true, selectable: true),
                });
            }
            Grid.SetColumn(body, 1);
            row.Children.Add(body);
            stack.Children.Add(row);
        }
        Content = stack;
    }
}
