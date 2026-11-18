using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using MahApps.Metro.IconPacks;

namespace WPF_Premium.Controls;

public enum BannerAlertKind
{
    Info,
    Success,
    Warning,
    Destructive
}

/// <summary>
/// A dismissible inline banner alert with optional action button.
/// </summary>
public sealed class BannerAlert : UserControl
{
    private readonly Border _border;
    private readonly PackIconMaterial _icon;
    private readonly TextBlock _titleText;
    private readonly TextBlock _messageText;
    private readonly Button _actionButton;
    private readonly Button _closeButton;

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(BannerAlert), new PropertyMetadata(string.Empty, OnContentChanged));

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(BannerAlert), new PropertyMetadata(string.Empty, OnContentChanged));

    public static readonly DependencyProperty KindProperty =
        DependencyProperty.Register(nameof(Kind), typeof(BannerAlertKind), typeof(BannerAlert), new PropertyMetadata(BannerAlertKind.Info, OnVisualChanged));

    public static readonly DependencyProperty IsClosableProperty =
        DependencyProperty.Register(nameof(IsClosable), typeof(bool), typeof(BannerAlert), new PropertyMetadata(true, OnVisualChanged));

    public static readonly DependencyProperty IsOpenProperty =
        DependencyProperty.Register(
            nameof(IsOpen),
            typeof(bool),
            typeof(BannerAlert),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnOpenChanged));

    public static readonly DependencyProperty ActionTextProperty =
        DependencyProperty.Register(nameof(ActionText), typeof(string), typeof(BannerAlert), new PropertyMetadata(string.Empty, OnContentChanged));

    public static readonly DependencyProperty ActionCommandProperty =
        DependencyProperty.Register(nameof(ActionCommand), typeof(ICommand), typeof(BannerAlert));

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius?), typeof(BannerAlert), new PropertyMetadata(null, OnVisualChanged));

    public static readonly DependencyProperty IconKindProperty =
        DependencyProperty.Register(nameof(IconKind), typeof(PackIconMaterialKind?), typeof(BannerAlert), new PropertyMetadata(null, OnVisualChanged));

    public BannerAlert()
    {
        _icon = new PackIconMaterial
        {
            Width = 16,
            Height = 16,
            Margin = new Thickness(0, 2, 10, 0),
            VerticalAlignment = VerticalAlignment.Top
        };

        _titleText = new TextBlock
        {
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
        _titleText.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundBrush");

        _messageText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 0)
        };
        _messageText.SetResourceReference(TextBlock.ForegroundProperty, "MutedForegroundBrush");

        var textStack = new StackPanel();
        textStack.Children.Add(_titleText);
        textStack.Children.Add(_messageText);

        _actionButton = new Button
        {
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        _actionButton.SetResourceReference(StyleProperty, "ButtonOutline");
        _actionButton.Click += (_, _) =>
        {
            ActionClicked?.Invoke(this, EventArgs.Empty);
            if (ActionCommand?.CanExecute(null) == true)
            {
                ActionCommand.Execute(null);
            }
        };

        _closeButton = new Button
        {
            Content = new PackIconMaterial { Kind = PackIconMaterialKind.Close, Width = 12, Height = 12 },
            Padding = new Thickness(4),
            MinHeight = 24,
            MinWidth = 24,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Top,
            ToolTip = "Đóng"
        };
        _closeButton.SetResourceReference(StyleProperty, "ButtonGhost");
        AutomationProperties.SetName(_closeButton, "Đóng thông báo");
        _closeButton.Click += (_, _) =>
        {
            SetCurrentValue(IsOpenProperty, false);
            Closed?.Invoke(this, EventArgs.Empty);
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(_icon);
        Grid.SetColumn(textStack, 1);
        grid.Children.Add(textStack);
        Grid.SetColumn(_actionButton, 2);
        grid.Children.Add(_actionButton);
        Grid.SetColumn(_closeButton, 3);
        grid.Children.Add(_closeButton);

        _border = new Border
        {
            Padding = new Thickness(14, 10, 12, 10),
            Child = grid
        };
        _border.SetResourceReference(Border.BackgroundProperty, "CardBrush");
        _border.SetResourceReference(Border.BorderThicknessProperty, "WpfPremium.SurfaceBorderThickness");
        _border.SetResourceReference(Border.CornerRadiusProperty, "WpfPremium.SurfaceCornerRadius");

        Content = _border;
        UpdateContent();
        UpdateVisual();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public BannerAlertKind Kind
    {
        get => (BannerAlertKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public bool IsClosable
    {
        get => (bool)GetValue(IsClosableProperty);
        set => SetValue(IsClosableProperty, value);
    }

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public string ActionText
    {
        get => (string)GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public ICommand? ActionCommand
    {
        get => (ICommand?)GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    public CornerRadius? CornerRadius
    {
        get => (CornerRadius?)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public PackIconMaterialKind? IconKind
    {
        get => (PackIconMaterialKind?)GetValue(IconKindProperty);
        set => SetValue(IconKindProperty, value);
    }

    public event EventHandler? Closed;
    public event EventHandler? ActionClicked;

    private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((BannerAlert)d).UpdateContent();

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((BannerAlert)d).UpdateVisual();

    private static void OnOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((BannerAlert)d).Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;

    private void UpdateContent()
    {
        _titleText.Text = Title;
        _titleText.Visibility = string.IsNullOrWhiteSpace(Title) ? Visibility.Collapsed : Visibility.Visible;
        _messageText.Text = Message;
        _messageText.Visibility = string.IsNullOrWhiteSpace(Message) ? Visibility.Collapsed : Visibility.Visible;
        _actionButton.Content = ActionText;
        _actionButton.Visibility = string.IsNullOrWhiteSpace(ActionText) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateVisual()
    {
        _icon.Kind = IconKind ?? Kind switch
        {
            BannerAlertKind.Success => PackIconMaterialKind.CheckCircleOutline,
            BannerAlertKind.Warning => PackIconMaterialKind.AlertOutline,
            BannerAlertKind.Destructive => PackIconMaterialKind.AlertCircleOutline,
            _ => PackIconMaterialKind.InformationOutline
        };

        var brushKey = Kind switch
        {
            BannerAlertKind.Success => Themes.WpfPremiumResourceKeys.SuccessBrush,
            BannerAlertKind.Warning => Themes.WpfPremiumResourceKeys.WarningBrush,
            BannerAlertKind.Destructive => Themes.WpfPremiumResourceKeys.DestructiveBrush,
            _ => Themes.WpfPremiumResourceKeys.InfoBrush
        };

        _icon.SetResourceReference(Control.ForegroundProperty, brushKey);
        _border.SetResourceReference(Border.BorderBrushProperty, brushKey);
        if (CornerRadius is CornerRadius customRadius)
        {
            _border.CornerRadius = customRadius;
        }
        else
        {
            _border.SetResourceReference(Border.CornerRadiusProperty, Themes.WpfPremiumResourceKeys.SurfaceCornerRadius);
        }
        _closeButton.Visibility = IsClosable ? Visibility.Visible : Visibility.Collapsed;
        Visibility = IsOpen ? Visibility.Visible : Visibility.Collapsed;
    }
}
