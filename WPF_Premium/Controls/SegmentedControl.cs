using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using WPF_Premium.Themes;

namespace WPF_Premium.Controls;

/// <summary>
/// A pill-style segmented single-selection control.
/// </summary>
public sealed class SegmentedControl : UserControl
{
    private readonly StackPanel _panel = new() { Orientation = Orientation.Horizontal };
    private readonly Border _container;
    private INotifyCollectionChanged? _notifyingSource;
    private bool _syncingSelection;

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(SegmentedControl),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(
            nameof(SelectedItem),
            typeof(object),
            typeof(SegmentedControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

    public static readonly DependencyProperty SelectedIndexProperty =
        DependencyProperty.Register(
            nameof(SelectedIndex),
            typeof(int),
            typeof(SegmentedControl),
            new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedIndexChanged));

    public static readonly DependencyProperty DisplayMemberPathProperty =
        DependencyProperty.Register(
            nameof(DisplayMemberPath),
            typeof(string),
            typeof(SegmentedControl),
            new PropertyMetadata(string.Empty, (_, _) => { }));

    public static readonly DependencyProperty ItemTemplateProperty =
        DependencyProperty.Register(
            nameof(ItemTemplate),
            typeof(DataTemplate),
            typeof(SegmentedControl),
            new PropertyMetadata(null, OnPresentationChanged));

    public static readonly DependencyProperty ItemPaddingProperty =
        DependencyProperty.Register(
            nameof(ItemPadding),
            typeof(Thickness),
            typeof(SegmentedControl),
            new PropertyMetadata(new Thickness(12, 4, 12, 4), OnPresentationChanged));

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(
            nameof(CornerRadius),
            typeof(CornerRadius?),
            typeof(SegmentedControl),
            new PropertyMetadata(null, OnPresentationChanged));

    public SegmentedControl()
    {
        AutomationProperties.SetName(this, "Bộ chọn phân đoạn");
        _container = new Border
        {
            Padding = new Thickness(3),
            Child = _panel,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        ControlHelpers.ApplyControlBorderTokens(_container, WpfPremiumResourceKeys.MutedBrush);
        Content = _container;
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public string DisplayMemberPath
    {
        get => (string)GetValue(DisplayMemberPathProperty);
        set => SetValue(DisplayMemberPathProperty, value);
    }

    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public Thickness ItemPadding
    {
        get => (Thickness)GetValue(ItemPaddingProperty);
        set => SetValue(ItemPaddingProperty, value);
    }

    public CornerRadius? CornerRadius
    {
        get => (CornerRadius?)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public event EventHandler? SelectionChanged;

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SegmentedControl)d;
        ControlHelpers.RebindCollectionChanged(ref control._notifyingSource, e.NewValue, control.Source_CollectionChanged);
        control.RebuildSegments();
    }

    private static void OnPresentationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SegmentedControl)d;
        if (control.CornerRadius is CornerRadius radius)
        {
            control._container.CornerRadius = radius;
        }
        else
        {
            control._container.SetResourceReference(Border.CornerRadiusProperty, WpfPremiumResourceKeys.ControlCornerRadius);
        }

        control.RebuildSegments();
    }

    private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SegmentedControl)d;
        if (control._syncingSelection)
        {
            return;
        }

        control.SyncIndexFromItem();
        control.UpdateSelectionVisuals();
        control.SelectionChanged?.Invoke(control, EventArgs.Empty);
    }

    private static void OnSelectedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SegmentedControl)d;
        if (control._syncingSelection)
        {
            return;
        }

        control.SyncItemFromIndex();
        control.UpdateSelectionVisuals();
        control.SelectionChanged?.Invoke(control, EventArgs.Empty);
    }

    private void Source_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        RebuildSegments();

    private List<object> GetItemsList() =>
        ItemsSource?.Cast<object>().ToList() ?? [];

    private void RebuildSegments()
    {
        _panel.Children.Clear();
        var items = GetItemsList();
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var index = i;
            var button = new Button
            {
                Content = ItemTemplate is not null ? item : ControlHelpers.GetDisplayText(item, DisplayMemberPath),
                ContentTemplate = ItemTemplate,
                Tag = item,
                Padding = ItemPadding,
                MinHeight = 26,
                Margin = i == 0 ? new Thickness(0) : new Thickness(2, 0, 0, 0)
            };
            button.Click += (_, _) =>
            {
                _syncingSelection = true;
                try
                {
                    SetCurrentValue(SelectedItemProperty, item);
                    SetCurrentValue(SelectedIndexProperty, index);
                }
                finally
                {
                    _syncingSelection = false;
                }

                UpdateSelectionVisuals();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            };
            _panel.Children.Add(button);
        }

        if (items.Count > 0 && SelectedItem is null && SelectedIndex < 0)
        {
            _syncingSelection = true;
            try
            {
                SetCurrentValue(SelectedIndexProperty, 0);
                SetCurrentValue(SelectedItemProperty, items[0]);
            }
            finally
            {
                _syncingSelection = false;
            }
        }
        else
        {
            SyncIndexFromItem();
        }

        UpdateSelectionVisuals();
    }

    private void SyncIndexFromItem()
    {
        var items = GetItemsList();
        var index = SelectedItem is null ? -1 : items.FindIndex(x => Equals(x, SelectedItem));
        _syncingSelection = true;
        try
        {
            SetCurrentValue(SelectedIndexProperty, index);
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private void SyncItemFromIndex()
    {
        var items = GetItemsList();
        var item = SelectedIndex >= 0 && SelectedIndex < items.Count ? items[SelectedIndex] : null;
        _syncingSelection = true;
        try
        {
            SetCurrentValue(SelectedItemProperty, item);
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private void UpdateSelectionVisuals()
    {
        for (var i = 0; i < _panel.Children.Count; i++)
        {
            if (_panel.Children[i] is Button button)
            {
                var isSelected = i == SelectedIndex || (SelectedItem is not null && Equals(button.Tag, SelectedItem));
                button.SetResourceReference(StyleProperty, isSelected ? "ButtonSecondary" : "ButtonGhost");
                button.FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal;
                if (isSelected)
                {
                    button.SetResourceReference(BackgroundProperty, "CardBrush");
                }
                else
                {
                    button.ClearValue(BackgroundProperty);
                }
            }
        }
    }
}
