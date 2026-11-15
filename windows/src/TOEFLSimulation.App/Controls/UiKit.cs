using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI.Text;

namespace TOEFLSimulation.App.Controls;

/// <summary>
/// Builds the small text and icon elements of code-generated cards. Colors come from styles in the owning page's
/// resources (TextNavy, TextSecondary, IconTeal, …) so they follow light and dark themes.
/// </summary>
internal sealed class UiKit(ResourceDictionary resources)
{
    public Style GetStyle(string key) => (Style)resources[key];

    public static Style AppStyle(string key) => (Style)Application.Current.Resources[key];

    public TextBlock Text(string text, string style = "TextPrimary", double fontSize = 14, FontWeight? weight = null, bool italic = false)
    {
        var block = new TextBlock
        {
            Style = GetStyle(style),
            Text = text,
            FontSize = fontSize,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = false,
        };
        if (weight is { } w) block.FontWeight = w;
        if (italic) block.FontStyle = FontStyle.Italic;
        return block;
    }

    public FontIcon Icon(string glyph, string style = "IconPrimary", double fontSize = 14) => new()
    {
        Style = GetStyle(style),
        Glyph = glyph,
        FontSize = fontSize,
    };

    /// <summary>An icon followed by text (SwiftUI <c>Label</c>).</summary>
    public StackPanel Label(string glyph, string text, string textStyle, string iconStyle, double fontSize = 14, FontWeight? weight = null)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        var icon = Icon(glyph, iconStyle, fontSize);
        icon.VerticalAlignment = VerticalAlignment.Center;
        panel.Children.Add(icon);
        var label = Text(text, textStyle, fontSize, weight);
        label.VerticalAlignment = VerticalAlignment.Center;
        panel.Children.Add(label);
        return panel;
    }

    public static Border Card(UIElement child) => new() { Style = AppStyle("CardStyle"), Child = child };

    /// <summary>Mac <c>.formatted(date: .abbreviated, time: .shortened)</c>, e.g. "Nov 15, 2026 at 3:04 PM".</summary>
    public static string AbbreviatedDateTime(DateTimeOffset value)
    {
        var local = value.ToLocalTime();
        var culture = CultureInfo.CurrentCulture;
        return $"{local.ToString("MMM d, yyyy", culture)} at {local.ToString("t", culture)}";
    }

    /// <summary>Mac <c>.dateTime.year().month().day().hour().minute()</c>, e.g. "11/15/2026, 3:04 PM".</summary>
    public static string NumericDateTime(DateTimeOffset value)
    {
        var local = value.ToLocalTime();
        var culture = CultureInfo.CurrentCulture;
        return $"{local.ToString("d", culture)}, {local.ToString("t", culture)}";
    }

    public static readonly FontWeight Bold = FontWeights.Bold;
    public static readonly FontWeight SemiBold = FontWeights.SemiBold;
    public static readonly FontWeight Black = FontWeights.Black;
}

/// <summary>Segoe Fluent Icons glyphs standing in for the Mac app's SF Symbols.</summary>
internal static class Glyphs
{
    public const string Book = "";
    public const string Headphones = "";
    public const string Keyboard = "";
    public const string Microphone = "";
    public const string Info = "";
    public const string CheckCircle = "";
    public const string ErrorCircle = "";
    public const string Checklist = "";
    public const string ChevronRight = "";
    public const string Chart = "";
    public const string Delete = "";
    public const string Star = "";
    public const string Eye = "";
    public const string EyeHidden = "";
    public const string Speaker = "";
    public const string Stop = "";
    public const string Stack = "";
    public const string Seal = "";
    public const string Repeat = "";
    public const string More = "";
    public const string OpenLink = "";
    public const string Cards = "";
    public const string Search = "";
    public const string CheckMark = "";
    public const string Dictionary = "";

    public static string Section(TOEFLSimulation.Core.TOEFLSectionKind kind) => kind switch
    {
        TOEFLSimulation.Core.TOEFLSectionKind.Reading => Book,
        TOEFLSimulation.Core.TOEFLSectionKind.Listening => Headphones,
        TOEFLSimulation.Core.TOEFLSectionKind.Writing => Keyboard,
        _ => Microphone,
    };
}
