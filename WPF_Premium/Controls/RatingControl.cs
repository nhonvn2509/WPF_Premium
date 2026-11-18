using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using MahApps.Metro.IconPacks;
using WPF_Premium.Themes;

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
            new PropertyMetadata(false, OnVisualChanged));

    public static readonly DependencyProperty AllowClearProperty =
        DependencyProperty.Register(
            nameof(AllowClear),
            typeof(bool),
            typeof(RatingControl),
            new PropertyMetadata(false));

    public static readonly DependencyProperty StarSizeProperty =
        DependencyProperty.Register(
            nameof(StarSize),
            typeof(double),
            typeof(RatingControl),
            new PropertyMetadata(20d, OnVisualChanged));

    public static readonly DependencyProperty FilledBrushProperty =
        DependencyProperty.Register(
            nameof(FilledBrush),
            typeof(Brush),
            typeof(RatingControl),
            new PropertyMetadata(null, OnVisualChanged));

    public static readonly DependencyProperty EmptyBrushProperty =
        DependencyProperty.Register(
            nameof(EmptyBrush),
            typeof(Brush),
            typeof(RatingControl),
            new PropertyMetadata(null, OnVisualChanged));

    public RatingControl()
    {
        Content = _items;
        AutomationProperties.SetName(this, "Đánh giá");
        RebuildItems();
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

    public bool AllowClear
    {
        get => (bool)GetValue(AllowClearProperty);
        set => SetValue(AllowClearProperty, value);
    }

    public double StarSize
    {
        get => (double)GetValue(StarSizeProperty);
        set => SetValue(StarSizeProperty, value);
    }

    public Brush? FilledBrush
    {
        get => (Brush?)GetValue(FilledBrushProperty);
        set => SetValue(FilledBrushProperty, value);
    }

    public Brush? EmptyBrush
    {
        get => (Brush?)GetValue(EmptyBrushProperty);
        set => SetValue(EmptyBrushProperty, value);
    }

    public event RoutedPropertyChangedEventHandler<int>? ValueChanged;

    private static object CoerceValue(DependencyObject d, object baseValue) =>
        Math.Clamp((int)baseValue, 0, ((RatingControl)d).Maximum);

    private static object CoerceMaximum(DependencyObject d, object baseValue) =>
        Math.Clamp((int)baseValue, 1, 10);

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (RatingControl)d;
        control.UpdateItemStates();
        control.ValueChanged?.Invoke(control, new RoutedPropertyChangedEventArgs<int>((int)e.OldValue, (int)e.NewValue));
    }

    private static void OnMaximumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (RatingControl)d;
        control.CoerceValue(ValueProperty);
        control.RebuildItems();
    }

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((RatingControl)d).UpdateItemStates();

    private void RebuildItems()
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
                Width = StarSize,
                Height = StarSize
            };
            ApplyStarBrush(icon, index <= Value);
            var button = new Button
            {
                Content = icon,
                Padding = new Thickness(3),
                IsEnabled = !IsReadOnly,
                ToolTip = $"{index} / {Maximum}"
            };
            button.SetResourceReference(FrameworkElement.StyleProperty, "ButtonGhost");
            AutomationProperties.SetName(button, $"Chọn {index} sao");
            button.Click += (_, _) =>
            {
                var nextValue = AllowClear && Value == value ? 0 : value;
                SetCurrentValue(ValueProperty, nextValue);
            };
            _items.Children.Add(button);
        }
    }

    private void UpdateItemStates()
    {
        if (_items is null || _items.Children.Count != Maximum)
        {
            RebuildItems();
            return;
        }

        for (var i = 0; i < _items.Children.Count; i++)
        {
            if (_items.Children[i] is Button button)
            {
                button.IsEnabled = !IsReadOnly;
                if (button.Content is PackIconMaterial icon)
                {
                    var isFilled = (i + 1) <= Value;
                    icon.Kind = isFilled ? PackIconMaterialKind.Star : PackIconMaterialKind.StarOutline;
                    icon.Width = StarSize;
                    icon.Height = StarSize;
                    ApplyStarBrush(icon, isFilled);
                }
            }
        }
    }

    private void ApplyStarBrush(PackIconMaterial icon, bool isFilled)
    {
        var customBrush = isFilled ? FilledBrush : EmptyBrush;
        if (customBrush is not null)
        {
            icon.Foreground = customBrush;
        }
        else
        {
            icon.SetResourceReference(Control.ForegroundProperty, isFilled ? WpfPremiumResourceKeys.WarningBrush : WpfPremiumResourceKeys.MutedForegroundBrush);
        }
    }
}
