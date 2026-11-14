using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WPF_Premium.Themes;

namespace WPF_Premium.Controls;

public partial class ChartControl : UserControl
{
    private static readonly TimeSpan RefreshDelay = TimeSpan.FromMilliseconds(50);
    private INotifyCollectionChanged? _seriesCollection;
    private INotifyCollectionChanged? _labelsCollection;
    private readonly DispatcherTimer _refreshTimer;

    private static readonly string[] PaletteResourceKeys =
    [
        "ChartColor1Brush",
        "ChartColor2Brush",
        "ChartColor3Brush",
        "ChartColor4Brush",
        "ChartColor5Brush"
    ];

    public static readonly DependencyProperty KindProperty =
        DependencyProperty.Register(
            nameof(Kind),
            typeof(ChartKind),
            typeof(ChartControl),
            new PropertyMetadata(ChartKind.Line, OnChartChanged));

    public static readonly DependencyProperty SeriesProperty =
        DependencyProperty.Register(
            nameof(Series),
            typeof(IReadOnlyList<ChartSeries>),
            typeof(ChartControl),
            new PropertyMetadata(Array.Empty<ChartSeries>(), OnChartChanged));

    public static readonly DependencyProperty LabelsProperty =
        DependencyProperty.Register(
            nameof(Labels),
            typeof(IReadOnlyList<string>),
            typeof(ChartControl),
            new PropertyMetadata(Array.Empty<string>(), OnChartChanged));

    public static readonly DependencyProperty ShowLegendProperty =
        DependencyProperty.Register(
            nameof(ShowLegend),
            typeof(bool),
            typeof(ChartControl),
            new PropertyMetadata(true, OnChartChanged));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(ChartControl),
            new PropertyMetadata(null, OnHeaderChanged));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(
            nameof(Description),
            typeof(string),
            typeof(ChartControl),
            new PropertyMetadata(null, OnHeaderChanged));

    public ChartControl()
    {
        InitializeComponent();
        _refreshTimer = new DispatcherTimer { Interval = RefreshDelay };
        _refreshTimer.Tick += RefreshTimer_Tick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public ChartKind Kind
    {
        get => (ChartKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public IReadOnlyList<ChartSeries> Series
    {
        get => (IReadOnlyList<ChartSeries>)GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    public IReadOnlyList<string> Labels
    {
        get => (IReadOnlyList<string>)GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    public bool ShowLegend
    {
        get => (bool)GetValue(ShowLegendProperty);
        set => SetValue(ShowLegendProperty, value);
    }

    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Description
    {
        get => (string?)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    private static void OnChartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (ChartControl)d;
        if (chart.IsLoaded)
        {
            if (e.Property == SeriesProperty || e.Property == LabelsProperty)
            {
                chart.AttachCollectionNotifications();
            }

            chart.RequestRefreshChart();
        }
    }

    private static void OnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((ChartControl)d).UpdateHeader();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ThemeManager.ThemeChanged += OnThemeChanged;
        AttachCollectionNotifications();
        UpdateHeader();
        RefreshChart();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ThemeManager.ThemeChanged -= OnThemeChanged;
        DetachCollectionNotifications();
        _refreshTimer.Stop();
    }

    private void AttachCollectionNotifications()
    {
        DetachCollectionNotifications();
        _seriesCollection = Series as INotifyCollectionChanged;
        _labelsCollection = Labels as INotifyCollectionChanged;
        if (_seriesCollection is not null)
        {
            _seriesCollection.CollectionChanged += OnCollectionChanged;
        }

        if (_labelsCollection is not null)
        {
            _labelsCollection.CollectionChanged += OnCollectionChanged;
        }
    }

    private void DetachCollectionNotifications()
    {
        if (_seriesCollection is not null)
        {
            _seriesCollection.CollectionChanged -= OnCollectionChanged;
            _seriesCollection = null;
        }

        if (_labelsCollection is not null)
        {
            _labelsCollection.CollectionChanged -= OnCollectionChanged;
            _labelsCollection = null;
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RequestRefreshChart();

    private void OnThemeChanged(object? sender, EventArgs e) => RequestRefreshChart();

    private void RequestRefreshChart()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(RequestRefreshChart);
            return;
        }

        if (!IsLoaded)
        {
            return;
        }

        _refreshTimer.Stop();
        _refreshTimer.Start();
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        _refreshTimer.Stop();
        RefreshChart();
    }

    private void UpdateHeader()
    {
        TitleText.Text = Title ?? string.Empty;
        TitleText.Visibility = string.IsNullOrWhiteSpace(Title) ? Visibility.Collapsed : Visibility.Visible;
        DescriptionText.Text = Description ?? string.Empty;
        DescriptionText.Visibility = string.IsNullOrWhiteSpace(Description) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void RefreshChart()
    {
        var series = Series ?? Array.Empty<ChartSeries>();
        ValidateSeries(series, Kind);

        var palette = PaletteResourceKeys.Select(GetThemeColor).ToArray();
        var foreground = GetThemeColor("ForegroundBrush");
        var mutedForeground = GetThemeColor("MutedForegroundBrush");
        var border = GetThemeColor("BorderBrush");
        var background = GetThemeColor("CardBrush");
        FrameworkElement chart = Kind is ChartKind.Pie or ChartKind.Doughnut
            ? CreatePieChart(series, palette, foreground, background)
            : CreateCartesianChart(series, palette, foreground, mutedForeground, border, background);

        ChartSurface.Children.Clear();
        ChartSurface.Children.Add(chart);
    }

    private CartesianChart CreateCartesianChart(
        IReadOnlyList<ChartSeries> series,
        IReadOnlyList<SKColor> palette,
        SKColor foreground,
        SKColor mutedForeground,
        SKColor border,
        SKColor background)
    {
        var labels = Labels ?? Array.Empty<string>();
        var horizontalBar = Kind is ChartKind.Bar or ChartKind.StackedBar;
        return new CartesianChart
        {
            Series = series.Select((item, index) => CreateCartesianSeries(item, palette[index % palette.Count])).ToArray(),
            XAxes =
            [
                CreateAxis(horizontalBar ? Array.Empty<string>() : labels, mutedForeground, border)
            ],
            YAxes =
            [
                CreateAxis(horizontalBar ? labels : Array.Empty<string>(), mutedForeground, border)
            ],
            LegendPosition = ShowLegend ? LegendPosition.Bottom : LegendPosition.Hidden,
            Background = CreateWpfBrush(background),
            LegendTextPaint = new SolidColorPaint(foreground),
            LegendBackgroundPaint = new SolidColorPaint(background),
            TooltipTextPaint = new SolidColorPaint(foreground),
            TooltipBackgroundPaint = new SolidColorPaint(background),
            AnimationsSpeed = TimeSpan.FromMilliseconds(350)
        };
    }

    private PieChart CreatePieChart(
        IReadOnlyList<ChartSeries> series,
        IReadOnlyList<SKColor> palette,
        SKColor foreground,
        SKColor background)
    {
        var innerRadius = Kind == ChartKind.Doughnut ? 48 : 0;
        return new PieChart
        {
            Series = series.Select((item, index) => new PieSeries<double>
            {
                Name = item.Name,
                Values = item.Values,
                Fill = new SolidColorPaint(palette[index % palette.Count]),
                Stroke = new SolidColorPaint(background) { StrokeThickness = 2 },
                InnerRadius = innerRadius,
                HoverPushout = 5
            }).ToArray(),
            LegendPosition = ShowLegend ? LegendPosition.Right : LegendPosition.Hidden,
            Background = CreateWpfBrush(background),
            LegendTextPaint = new SolidColorPaint(foreground),
            LegendBackgroundPaint = new SolidColorPaint(background),
            TooltipTextPaint = new SolidColorPaint(foreground),
            TooltipBackgroundPaint = new SolidColorPaint(background),
            AnimationsSpeed = TimeSpan.FromMilliseconds(350)
        };
    }

    private ISeries CreateCartesianSeries(ChartSeries series, SKColor color)
    {
        var stroke = new SolidColorPaint(color) { StrokeThickness = 2 };
        var fill = new SolidColorPaint(new SKColor(color.Red, color.Green, color.Blue, 64));
        return Kind switch
        {
            ChartKind.Line => new LineSeries<double>
            {
                Name = series.Name,
                Values = series.Values,
                Stroke = stroke,
                Fill = null
            },
            ChartKind.Area => new LineSeries<double>
            {
                Name = series.Name,
                Values = series.Values,
                Stroke = stroke,
                Fill = fill
            },
            ChartKind.Column => new ColumnSeries<double>
            {
                Name = series.Name,
                Values = series.Values,
                Fill = new SolidColorPaint(color),
                Stroke = null
            },
            ChartKind.Bar => new RowSeries<double>
            {
                Name = series.Name,
                Values = series.Values,
                Fill = new SolidColorPaint(color),
                Stroke = null
            },
            ChartKind.StackedColumn => new StackedColumnSeries<double>
            {
                Name = series.Name,
                Values = series.Values,
                Fill = new SolidColorPaint(color),
                Stroke = null
            },
            ChartKind.StackedBar => new StackedRowSeries<double>
            {
                Name = series.Name,
                Values = series.Values,
                Fill = new SolidColorPaint(color),
                Stroke = null
            },
            ChartKind.StepLine => new StepLineSeries<double>
            {
                Name = series.Name,
                Values = series.Values,
                Stroke = stroke,
                Fill = null
            },
            ChartKind.Scatter => new ScatterSeries<ObservablePoint>
            {
                Name = series.Name,
                Values = series.Values
                    .Select((value, index) => new ObservablePoint(series.XValues?[index] ?? index, value))
                    .ToArray(),
                Stroke = stroke,
                Fill = new SolidColorPaint(color),
                GeometrySize = 9
            },
            _ => throw new InvalidOperationException($"Chart kind '{Kind}' is not supported by a Cartesian chart.")
        };
    }

    private static Axis CreateAxis(IReadOnlyList<string> labels, SKColor foreground, SKColor border) =>
        new()
        {
            Labels = labels.ToArray(),
            LabelsPaint = new SolidColorPaint(foreground),
            SeparatorsPaint = new SolidColorPaint(border),
            TicksPaint = new SolidColorPaint(border),
            MinStep = 1,
            ForceStepToMin = labels.Count > 0
        };

    private static void ValidateSeries(IReadOnlyList<ChartSeries> series, ChartKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported chart kind.");
        }

        var isPie = kind is ChartKind.Pie or ChartKind.Doughnut;
        for (var index = 0; index < series.Count; index++)
        {
            var item = series[index]
                ?? throw new ArgumentException("Chart series cannot contain null entries.", nameof(series));
            if (item.XValues is not null && kind != ChartKind.Scatter)
            {
                throw new ArgumentException("X values are only supported by scatter charts.", nameof(series));
            }

            if (item.XValues is not null && item.XValues.Count != item.Values.Count)
            {
                throw new ArgumentException("X and Y values must have the same number of points.", nameof(series));
            }

            if (isPie && item.Values.Count != 1)
            {
                throw new ArgumentException(
                    "Pie and doughnut charts require exactly one value per series.",
                    nameof(series));
            }

            for (var pointIndex = 0; pointIndex < item.Values.Count; pointIndex++)
            {
                var value = item.Values[pointIndex];
                if (!double.IsFinite(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(series), value, "Chart values must be finite numbers.");
                }

                if (isPie && value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(series), value, "Pie chart values cannot be negative.");
                }

                if (item.XValues is not null && !double.IsFinite(item.XValues[pointIndex]))
                {
                    throw new ArgumentOutOfRangeException(nameof(series), item.XValues[pointIndex], "Chart values must be finite numbers.");
                }
            }
        }
    }

    private static SKColor GetThemeColor(string key)
    {
        var application = Application.Current
            ?? throw new InvalidOperationException("The WPF application is not available.");
        var value = FindResource(application.Resources, key);
        if (value is not SolidColorBrush brush)
        {
            throw new InvalidOperationException($"Theme resource '{key}' must be a SolidColorBrush.");
        }

        var color = brush.Color;
        return new SKColor(color.R, color.G, color.B, color.A);
    }

    private static SolidColorBrush CreateWpfBrush(SKColor color) =>
        new(Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue));

    private static object? FindResource(ResourceDictionary dictionary, object key)
    {
        if (dictionary.Contains(key))
        {
            return dictionary[key];
        }

        for (var index = dictionary.MergedDictionaries.Count - 1; index >= 0; index--)
        {
            var value = FindResource(dictionary.MergedDictionaries[index], key);
            if (value is not null)
            {
                return value;
            }
        }

        return null;
    }
}
