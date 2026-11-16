using System.Windows;
using System.Windows.Controls;

namespace WPF_Premium.Controls;

public sealed class ToolbarButton : Button
{
    static ToolbarButton() =>
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ToolbarButton),
            new FrameworkPropertyMetadata(typeof(ToolbarButton)));
}