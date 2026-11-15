using System.Windows;
using System.Windows.Media;

namespace WPF_Premium.Themes;

public enum WpfPremiumStyle
{
    Default,
    Modern,
    Simple,
    Classic
}

public static class ThemeStyleManager
{
    public const string StyleResourceKey = "WpfPremium.Style";
    private const string PaletteMarkerKey = "WpfPremium.StylePalette";

    public static event EventHandler? StyleChanged;

    public static WpfPremiumStyle GetCurrent(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        application.Dispatcher.VerifyAccess();

        return application.Resources[StyleResourceKey] is WpfPremiumStyle style
            ? style
            : WpfPremiumStyle.Default;
    }

    public static void SetStyle(Application application, WpfPremiumStyle style)
    {
        ArgumentNullException.ThrowIfNull(application);
        application.Dispatcher.VerifyAccess();
        if (!Enum.IsDefined(style))
        {
            throw new ArgumentOutOfRangeException(nameof(style), style, "Unsupported WPF Premium style.");
        }

        AppearanceSettingsManager.Apply(
            application,
            AppearanceSettingsManager.GetCurrent(application) with { Style = style });
    }

    internal static void ApplyResources(Application application, WpfPremiumStyle style, ComponentSize size)
    {
        var resources = application.Resources;
        var previousStyle = GetCurrent(application);
        var styled = GetDimensions(style, size);

        resources[StyleResourceKey] = style;
        resources[AppearanceSettingsManager.ControlMinHeightResourceKey] = styled.ControlMinHeight;
        resources[AppearanceSettingsManager.ControlPaddingResourceKey] = styled.ControlPadding;
        resources[AppearanceSettingsManager.ControlCornerRadiusResourceKey] = new CornerRadius(styled.CornerRadius);
        resources[AppearanceSettingsManager.SplitButtonMainCornerRadiusResourceKey] = new CornerRadius(styled.CornerRadius, 0, 0, styled.CornerRadius);
        resources[AppearanceSettingsManager.SplitButtonArrowCornerRadiusResourceKey] = new CornerRadius(0, styled.CornerRadius, styled.CornerRadius, 0);
        resources[AppearanceSettingsManager.TableRowHeightResourceKey] = styled.TableRowHeight;
        resources["WpfPremium.SurfaceCornerRadius"] = new CornerRadius(styled.SurfaceCornerRadius);
        resources["WpfPremium.ControlBorderThickness"] = new Thickness(styled.BorderThickness);
        resources["WpfPremium.SurfaceBorderThickness"] = new Thickness(styled.BorderThickness);

        ApplyThemePalette(application, ThemeManager.GetCurrentTheme(application));

        if (previousStyle != style)
        {
            StyleChanged?.Invoke(application, EventArgs.Empty);
        }
    }

    internal static ControlStyleMetrics GetControlMetrics(WpfPremiumStyle style, ComponentSize size)
    {
        var dimensions = GetDimensions(style, size);
        return new ControlStyleMetrics(
            dimensions.ControlMinHeight,
            dimensions.ControlPadding,
            new CornerRadius(dimensions.CornerRadius));
    }

    private static StyleDimensions GetDimensions(WpfPremiumStyle style, ComponentSize size)
    {
        var dimensions = size switch
        {
            ComponentSize.Small => new StyleDimensions(30, new Thickness(8, 4, 8, 4), 4, 8, 32, 1),
            ComponentSize.Medium => new StyleDimensions(34, new Thickness(10, 6, 10, 6), 6, 8, 36, 1),
            ComponentSize.Large => new StyleDimensions(42, new Thickness(14, 8, 14, 8), 8, 8, 44, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(size), size, "Unsupported component size.")
        };
        var styled = style switch
        {
            WpfPremiumStyle.Default => dimensions,
            WpfPremiumStyle.Modern => size switch
            {
                ComponentSize.Small => new StyleDimensions(dimensions.ControlMinHeight, dimensions.ControlPadding, 12, 16, dimensions.TableRowHeight, 1),
                ComponentSize.Medium => new StyleDimensions(dimensions.ControlMinHeight, dimensions.ControlPadding, 16, 20, dimensions.TableRowHeight, 1),
                ComponentSize.Large => new StyleDimensions(dimensions.ControlMinHeight, dimensions.ControlPadding, 20, 24, dimensions.TableRowHeight, 1),
                _ => throw new ArgumentOutOfRangeException(nameof(size), size, "Unsupported component size.")
            },
            WpfPremiumStyle.Simple => new StyleDimensions(
                dimensions.ControlMinHeight,
                dimensions.ControlPadding,
                0,
                3,
                dimensions.TableRowHeight,
                1),
            WpfPremiumStyle.Classic => new StyleDimensions(
                dimensions.ControlMinHeight,
                dimensions.ControlPadding,
                2,
                4,
                dimensions.TableRowHeight,
                1),
            _ => throw new ArgumentOutOfRangeException(nameof(style), style, "Unsupported WPF Premium style.")
        };

        return styled;
    }

    internal static void ApplyThemePalette(Application application, WpfPremiumTheme theme)
    {
        var mergedDictionaries = application.Resources.MergedDictionaries;
        for (var index = mergedDictionaries.Count - 1; index >= 0; index--)
        {
            if (mergedDictionaries[index].Contains(PaletteMarkerKey))
            {
                mergedDictionaries.RemoveAt(index);
            }
        }

        var style = GetCurrent(application);
        if (style == WpfPremiumStyle.Default)
        {
            return;
        }

        var palette = CreatePalette(style, theme);
        palette[PaletteMarkerKey] = style;
        mergedDictionaries.Add(palette);
    }

    internal static Brush? GetPreviewBrush(Application application, WpfPremiumStyle style, string resourceKey)
    {
        if (style == WpfPremiumStyle.Default)
        {
            return application.TryFindResource(resourceKey) as Brush;
        }

        return CreatePalette(style, ThemeManager.GetCurrentTheme(application))[resourceKey] as Brush;
    }

    private static ResourceDictionary CreatePalette(WpfPremiumStyle style, WpfPremiumTheme theme)
    {
        var colors = (style, theme) switch
        {
            (WpfPremiumStyle.Modern, WpfPremiumTheme.Light) => new[]
            {
                ("BackgroundBrush", "#FFF4F7FF"), ("ForegroundBrush", "#FF172554"),
                ("CardBrush", "#FFFFFFFF"), ("PopoverBrush", "#FFFFFFFF"),
                ("MutedBrush", "#FFE7ECFA"), ("MutedForegroundBrush", "#FF53658C"),
                ("BorderBrush", "#FFD2DCF3"), ("InputBrush", "#FFFFFFFF"),
                ("PrimaryBrush", "#FF4F46E5"), ("PrimaryForegroundBrush", "#FFFFFFFF"),
                ("SecondaryBrush", "#FFE0E7FF"), ("SecondaryForegroundBrush", "#FF3730A3"),
                ("DestructiveBrush", "#FFDC2626"), ("DestructiveForegroundBrush", "#FFFFFFFF"),
                ("SuccessBrush", "#FF15803D"), ("WarningBrush", "#FFB45309"),
                ("WarningForegroundBrush", "#FFFFFFFF"),
                ("FocusBrush", "#FF0891B2"), ("TraceActiveBrush", "#FF4F46E5"),
                ("SelectedTabBrush", "#FFC7D2FE"), ("ChartColor1Brush", "#FF4F46E5"),
                ("ChartColor2Brush", "#FF06B6D4"), ("ChartColor3Brush", "#FF10B981"),
                ("ChartColor4Brush", "#FFF59E0B"), ("ChartColor5Brush", "#FFEF4444"),
                ("ChartColor6Brush", "#FFEC4899"), ("ChartColor7Brush", "#FF8B5CF6"),
                ("ChartColor8Brush", "#FF14B8A6"), ("ChartColor9Brush", "#FFF97316"),
                ("ChartColor10Brush", "#FF84CC16"),
                ("ScrollBarThumbBrush", "#FFB6C2DE"), ("ScrollBarThumbHoverBrush", "#FF7B8CB3")
            },
            (WpfPremiumStyle.Modern, WpfPremiumTheme.Dark) => new[]
            {
                ("BackgroundBrush", "#FF0A1022"), ("ForegroundBrush", "#FFF1F5FF"),
                ("CardBrush", "#FF101A32"), ("PopoverBrush", "#FF172341"),
                ("MutedBrush", "#FF1D2A49"), ("MutedForegroundBrush", "#FFA8B7D6"),
                ("BorderBrush", "#FF29395D"), ("InputBrush", "#FF121E39"),
                ("PrimaryBrush", "#FFA5B4FC"), ("PrimaryForegroundBrush", "#FF1E1B4B"),
                ("SecondaryBrush", "#FF26375F"), ("SecondaryForegroundBrush", "#FFDBEAFE"),
                ("DestructiveBrush", "#FF7F1D1D"), ("DestructiveForegroundBrush", "#FFFFF7F7"),
                ("SuccessBrush", "#FF22C55E"), ("WarningBrush", "#FFFBBF24"),
                ("WarningForegroundBrush", "#FF2B1D00"),
                ("FocusBrush", "#FF67E8F9"), ("TraceActiveBrush", "#FF818CF8"),
                ("SelectedTabBrush", "#FF312E81"), ("ChartColor1Brush", "#FFA5B4FC"),
                ("ChartColor2Brush", "#FF22D3EE"), ("ChartColor3Brush", "#FF4ADE80"),
                ("ChartColor4Brush", "#FFFACC15"), ("ChartColor5Brush", "#FFFB7185"),
                ("ChartColor6Brush", "#FFF472B6"), ("ChartColor7Brush", "#FFC4B5FD"),
                ("ChartColor8Brush", "#FF2DD4BF"), ("ChartColor9Brush", "#FFFB923C"),
                ("ChartColor10Brush", "#FFA3E635"),
                ("ScrollBarThumbBrush", "#FF3B4D71"), ("ScrollBarThumbHoverBrush", "#FF637AA8")
            },
            (WpfPremiumStyle.Simple, WpfPremiumTheme.Light) => new[]
            {
                ("BackgroundBrush", "#FFF7F9FA"), ("ForegroundBrush", "#FF202B32"),
                ("CardBrush", "#FFFFFFFF"), ("PopoverBrush", "#FFFFFFFF"),
                ("MutedBrush", "#FFEEF2F4"), ("MutedForegroundBrush", "#FF5C6B73"),
                ("BorderBrush", "#FFD5DEE2"), ("InputBrush", "#FFFFFFFF"),
                ("PrimaryBrush", "#FF0F766E"), ("PrimaryForegroundBrush", "#FFFFFFFF"),
                ("SecondaryBrush", "#FFCCFBF1"), ("SecondaryForegroundBrush", "#FF115E59"),
                ("DestructiveBrush", "#FFB42318"), ("DestructiveForegroundBrush", "#FFFFFFFF"),
                ("SuccessBrush", "#FF15803D"), ("WarningBrush", "#FFB45309"),
                ("WarningForegroundBrush", "#FFFFFFFF"),
                ("FocusBrush", "#FF0F766E"), ("TraceActiveBrush", "#FF0F766E"),
                ("SelectedTabBrush", "#FFCCFBF1"), ("ChartColor1Brush", "#FF0F766E"),
                ("ChartColor2Brush", "#FF64748B"), ("ChartColor3Brush", "#FF15803D"),
                ("ChartColor4Brush", "#FFD97706"), ("ChartColor5Brush", "#FFB42318"),
                ("ChartColor6Brush", "#FF2563EB"), ("ChartColor7Brush", "#FF7C3AED"),
                ("ChartColor8Brush", "#FFDB2777"), ("ChartColor9Brush", "#FF0891B2"),
                ("ChartColor10Brush", "#FF65A30D"),
                ("ScrollBarThumbBrush", "#FFC3CDD1"), ("ScrollBarThumbHoverBrush", "#FF87969C")
            },
            (WpfPremiumStyle.Simple, WpfPremiumTheme.Dark) => new[]
            {
                ("BackgroundBrush", "#FF121A1C"), ("ForegroundBrush", "#FFE7F0F0"),
                ("CardBrush", "#FF182224"), ("PopoverBrush", "#FF202B2D"),
                ("MutedBrush", "#FF263235"), ("MutedForegroundBrush", "#FFA8B8B8"),
                ("BorderBrush", "#FF344244"), ("InputBrush", "#FF1C282A"),
                ("PrimaryBrush", "#FF5EEAD4"), ("PrimaryForegroundBrush", "#FF083344"),
                ("SecondaryBrush", "#FF213B3D"), ("SecondaryForegroundBrush", "#FFD1FAE5"),
                ("DestructiveBrush", "#FFEF4444"), ("DestructiveForegroundBrush", "#FFFFFFFF"),
                ("SuccessBrush", "#FF4ADE80"), ("WarningBrush", "#FFFBBF24"),
                ("WarningForegroundBrush", "#FF1F2937"),
                ("FocusBrush", "#FF5EEAD4"), ("TraceActiveBrush", "#FF2DD4BF"),
                ("SelectedTabBrush", "#FF164E4A"), ("ChartColor1Brush", "#FF5EEAD4"),
                ("ChartColor2Brush", "#FF94A3B8"), ("ChartColor3Brush", "#FF4ADE80"),
                ("ChartColor4Brush", "#FFFBBF24"), ("ChartColor5Brush", "#FFFB7185"),
                ("ChartColor6Brush", "#FF60A5FA"), ("ChartColor7Brush", "#FFC4B5FD"),
                ("ChartColor8Brush", "#FFF472B6"), ("ChartColor9Brush", "#FF22D3EE"),
                ("ChartColor10Brush", "#FFA3E635"),
                ("ScrollBarThumbBrush", "#FF3B4A4D"), ("ScrollBarThumbHoverBrush", "#FF64777A")
            },
            (WpfPremiumStyle.Classic, WpfPremiumTheme.Light) => new[]
            {
                ("BackgroundBrush", "#FFF3EFE5"), ("ForegroundBrush", "#FF242B39"),
                ("CardBrush", "#FFFFFCF4"), ("PopoverBrush", "#FFFFFDF7"),
                ("MutedBrush", "#FFE8E1D2"), ("MutedForegroundBrush", "#FF5F6570"),
                ("BorderBrush", "#FFC5B9A5"), ("InputBrush", "#FFFFFCF4"),
                ("PrimaryBrush", "#FF243B64"), ("PrimaryForegroundBrush", "#FFFFFFFF"),
                ("SecondaryBrush", "#FFE9E0D0"), ("SecondaryForegroundBrush", "#FF3A2D1F"),
                ("DestructiveBrush", "#FF9B2C2C"), ("DestructiveForegroundBrush", "#FFFFFFFF"),
                ("SuccessBrush", "#FF3C6B4F"), ("WarningBrush", "#FF9A6700"),
                ("WarningForegroundBrush", "#FFFFFFFF"),
                ("FocusBrush", "#FFB78932"), ("TraceActiveBrush", "#FF294D7A"),
                ("SelectedTabBrush", "#FFD7DFEC"), ("ChartColor1Brush", "#FF274C77"),
                ("ChartColor2Brush", "#FFC18F3C"), ("ChartColor3Brush", "#FF557A46"),
                ("ChartColor4Brush", "#FFCB6D35"), ("ChartColor5Brush", "#FFB94747"),
                ("ChartColor6Brush", "#FF7C3AED"), ("ChartColor7Brush", "#FF0F766E"),
                ("ChartColor8Brush", "#FF6D597A"), ("ChartColor9Brush", "#FF7F1D1D"),
                ("ChartColor10Brush", "#FF2563EB"),
                ("ScrollBarThumbBrush", "#FFC4BBAA"), ("ScrollBarThumbHoverBrush", "#FF8D826E")
            },
            (WpfPremiumStyle.Classic, WpfPremiumTheme.Dark) => new[]
            {
                ("BackgroundBrush", "#FF1F2024"), ("ForegroundBrush", "#FFEAE3D6"),
                ("CardBrush", "#FF292B31"), ("PopoverBrush", "#FF303239"),
                ("MutedBrush", "#FF34363D"), ("MutedForegroundBrush", "#FFB6B2A8"),
                ("BorderBrush", "#FF4A4B50"), ("InputBrush", "#FF303238"),
                ("PrimaryBrush", "#FFD8B16A"), ("PrimaryForegroundBrush", "#FF252525"),
                ("SecondaryBrush", "#FF403A32"), ("SecondaryForegroundBrush", "#FFF3E7CE"),
                ("DestructiveBrush", "#FF9B3D3D"), ("DestructiveForegroundBrush", "#FFFFF7F0"),
                ("SuccessBrush", "#FF6FA17A"), ("WarningBrush", "#FFE0B45C"),
                ("WarningForegroundBrush", "#FF302100"),
                ("FocusBrush", "#FFE0B45C"), ("TraceActiveBrush", "#FF8EACD0"),
                ("SelectedTabBrush", "#FF3D4656"), ("ChartColor1Brush", "#FF8EACD0"),
                ("ChartColor2Brush", "#FFE0B45C"), ("ChartColor3Brush", "#FF8CBF8C"),
                ("ChartColor4Brush", "#FFE28B52"), ("ChartColor5Brush", "#FFE07474"),
                ("ChartColor6Brush", "#FFC4B5FD"), ("ChartColor7Brush", "#FF2DD4BF"),
                ("ChartColor8Brush", "#FFD8B4FE"), ("ChartColor9Brush", "#FFFCA5A5"),
                ("ChartColor10Brush", "#FF93C5FD"),
                ("ScrollBarThumbBrush", "#FF5A5B60"), ("ScrollBarThumbHoverBrush", "#FF858177")
            },
            _ => throw new ArgumentOutOfRangeException(nameof(style), style, "Unsupported WPF Premium style.")
        };

        var dictionary = new ResourceDictionary();
        foreach (var (key, hexColor) in colors)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor)!);
            brush.Freeze();
            dictionary[key] = brush;
        }

        return dictionary;
    }

    private sealed record StyleDimensions(
        double ControlMinHeight,
        Thickness ControlPadding,
        double CornerRadius,
        double SurfaceCornerRadius,
        double TableRowHeight,
        double BorderThickness);

    internal readonly record struct ControlStyleMetrics(
        double ControlMinHeight,
        Thickness ControlPadding,
        CornerRadius ControlCornerRadius);
}
