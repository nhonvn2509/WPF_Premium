using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace WPF_Premium.Controls;

public enum SkeletonKind
{
    Text,
    Avatar,
    Card,
    Rect
}

/// <summary>
/// A lightweight animated pulse placeholder shown while content is loading.
/// </summary>
public sealed class Skeleton : UserControl
{
    private readonly Border _shape;
    private Storyboard? _pulseStoryboard;

    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(
            nameof(IsActive),
            typeof(bool),
            typeof(Skeleton),
            new PropertyMetadata(true, OnVisualChanged));

    public static readonly DependencyProperty KindProperty =
        DependencyProperty.Register(
            nameof(Kind),
            typeof(SkeletonKind),
            typeof(Skeleton),
            new PropertyMetadata(SkeletonKind.Text, OnVisualChanged));

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(
            nameof(CornerRadius),
            typeof(CornerRadius?),
            typeof(Skeleton),
            new PropertyMetadata(null, OnVisualChanged));

    public Skeleton()
    {
        _shape = new Border
        {
            Opacity = 0.85
        };
        _shape.SetResourceReference(Border.BackgroundProperty, "MutedBrush");
        Content = _shape;
        Loaded += (_, _) => UpdatePulse();
        Unloaded += (_, _) => StopPulse();
        UpdateShape();
    }

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public SkeletonKind Kind
    {
        get => (SkeletonKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public CornerRadius? CornerRadius
    {
        get => (CornerRadius?)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (Skeleton)d;
        control.UpdateShape();
        control.UpdatePulse();
    }

    private void UpdateShape()
    {
        switch (Kind)
        {
            case SkeletonKind.Avatar:
                if (double.IsNaN(Width)) Width = 40;
                if (double.IsNaN(Height)) Height = 40;
                _shape.CornerRadius = CornerRadius ?? new CornerRadius(999);
                break;
            case SkeletonKind.Card:
                if (double.IsNaN(Height)) Height = 96;
                _shape.CornerRadius = CornerRadius ?? new CornerRadius(8);
                break;
            case SkeletonKind.Text:
                if (double.IsNaN(Height)) Height = 14;
                _shape.CornerRadius = CornerRadius ?? new CornerRadius(4);
                break;
            default:
                _shape.CornerRadius = CornerRadius ?? new CornerRadius(4);
                break;
        }
    }

    private void UpdatePulse()
    {
        StopPulse();
        if (!IsActive || !IsLoaded)
        {
            _shape.Opacity = 0.85;
            return;
        }

        var animation = new DoubleAnimation
        {
            From = 0.45,
            To = 0.95,
            Duration = TimeSpan.FromMilliseconds(900),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        Storyboard.SetTarget(animation, _shape);
        Storyboard.SetTargetProperty(animation, new PropertyPath(OpacityProperty));

        _pulseStoryboard = new Storyboard();
        _pulseStoryboard.Children.Add(animation);
        _pulseStoryboard.Begin();
    }

    private void StopPulse()
    {
        _pulseStoryboard?.Stop();
        _pulseStoryboard = null;
    }
}
