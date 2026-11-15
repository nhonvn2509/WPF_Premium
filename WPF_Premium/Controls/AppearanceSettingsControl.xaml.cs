using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using WPF_Premium.Themes;

namespace WPF_Premium.Controls;

public partial class AppearanceSettingsControl : UserControl
{
    public AppearanceSettingsControl()
    {
        InitializeComponent();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this)
            ?? throw new InvalidOperationException("Appearance settings require a host window.");
        AppearanceSettingsDialog.Show(owner);
    }
}

internal static class AppearanceSettingsDialog
{
    public static void Show(Window owner)
    {
        var application = Application.Current
            ?? throw new InvalidOperationException("The WPF application is not available.");
        application.Dispatcher.VerifyAccess();

        var current = AppearanceSettingsManager.GetCurrent(application);
        var fontFamilies = Fonts.SystemFontFamilies
            .Select(font => font.Source)
            .Append(current.FontFamily)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        var componentSizes = new[]
        {
            new ComponentSizeOption("Nhỏ (sm)", ComponentSize.Small),
            new ComponentSizeOption("Vừa (md)", ComponentSize.Medium),
            new ComponentSizeOption("Lớn (lg)", ComponentSize.Large)
        };
        var styles = new[]
        {
            new StyleOption("Mặc định", WpfPremiumStyle.Default),
            new StyleOption("Hiện đại", WpfPremiumStyle.Modern),
            new StyleOption("Đơn giản", WpfPremiumStyle.Simple),
            new StyleOption("Cổ điển", WpfPremiumStyle.Classic)
        };

        var dialog = new PremiumWindow
        {
            Title = "Tùy chỉnh giao diện",
            Owner = owner,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Width = 480,
            SizeToContent = SizeToContent.Height,
            MinHeight = 420,
            Background = GetBrush(application, "BackgroundBrush", Colors.White),
            Foreground = GetBrush(application, "ForegroundBrush", Colors.Black)
        };
        AutomationProperties.SetAutomationId(dialog, "AppearanceSettingsDialog");

        var fontFamilySelector = new ComboBoxControl
        {
            ItemsSource = fontFamilies,
            SelectedItem = fontFamilies.FirstOrDefault(name =>
                string.Equals(name, current.FontFamily, StringComparison.OrdinalIgnoreCase))
                ?? fontFamilies.FirstOrDefault(name => name.StartsWith("Segoe UI", StringComparison.OrdinalIgnoreCase))
                ?? fontFamilies.FirstOrDefault(),
            Placeholder = "Phông chữ",
            SearchEnabled = true,
            MinWidth = 250,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        AutomationProperties.SetAutomationId(fontFamilySelector, "AppearanceFontFamilySelector");

        var fontSizeSlider = new Slider
        {
            Minimum = 9,
            Maximum = 32,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            Value = current.FontSize,
            VerticalAlignment = VerticalAlignment.Center
        };
        fontSizeSlider.SetResourceReference(FrameworkElement.StyleProperty, "Slider");
        AutomationProperties.SetAutomationId(fontSizeSlider, "AppearanceFontSizeSlider");
        var fontSizeLabel = new TextBlock
        {
            Text = $"{current.FontSize:0} pt",
            MinWidth = 48,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Right
        };
        fontSizeSlider.ValueChanged += (_, _) => fontSizeLabel.Text = $"{fontSizeSlider.Value:0} pt";

        var componentSizeSelector = new ComboBoxControl
        {
            ItemsSource = componentSizes,
            DisplayMemberPath = nameof(ComponentSizeOption.Label),
            SelectedItem = componentSizes.First(option => option.Size == current.ComponentSize),
            Placeholder = "Kích thước thành phần",
            SearchEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        AutomationProperties.SetAutomationId(componentSizeSelector, "AppearanceComponentSizeSelector");

        var styleSelector = new ComboBoxControl
        {
            ItemsSource = styles,
            DisplayMemberPath = nameof(StyleOption.Label),
            SelectedItem = styles.First(option => option.Style == current.Style),
            Placeholder = "Phong cách giao diện",
            SearchEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        AutomationProperties.SetAutomationId(styleSelector, "AppearanceStyleSelector");

        var previewText = new TextBlock
        {
            Text = "Văn bản xem trước · WPF Premium",
            Margin = new Thickness(0, 0, 0, 12),
            TextWrapping = TextWrapping.Wrap
        };
        var previewButton = new Button
        {
            Content = "Nút xem trước",
            HorizontalAlignment = HorizontalAlignment.Left
        };
        if (dialog.TryFindResource("ButtonOutline") is Style previewButtonStyle)
        {
            previewButton.Style = previewButtonStyle;
        }

        void UpdatePreview()
        {
            if (fontFamilySelector.SelectedItem is string selectedFont)
            {
                previewText.FontFamily = new FontFamily(selectedFont);
            }

            previewText.FontSize = fontSizeSlider.Value;
            var dimensions = componentSizeSelector.SelectedItem is ComponentSizeOption selectedOption
                ? selectedOption.Size
                : current.ComponentSize;
            var selectedStyle = styleSelector.SelectedItem is StyleOption styleOption
                ? styleOption.Style
                : current.Style;
            var metrics = ThemeStyleManager.GetControlMetrics(selectedStyle, dimensions);
            previewButton.MinHeight = metrics.ControlMinHeight;
            previewButton.Padding = metrics.ControlPadding;
            previewText.Foreground = ThemeStyleManager.GetPreviewBrush(application, selectedStyle, "ForegroundBrush");
            previewButton.Background = ThemeStyleManager.GetPreviewBrush(application, selectedStyle, "PrimaryBrush");
            previewButton.Foreground = ThemeStyleManager.GetPreviewBrush(application, selectedStyle, "PrimaryForegroundBrush");
            previewButton.BorderBrush = ThemeStyleManager.GetPreviewBrush(application, selectedStyle, "PrimaryBrush");
            previewButton.Resources[AppearanceSettingsManager.ControlCornerRadiusResourceKey] = metrics.ControlCornerRadius;
        }

        fontFamilySelector.SelectionChanged += (_, _) => UpdatePreview();
        componentSizeSelector.SelectionChanged += (_, _) => UpdatePreview();
        styleSelector.SelectionChanged += (_, _) => UpdatePreview();

        var body = new StackPanel { Margin = new Thickness(24) };
        body.Children.Add(new TextBlock
        {
            Text = "Tùy chỉnh giao diện",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 18)
        });
        body.Children.Add(CreateSettingRow("Phông chữ", fontFamilySelector));
        body.Children.Add(CreateSettingRow("Cỡ chữ", CreateSliderRow(fontSizeSlider, fontSizeLabel)));
        body.Children.Add(CreateSettingRow("Kích thước thành phần", componentSizeSelector));
        body.Children.Add(CreateSettingRow("Phong cách", styleSelector));
        body.Children.Add(new Separator { Margin = new Thickness(0, 14, 0, 14) });
        body.Children.Add(previewText);
        body.Children.Add(previewButton);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 20, 0, 0)
        };
        var resetButton = new Button
        {
            Content = "Mặc định",
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(10, 6, 10, 6)
        };
        var cancelButton = new Button
        {
            Content = "Hủy",
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(10, 6, 10, 6)
        };
        var applyButton = new Button
        {
            Content = "Áp dụng",
            Padding = new Thickness(12, 6, 12, 6),
            IsDefault = true
        };
        AutomationProperties.SetAutomationId(resetButton, "AppearanceResetButton");
        AutomationProperties.SetAutomationId(cancelButton, "AppearanceCancelButton");
        AutomationProperties.SetAutomationId(applyButton, "AppearanceApplyButton");
        if (dialog.TryFindResource("ButtonOutline") is Style outlineStyle)
        {
            resetButton.Style = outlineStyle;
            cancelButton.Style = outlineStyle;
        }

        if (dialog.TryFindResource("ButtonPrimary") is Style primaryStyle)
        {
            applyButton.Style = primaryStyle;
        }

        resetButton.Click += (_, _) =>
        {
            AppearanceSettingsManager.Apply(application, AppearanceSettings.Default);
            dialog.DialogResult = true;
        };
        cancelButton.Click += (_, _) => dialog.DialogResult = false;
        applyButton.Click += (_, _) =>
        {
            if (fontFamilySelector.SelectedItem is not string selectedFont ||
                componentSizeSelector.SelectedItem is not ComponentSizeOption selectedOption ||
                styleSelector.SelectedItem is not StyleOption selectedStyle)
            {
                return;
            }

            AppearanceSettingsManager.Apply(
                application,
                new AppearanceSettings(selectedFont, fontSizeSlider.Value, selectedOption.Size, selectedStyle.Style));
            dialog.DialogResult = true;
        };
        actions.Children.Add(resetButton);
        actions.Children.Add(cancelButton);
        actions.Children.Add(applyButton);
        body.Children.Add(actions);

        var dialogSurface = new Border
        {
            Background = GetBrush(application, "CardBrush", Colors.White),
            BorderBrush = GetBrush(application, "BorderBrush", Colors.LightGray),
            Child = body
        };
        dialogSurface.SetResourceReference(Border.BorderThicknessProperty, "WpfPremium.SurfaceBorderThickness");
        dialogSurface.SetResourceReference(Border.CornerRadiusProperty, "WpfPremium.SurfaceCornerRadius");
        dialog.Content = dialogSurface;

        UpdatePreview();
        dialog.ShowDialog();
    }

    private static UIElement CreateSettingRow(string label, UIElement control)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            Margin = new Thickness(0, 0, 0, 6),
            FontWeight = FontWeights.Medium
        });
        panel.Children.Add(control);
        return panel;
    }

    private static UIElement CreateSliderRow(Slider slider, TextBlock label)
    {
        var panel = new Grid();
        panel.ColumnDefinitions.Add(new ColumnDefinition());
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(label, 1);
        panel.Children.Add(slider);
        panel.Children.Add(label);
        return panel;
    }

    private static Brush GetBrush(Application application, string key, Color fallback) =>
        application.TryFindResource(key) as Brush ?? new SolidColorBrush(fallback);

    private sealed record ComponentSizeOption(string Label, ComponentSize Size);
    private sealed record StyleOption(string Label, WpfPremiumStyle Style);
}
