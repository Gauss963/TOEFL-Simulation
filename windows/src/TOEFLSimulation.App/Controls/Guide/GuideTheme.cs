using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TOEFLSimulation.Core;
using Windows.UI;
using Windows.UI.Text;

namespace TOEFLSimulation.App.Controls.Guide;

/// <summary>
/// Shared styling for the study pages and guides: the theme-aware styles in GuideStyles.xaml, the Mac app's font sizes
/// mapped to the Windows type ramp, and small element builders.
/// </summary>
public static class GuideTheme
{
    private static ResourceDictionary? styles;

    private static ResourceDictionary Styles =>
        styles ??= new ResourceDictionary { Source = new Uri("ms-appx:///Controls/Guide/GuideStyles.xaml") };

    /// <summary>A style from GuideStyles.xaml (for example "GuideSecondaryText").</summary>
    public static Style Style(string key) => (Style)Styles[key];

    /// <summary>A style from the app resources (for example "CardStyle" or "AccentButtonStyle").</summary>
    public static Style AppStyle(string key) => (Style)Application.Current.Resources[key];

    // Mac text styles on the Windows type ramp.
    public const double Caption = 12;
    public const double Caption2 = 11;
    public const double Subheadline = 14;
    public const double Body = 14;
    public const double Headline = 14;
    public const double Title3 = 18;
    public const double Title2 = 20;
    public const double PageTitle = 28;

    public static readonly FontFamily Monospaced = new("Cascadia Mono, Consolas, Courier New");
    public static readonly FontFamily Serif = new("Georgia, Cambria, Times New Roman");

    /// <summary>A wrapping text block. <paramref name="style"/> is a GuideStyles.xaml TextBlock style key.</summary>
    public static TextBlock Text(string text, double size = Body, FontWeight? weight = null, string? style = null,
        bool wrap = true, bool selectable = false, bool italic = false, FontFamily? font = null)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            IsTextSelectionEnabled = selectable,
        };
        if (style is not null) block.Style = Style(style);
        if (weight is { } w) block.FontWeight = w;
        if (italic) block.FontStyle = FontStyle.Italic;
        if (font is not null) block.FontFamily = font;
        return block;
    }

    public static TextBlock Secondary(string text, double size = Caption, bool selectable = false) =>
        Text(text, size, style: "GuideSecondaryText", selectable: selectable);

    public static FontIcon Icon(string glyph, double size = 14, string? style = null)
    {
        var icon = new FontIcon { Glyph = glyph, FontSize = size };
        if (style is not null) icon.Style = Style(style);
        AutomationProperties.SetAccessibilityView(icon, Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Raw);
        return icon;
    }

    /// <summary>The Mac card (toeflCard): the app's CardStyle border.</summary>
    public static Border Card(UIElement child) => new() { Style = AppStyle("CardStyle"), Child = child };

    public static Border Divider() => new() { Style = Style("GuideDivider") };

    public static StackPanel VStack(double spacing, params UIElement[] children)
    {
        var stack = new StackPanel { Spacing = spacing };
        foreach (var child in children) stack.Children.Add(child);
        return stack;
    }

    public static StackPanel HStack(double spacing, params UIElement[] children)
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing };
        foreach (var child in children) stack.Children.Add(child);
        return stack;
    }

    /// <summary>A row with leading content and trailing content pushed to the far edge (HStack with a Spacer).</summary>
    public static Grid SpacedRow(UIElement leading, UIElement? trailing, double spacing = 12, VerticalAlignment alignment = VerticalAlignment.Center)
    {
        var grid = new Grid { ColumnSpacing = spacing };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        if (leading is FrameworkElement l) l.VerticalAlignment = alignment;
        grid.Children.Add(leading);
        if (trailing is not null)
        {
            if (trailing is FrameworkElement t) t.VerticalAlignment = alignment;
            Grid.SetColumn((FrameworkElement)trailing, 1);
            grid.Children.Add(trailing);
        }
        return grid;
    }

    /// <summary>Text style key for the Mac score colors ("teal", "gold", "red").</summary>
    public static string TierStyle(string tier) => tier switch
    {
        "teal" => "GuideTealText",
        "gold" => "GuideGoldText",
        _ => "GuideCriticalText",
    };

    public static string ColorName(GuideColor color) => color switch
    {
        GuideColor.Action => "Action",
        GuideColor.Teal => "Teal",
        GuideColor.Navy => "Navy",
        GuideColor.Gold => "Gold",
        _ => "Secondary",
    };

    /// <summary>
    /// A brand color for text runs, which cannot take styles (see <see cref="SpanText"/>): the Mac TOEFLTheme value for the
    /// given theme.
    /// </summary>
    public static Color RunColor(GuideColor color, ElementTheme theme)
    {
        var dark = theme == ElementTheme.Dark;
        var value = color switch
        {
            GuideColor.Action => TOEFLThemeColors.Action,
            GuideColor.Teal => dark ? TOEFLThemeColors.TealDark : TOEFLThemeColors.TealLight,
            GuideColor.Navy => dark ? TOEFLThemeColors.NavyDark : TOEFLThemeColors.NavyLight,
            GuideColor.Gold => TOEFLThemeColors.Gold,
            _ => new ThemeColor(0.55, 0.55, 0.55),
        };
        return ColorHelper.FromArgb(255, To8(value.Red), To8(value.Green), To8(value.Blue));
    }

    private static byte To8(double value) => (byte)Math.Round(Math.Clamp(value, 0, 1) * 255, MidpointRounding.AwayFromZero);

    /// <summary>The Mac <c>date.formatted(date: .abbreviated, time: .shortened)</c>, in the user's locale.</summary>
    public static string ShortDateTime(DateTimeOffset date)
    {
        var local = date.ToLocalTime();
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        return $"{local.ToString("MMM d, yyyy", culture)} at {local.ToString("t", culture)}";
    }
}
