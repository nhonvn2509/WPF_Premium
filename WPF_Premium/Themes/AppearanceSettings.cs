using System.Windows;
using System.Windows.Media;

namespace WPF_Premium.Themes;

public enum ComponentSize
{
    Small,
    Medium,
    Large
}

public sealed record AppearanceSettings(
    string FontFamily,
    double FontSize,
    ComponentSize ComponentSize)
{
    public static AppearanceSettings Default { get; } = new("Segoe UI", 14, ComponentSize.Medium);
}

public static class AppearanceSettingsManager
{
    public const string FontFamilyResourceKey = "WpfPremium.FontFamily";
    public const string FontSizeResourceKey = "WpfPremium.FontSize";
    public const string SmallFontSizeResourceKey = "WpfPremium.SmallFontSize";
    public const string Heading1FontSizeResourceKey = "WpfPremium.Heading1FontSize";
    public const string Heading2FontSizeResourceKey = "WpfPremium.Heading2FontSize";
    public const string Heading3FontSizeResourceKey = "WpfPremium.Heading3FontSize";
    public const string ControlMinHeightResourceKey = "WpfPremium.ControlMinHeight";
    public const string ControlPaddingResourceKey = "WpfPremium.ControlPadding";
    public const string ControlCornerRadiusResourceKey = "WpfPremium.ControlCornerRadius";
    public const string SplitButtonMainCornerRadiusResourceKey = "WpfPremium.SplitButtonMainCornerRadius";
    public const string SplitButtonArrowCornerRadiusResourceKey = "WpfPremium.SplitButtonArrowCornerRadius";
    public const string IconSizeResourceKey = "WpfPremium.IconSize";
    public const string CheckBoxSizeResourceKey = "WpfPremium.CheckBoxSize";
    public const string SwitchWidthResourceKey = "WpfPremium.SwitchWidth";
    public const string SwitchHeightResourceKey = "WpfPremium.SwitchHeight";
    public const string SwitchCornerRadiusResourceKey = "WpfPremium.SwitchCornerRadius";
    public const string SwitchThumbSizeResourceKey = "WpfPremium.SwitchThumbSize";
    public const string TableRowHeightResourceKey = "WpfPremium.TableRowHeight";

    public static event EventHandler? SettingsChanged;

    public static AppearanceSettings GetCurrent(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        application.Dispatcher.VerifyAccess();

        var resources = application.Resources;
        var fontFamily = resources.Contains(FontFamilyResourceKey)
            ? resources[FontFamilyResourceKey] as FontFamily
            : null;
        var fontSize = resources.Contains(FontSizeResourceKey) &&
                       resources[FontSizeResourceKey] is double configuredFontSize
            ? configuredFontSize
            : AppearanceSettings.Default.FontSize;
        var componentSize = resources.Contains("WpfPremium.ComponentSize") &&
                            resources["WpfPremium.ComponentSize"] is ComponentSize configuredComponentSize
            ? configuredComponentSize
            : AppearanceSettings.Default.ComponentSize;
        return new AppearanceSettings(
            fontFamily?.Source ?? AppearanceSettings.Default.FontFamily,
            fontSize,
            componentSize);
    }

    public static void Apply(Application application, AppearanceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(settings);
        application.Dispatcher.VerifyAccess();

        if (string.IsNullOrWhiteSpace(settings.FontFamily))
        {
            throw new ArgumentException("Font family cannot be empty.", nameof(settings));
        }

        if (!double.IsFinite(settings.FontSize) || settings.FontSize is < 9 or > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "Font size must be between 9 and 32 points.");
        }

        if (!Enum.IsDefined(settings.ComponentSize))
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "Unsupported component size.");
        }

        var resources = application.Resources;
        resources[FontFamilyResourceKey] = new FontFamily(settings.FontFamily);
        resources[FontSizeResourceKey] = settings.FontSize;
        resources[SmallFontSizeResourceKey] = Math.Max(8, settings.FontSize - 2);
        resources[Heading1FontSizeResourceKey] = settings.FontSize * 2.15;
        resources[Heading2FontSizeResourceKey] = settings.FontSize * 1.72;
        resources[Heading3FontSizeResourceKey] = settings.FontSize * 1.43;
        resources["WpfPremium.ComponentSize"] = settings.ComponentSize;

        var dimensions = settings.ComponentSize switch
        {
            ComponentSize.Small => new ComponentDimensions(30, new Thickness(8, 4, 8, 4), new CornerRadius(4), 12, 14, 40, 22, new CornerRadius(11), 14, 32),
            ComponentSize.Medium => new ComponentDimensions(34, new Thickness(10, 6, 10, 6), new CornerRadius(6), 14, 16, 44, 24, new CornerRadius(12), 18, 36),
            ComponentSize.Large => new ComponentDimensions(42, new Thickness(14, 8, 14, 8), new CornerRadius(8), 18, 20, 52, 28, new CornerRadius(14), 20, 44),
            _ => throw new ArgumentOutOfRangeException(nameof(settings), "Unsupported component size.")
        };

        resources[ControlMinHeightResourceKey] = dimensions.ControlMinHeight;
        resources[ControlPaddingResourceKey] = dimensions.ControlPadding;
        resources[ControlCornerRadiusResourceKey] = dimensions.ControlCornerRadius;
        resources[SplitButtonMainCornerRadiusResourceKey] = new CornerRadius(dimensions.ControlCornerRadius.TopLeft, 0, 0, dimensions.ControlCornerRadius.TopLeft);
        resources[SplitButtonArrowCornerRadiusResourceKey] = new CornerRadius(0, dimensions.ControlCornerRadius.TopLeft, dimensions.ControlCornerRadius.TopLeft, 0);
        resources[IconSizeResourceKey] = dimensions.IconSize;
        resources[CheckBoxSizeResourceKey] = dimensions.CheckBoxSize;
        resources[SwitchWidthResourceKey] = dimensions.SwitchWidth;
        resources[SwitchHeightResourceKey] = dimensions.SwitchHeight;
        resources[SwitchCornerRadiusResourceKey] = dimensions.SwitchCornerRadius;
        resources[SwitchThumbSizeResourceKey] = dimensions.SwitchThumbSize;
        resources[TableRowHeightResourceKey] = dimensions.TableRowHeight;
        SettingsChanged?.Invoke(application, EventArgs.Empty);
    }

    private sealed record ComponentDimensions(
        double ControlMinHeight,
        Thickness ControlPadding,
        CornerRadius ControlCornerRadius,
        double IconSize,
        double CheckBoxSize,
        double SwitchWidth,
        double SwitchHeight,
        CornerRadius SwitchCornerRadius,
        double SwitchThumbSize,
        double TableRowHeight);
}
