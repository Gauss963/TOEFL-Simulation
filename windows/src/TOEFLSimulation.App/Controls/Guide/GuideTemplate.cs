using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace TOEFLSimulation.App.Controls.Guide;

/// <summary>Guide.template: a selectable monospaced template with a reminder to adapt it.</summary>
public sealed partial class GuideTemplate : UserControl
{
    public GuideTemplate(string text)
    {
        var box = new Border
        {
            Style = GuideTheme.Style("GuideInsetStrokedBorder"),
            Padding = new Thickness(14),
            Child = GuideTheme.Text(text, GuideTheme.Body, selectable: true, font: GuideTheme.Monospaced),
        };
        Content = GuideTheme.VStack(8, box,
            GuideTheme.Secondary("可選取文字複製；模板只是骨架，內容一定要依題目情境改寫，不要原封不動背誦。"));
    }
}
