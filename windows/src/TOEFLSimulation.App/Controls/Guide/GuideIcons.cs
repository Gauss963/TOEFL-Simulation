namespace TOEFLSimulation.App.Controls.Guide;

/// <summary>Segoe Fluent Icons glyphs for the SF Symbols used by the Mac guides and study pages.</summary>
public static class GuideIcons
{
    public const string CheckmarkCircleFill = "";
    public const string XmarkCircleFill = "";
    public const string Circle = "";
    public const string ChevronRight = "";
    public const string ChevronLeft = "";
    public const string Lightbulb = "";
    public const string Info = "";
    public const string CheckSquare = "";
    public const string Document = "";

    private static readonly Dictionary<string, string> Glyphs = new()
    {
        ["envelope"] = "",
        ["envelope.open"] = "",
        ["list.number"] = "",
        ["doc.text"] = "",
        ["timer"] = "",
        ["person.2"] = "",
        ["text.quote"] = "",
        ["text.alignleft"] = "",
        ["slider.horizontal.3"] = "",
        ["exclamationmark.triangle"] = "",
        ["checklist"] = "",
        ["bubble.left.and.bubble.right"] = "",
        ["text.bubble"] = "",
        ["square.stack.3d.up"] = "",
        ["lightbulb"] = "",
        ["repeat"] = "",
        ["figure.run"] = "",
        ["person.crop.rectangle"] = "",
        ["stairs"] = "",
        ["tray.full"] = "",
        ["waveform"] = "",
        ["mouth"] = "",
        ["magnifyingglass"] = "",
        ["calendar"] = "",
        ["speaker.wave.2"] = "",
    };

    /// <summary>The glyph for an SF Symbol name (a document icon when unknown).</summary>
    public static string Glyph(string symbol) => Glyphs.GetValueOrDefault(symbol, Document);
}
