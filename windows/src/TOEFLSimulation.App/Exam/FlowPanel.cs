using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace TOEFLSimulation.App.Exam;

/// <summary>
/// The Mac exam's FlowLayout: children placed left to right at their natural size and wrapped onto new lines like text.
/// Children on a line share its baseline area (aligned to the bottom of the tallest child when <see cref="AlignBottom"/>,
/// otherwise centered).
/// </summary>
public sealed partial class FlowPanel : Panel
{
    private double spacing = 6;
    private double lineSpacing = 10;

    public double Spacing
    {
        get => spacing;
        set { spacing = value; InvalidateMeasure(); }
    }

    public double LineSpacing
    {
        get => lineSpacing;
        set { lineSpacing = value; InvalidateMeasure(); }
    }

    public bool AlignBottom { get; set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 800 : availableSize.Width;
        double x = 0, y = 0, lineHeight = 0, widest = 0;
        foreach (var child in Children)
        {
            child.Measure(new Size(width, double.PositiveInfinity));
            var size = child.DesiredSize;
            if (x > 0 && x + size.Width > width)
            {
                x = 0;
                y += lineHeight + lineSpacing;
                lineHeight = 0;
            }
            x += size.Width + spacing;
            widest = Math.Max(widest, x - spacing);
            lineHeight = Math.Max(lineHeight, size.Height);
        }
        return new Size(double.IsInfinity(availableSize.Width) ? widest : width, y + lineHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var lines = new List<(List<UIElement> Items, double Height)>();
        var current = new List<UIElement>();
        double x = 0, lineHeight = 0;
        foreach (var child in Children)
        {
            var size = child.DesiredSize;
            if (x > 0 && x + size.Width > finalSize.Width)
            {
                lines.Add((current, lineHeight));
                current = [];
                x = 0;
                lineHeight = 0;
            }
            current.Add(child);
            x += size.Width + spacing;
            lineHeight = Math.Max(lineHeight, size.Height);
        }
        if (current.Count > 0) lines.Add((current, lineHeight));

        double y = 0;
        foreach (var (items, height) in lines)
        {
            double left = 0;
            foreach (var child in items)
            {
                var size = child.DesiredSize;
                var top = AlignBottom ? y + height - size.Height : y + (height - size.Height) / 2;
                child.Arrange(new Rect(left, top, Math.Min(size.Width, finalSize.Width), size.Height));
                left += size.Width + spacing;
            }
            y += height + lineSpacing;
        }
        return finalSize;
    }
}
