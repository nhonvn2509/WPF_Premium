using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using MahApps.Metro.IconPacks;

namespace WPF_Premium.Controls;

/// <summary>
/// A compact numeric stepper control with decrement/increment buttons, direct text entry, and mouse wheel support.
/// </summary>
public sealed class NumericUpDown : UserControl
{
    private readonly TextBox _input;
    private readonly Button _decrementButton;
    private readonly Button _incrementButton;
    private readonly Border _border;
    private bool _updatingText;

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(double),
            typeof(NumericUpDown),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged, CoerceValue));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(
            nameof(Minimum),
            typeof(double),
            typeof(NumericUpDown),
            new PropertyMetadata(0d, OnRangeChanged));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(
            nameof(Maximum),
            typeof(double),
            typeof(NumericUpDown),
            new PropertyMetadata(100d, OnRangeChanged));

    public static readonly DependencyProperty StepProperty =
        DependencyProperty.Register(
            nameof(Step),
            typeof(double),
            typeof(NumericUpDown),
            new PropertyMetadata(1d),
            v => (double)v > 0);

    public static readonly DependencyProperty StringFormatProperty =
        DependencyProperty.Register(
            nameof(StringFormat),
            typeof(string),
            typeof(NumericUpDown),
            new PropertyMetadata("0.##", OnFormatChanged));

    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(
            nameof(IsReadOnly),
            typeof(bool),
            typeof(NumericUpDown),
            new PropertyMetadata(false, OnReadOnlyChanged));

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(
            nameof(CornerRadius),
            typeof(CornerRadius?),
            typeof(NumericUpDown),
            new PropertyMetadata(null, OnVisualOverrideChanged));

    public static readonly DependencyProperty ShowButtonsProperty =
        DependencyProperty.Register(
            nameof(ShowButtons),
            typeof(bool),
            typeof(NumericUpDown),
            new PropertyMetadata(true, OnVisualOverrideChanged));

    public NumericUpDown()
    {
        AutomationProperties.SetName(this, "Bộ chọn số");

        _decrementButton = CreateStepButton(PackIconMaterialKind.Minus, "Giảm", () => StepBy(-Step));
        _incrementButton = CreateStepButton(PackIconMaterialKind.Plus, "Tăng", () => StepBy(Step));

        _input = new TextBox
        {
            BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(6, 2, 6, 2),
            MinWidth = 44
        };
        _input.SetResourceReference(ForegroundProperty, "ForegroundBrush");
        _input.LostFocus += (_, _) => CommitText();
        _input.KeyDown += Input_KeyDown;

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(_decrementButton);
        Grid.SetColumn(_input, 1);
        grid.Children.Add(_input);
        Grid.SetColumn(_incrementButton, 2);
        grid.Children.Add(_incrementButton);

        _border = new Border { Child = grid };
        _border.SetResourceReference(Border.BackgroundProperty, "InputBrush");
        _border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        _border.SetResourceReference(Border.BorderThicknessProperty, "WpfPremium.ControlBorderThickness");
        _border.SetResourceReference(Border.CornerRadiusProperty, "WpfPremium.ControlCornerRadius");
        _border.SetResourceReference(MinHeightProperty, "WpfPremium.ControlMinHeight");

        IsKeyboardFocusWithinChanged += (_, _) =>
            _border.SetResourceReference(Border.BorderBrushProperty, IsKeyboardFocusWithin ? "FocusBrush" : "BorderBrush");
        MouseWheel += OnControlMouseWheel;

        Content = _border;
        UpdateVisualState();
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Step
    {
        get => (double)GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public string StringFormat
    {
        get => (string)GetValue(StringFormatProperty);
        set => SetValue(StringFormatProperty, value);
    }

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public CornerRadius? CornerRadius
    {
        get => (CornerRadius?)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public bool ShowButtons
    {
        get => (bool)GetValue(ShowButtonsProperty);
        set => SetValue(ShowButtonsProperty, value);
    }

    public event RoutedPropertyChangedEventHandler<double>? ValueChanged;

    private static Button CreateStepButton(PackIconMaterialKind iconKind, string tooltip, Action onClick)
    {
        var button = new Button
        {
            Content = new PackIconMaterial { Kind = iconKind, Width = 12, Height = 12 },
            ToolTip = tooltip,
            Padding = new Thickness(8, 2, 8, 2),
            MinWidth = 28
        };
        button.SetResourceReference(StyleProperty, "ButtonGhost");
        AutomationProperties.SetName(button, tooltip);
        button.Click += (_, _) => onClick();
        return button;
    }

    private static object CoerceValue(DependencyObject d, object baseValue)
    {
        var control = (NumericUpDown)d;
        var min = control.Minimum;
        var max = Math.Max(min, control.Maximum);
        var val = (double)baseValue;
        return double.IsNaN(val) ? min : Math.Clamp(val, min, max);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (NumericUpDown)d;
        control.UpdateVisualState();
        control.ValueChanged?.Invoke(control, new RoutedPropertyChangedEventArgs<double>((double)e.OldValue, (double)e.NewValue));
    }

    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (NumericUpDown)d;
        control.CoerceValue(ValueProperty);
        control.UpdateVisualState();
    }

    private static void OnFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((NumericUpDown)d).UpdateVisualState();

    private static void OnReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((NumericUpDown)d).UpdateVisualState();

    private static void OnVisualOverrideChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((NumericUpDown)d).UpdateVisualState();

    private void StepBy(double delta)
    {
        if (IsReadOnly)
        {
            return;
        }

        var next = Math.Round(Value + delta, 6, MidpointRounding.AwayFromZero);
        SetCurrentValue(ValueProperty, Math.Clamp(next, Minimum, Math.Max(Minimum, Maximum)));
    }

    private void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitText();
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            StepBy(Step);
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            StepBy(-Step);
            e.Handled = true;
        }
    }

    private void OnControlMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!IsKeyboardFocusWithin || IsReadOnly)
        {
            return;
        }

        StepBy(e.Delta > 0 ? Step : -Step);
        e.Handled = true;
    }

    private void CommitText()
    {
        if (_updatingText || IsReadOnly)
        {
            return;
        }

        if (double.TryParse(_input.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsed) ||
            double.TryParse(_input.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
        {
            SetCurrentValue(ValueProperty, parsed);
        }
        else
        {
            UpdateVisualState();
        }
    }

    private void UpdateVisualState()
    {
        _updatingText = true;
        try
        {
            var format = string.IsNullOrWhiteSpace(StringFormat) ? "0.##" : StringFormat;
            _input.Text = Value.ToString(format, CultureInfo.CurrentCulture);
            _input.IsReadOnly = IsReadOnly;
            _decrementButton.Visibility = ShowButtons ? Visibility.Visible : Visibility.Collapsed;
            _incrementButton.Visibility = ShowButtons ? Visibility.Visible : Visibility.Collapsed;
            _decrementButton.IsEnabled = !IsReadOnly && Value > Minimum;
            _incrementButton.IsEnabled = !IsReadOnly && Value < Maximum;
            if (CornerRadius is CornerRadius customRadius)
            {
                _border.CornerRadius = customRadius;
            }
            else
            {
                _border.SetResourceReference(Border.CornerRadiusProperty, Themes.WpfPremiumResourceKeys.ControlCornerRadius);
            }
        }
        finally
        {
            _updatingText = false;
        }
    }
}
