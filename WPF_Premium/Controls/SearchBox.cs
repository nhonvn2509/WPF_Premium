using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MahApps.Metro.IconPacks;

namespace WPF_Premium.Controls;

public sealed class SearchBox : UserControl
{
    private readonly TextBox _textBox;
    private readonly Button _clearButton;
    private readonly Border _inputBorder;

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(SearchBox),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(
            nameof(Placeholder),
            typeof(string),
            typeof(SearchBox),
            new PropertyMetadata("Tìm kiếm", OnPlaceholderChanged));

    public static readonly DependencyProperty AutomationIdProperty =
        DependencyProperty.Register(
            nameof(AutomationId),
            typeof(string),
            typeof(SearchBox),
            new PropertyMetadata("SearchBoxInput", OnAutomationIdChanged));

    public SearchBox()
    {
        _textBox = new TextBox
        {
            MinWidth = 0,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent
        };
        _textBox.SetResourceReference(FrameworkElement.StyleProperty, "SearchBoxInput");
        _textBox.Tag = Placeholder;
        AutomationProperties.SetName(_textBox, "Từ khóa tìm kiếm");
        AutomationProperties.SetAutomationId(_textBox, AutomationId);
        _textBox.TextChanged += (_, _) =>
        {
            if (Text != _textBox.Text)
            {
                SetCurrentValue(TextProperty, _textBox.Text);
            }

            UpdateClearButtonVisibility();
        };

        _clearButton = new Button
        {
            Content = new PackIconMaterial
            {
                Kind = PackIconMaterialKind.Close,
                Width = 12,
                Height = 12
            },
            Width = 28,
            Height = 28,
            MinHeight = 0,
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
            ToolTip = "Xóa nội dung tìm kiếm"
        };
        _clearButton.SetResourceReference(FrameworkElement.StyleProperty, "ButtonGhost");
        AutomationProperties.SetName(_clearButton, "Xóa tìm kiếm");
        _clearButton.Click += (_, _) =>
        {
            SetCurrentValue(TextProperty, string.Empty);
            _textBox.Focus();
        };
        ((PackIconMaterial)_clearButton.Content).SetResourceReference(Control.ForegroundProperty, "MutedForegroundBrush");

        var searchIcon = new PackIconMaterial
        {
            Kind = PackIconMaterialKind.Magnify,
            Width = 14,
            Height = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 8, 0)
        };
        searchIcon.SetResourceReference(Control.ForegroundProperty, "MutedForegroundBrush");

        var layout = new Grid { Margin = new Thickness(8, 2, 4, 2) };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition());
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_textBox, 1);
        Grid.SetColumn(_clearButton, 2);
        layout.Children.Add(searchIcon);
        layout.Children.Add(_textBox);
        layout.Children.Add(_clearButton);

        _inputBorder = new Border
        {
            Child = layout,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(4, 1, 4, 1)
        };
        _inputBorder.SetResourceReference(Border.BackgroundProperty, "InputBrush");
        _inputBorder.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        _inputBorder.SetResourceReference(Border.BorderThicknessProperty, "WpfPremium.ControlBorderThickness");
        _inputBorder.SetResourceReference(Border.CornerRadiusProperty, "WpfPremium.ControlCornerRadius");
        _inputBorder.IsKeyboardFocusWithinChanged += (_, _) =>
        {
            _inputBorder.SetResourceReference(
                Border.BorderBrushProperty,
                _inputBorder.IsKeyboardFocusWithin ? "FocusBrush" : "BorderBrush");
        };

        Content = _inputBorder;
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public string AutomationId
    {
        get => (string)GetValue(AutomationIdProperty);
        set => SetValue(AutomationIdProperty, value);
    }

    public event EventHandler? TextChanged;

    public void FocusInput()
    {
        _textBox.Focus();
        _textBox.SelectAll();
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var searchBox = (SearchBox)d;
        var text = (string?)e.NewValue ?? string.Empty;
        if (searchBox._textBox.Text != text)
        {
            searchBox._textBox.Text = text;
        }

        searchBox.UpdateClearButtonVisibility();
        searchBox.TextChanged?.Invoke(searchBox, EventArgs.Empty);
    }

    private static void OnPlaceholderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((SearchBox)d)._textBox.Tag = e.NewValue;

    private static void OnAutomationIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        AutomationProperties.SetAutomationId(((SearchBox)d)._textBox, e.NewValue as string);

    private void UpdateClearButtonVisibility() =>
        _clearButton.Visibility = string.IsNullOrEmpty(_textBox.Text) ? Visibility.Collapsed : Visibility.Visible;
}
