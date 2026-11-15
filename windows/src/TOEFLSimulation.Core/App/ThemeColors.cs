namespace TOEFLSimulation.Core;

/// <summary>An sRGB color with 0–1 components (convert to a WinUI Color in the App).</summary>
public readonly record struct ThemeColor(double Red, double Green, double Blue)
{
    /// <summary>"#RRGGBB".</summary>
    public string Hex => $"#{To8(Red):X2}{To8(Green):X2}{To8(Blue):X2}";

    private static int To8(double value) => (int)Math.Round(Math.Clamp(value, 0, 1) * 255, MidpointRounding.AwayFromZero);
}

/// <summary>
/// The Mac app's TOEFLTheme palette (Theme.swift), for the UI to match. Canvas, surface, input, and border are system colors
/// on the Mac (window, control, text background, separator); use the WinUI equivalents.
/// </summary>
public static class TOEFLThemeColors
{
    public static readonly ThemeColor Header = new(0.03, 0.22, 0.39);
    public static readonly ThemeColor Action = new(0.02, 0.43, 0.66);
    public static readonly ThemeColor Aqua = new(0.02, 0.62, 0.67);
    public static readonly ThemeColor Gold = new(0.93, 0.60, 0.12);
    public static readonly ThemeColor NavyLight = new(0.03, 0.22, 0.39);
    public static readonly ThemeColor NavyDark = new(0.60, 0.84, 0.98);
    public static readonly ThemeColor BlueLight = new(0.02, 0.43, 0.66);
    public static readonly ThemeColor BlueDark = new(0.33, 0.76, 0.96);
    public static readonly ThemeColor TealLight = new(0.02, 0.50, 0.54);
    public static readonly ThemeColor TealDark = new(0.33, 0.84, 0.82);
}
