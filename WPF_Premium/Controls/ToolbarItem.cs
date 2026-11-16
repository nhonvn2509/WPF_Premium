using System.Windows;
using System.Windows.Controls;

namespace WPF_Premium.Controls;

public sealed class ToolbarItem : ContentControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(
            nameof(Label),
            typeof(string),
            typeof(ToolbarItem),
            new PropertyMetadata(string.Empty));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    static ToolbarItem() =>
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ToolbarItem),
            new FrameworkPropertyMetadata(typeof(ToolbarItem)));
}