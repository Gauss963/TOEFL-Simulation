using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace TOEFLSimulation.App.Controls;

/// <summary>
/// SwiftUI's <c>LazyVGrid(columns: [GridItem(.adaptive(minimum:))])</c>: as many equal columns as fit at
/// <see cref="MinItemWidth"/>, rows as tall as their tallest item, and every item stretched to its cell.
/// </summary>
public sealed partial class AdaptiveGridPanel : Panel
{
    private double minItemWidth = 220;
    private double spacing = 14;

    public double MinItemWidth
    {
        get => minItemWidth;
        set { minItemWidth = Math.Max(1, value); InvalidateMeasure(); }
    }

    public double Spacing
    {
        get => spacing;
        set { spacing = Math.Max(0, value); InvalidateMeasure(); }
    }

    /// <summary>The number of columns that fit in <paramref name="width"/>.</summary>
    public static int ColumnCount(double width, double minItemWidth, double spacing) =>
        double.IsInfinity(width) || double.IsNaN(width) ? 1 : Math.Max(1, (int)((width + spacing) / (minItemWidth + spacing)));

    private List<UIElement> VisibleChildren() => Children.Where(c => c.Visibility == Visibility.Visible).ToList();

    protected override Size MeasureOverride(Size availableSize)
    {
        var children = VisibleChildren();
        var width = double.IsInfinity(availableSize.Width) ? minItemWidth : availableSize.Width;
        var columns = ColumnCount(width, minItemWidth, spacing);
        var itemWidth = Math.Max(0, (width - spacing * (columns - 1)) / columns);
        double height = 0;
        for (var start = 0; start < children.Count; start += columns)
        {
            double rowHeight = 0;
            for (var i = start; i < Math.Min(start + columns, children.Count); i++)
            {
                children[i].Measure(new Size(itemWidth, double.PositiveInfinity));
                rowHeight = Math.Max(rowHeight, children[i].DesiredSize.Height);
            }
            height += rowHeight + (start > 0 ? spacing : 0);
        }
        foreach (var hidden in Children.Where(c => c.Visibility != Visibility.Visible)) hidden.Measure(new Size(0, 0));
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var children = VisibleChildren();
        var columns = ColumnCount(finalSize.Width, minItemWidth, spacing);
        var itemWidth = Math.Max(0, (finalSize.Width - spacing * (columns - 1)) / columns);
        double y = 0;
        for (var start = 0; start < children.Count; start += columns)
        {
            var end = Math.Min(start + columns, children.Count);
            double rowHeight = 0;
            for (var i = start; i < end; i++) rowHeight = Math.Max(rowHeight, children[i].DesiredSize.Height);
            for (var i = start; i < end; i++)
                children[i].Arrange(new Rect((i - start) * (itemWidth + spacing), y, itemWidth, rowHeight));
            y += rowHeight + spacing;
        }
        return finalSize;
    }
}

/// <summary>Lays children out left to right and wraps them onto new lines (SwiftUI's ViewThatFits fallback for control rows).</summary>
public sealed partial class WrapFlowPanel : Panel
{
    private double horizontalSpacing = 14;
    private double verticalSpacing = 12;

    public double HorizontalSpacing
    {
        get => horizontalSpacing;
        set { horizontalSpacing = Math.Max(0, value); InvalidateMeasure(); }
    }

    public double VerticalSpacing
    {
        get => verticalSpacing;
        set { verticalSpacing = Math.Max(0, value); InvalidateMeasure(); }
    }

    private List<List<UIElement>> Lines(double width)
    {
        var lines = new List<List<UIElement>>();
        var line = new List<UIElement>();
        double x = 0;
        foreach (var child in Children.Where(c => c.Visibility == Visibility.Visible))
        {
            var childWidth = child.DesiredSize.Width;
            if (line.Count > 0 && x + horizontalSpacing + childWidth > width)
            {
                lines.Add(line);
                line = [];
                x = 0;
            }
            x += (line.Count > 0 ? horizontalSpacing : 0) + childWidth;
            line.Add(child);
        }
        if (line.Count > 0) lines.Add(line);
        return lines;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children) child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        double width = 0, height = 0;
        var lines = Lines(availableSize.Width);
        for (var i = 0; i < lines.Count; i++)
        {
            var lineWidth = lines[i].Sum(c => c.DesiredSize.Width) + horizontalSpacing * (lines[i].Count - 1);
            width = Math.Max(width, lineWidth);
            height += lines[i].Max(c => c.DesiredSize.Height) + (i > 0 ? verticalSpacing : 0);
        }
        if (!double.IsInfinity(availableSize.Width)) width = Math.Min(width, availableSize.Width);
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double y = 0;
        foreach (var line in Lines(finalSize.Width))
        {
            var lineHeight = line.Max(c => c.DesiredSize.Height);
            double x = 0;
            foreach (var child in line)
            {
                var size = child.DesiredSize;
                child.Arrange(new Rect(x, y + (lineHeight - size.Height) / 2, Math.Min(size.Width, finalSize.Width), size.Height));
                x += size.Width + horizontalSpacing;
            }
            y += lineHeight + verticalSpacing;
        }
        foreach (var hidden in Children.Where(c => c.Visibility != Visibility.Visible)) hidden.Arrange(new Rect(0, 0, 0, 0));
        return finalSize;
    }
}
