using System.Windows;
using System.Windows.Controls;

namespace WPF_Premium.Controls;

public sealed class Field : ContentControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(Field));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(Field));

    public static readonly DependencyProperty ErrorProperty =
        DependencyProperty.Register(
            nameof(Error),
            typeof(string),
            typeof(Field),
            new FrameworkPropertyMetadata(null, OnErrorChanged));

    private static readonly DependencyPropertyKey HasErrorPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(HasError), typeof(bool), typeof(Field), new PropertyMetadata(false));

    public static readonly DependencyProperty HasErrorProperty = HasErrorPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey DisplayErrorPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(DisplayError), typeof(string), typeof(Field), new PropertyMetadata(null));

    public static readonly DependencyProperty DisplayErrorProperty = DisplayErrorPropertyKey.DependencyProperty;

    public static readonly DependencyProperty IsRequiredProperty =
        DependencyProperty.Register(nameof(IsRequired), typeof(bool), typeof(Field));

    private readonly List<ValidationError> _validationErrors = [];

    static Field()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Field), new FrameworkPropertyMetadata(typeof(Field)));
    }

    public Field()
    {
        Validation.AddErrorHandler(this, OnValidationError);
    }

    public string? Label
    {
        get => (string?)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Description
    {
        get => (string?)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string? Error
    {
        get => (string?)GetValue(ErrorProperty);
        set => SetValue(ErrorProperty, value);
    }

    public bool HasError => (bool)GetValue(HasErrorProperty);

    public string? DisplayError => (string?)GetValue(DisplayErrorProperty);

    public bool IsRequired
    {
        get => (bool)GetValue(IsRequiredProperty);
        set => SetValue(IsRequiredProperty, value);
    }

    private static void OnErrorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((Field)d).UpdateValidationState();
    }

    private void OnValidationError(object? sender, ValidationErrorEventArgs e)
    {
        if (e.Action == ValidationErrorEventAction.Added)
        {
            if (!_validationErrors.Contains(e.Error))
            {
                _validationErrors.Add(e.Error);
            }
        }
        else
        {
            _validationErrors.Remove(e.Error);
        }

        UpdateValidationState();
    }

    private void UpdateValidationState()
    {
        var bindingError = _validationErrors
            .Select(error => error.ErrorContent?.ToString())
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));
        var displayError = !string.IsNullOrWhiteSpace(Error) ? Error : bindingError;

        SetValue(DisplayErrorPropertyKey, displayError);
        SetValue(HasErrorPropertyKey, !string.IsNullOrWhiteSpace(displayError));
    }
}
