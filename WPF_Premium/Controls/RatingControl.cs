using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MahApps.Metro.IconPacks;

namespace WPF_Premium.Controls;

public sealed class RatingControl : UserControl
{
    private readonly StackPanel _items = new() { Orientation = Orientation.Horizontal };

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(int),
            typeof(RatingControl),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged, CoerceValue));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(
            nameof(Maximum),
            typeof(int),
            typeof(RatingControl),
            new PropertyMetadata(5, OnMaximumChanged, CoerceMaximum));

    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(
            nameof(IsReadOnly),
            typeof(bool),
            typeof(RatingControl),
            new PropertyMetadata(false, OnReadOnlyChanged));

    public RatingControl()
    {
        Content = _items;
        AutomationProperties.SetName(this, "Đánh giá");
        UpdateItems();
    }

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public int Maximum
    {
        get => (int)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public event RoutedPropertyChangedEventHandler<int>? ValueChanged;

    private static object CoerceValue(DependencyObject d, object baseValue) =>
        Math.Clamp((int)baseValue, 0, ((RatingControl)d).Maximum);

    private static object CoerceMaximum(DependencyObject d, object baseValue) =>
        Math.Clamp((int)baseValue, 1, 10);

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (RatingControl)d;
        control.UpdateItems();
        control.ValueChanged?.Invoke(control, new RoutedPropertyChangedEventArgs<int>((int)e.OldValue, (int)e.NewValue));
    }

    private static void OnMaximumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (RatingControl)d;
        control.CoerceValue(ValueProperty);
        control.UpdateItems();
    }

    private static void OnReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((RatingControl)d).UpdateItems();

    private void UpdateItems()
    {
        if (_items is null)
        {
            return;
        }

        _items.Children.Clear();
        for (var index = 1; index <= Maximum; index++)
        {
            var value = index;
            var icon = new PackIconMaterial
            {
                Kind = index <= Value ? PackIconMaterialKind.Star : PackIconMaterialKind.StarOutline,
                Width = 20,
                Height = 20
            };
            icon.SetResourceReference(Control.ForegroundProperty, "WarningBrush");
            var button = new Button
            {
                Content = icon,
                Padding = new Thickness(3),
                IsEnabled = !IsReadOnly,
                ToolTip = $"{index} / {Maximum}"
            };
            button.SetResourceReference(FrameworkElement.StyleProperty, "ButtonGhost");
            AutomationProperties.SetName(button, $"Chọn {index} sao");
            button.Click += (_, _) => SetCurrentValue(ValueProperty, value);
            _items.Children.Add(button);
        }
    }
}
