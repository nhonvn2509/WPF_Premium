using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using WPF_Premium.Themes;

namespace WPF_Premium.Controls;

internal static class ControlHelpers
{
    public static readonly string[] Hours24 = CreateTwoDigitSeries(24);
    public static readonly string[] MinutesOrSeconds60 = CreateTwoDigitSeries(60);

    public static string[] CreateTwoDigitSeries(int count, int step = 1) =>
        Enumerable.Range(0, count)
            .Where(value => step <= 1 || value % step == 0)
            .Select(value => value.ToString("D2", CultureInfo.InvariantCulture))
            .ToArray();
    public static string GetDisplayText(object? item, string? displayMemberPath = null)
    {
        if (item is null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(displayMemberPath))
        {
            var property = TypeDescriptor.GetProperties(item)[displayMemberPath];
            if (property is not null)
            {
                return property.GetValue(item)?.ToString() ?? string.Empty;
            }
        }

        return item.ToString() ?? string.Empty;
    }

    public static void RebindCollectionChanged(
        ref INotifyCollectionChanged? current,
        object? next,
        NotifyCollectionChangedEventHandler handler)
    {
        if (current is not null)
        {
            current.CollectionChanged -= handler;
        }

        current = next as INotifyCollectionChanged;
        if (current is not null)
        {
            current.CollectionChanged += handler;
        }
    }

    public static void ApplyControlBorderTokens(Border border, string backgroundKey = WpfPremiumResourceKeys.InputBrush)
    {
        border.SetResourceReference(Border.BackgroundProperty, backgroundKey);
        border.SetResourceReference(Border.BorderBrushProperty, WpfPremiumResourceKeys.BorderBrush);
        border.SetResourceReference(Border.BorderThicknessProperty, WpfPremiumResourceKeys.ControlBorderThickness);
        border.SetResourceReference(Border.CornerRadiusProperty, WpfPremiumResourceKeys.ControlCornerRadius);
        border.SetResourceReference(FrameworkElement.MinHeightProperty, WpfPremiumResourceKeys.ControlMinHeight);
    }

    public static void ApplySurfaceBorderTokens(Border border, string backgroundKey = WpfPremiumResourceKeys.CardBrush)
    {
        border.SetResourceReference(Border.BackgroundProperty, backgroundKey);
        border.SetResourceReference(Border.BorderBrushProperty, WpfPremiumResourceKeys.BorderBrush);
        border.SetResourceReference(Border.BorderThicknessProperty, WpfPremiumResourceKeys.SurfaceBorderThickness);
        border.SetResourceReference(Border.CornerRadiusProperty, WpfPremiumResourceKeys.SurfaceCornerRadius);
    }
}
