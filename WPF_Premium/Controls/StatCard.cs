using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace WPF_Premium.Controls;

public sealed class StatCard : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(StatCard), new PropertyMetadata(string.Empty, OnContentChanged));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(StatCard), new PropertyMetadata(string.Empty, OnContentChanged));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(StatCard), new PropertyMetadata(string.Empty, OnContentChanged));

    public static readonly DependencyProperty TrendProperty =
        DependencyProperty.Register(nameof(Trend), typeof(string), typeof(StatCard), new PropertyMetadata(string.Empty, OnContentChanged));

    public static readonly DependencyProperty IsPositiveTrendProperty =
        DependencyProperty.Register(nameof(IsPositiveTrend), typeof(bool), typeof(StatCard), new PropertyMetadata(true, OnTrendChanged));

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius?), typeof(StatCard), new PropertyMetadata(null, OnSurfaceChanged));

    private readonly Border _card;
    private readonly TextBlock _titleText;
    private readonly TextBlock _valueText;
    private readonly TextBlock _descriptionText;
    private readonly TextBlock _trendText;

    public StatCard()
    {
        _titleText = new TextBlock { FontSize = 12 };
        _titleText.SetResourceReference(ForegroundProperty, Themes.WpfPremiumResourceKeys.MutedForegroundBrush);
        _valueText = new TextBlock { FontSize = 28, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 4) };
        _valueText.SetResourceReference(ForegroundProperty, Themes.WpfPremiumResourceKeys.ForegroundBrush);
        _descriptionText = new TextBlock { FontSize = 11, TextWrapping = TextWrapping.Wrap };
        _descriptionText.SetResourceReference(ForegroundProperty, Themes.WpfPremiumResourceKeys.MutedForegroundBrush);
        _trendText = new TextBlock { FontSize = 11, FontWeight = FontWeights.SemiBold };

        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition());
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.Children.Add(_titleText);
        Grid.SetRow(_valueText, 1);
        content.Children.Add(_valueText);
        Grid.SetRow(_descriptionText, 2);
        content.Children.Add(_descriptionText);
        Grid.SetRow(_trendText, 1);
        Grid.SetColumn(_trendText, 1);
        _trendText.VerticalAlignment = VerticalAlignment.Center;
        content.Children.Add(_trendText);

        _card = new Border
        {
            Padding = new Thickness(16),
            Child = content
        };
        ControlHelpers.ApplySurfaceBorderTokens(_card);
        Content = _card;
        UpdateContent();
        UpdateTrend();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string Trend
    {
        get => (string)GetValue(TrendProperty);
        set => SetValue(TrendProperty, value);
    }

    public bool IsPositiveTrend
    {
        get => (bool)GetValue(IsPositiveTrendProperty);
        set => SetValue(IsPositiveTrendProperty, value);
    }

    public CornerRadius? CornerRadius
    {
        get => (CornerRadius?)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    private static void OnSurfaceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (StatCard)d;
        if (card.CornerRadius is CornerRadius radius)
        {
            card._card.CornerRadius = radius;
        }
        else
        {
            card._card.SetResourceReference(Border.CornerRadiusProperty, Themes.WpfPremiumResourceKeys.SurfaceCornerRadius);
        }
    }

    private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (StatCard)d;
        card.UpdateContent(e.Property == ValueProperty && card.IsLoaded);
    }

    private static void OnTrendChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((StatCard)d).UpdateTrend();

    private void UpdateContent(bool animateValue = false)
    {
        _titleText.Text = Title;
        _valueText.Text = Value;
        _descriptionText.Text = Description;
        _trendText.Text = Trend;

        if (animateValue)
        {
            var duration = TimeSpan.FromMilliseconds(140);
            _valueText.BeginAnimation(
                UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, duration)
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.Stop
                },
                HandoffBehavior.SnapshotAndReplace);

            var translation = new TranslateTransform();
            _valueText.RenderTransform = translation;
            translation.BeginAnimation(
                TranslateTransform.YProperty,
                new DoubleAnimation(4, 0, duration)
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.Stop
                },
                HandoffBehavior.SnapshotAndReplace);
        }
    }

    private void UpdateTrend() =>
        _trendText.SetResourceReference(ForegroundProperty, IsPositiveTrend ? "SuccessBrush" : "DestructiveBrush");
}
