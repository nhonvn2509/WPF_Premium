using System.Windows;
using System.Windows.Controls;
using WPF_Premium.Controls;
using WPF_Premium.Themes;

namespace WPF_Premium_Demo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var arguments = Environment.GetCommandLineArgs();
        var previewArgumentIndex = Array.FindIndex(arguments, argument => argument == "--preview");
        if (previewArgumentIndex >= 0 && previewArgumentIndex + 1 < arguments.Length)
        {
            var previewPath = arguments[previewArgumentIndex + 1];
            Loaded += (_, _) =>
            {
                FilePreview.LoadFile(previewPath);
                FilePreview.BringIntoView();
            };
        }
    }

    private void SetTheme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string themeName })
        {
            throw new InvalidOperationException("Theme buttons must specify a theme name.");
        }

        var theme = themeName switch
        {
            "Light" => WpfPremiumTheme.Light,
            "Dark" => WpfPremiumTheme.Dark,
            _ => throw new InvalidOperationException($"Unknown theme '{themeName}'.")
        };
        var application = Application.Current
            ?? throw new InvalidOperationException("The WPF application is not available.");
        ThemeManager.SetTheme(application, theme);
    }

    private void OpenDialog_Click(object sender, RoutedEventArgs e)
    {
        var content = new TextBlock
        {
            Text = "Lớp phủ hộp thoại này được căn giữa cửa sổ cha.",
            TextWrapping = TextWrapping.Wrap
        };
        DialogService.ShowDialog(this, "Modal dialog", content);
    }

    private void ChoosePreviewFile_Click(object sender, RoutedEventArgs e) => FilePreview.Browse();
}
