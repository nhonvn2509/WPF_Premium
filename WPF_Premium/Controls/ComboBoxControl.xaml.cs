using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace WPF_Premium.Controls;

public partial class ComboBoxControl : UserControl
{
    private INotifyCollectionChanged? _notifyingSource;
    private bool _syncingSelection;

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(ComboBoxControl), new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(ComboBoxControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(ComboBoxControl), new PropertyMetadata("Chọn một mục", OnDisplayPropertyChanged));

    public static readonly DependencyProperty DisplayMemberPathProperty =
        DependencyProperty.Register(nameof(DisplayMemberPath), typeof(string), typeof(ComboBoxControl), new PropertyMetadata(string.Empty, OnDisplayPropertyChanged));

    public static readonly DependencyProperty ItemTemplateProperty =
        DependencyProperty.Register(nameof(ItemTemplate), typeof(DataTemplate), typeof(ComboBoxControl), new PropertyMetadata(null, OnItemTemplateChanged));

    public static readonly DependencyProperty SearchEnabledProperty =
        DependencyProperty.Register(nameof(SearchEnabled), typeof(bool), typeof(ComboBoxControl), new PropertyMetadata(true));

    public static readonly DependencyProperty SearchTextProperty =
        DependencyProperty.Register(nameof(SearchText), typeof(string), typeof(ComboBoxControl), new PropertyMetadata(string.Empty, OnSearchTextChanged));

    public static readonly DependencyProperty IsDropDownOpenProperty =
        DependencyProperty.Register(nameof(IsDropDownOpen), typeof(bool), typeof(ComboBoxControl),
            new PropertyMetadata(false, OnIsDropDownOpenChanged));

    public static readonly DependencyProperty NoResultsTextProperty =
        DependencyProperty.Register(nameof(NoResultsText), typeof(string), typeof(ComboBoxControl), new PropertyMetadata("Không tìm thấy kết quả"));

    private static readonly DependencyPropertyKey FilteredItemsPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(FilteredItems), typeof(ObservableCollection<object>), typeof(ComboBoxControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty FilteredItemsProperty = FilteredItemsPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey DisplayTextPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(DisplayText), typeof(string), typeof(ComboBoxControl), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DisplayTextProperty = DisplayTextPropertyKey.DependencyProperty;

    public ComboBoxControl()
    {
        InitializeComponent();
        DropDownPopup.DataContext = this;
        SetValue(FilteredItemsPropertyKey, new ObservableCollection<object>());
        UpdateItemPresentation();
        UpdateDisplayText();
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
        var control = (ComboBoxControl)d;
        if (control._notifyingSource is not null)
        {
            control._notifyingSource.CollectionChanged -= control.Source_CollectionChanged;
        }

        control._notifyingSource = e.NewValue as INotifyCollectionChanged;
        if (control._notifyingSource is not null)
        {
            control._notifyingSource.CollectionChanged += control.Source_CollectionChanged;
        }
        control.RefreshItems();
    }

    private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ComboBoxControl)d;
        control.UpdateDisplayText();
        if (control.ItemsList is null)
        {
            return;
        }

        control._syncingSelection = true;
        control.ItemsList.SelectedItem = e.NewValue;
        control._syncingSelection = false;
        control.SelectionChanged?.Invoke(control, EventArgs.Empty);
    }

    private static void OnDisplayPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ComboBoxControl)d;
        control.UpdateDisplayText();
        control.UpdateItemPresentation();
        control.RefreshItems();
    }

    private static void OnItemTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((ComboBoxControl)d).UpdateItemPresentation();

    private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((ComboBoxControl)d).RefreshItems();

    private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ComboBoxControl)d;
        if (control.DropDownPopup is not null && control.DropDownPopup.IsOpen != (bool)e.NewValue)
        {
            control.DropDownPopup.IsOpen = (bool)e.NewValue;
        }
    }

    private void Source_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
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

    private void RefreshItems()
    {
        if (FilteredItems is null)
        {
            return;
        }

        var filter = SearchText?.Trim();
        var items = ItemsSource?.Cast<object>() ?? Enumerable.Empty<object>();
        var filtered = new ObservableCollection<object>(items.Where(item => string.IsNullOrEmpty(filter) ||
            GetDisplayText(item).Contains(filter, StringComparison.CurrentCultureIgnoreCase)));
        SetValue(FilteredItemsPropertyKey, filtered);
    }

    private void UpdateDisplayText()
    {
        var text = SelectedItem is null ? Placeholder : GetDisplayText(SelectedItem);
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

    private string GetDisplayText(object item)
    {
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

    private void ItemsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection || ItemsList.SelectedItem is null)
        {
            return;
        }

        SetCurrentValue(SelectedItemProperty, ItemsList.SelectedItem);
        SearchText = string.Empty;
        IsDropDownOpen = false;
    }

    private void DropDownPopup_Closed(object? sender, EventArgs e)
    {
        if (IsDropDownOpen)
        {
            SetCurrentValue(IsDropDownOpenProperty, false);
        }
    }

    private void DropDownPopup_Opened(object? sender, EventArgs e)
    {
        if (SearchEnabled)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
        }
    }
}
