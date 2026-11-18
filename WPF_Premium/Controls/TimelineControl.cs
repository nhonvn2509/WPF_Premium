using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace WPF_Premium.Controls;

public enum TimelineStatus
{
    Default,
    Info,
    Success,
    Warning,
    Destructive
}

public sealed class TimelineItem
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public TimelineStatus Status { get; set; } = TimelineStatus.Default;
}

/// <summary>
/// A vertical activity/event timeline control.
/// </summary>
public sealed class TimelineControl : UserControl
{
    private readonly StackPanel _itemsPanel = new();
    private INotifyCollectionChanged? _notifyingSource;

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(TimelineControl),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public TimelineControl()
    {
        Content = _itemsPanel;
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TimelineControl)d;
        if (control._notifyingSource is not null)
        {
            control._notifyingSource.CollectionChanged -= control.Source_CollectionChanged;
        }

        control._notifyingSource = e.NewValue as INotifyCollectionChanged;
        if (control._notifyingSource is not null)
        {
            control._notifyingSource.CollectionChanged += control.Source_CollectionChanged;
        }

        control.Rebuild();
    }

    private void Source_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Rebuild();

    private void Rebuild()
    {
        _itemsPanel.Children.Clear();
        var items = ItemsSource?.Cast<object>().ToList() ?? [];

        for (var i = 0; i < items.Count; i++)
        {
            var isLast = i == items.Count - 1;
            var entry = items[i] as TimelineItem ?? new TimelineItem { Title = items[i]?.ToString() ?? string.Empty };
            _itemsPanel.Children.Add(CreateRow(entry, isLast));
        }
    }

    private static UIElement CreateRow(TimelineItem item, bool isLast)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var rail = new Grid();
        rail.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
        rail.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var dot = new Ellipse
        {
            Width = 10,
            Height = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        dot.SetResourceReference(Shape.FillProperty, item.Status switch
        {
            TimelineStatus.Info => "TraceActiveBrush",
            TimelineStatus.Success => "SuccessBrush",
            TimelineStatus.Warning => "WarningBrush",
            TimelineStatus.Destructive => "DestructiveBrush",
            _ => "PrimaryBrush"
        });
        rail.Children.Add(dot);

        if (!isLast)
        {
            var line = new Border
            {
                Width = 2,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 2)
            };
            line.SetResourceReference(Border.BackgroundProperty, "BorderBrush");
            Grid.SetRow(line, 1);
            rail.Children.Add(line);
        }

        grid.Children.Add(rail);

        var body = new StackPanel
        {
            Margin = new Thickness(8, 0, 0, isLast ? 0 : 16)
        };

        var header = new DockPanel();
        if (!string.IsNullOrWhiteSpace(item.Timestamp))
        {
            var timeText = new TextBlock
            {
                Text = item.Timestamp,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            timeText.SetResourceReference(TextBlock.ForegroundProperty, "MutedForegroundBrush");
            DockPanel.SetDock(timeText, Dock.Right);
            header.Children.Add(timeText);
        }

        var titleText = new TextBlock
        {
            Text = item.Title,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
        titleText.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundBrush");
        header.Children.Add(titleText);
        body.Children.Add(header);

        if (!string.IsNullOrWhiteSpace(item.Description))
        {
            var descText = new TextBlock
            {
                Text = item.Description,
                Margin = new Thickness(0, 3, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12
            };
            descText.SetResourceReference(TextBlock.ForegroundProperty, "MutedForegroundBrush");
            body.Children.Add(descText);
        }

        Grid.SetColumn(body, 1);
        grid.Children.Add(body);
        return grid;
    }
}
