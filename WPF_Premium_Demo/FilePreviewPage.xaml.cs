using System.Windows;
using System.Windows.Controls;
using WPF_Premium.Controls;

namespace WPF_Premium_Demo;

public partial class FilePreviewPage : UserControl
{
    public FilePreviewPage()
    {
        InitializeComponent();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        FilePreview.Browse();
    }

    public void LoadFile(string path) => FilePreview.LoadFile(path);
}