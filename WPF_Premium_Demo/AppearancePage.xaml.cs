using System.Windows;
using System.Windows.Controls;
using WPF_Premium.Themes;

namespace WPF_Premium_Demo;

public partial class AppearancePage : UserControl
{
    public AppearancePage()
    {
        InitializeComponent();
    }

    private void SetTheme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string themeName }) return;

        var theme = themeName switch
        {
            "Light" => WpfPremiumTheme.Light,
            "Dark" => WpfPremiumTheme.Dark,
            "System" => WpfPremiumTheme.Light, // System theme not supported, default to Light
            _ => throw new InvalidOperationException($"Unknown theme '{themeName}'.")
        };
        var application = Application.Current
            ?? throw new InvalidOperationException("The WPF application is not available.");
        ThemeManager.SetTheme(application, theme);
    }
}