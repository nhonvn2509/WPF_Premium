using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MahApps.Metro.IconPacks;

namespace WPF_Premium.Controls;

/// <summary>A reusable WPF window with the WPF Premium themed title bar and caption controls.</summary>
[TemplatePart(Name = WindowRootPartName, Type = typeof(Border))]
[TemplatePart(Name = TitleBarPartName, Type = typeof(FrameworkElement))]
[TemplatePart(Name = MinimizeButtonPartName, Type = typeof(Button))]
[TemplatePart(Name = MaximizeButtonPartName, Type = typeof(Button))]
[TemplatePart(Name = CloseButtonPartName, Type = typeof(Button))]
[TemplatePart(Name = MaximizeIconPartName, Type = typeof(PackIconMaterial))]
public class PremiumWindow : Window
{
    private const string WindowRootPartName = "PART_WindowRoot";
    private const string TitleBarPartName = "PART_TitleBar";
    private const string MinimizeButtonPartName = "PART_MinimizeButton";
    private const string MaximizeButtonPartName = "PART_MaximizeButton";
    private const string CloseButtonPartName = "PART_CloseButton";
    private const string MaximizeIconPartName = "PART_MaximizeIcon";

    public static readonly DependencyProperty TitleBarLeftContentProperty = DependencyProperty.Register(
        nameof(TitleBarLeftContent),
        typeof(object),
        typeof(PremiumWindow),
        new PropertyMetadata(null));

    public static readonly DependencyProperty TitleBarCenterContentProperty = DependencyProperty.Register(
        nameof(TitleBarCenterContent),
        typeof(object),
        typeof(PremiumWindow),
        new PropertyMetadata(null));

    public static readonly DependencyProperty TitleBarRightContentProperty = DependencyProperty.Register(
        nameof(TitleBarRightContent),
        typeof(object),
        typeof(PremiumWindow),
        new PropertyMetadata(null));

    public static readonly DependencyProperty ShowTitleProperty = DependencyProperty.Register(
        nameof(ShowTitle),
        typeof(bool),
        typeof(PremiumWindow),
        new PropertyMetadata(true));

    public static readonly DependencyProperty ShowIconProperty = DependencyProperty.Register(
        nameof(ShowIcon),
        typeof(bool),
        typeof(PremiumWindow),
        new PropertyMetadata(true));

    public static readonly DependencyProperty TitleBarHeightProperty = DependencyProperty.Register(
        nameof(TitleBarHeight),
        typeof(double),
        typeof(PremiumWindow),
        new PropertyMetadata(38d));

    public object? TitleBarLeftContent
    {
        get => GetValue(TitleBarLeftContentProperty);
        set => SetValue(TitleBarLeftContentProperty, value);
    }

    public object? TitleBarCenterContent
    {
        get => GetValue(TitleBarCenterContentProperty);
        set => SetValue(TitleBarCenterContentProperty, value);
    }

    public object? TitleBarRightContent
    {
        get => GetValue(TitleBarRightContentProperty);
        set => SetValue(TitleBarRightContentProperty, value);
    }

    public bool ShowTitle
    {
        get => (bool)GetValue(ShowTitleProperty);
        set => SetValue(ShowTitleProperty, value);
    }

    public bool ShowIcon
    {
        get => (bool)GetValue(ShowIconProperty);
        set => SetValue(ShowIconProperty, value);
    }

    public double TitleBarHeight
    {
        get => (double)GetValue(TitleBarHeightProperty);
        set => SetValue(TitleBarHeightProperty, value);
    }

    private Border? _windowRoot;
    private FrameworkElement? _titleBar;
    private Button? _minimizeButton;
    private Button? _maximizeButton;
    private Button? _closeButton;
    private PackIconMaterial? _maximizeIcon;

    public override void OnApplyTemplate()
    {
        if (_titleBar is not null) _titleBar.MouseLeftButtonDown -= TitleBar_MouseLeftButtonDown;
        if (_minimizeButton is not null) _minimizeButton.Click -= MinimizeButton_Click;
        if (_maximizeButton is not null) _maximizeButton.Click -= MaximizeButton_Click;
        if (_closeButton is not null) _closeButton.Click -= CloseButton_Click;
        StateChanged -= PremiumWindow_StateChanged;

        base.OnApplyTemplate();
        _windowRoot = GetTemplateChild(WindowRootPartName) as Border;
        _titleBar = GetTemplateChild(TitleBarPartName) as FrameworkElement;
        _minimizeButton = GetTemplateChild(MinimizeButtonPartName) as Button;
        _maximizeButton = GetTemplateChild(MaximizeButtonPartName) as Button;
        _closeButton = GetTemplateChild(CloseButtonPartName) as Button;
        _maximizeIcon = GetTemplateChild(MaximizeIconPartName) as PackIconMaterial;

        if (_titleBar is not null) _titleBar.MouseLeftButtonDown += TitleBar_MouseLeftButtonDown;
        if (_minimizeButton is not null) _minimizeButton.Click += MinimizeButton_Click;
        if (_maximizeButton is not null) _maximizeButton.Click += MaximizeButton_Click;
        if (_closeButton is not null) _closeButton.Click += CloseButton_Click;
        StateChanged += PremiumWindow_StateChanged;
        if (_minimizeButton is not null) _minimizeButton.Visibility = ResizeMode == ResizeMode.NoResize ? Visibility.Collapsed : Visibility.Visible;
        if (_maximizeButton is not null) _maximizeButton.Visibility = ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip ? Visibility.Visible : Visibility.Collapsed;
        UpdateMaximizeState();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip)
        {
            ToggleMaximize();
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed && WindowState == WindowState.Normal)
        {
            try { DragMove(); }
            catch (InvalidOperationException) { }
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeButton_Click(object sender, RoutedEventArgs e) => ToggleMaximize();
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    private void PremiumWindow_StateChanged(object? sender, EventArgs e) => UpdateMaximizeState();

    private void ToggleMaximize() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void UpdateMaximizeState()
    {
        var isMaximized = WindowState == WindowState.Maximized;
        if (_maximizeIcon is not null)
        {
            _maximizeIcon.Kind = isMaximized
                ? PackIconMaterialKind.WindowRestore
                : PackIconMaterialKind.WindowMaximize;
        }

        if (_windowRoot is not null)
        {
            _windowRoot.Margin = isMaximized ? new Thickness(7) : new Thickness(0);
        }
    }
}
