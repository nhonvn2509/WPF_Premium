using System.Windows;
using System.Windows.Controls;

namespace WPF_Premium.Controls;

// shortcut: measures every child; add virtualization when used for large scrolling collections.
public class WaterfallPanel : Panel
{
    private readonly List<Placement> _placements = [];

    public static readonly DependencyProperty ColumnCountProperty =
        DependencyProperty.Register(
            nameof(ColumnCount),
            typeof(int),
            typeof(WaterfallPanel),
            new FrameworkPropertyMetadata(3, FrameworkPropertyMetadataOptions.AffectsMeasure),
            value => (int)value > 0);

    public static readonly DependencyProperty HorizontalSpacingProperty =
        DependencyProperty.Register(
            nameof(HorizontalSpacing),
            typeof(double),
            typeof(WaterfallPanel),
            new FrameworkPropertyMetadata(12d, FrameworkPropertyMetadataOptions.AffectsMeasure),
            IsValidSpacing);

    public static readonly DependencyProperty VerticalSpacingProperty =
        DependencyProperty.Register(
            nameof(VerticalSpacing),
            typeof(double),
            typeof(WaterfallPanel),
            new FrameworkPropertyMetadata(12d, FrameworkPropertyMetadataOptions.AffectsMeasure),
            IsValidSpacing);

    public int ColumnCount
    {
        get => (int)GetValue(ColumnCountProperty);
        set => SetValue(ColumnCountProperty, value);
    }

    public double HorizontalSpacing
    {
        get => (double)GetValue(HorizontalSpacingProperty);
        set => SetValue(HorizontalSpacingProperty, value);
    }

    public double VerticalSpacing
    {
        get => (double)GetValue(VerticalSpacingProperty);
        set => SetValue(VerticalSpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _placements.Clear();
        var heights = new double[ColumnCount];
        var widths = new double[ColumnCount];
        var hasVisibleChildren = false;
        var horizontalSpacing = GetHorizontalSpacing(availableSize.Width);
        var columnWidth = double.IsPositiveInfinity(availableSize.Width)
            ? double.PositiveInfinity
            : Math.Max(0, (availableSize.Width - horizontalSpacing * (ColumnCount - 1)) / ColumnCount);

        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                _placements.Add(Placement.Collapsed);
                continue;
            }

            hasVisibleChildren = true;
            child.Measure(new Size(columnWidth, double.PositiveInfinity));
            var column = FindShortestColumn(heights);
            var top = heights[column] > 0 ? heights[column] + VerticalSpacing : 0;
            var height = child.DesiredSize.Height;
            _placements.Add(new Placement(column, top));
            heights[column] = top + height;
            widths[column] = Math.Max(widths[column], child.DesiredSize.Width);
        }

        var desiredHeight = heights.Max();
        var desiredWidth = availableSize.Width;
        if (double.IsPositiveInfinity(availableSize.Width))
        {
            desiredWidth = hasVisibleChildren
                ? widths.Max() * ColumnCount + horizontalSpacing * (ColumnCount - 1)
                : 0;
        }

        return new Size(desiredWidth, desiredHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var horizontalSpacing = GetHorizontalSpacing(finalSize.Width);
        var columnWidth = Math.Max(0, (finalSize.Width - horizontalSpacing * (ColumnCount - 1)) / ColumnCount);
        for (var placementIndex = 0; placementIndex < InternalChildren.Count; placementIndex++)
        {
            var child = InternalChildren[placementIndex];
            var placement = _placements[placementIndex];
            if (placement.Column < 0 || child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            var left = placement.Column * (columnWidth + horizontalSpacing);
            child.Arrange(new Rect(left, placement.Top, columnWidth, child.DesiredSize.Height));
        }

        return finalSize;
    }

    private double GetHorizontalSpacing(double width)
    {
        if (double.IsPositiveInfinity(width) || ColumnCount == 1)
        {
            return HorizontalSpacing;
        }

        return Math.Min(HorizontalSpacing, Math.Max(0, width) / (ColumnCount - 1));
    }

    private static int FindShortestColumn(double[] heights)
    {
        var shortest = 0;
        for (var index = 1; index < heights.Length; index++)
        {
            if (heights[index] < heights[shortest])
            {
                shortest = index;
            }
        }

        return shortest;
    }

    private static bool IsValidSpacing(object value)
    {
        var spacing = (double)value;
        return double.IsFinite(spacing) && spacing >= 0;
    }

    private readonly record struct Placement(int Column, double Top)
    {
        public static Placement Collapsed { get; } = new(-1, 0);
    }
}
