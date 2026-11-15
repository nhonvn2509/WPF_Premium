using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using WPF_Premium.Themes;

namespace WPF_Premium.UIAutomationTests;

[TestClass]
public sealed class ThemeStyleManagerTests
{
    [TestMethod]
    public void Applies_each_style_and_preserves_it_when_component_size_changes()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var application = new Application();
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("/WPF_Premium;component/Themes/WpfPremium.xaml", UriKind.Relative)
                });
                Assert.AreEqual(
                    WpfPremiumStyle.Default,
                    new AppearanceSettings("Segoe UI", 14, ComponentSize.Medium).Style);
                var styles = new[]
                {
                    (WpfPremiumStyle.Default, 6d, 34d),
                    (WpfPremiumStyle.Modern, 16d, 40d),
                    (WpfPremiumStyle.Simple, 0d, 32d),
                    (WpfPremiumStyle.Classic, 2d, 36d)
                };
                var styleBackgrounds = new List<Color>();

                foreach (var (style, radius, minHeight) in styles)
                {
                    ThemeStyleManager.SetStyle(application, style);

                    Assert.AreEqual(style, ThemeStyleManager.GetCurrent(application));
                    Assert.AreEqual(
                        radius,
                        ((CornerRadius)application.Resources[AppearanceSettingsManager.ControlCornerRadiusResourceKey]).TopLeft);
                    Assert.AreEqual(minHeight, application.Resources[AppearanceSettingsManager.ControlMinHeightResourceKey]);
                    styleBackgrounds.Add(GetBrushColor(application.Resources, "BackgroundBrush"));
                }

                Assert.AreEqual(4, styleBackgrounds.Distinct().Count(), "Every style should have its own color palette.");

                ThemeStyleManager.SetStyle(application, WpfPremiumStyle.Modern);
                var modernLightBackground = GetBrushColor(application.Resources, "BackgroundBrush");
                ThemeManager.SetTheme(application, WpfPremiumTheme.Dark);
                var modernDarkBackground = GetBrushColor(application.Resources, "BackgroundBrush");
                Assert.AreNotEqual(modernLightBackground, modernDarkBackground,
                    "The selected style should provide a matching palette for dark mode.");

                AppearanceSettingsManager.Apply(
                    application,
                    AppearanceSettingsManager.GetCurrent(application) with { ComponentSize = ComponentSize.Small });

                Assert.AreEqual(WpfPremiumStyle.Modern, ThemeStyleManager.GetCurrent(application));
                Assert.AreEqual(
                    12d,
                    ((CornerRadius)application.Resources[AppearanceSettingsManager.ControlCornerRadiusResourceKey]).TopLeft);
                Assert.AreEqual(34d, application.Resources[AppearanceSettingsManager.ControlMinHeightResourceKey]);
                application.Shutdown();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static Color GetBrushColor(ResourceDictionary resources, string key)
    {
        if (resources.Contains(key) && resources[key] is SolidColorBrush brush)
        {
            return brush.Color;
        }

        for (var index = resources.MergedDictionaries.Count - 1; index >= 0; index--)
        {
            var dictionary = resources.MergedDictionaries[index];
            if (TryGetBrushColor(dictionary, key, out var color))
            {
                return color;
            }
        }

        throw new AssertFailedException($"Color resource '{key}' was not found.");
    }

    private static bool TryGetBrushColor(ResourceDictionary resources, string key, out Color color)
    {
        if (resources.Contains(key) && resources[key] is SolidColorBrush brush)
        {
            color = brush.Color;
            return true;
        }

        for (var index = resources.MergedDictionaries.Count - 1; index >= 0; index--)
        {
            if (TryGetBrushColor(resources.MergedDictionaries[index], key, out color))
            {
                return true;
            }
        }

        color = default;
        return false;
    }
}
