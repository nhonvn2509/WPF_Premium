using System.Windows;
using System.Windows.Controls;

namespace WPF_Premium.Controls;

public enum BadgeVariant
{
    Primary,
    Secondary,
    Success,
    Warning,
    Destructive,
    Outline
}

public class Badge : ContentControl
{
    public static readonly DependencyProperty VariantProperty =
        DependencyProperty.Register(
            nameof(Variant),
            typeof(BadgeVariant),
            typeof(Badge),
            new PropertyMetadata(BadgeVariant.Primary));

    public BadgeVariant Variant
    {
        get => (BadgeVariant)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }
}
