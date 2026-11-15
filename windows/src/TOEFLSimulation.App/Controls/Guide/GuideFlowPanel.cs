using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace TOEFLSimulation.App.Controls.Guide;

/// <summary>
/// The Mac app's FlowLayout: places children left to right and wraps to a new line when the row is full. Items in a line
/// are centered vertically. Used for Complete the Words paragraphs (words and blanks) and accuracy chips.
/// </summary>
public sealed partial class GuideFlowPanel : Panel
{
    public GuideFlowPanel(double spacing = 6, double lineSpacing = 12)
    {
        Spacing = spacing;
        LineSpacing = lineSpacing;
    }

    public double Spacing { get; }
    public double LineSpacing { get; }

    private List<(int Start, int End, double Height)> Lines(double width)
    {
        var lines = new List<(int, int, double)>();
        double x = 0, height = 0;
        var start = 0;
        for (var i = 0; i < Children.Count; i++)
        {
            var size = Children[i].DesiredSize;
            if (i > start && x + size.Width > width)
            {
                lines.Add((start, i, height));
                start = i;
                x = 0;
                height = 0;
            }
            x += size.Width + Spacing;
            height = Math.Max(height, size.Height);
        }
        if (start < Children.Count) lines.Add((start, Children.Count, height));
        return lines;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        double widest = 0;
        foreach (var child in Children) child.Measure(new Size(width, double.PositiveInfinity));
        var lines = Lines(width);
        double height = 0;
        foreach (var (start, end, lineHeight) in lines)
        {
            double x = 0;
            for (var i = start; i < end; i++) x += Children[i].DesiredSize.Width + (i > start ? Spacing : 0);
            widest = Math.Max(widest, x);
            height += lineHeight;
        }
        if (lines.Count > 1) height += LineSpacing * (lines.Count - 1);
        return new Size(double.IsInfinity(width) ? widest : Math.Min(widest, width), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double y = 0;
        foreach (var (start, end, lineHeight) in Lines(finalSize.Width))
        {
            double x = 0;
            for (var i = start; i < end; i++)
            {
                var size = Children[i].DesiredSize;
                Children[i].Arrange(new Rect(x, y + (lineHeight - size.Height) / 2, size.Width, size.Height));
                x += size.Width + Spacing;
            }
            y += lineHeight + LineSpacing;
        }
        return finalSize;
    }
}

/// <summary>
/// The Mac <c>LazyVGrid(columns: [GridItem(.adaptive(minimum:))])</c>: as many equal columns as fit with at least
/// <see cref="MinItemWidth"/> each; items in a row share the row's height.
/// </summary>
public sealed partial class GuideGridPanel : Panel
{
    public GuideGridPanel(double minItemWidth, double spacing)
    {
        MinItemWidth = minItemWidth;
        Spacing = spacing;
    }

    public double MinItemWidth { get; }
    public double Spacing { get; }

    private (int Columns, double ItemWidth) Layout(double width)
    {
        if (double.IsInfinity(width) || width <= 0) return (1, MinItemWidth);
        var columns = Math.Max(1, (int)Math.Floor((width + Spacing) / (MinItemWidth + Spacing)));
        return (columns, (width - Spacing * (columns - 1)) / columns);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var (columns, itemWidth) = Layout(availableSize.Width);
        foreach (var child in Children) child.Measure(new Size(itemWidth, double.PositiveInfinity));
        var rows = (Children.Count + columns - 1) / columns;
        var total = 0.0;
        for (var row = 0; row < rows; row++) total += RowHeight(row, columns);
        if (rows > 1) total += Spacing * (rows - 1);
        var width = double.IsInfinity(availableSize.Width) ? columns * itemWidth : availableSize.Width;
        return new Size(width, total);
    }

    private double RowHeight(int row, int columns)
    {
        double height = 0;
        for (var i = row * columns; i < Math.Min(Children.Count, (row + 1) * columns); i++)
            height = Math.Max(height, Children[i].DesiredSize.Height);
        return height;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (columns, itemWidth) = Layout(finalSize.Width);
        var rows = (Children.Count + columns - 1) / columns;
        double y = 0;
        for (var row = 0; row < rows; row++)
        {
            var height = RowHeight(row, columns);
            for (var i = row * columns; i < Math.Min(Children.Count, (row + 1) * columns); i++)
                Children[i].Arrange(new Rect((i - row * columns) * (itemWidth + Spacing), y, itemWidth, height));
            y += height + Spacing;
        }
        return finalSize;
    }
}
