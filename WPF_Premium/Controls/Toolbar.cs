using System.Windows;
using System.Windows.Controls;

namespace WPF_Premium.Controls;

public sealed class Toolbar : ItemsControl
{
    static Toolbar() =>
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(Toolbar),
            new FrameworkPropertyMetadata(typeof(Toolbar)));
}
