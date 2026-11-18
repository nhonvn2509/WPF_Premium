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
        PremiumInteractionTests.RunOnSta(() =>
        {
            var application = Application.Current!;
                Assert.AreEqual(
                    WpfPremiumStyle.Default,
                    new AppearanceSettings("Segoe UI", 14, ComponentSize.Medium).Style);
                var styles = new[]
                {
                    (WpfPremiumStyle.Default, 6d),
                    (WpfPremiumStyle.Modern, 16d),
                    (WpfPremiumStyle.Simple, 0d),
                    (WpfPremiumStyle.Classic, 2d)
                };
                var styleBackgrounds = new List<Color>();

                foreach (var (style, radius) in styles)
                {
                    ThemeStyleManager.SetStyle(application, style);

                    Assert.AreEqual(style, ThemeStyleManager.GetCurrent(application));
                    Assert.AreEqual(
                        radius,
                        ((CornerRadius)application.Resources[AppearanceSettingsManager.ControlCornerRadiusResourceKey]).TopLeft);
                    Assert.AreEqual(
                        new Thickness(1),
                        application.Resources["WpfPremium.ControlBorderThickness"]);
                    Assert.AreEqual(
                        new Thickness(1),
                        application.Resources["WpfPremium.SurfaceBorderThickness"]);
                    styleBackgrounds.Add(GetBrushColor(application.Resources, "BackgroundBrush"));
                    Assert.AreEqual(10, Enumerable.Range(1, 10)
                        .Select(index => GetBrushColor(application.Resources, $"ChartColor{index}Brush"))
                        .Distinct()
                        .Count(), $"{style} should expose ten distinct chart colors.");
                }

                Assert.AreEqual(4, styleBackgrounds.Distinct().Count(), "Every style should have its own color palette.");

                var componentSizes = new[]
                {
                    (ComponentSize.Small, 30d, new Thickness(8, 4, 8, 4), 32d),
                    (ComponentSize.Medium, 34d, new Thickness(10, 6, 10, 6), 36d),
                    (ComponentSize.Large, 42d, new Thickness(14, 8, 14, 8), 44d)
                };
                foreach (var (size, expectedHeight, expectedPadding, expectedRowHeight) in componentSizes)
                {
                    foreach (var (style, _) in styles)
                    {
                        AppearanceSettingsManager.Apply(
                            application,
                            AppearanceSettingsManager.GetCurrent(application) with { ComponentSize = size, Style = style });

                        Assert.AreEqual(
                            expectedHeight,
                            application.Resources[AppearanceSettingsManager.ControlMinHeightResourceKey],
                            $"{style} should preserve the {size} control height.");
                        Assert.AreEqual(
                            expectedPadding,
                            application.Resources[AppearanceSettingsManager.ControlPaddingResourceKey],
                            $"{style} should preserve the {size} control padding.");
                        Assert.AreEqual(
                            expectedRowHeight,
                            application.Resources[AppearanceSettingsManager.TableRowHeightResourceKey],
                            $"{style} should preserve the {size} table row height.");
                    }
                }

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
                Assert.AreEqual(30d, application.Resources[AppearanceSettingsManager.ControlMinHeightResourceKey]);
        });
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
