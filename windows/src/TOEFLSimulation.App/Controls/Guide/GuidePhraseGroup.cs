using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace TOEFLSimulation.App.Controls.Guide;

/// <summary>Guide.phraseGroup: a teal heading over indented, selectable phrases.</summary>
public sealed partial class GuidePhraseGroup : UserControl
{
    public GuidePhraseGroup(string title, string[] items)
    {
        var stack = new StackPanel { Spacing = 5, Padding = new Thickness(0, 0, 0, 4) };
        stack.Children.Add(GuideTheme.Text(title, GuideTheme.Subheadline, FontWeights.Bold, "GuideTealText"));
        foreach (var item in items)
        {
            var text = GuideTheme.Text(item, GuideTheme.Subheadline, selectable: true);
            text.Margin = new Thickness(10, 0, 0, 0);
            stack.Children.Add(text);
        }
        Content = stack;
    }
}
