using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using MahApps.Metro.IconPacks;

namespace WPF_Premium.Controls;

public sealed class Pagination : UserControl
{
    private readonly StackPanel _pages = new() { Orientation = Orientation.Horizontal };

    public static readonly DependencyProperty CurrentPageProperty =
        DependencyProperty.Register(
            nameof(CurrentPage),
            typeof(int),
            typeof(Pagination),
            new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCurrentPageChanged, CoerceCurrentPage));

    public static readonly DependencyProperty PageCountProperty =
        DependencyProperty.Register(
            nameof(PageCount),
            typeof(int),
            typeof(Pagination),
            new FrameworkPropertyMetadata(1, OnPageCountChanged, CoercePageCount));

    public static readonly DependencyProperty MaximumVisiblePagesProperty =
        DependencyProperty.Register(
            nameof(MaximumVisiblePages),
            typeof(int),
            typeof(Pagination),
            new FrameworkPropertyMetadata(5, OnMaximumVisiblePagesChanged, CoerceMaximumVisiblePages));

    public static readonly RoutedEvent PageChangedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(PageChanged),
            RoutingStrategy.Bubble,
            typeof(RoutedPropertyChangedEventHandler<int>),
            typeof(Pagination));

    public Pagination()
    {
        Content = _pages;
        UpdateButtons();
    }

    public int CurrentPage
    {
        get => (int)GetValue(CurrentPageProperty);
        set => SetValue(CurrentPageProperty, value);
    }

    public int PageCount
    {
        get => (int)GetValue(PageCountProperty);
        set => SetValue(PageCountProperty, value);
    }

    public int MaximumVisiblePages
    {
        get => (int)GetValue(MaximumVisiblePagesProperty);
        set => SetValue(MaximumVisiblePagesProperty, value);
    }

    public event RoutedPropertyChangedEventHandler<int> PageChanged
    {
        add => AddHandler(PageChangedEvent, value);
        remove => RemoveHandler(PageChangedEvent, value);
    }

    private static object CoercePageCount(DependencyObject d, object baseValue) =>
        Math.Max(1, (int)baseValue);

    private static object CoerceCurrentPage(DependencyObject d, object baseValue)
    {
        var pagination = (Pagination)d;
        return Math.Clamp((int)baseValue, 1, pagination.PageCount);
    }

    private static object CoerceMaximumVisiblePages(DependencyObject d, object baseValue) =>
        Math.Max(3, (int)baseValue);

    private static void OnPageCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var pagination = (Pagination)d;
        pagination.CoerceValue(CurrentPageProperty);
        pagination.UpdateButtons();
    }

    private static void OnMaximumVisiblePagesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((Pagination)d).UpdateButtons();

    private static void OnCurrentPageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var pagination = (Pagination)d;
        pagination.UpdateButtons();
        pagination.RaiseEvent(new RoutedPropertyChangedEventArgs<int>(
            (int)e.OldValue,
            (int)e.NewValue,
            PageChangedEvent));
    }

    private void UpdateButtons()
    {
        if (_pages is null)
        {
            return;
        }

        _pages.Children.Clear();
        _pages.Children.Add(CreateButton(
            CreateNavigationContent(PackIconMaterialKind.ChevronLeft, "Trước"),
            CurrentPage - 1,
            "Trang trước",
            CurrentPage > 1));

        foreach (var token in GetPageTokens())
        {
            if (token is null)
            {
                _pages.Children.Add(new TextBlock
                {
                    Text = "…",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 8, 0)
                });
                continue;
            }

            var button = CreateButton(token.Value.ToString(), token.Value, $"Trang {token.Value}", true);
            if (token.Value == CurrentPage)
            {
                button.SetResourceReference(FrameworkElement.StyleProperty, "ButtonSecondary");
                AutomationProperties.SetItemStatus(button, "Trang hiện tại");
            }
            _pages.Children.Add(button);
        }

        _pages.Children.Add(CreateButton(
            CreateNavigationContent(PackIconMaterialKind.ChevronRight, "Sau"),
            CurrentPage + 1,
            "Trang sau",
            CurrentPage < PageCount));
    }

    private StackPanel CreateNavigationContent(PackIconMaterialKind icon, string label) =>
        new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new PackIconMaterial
                {
                    Kind = icon,
                    Width = 12,
                    Height = 12,
                    Margin = new Thickness(0, 0, 5, 0),
                    VerticalAlignment = VerticalAlignment.Center
                },
                new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }
            }
        };

    private Button CreateButton(object content, int page, string automationName, bool isEnabled)
    {
        var button = new Button
        {
            Content = content,
            IsEnabled = isEnabled,
            MinWidth = 36,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(2, 0, 2, 0)
        };
        button.SetResourceReference(FrameworkElement.StyleProperty, "ButtonOutline");
        AutomationProperties.SetName(button, automationName);
        button.Click += (_, _) => SetCurrentValue(CurrentPageProperty, page);
        return button;
    }

    private IReadOnlyList<int?> GetPageTokens()
    {
        if (PageCount <= MaximumVisiblePages)
        {
            return Enumerable.Range(1, PageCount).Select(page => (int?)page).ToArray();
        }

        var innerPageCount = MaximumVisiblePages - 2;
        var start = Math.Clamp(CurrentPage - innerPageCount / 2, 2, PageCount - innerPageCount);
        var end = start + innerPageCount - 1;
        var pages = new List<int?> { 1 };
        if (start > 2)
        {
            pages.Add(null);
        }

        for (var page = start; page <= end; page++)
        {
            pages.Add(page);
        }

        if (end < PageCount - 1)
        {
            pages.Add(null);
        }
        pages.Add(PageCount);
        return pages;
    }
}
