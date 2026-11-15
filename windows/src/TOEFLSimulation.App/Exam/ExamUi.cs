using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Text;

namespace TOEFLSimulation.App.Exam;

/// <summary>Segoe Fluent Icons glyphs standing in for the Mac exam's SF Symbols.</summary>
internal static class ExamGlyphs
{
    public const string Cancel = "";
    public const string ChevronLeft = "";
    public const string ChevronRight = "";
    public const string Volume = "";
    public const string Timer = "";
    public const string List = "";
    public const string Branch = "";
    public const string CheckCircle = "";
    public const string CheckMark = "";
    public const string Info = "";
    public const string Microphone = "";
    public const string Warning = "";
    public const string Back = "";
    public const string Person = "";
    public const string Map = "";
    public const string Location = "";
    public const string CircleRing = "";
    public const string Headphones = "";
    public const string Book = "";
    public const string Keyboard = "";
    public const string Edit = "";
    public const string Chart = "";
    public const string Library = "";

    public static string Section(TOEFLSimulation.Core.TOEFLSectionKind kind) => kind switch
    {
        TOEFLSimulation.Core.TOEFLSectionKind.Reading => Book,
        TOEFLSimulation.Core.TOEFLSectionKind.Listening => Headphones,
        TOEFLSimulation.Core.TOEFLSectionKind.Writing => Keyboard,
        _ => Microphone,
    };
}

/// <summary>Builds the elements of the code-built exam screens with the theme-aware styles in ExamResources.xaml.</summary>
internal sealed class ExamUi(ResourceDictionary styles)
{
    public Style Style(string key) => (Style)styles[key];

    public static Style AppStyle(string key) => (Style)Application.Current.Resources[key];

    public TextBlock Text(string text, double size = 14, string style = "ExamText", FontWeight? weight = null)
    {
        var block = new TextBlock { Style = Style(style), Text = text, FontSize = size };
        if (weight is { } w) block.FontWeight = w;
        return block;
    }

    public TextBlock Bold(string text, double size = 14, string style = "ExamText") => Text(text, size, style, FontWeights.SemiBold);

    public FontIcon Icon(string glyph, double size = 16, string style = "ExamIcon") =>
        new() { Style = Style(style), Glyph = glyph, FontSize = size };

    /// <summary>SwiftUI <c>Label</c>: an icon followed by text.</summary>
    public StackPanel Label(string glyph, string text, double size = 14, string textStyle = "ExamText", string iconStyle = "ExamIcon",
        FontWeight? weight = null, double spacing = 8)
    {
        var icon = Icon(glyph, size, iconStyle);
        icon.VerticalAlignment = VerticalAlignment.Center;
        var label = Text(text, size, textStyle, weight);
        label.VerticalAlignment = VerticalAlignment.Center;
        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing, Children = { icon, label } };
    }

    public Border Card(UIElement child, string style = "ExamCard") => new() { Style = Style(style), Child = child };

    public static StackPanel VStack(double spacing, params UIElement[] children)
    {
        var panel = new StackPanel { Spacing = spacing };
        foreach (var child in children) panel.Children.Add(child);
        return panel;
    }

    public static StackPanel HStack(double spacing, params UIElement[] children)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing };
        foreach (var child in children) panel.Children.Add(child);
        return panel;
    }

    /// <summary>Button content: optional icon and a title.</summary>
    public static UIElement ButtonContent(string title, string? glyph, bool iconAfter = false)
    {
        if (glyph is null) return new TextBlock { Text = title };
        var icon = new FontIcon { Glyph = glyph, FontSize = 14 };
        var text = new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center };
        return iconAfter ? HStack(8, text, icon) : HStack(8, icon, text);
    }

    /// <summary>TOEFLPrimaryButtonStyle: the accent (brand action) button.</summary>
    public static Button Primary(string title, string? glyph = null, bool iconAfter = false)
    {
        var button = new Button
        {
            Style = AppStyle("AccentButtonStyle"),
            Content = ButtonContent(title, glyph, iconAfter),
            Padding = new Thickness(22, 9, 22, 9),
            FontWeight = FontWeights.SemiBold,
        };
        AutomationProperties.SetName(button, title);
        return button;
    }

    /// <summary>TOEFLSecondaryButtonStyle: the standard button.</summary>
    public static Button Secondary(string title, string? glyph = null, bool iconAfter = false)
    {
        var button = new Button
        {
            Content = ButtonContent(title, glyph, iconAfter),
            Padding = new Thickness(18, 8, 18, 8),
        };
        AutomationProperties.SetName(button, title);
        return button;
    }

    /// <summary>A horizontal layout with a stretching middle (SwiftUI <c>HStack { a; Spacer(); b }</c>).</summary>
    public static Grid Spread(FrameworkElement leading, FrameworkElement trailing, double spacing = 12)
    {
        var grid = new Grid { ColumnSpacing = spacing };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        if (leading is FrameworkElement l) l.VerticalAlignment = VerticalAlignment.Center;
        if (trailing is FrameworkElement t) t.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(trailing, 1);
        grid.Children.Add(leading);
        grid.Children.Add(trailing);
        return grid;
    }

    /// <summary>An icon (or avatar) beside wrapping content (SwiftUI <c>HStack(alignment: .top)</c>).</summary>
    public static Grid IconRow(FrameworkElement icon, FrameworkElement content, double spacing = 12, bool center = false)
    {
        var grid = new Grid { ColumnSpacing = spacing };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        if (icon is FrameworkElement i)
        {
            i.VerticalAlignment = center ? VerticalAlignment.Center : VerticalAlignment.Top;
            if (!center && i is FontIcon) i.Margin = new Thickness(0, 2, 0, 0);
        }
        if (center && content is FrameworkElement c) c.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(content, 1);
        grid.Children.Add(icon);
        grid.Children.Add(content);
        return grid;
    }

    /// <summary>A person avatar: a tinted circle with the person glyph (Mac person.crop.circle).</summary>
    public Grid Avatar(int colorIndex, double diameter, double glyphSize)
    {
        var palette = colorIndex % 3;
        var grid = new Grid { Width = diameter, Height = diameter };
        grid.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse { Style = Style($"ExamAvatar{palette}") });
        grid.Children.Add(new FontIcon
        {
            Glyph = ExamGlyphs.Person,
            FontSize = glyphSize,
            Style = Style(palette switch { 0 => "ExamIconBlue", 1 => "ExamIconTeal", _ => "ExamIconGold" }),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return grid;
    }

    public static readonly FontFamily Monospace = new("Cascadia Mono, Consolas");
}
