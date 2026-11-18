using System.Windows;
using System.Windows.Media;

namespace WPF_Premium.Themes;

public enum WpfPremiumTheme
{
    Light,
    Dark
}

public static class ThemeManager
{
    private const string LightThemeSource = "/WPF_Premium;component/Themes/WpfPremium.xaml";
    private const string DarkThemeSource = "/WPF_Premium;component/Themes/WpfPremiumDark.xaml";

    public static event EventHandler? ThemeChanged;

    /// <summary>
    /// Overrides the primary accent brush and automatically computes an accessible contrasting foreground brush.
    /// </summary>
    public static void SetAccentColor(Application application, Color accentColor)
    {
        ArgumentNullException.ThrowIfNull(application);
        application.Dispatcher.VerifyAccess();

        var primaryBrush = new SolidColorBrush(accentColor);
        primaryBrush.Freeze();

        var luminance = (0.299 * accentColor.R + 0.587 * accentColor.G + 0.114 * accentColor.B) / 255d;
        var foregroundColor = luminance > 0.6 ? Color.FromRgb(24, 24, 27) : Color.FromRgb(250, 250, 250);
        var foregroundBrush = new SolidColorBrush(foregroundColor);
        foregroundBrush.Freeze();

        application.Resources[WpfPremiumResourceKeys.PrimaryBrush] = primaryBrush;
        application.Resources[WpfPremiumResourceKeys.PrimaryForegroundBrush] = foregroundBrush;
        application.Resources[WpfPremiumResourceKeys.FocusBrush] = primaryBrush;
        ThemeChanged?.Invoke(application, EventArgs.Empty);
    }

    /// <summary>
    /// Removes custom accent overrides so the active theme's default primary palette is restored.
    /// </summary>
    public static void ResetAccentColor(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        application.Dispatcher.VerifyAccess();

        application.Resources.Remove(WpfPremiumResourceKeys.PrimaryBrush);
        application.Resources.Remove(WpfPremiumResourceKeys.PrimaryForegroundBrush);
        application.Resources.Remove(WpfPremiumResourceKeys.FocusBrush);
        ThemeChanged?.Invoke(application, EventArgs.Empty);
    }

    public static void SetTheme(Application application, WpfPremiumTheme theme)
    {
        ArgumentNullException.ThrowIfNull(application);
        application.Dispatcher.VerifyAccess();

        var source = theme switch
        {
            WpfPremiumTheme.Light => LightThemeSource,
            WpfPremiumTheme.Dark => DarkThemeSource,
            _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, "Unsupported WPF Premium theme.")
        };
        var dictionaries = application.Resources.MergedDictionaries;
        var themeIndex = -1;

        for (var index = 0; index < dictionaries.Count; index++)
        {
            if (!IsThemeDictionary(dictionaries[index].Source))
            {
                continue;
            }

            themeIndex = index;
            if (HasSource(dictionaries[index].Source, source))
            {
                ThemeStyleManager.ApplyThemePalette(application, theme);
                return;
            }

            break;
        }

        var newTheme = new ResourceDictionary { Source = new Uri(source, UriKind.Relative) };
        if (themeIndex < 0)
        {
            dictionaries.Add(newTheme);
            ThemeStyleManager.ApplyThemePalette(application, theme);
            ThemeChanged?.Invoke(application, EventArgs.Empty);
            return;
        }

        dictionaries.RemoveAt(themeIndex);
        dictionaries.Insert(themeIndex, newTheme);
        ThemeStyleManager.ApplyThemePalette(application, theme);
        ThemeChanged?.Invoke(application, EventArgs.Empty);
    }

    internal static WpfPremiumTheme GetCurrentTheme(Application application)
    {
        foreach (var dictionary in application.Resources.MergedDictionaries)
        {
            if (HasSource(dictionary.Source, DarkThemeSource))
            {
                return WpfPremiumTheme.Dark;
            }
            if (HasSource(dictionary.Source, LightThemeSource))
            {
                return WpfPremiumTheme.Light;
            }
        }

        return WpfPremiumTheme.Light;
    }

    private static bool IsThemeDictionary(Uri? source) =>
        HasSource(source, LightThemeSource) || HasSource(source, DarkThemeSource);

    private static bool HasSource(Uri? source, string expectedSource) =>
        source?.OriginalString.EndsWith(expectedSource, StringComparison.OrdinalIgnoreCase) == true;
}
