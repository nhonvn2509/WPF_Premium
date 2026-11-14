using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace WPF_Premium.Controls;

public partial class MultiSelectControl : UserControl
{
    private INotifyCollectionChanged? _notifyingItemsSource;
    private INotifyCollectionChanged? _notifyingSelectedItems;
    private bool _syncingSelection;

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(MultiSelectControl),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty SelectedItemsProperty =
        DependencyProperty.Register(nameof(SelectedItems), typeof(IList), typeof(MultiSelectControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemsChanged));

    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(MultiSelectControl),
            new PropertyMetadata("Chọn các mục", OnDisplayPropertyChanged));

    public static readonly DependencyProperty DisplayMemberPathProperty =
        DependencyProperty.Register(nameof(DisplayMemberPath), typeof(string), typeof(MultiSelectControl),
            new PropertyMetadata(string.Empty, OnDisplayPropertyChanged));

    public static readonly DependencyProperty ItemTemplateProperty =
        DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate), typeof(MultiSelectControl),
            new PropertyMetadata(null, OnItemTemplateChanged));

    public static readonly DependencyProperty SearchEnabledProperty =
        DependencyProperty.Register(nameof(SearchEnabled), typeof(bool), typeof(MultiSelectControl),
            new PropertyMetadata(true));

    public static readonly DependencyProperty SearchTextProperty =
        DependencyProperty.Register(nameof(SearchText), typeof(string), typeof(MultiSelectControl),
            new PropertyMetadata(string.Empty, OnSearchTextChanged));

    public static readonly DependencyProperty IsDropDownOpenProperty =
        DependencyProperty.Register(nameof(IsDropDownOpen), typeof(bool), typeof(MultiSelectControl),
            new PropertyMetadata(false, OnIsDropDownOpenChanged));

    public static readonly DependencyProperty NoResultsTextProperty =
        DependencyProperty.Register(nameof(NoResultsText), typeof(string), typeof(MultiSelectControl),
            new PropertyMetadata("Không tìm thấy kết quả"));

    private static readonly DependencyPropertyKey FilteredItemsPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(FilteredItems), typeof(ObservableCollection<object>), typeof(MultiSelectControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty FilteredItemsProperty = FilteredItemsPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey DisplayTextPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(DisplayText), typeof(string), typeof(MultiSelectControl),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DisplayTextProperty = DisplayTextPropertyKey.DependencyProperty;

    public MultiSelectControl()
    {
        SetValue(SelectedItemsProperty, new ObservableCollection<object>());
        InitializeComponent();
        MultiSelectPopup.DataContext = this;
        SetValue(FilteredItemsPropertyKey, new ObservableCollection<object>());
        UpdateItemPresentation();
        RefreshItems();
        UpdateDisplayText();
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public IList SelectedItems
    {
        get => (IList)GetValue(SelectedItemsProperty);
        set => SetValue(SelectedItemsProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
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

    public bool SearchEnabled
    {
        get => (bool)GetValue(SearchEnabledProperty);
        set => SetValue(SearchEnabledProperty, value);
    }

    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public bool IsDropDownOpen
    {
        get => (bool)GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    public string NoResultsText
    {
        get => (string)GetValue(NoResultsTextProperty);
        set => SetValue(NoResultsTextProperty, value);
    }

    public ObservableCollection<object> FilteredItems => (ObservableCollection<object>)GetValue(FilteredItemsProperty);
    public string DisplayText => (string)GetValue(DisplayTextProperty);

    public event EventHandler? SelectionChanged;

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (MultiSelectControl)d;
        if (control._notifyingItemsSource is not null)
        {
            control._notifyingItemsSource.CollectionChanged -= control.ItemsSource_CollectionChanged;
        }

        control._notifyingItemsSource = e.NewValue as INotifyCollectionChanged;
        if (control._notifyingItemsSource is not null)
        {
            control._notifyingItemsSource.CollectionChanged += control.ItemsSource_CollectionChanged;
        }

        control.RefreshItems();
    }

    private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (MultiSelectControl)d;
        if (control._notifyingSelectedItems is not null)
        {
            control._notifyingSelectedItems.CollectionChanged -= control.SelectedItems_CollectionChanged;
        }

        if (e.NewValue is not IList selectedItems)
        {
            control.SetCurrentValue(SelectedItemsProperty, new ObservableCollection<object>());
            return;
        }

        if (selectedItems.IsReadOnly || selectedItems.IsFixedSize)
        {
            throw new ArgumentException("SelectedItems must be a mutable list.", nameof(SelectedItems));
        }

        control._notifyingSelectedItems = selectedItems as INotifyCollectionChanged;
        if (control._notifyingSelectedItems is not null)
        {
            control._notifyingSelectedItems.CollectionChanged += control.SelectedItems_CollectionChanged;
        }

        control.UpdateListSelection();
        control.UpdateDisplayText();
        control.SelectionChanged?.Invoke(control, EventArgs.Empty);
    }

    private static void OnDisplayPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (MultiSelectControl)d;
        control.UpdateItemPresentation();
        control.RefreshItems();
        control.UpdateDisplayText();
    }

    private static void OnItemTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((MultiSelectControl)d).UpdateItemPresentation();

    private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((MultiSelectControl)d).RefreshItems();

    private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (MultiSelectControl)d;
        if (control.MultiSelectPopup is not null && control.MultiSelectPopup.IsOpen != (bool)e.NewValue)
        {
            control.MultiSelectPopup.IsOpen = (bool)e.NewValue;
        }
    }

    private void ItemsSource_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (Dispatcher.CheckAccess())
        {
            RefreshItems();
        }
        else
        {
            Dispatcher.BeginInvoke(new Action(RefreshItems));
        }
    }

    private void SelectedItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        void UpdateSelection()
        {
            if (!_syncingSelection)
            {
                UpdateListSelection();
                UpdateDisplayText();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        if (Dispatcher.CheckAccess())
        {
            UpdateSelection();
        }
        else
        {
            Dispatcher.BeginInvoke(new Action(UpdateSelection));
        }
    }

    private void RefreshItems()
    {
        if (FilteredItems is null)
        {
            return;
        }

        var filter = SearchText?.Trim();
        var filtered = new ObservableCollection<object>((ItemsSource?.Cast<object>() ?? Enumerable.Empty<object>())
            .Where(item => string.IsNullOrEmpty(filter) ||
                GetDisplayText(item).Contains(filter, StringComparison.CurrentCultureIgnoreCase)));

        var wasSyncingSelection = _syncingSelection;
        _syncingSelection = true;
        try
        {
            SetValue(FilteredItemsPropertyKey, filtered);
            UpdateListSelection();
        }
        finally
        {
            _syncingSelection = wasSyncingSelection;
        }
    }

    private void UpdateDisplayText()
    {
        var selectedItems = SelectedItems;
        var selectedCount = selectedItems.Count;
        var text = selectedCount switch
        {
            0 => Placeholder,
            1 => GetDisplayText(selectedItems[0]),
            _ => $"{selectedCount} selected"
        };
        SetValue(DisplayTextPropertyKey, text);
    }

    private void UpdateItemPresentation()
    {
        if (ItemsList is null)
        {
            return;
        }

        if (ItemTemplate is null)
        {
            ItemsList.ItemTemplate = null;
            ItemsList.DisplayMemberPath = DisplayMemberPath;
        }
        else
        {
            ItemsList.DisplayMemberPath = string.Empty;
            ItemsList.ItemTemplate = ItemTemplate;
        }
    }

    private string GetDisplayText(object? item)
    {
        if (item is null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(DisplayMemberPath))
        {
            var property = TypeDescriptor.GetProperties(item)[DisplayMemberPath];
            if (property is not null)
            {
                return property.GetValue(item)?.ToString() ?? string.Empty;
            }
        }

        return item.ToString() ?? string.Empty;
    }

    private void UpdateListSelection()
    {
        if (ItemsList is null || SelectedItems is null)
        {
            return;
        }

        var wasSyncingSelection = _syncingSelection;
        _syncingSelection = true;
        try
        {
            ItemsList.SelectedItems.Clear();
            foreach (var item in FilteredItems)
            {
                if (SelectedItems.Contains(item))
                {
                    ItemsList.SelectedItems.Add(item);
                }
            }
        }
        finally
        {
            _syncingSelection = wasSyncingSelection;
        }
    }

    private void ItemsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection)
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            foreach (var item in e.RemovedItems)
            {
                SelectedItems.Remove(item);
            }

            foreach (var item in e.AddedItems)
            {
                if (!SelectedItems.Contains(item))
                {
                    SelectedItems.Add(item);
                }
            }
        }
        finally
        {
            _syncingSelection = false;
        }

        UpdateDisplayText();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ClearSelection_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItems.Count == 0)
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            SelectedItems.Clear();
        }
        finally
        {
            _syncingSelection = false;
        }

        UpdateListSelection();
        UpdateDisplayText();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void MultiSelectPopup_Opened(object? sender, EventArgs e)
    {
        if (SearchEnabled)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
        }
    }

    private void MultiSelectPopup_Closed(object? sender, EventArgs e)
    {
        if (IsDropDownOpen)
        {
            SetCurrentValue(IsDropDownOpenProperty, false);
        }

        SetCurrentValue(SearchTextProperty, string.Empty);
    }
}
