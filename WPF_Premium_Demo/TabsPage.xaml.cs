using System.Windows.Controls;

namespace WPF_Premium_Demo;

public partial class TabsPage : UserControl
{
    private int _tabCount = 2;

    public TabsPage()
    {
        InitializeComponent();
    }

    private void AddTab_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _tabCount++;
        var tab = new TabItem
        {
            Header = $"Tab {_tabCount}",
            Content = new TextBlock { Text = $"Nội dung tab {_tabCount}", Margin = new System.Windows.Thickness(16) }
        };
        ClosableTabControl.Items.Add(tab);
        ClosableTabControl.SelectedItem = tab;
    }
}