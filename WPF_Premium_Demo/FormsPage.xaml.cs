using System.Windows.Controls;
using WPF_Premium.Controls;

namespace WPF_Premium_Demo;

public partial class FormsPage : UserControl
{
    public FormsPage()
    {
        InitializeComponent();
        PlatformCombo.ItemsSource = new[] { "WPF", "WinUI", "Avalonia", "Blazor", "MAUI" };
        TechMultiSelect.ItemsSource = new[] { "WPF", "WinUI", "Avalonia", "Blazor", "MAUI", "React", "Vue" };
    }
}