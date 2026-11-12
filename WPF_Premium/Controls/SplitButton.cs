using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace WPF_Premium.Controls;

[TemplatePart(Name = "PART_MainButton", Type = typeof(Button))]
[TemplatePart(Name = "PART_DropDownButton", Type = typeof(Button))]
[ContentProperty(nameof(Content))]
public sealed class SplitButton : Control
{
    private Button? _mainButton;
    private Button? _dropDownButton;

    public static readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(nameof(Content), typeof(object), typeof(SplitButton));

    public static readonly DependencyProperty ContentTemplateProperty =
        DependencyProperty.Register(nameof(ContentTemplate), typeof(DataTemplate), typeof(SplitButton));

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(SplitButton));

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(SplitButton));

    public static readonly DependencyProperty CommandTargetProperty =
        DependencyProperty.Register(nameof(CommandTarget), typeof(IInputElement), typeof(SplitButton));

    public static readonly DependencyProperty ButtonStyleProperty =
        DependencyProperty.Register(nameof(ButtonStyle), typeof(Style), typeof(SplitButton));

    public static readonly RoutedEvent ClickEvent =
        EventManager.RegisterRoutedEvent(
            nameof(Click),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(SplitButton));

    static SplitButton() =>
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SplitButton),
            new FrameworkPropertyMetadata(typeof(SplitButton)));

    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public DataTemplate? ContentTemplate
    {
        get => (DataTemplate?)GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public IInputElement? CommandTarget
    {
        get => (IInputElement?)GetValue(CommandTargetProperty);
        set => SetValue(CommandTargetProperty, value);
    }

    public Style? ButtonStyle
    {
        get => (Style?)GetValue(ButtonStyleProperty);
        set => SetValue(ButtonStyleProperty, value);
    }

    public event RoutedEventHandler Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    public override void OnApplyTemplate()
    {
        if (_mainButton is not null)
        {
            _mainButton.Click -= MainButton_Click;
        }

        if (_dropDownButton is not null)
        {
            _dropDownButton.Click -= DropDownButton_Click;
        }

        base.OnApplyTemplate();

        _mainButton = GetTemplateChild("PART_MainButton") as Button;
        _dropDownButton = GetTemplateChild("PART_DropDownButton") as Button;

        if (_mainButton is not null)
        {
            _mainButton.Click += MainButton_Click;
        }

        if (_dropDownButton is not null)
        {
            _dropDownButton.Click += DropDownButton_Click;
            AutomationProperties.SetName(_dropDownButton, "Mở trình đơn");
        }
    }

    private void MainButton_Click(object sender, RoutedEventArgs e) =>
        RaiseEvent(new RoutedEventArgs(ClickEvent, this));

    private void DropDownButton_Click(object sender, RoutedEventArgs e)
    {
        if (ContextMenu is not { } menu || _dropDownButton is null)
        {
            return;
        }

        menu.PlacementTarget = _dropDownButton;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }
}
