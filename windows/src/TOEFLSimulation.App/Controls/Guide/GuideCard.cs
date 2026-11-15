using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace TOEFLSimulation.App.Controls.Guide;

/// <summary>Guide.card: a titled card with an icon, holding guide content.</summary>
public sealed partial class GuideCard : UserControl
{
    public GuideCard(string title, string icon, UIElement[] content)
    {
        Body = new StackPanel { Spacing = 12 };
        var header = GuideTheme.HStack(8,
            GuideTheme.Icon(GuideIcons.Glyph(icon), GuideTheme.Title3, "GuideNavyIcon"),
            GuideTheme.Text(title, GuideTheme.Title3, FontWeights.Bold, "GuideNavyText"));
        Body.Children.Add(header);
        foreach (var element in content) Body.Children.Add(element);
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        Content = GuideTheme.Card(Body);
    }

    /// <summary>The card's content stack (title first).</summary>
    public StackPanel Body { get; }
}
