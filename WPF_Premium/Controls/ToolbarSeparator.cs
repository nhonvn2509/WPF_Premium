using System.Windows;
using System.Windows.Controls;

namespace WPF_Premium.Controls;

public sealed class ToolbarSeparator : Separator
{
    static ToolbarSeparator() =>
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ToolbarSeparator),
            new FrameworkPropertyMetadata(typeof(ToolbarSeparator)));
}