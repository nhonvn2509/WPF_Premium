using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using MahApps.Metro.IconPacks;

namespace WPF_Premium.Controls;

public sealed class Pagination : UserControl
{
    private readonly StackPanel _pages = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock _pageText = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(8, 0, 0, 0),
    };

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

    public static readonly DependencyProperty PageChangedCommandProperty =
        DependencyProperty.Register(
            nameof(PageChangedCommand),
            typeof(ICommand),
            typeof(Pagination));

    public static readonly DependencyProperty ShowFirstLastButtonsProperty =
        DependencyProperty.Register(
            nameof(ShowFirstLastButtons),
            typeof(bool),
            typeof(Pagination),
            new PropertyMetadata(false, OnShowFirstLastButtonsChanged));

    public static readonly DependencyProperty FirstPageCommandProperty =
        DependencyProperty.Register(
            nameof(FirstPageCommand),
            typeof(ICommand),
            typeof(Pagination));

    public static readonly DependencyProperty LastPageCommandProperty =
        DependencyProperty.Register(
            nameof(LastPageCommand),
            typeof(ICommand),
            typeof(Pagination));

    public static readonly DependencyProperty PageTextProperty =
        DependencyProperty.Register(
            nameof(PageText),
            typeof(string),
            typeof(Pagination),
            new PropertyMetadata(string.Empty, OnPageTextChanged));

    public static readonly DependencyProperty CanGoPreviousPageProperty =
        DependencyProperty.Register(
            nameof(CanGoPreviousPage),
            typeof(bool),
            typeof(Pagination),
            new PropertyMetadata(true));

    public static readonly DependencyProperty CanGoNextPageProperty =
        DependencyProperty.Register(
            nameof(CanGoNextPage),
            typeof(bool),
            typeof(Pagination),
            new PropertyMetadata(true));

    public static readonly RoutedEvent PageChangedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(PageChanged),
            RoutingStrategy.Bubble,
            typeof(RoutedPropertyChangedEventHandler<int>),
            typeof(Pagination));

    public Pagination()
    {
        var root = new StackPanel { Orientation = Orientation.Horizontal };
        root.Children.Add(_pages);
        root.Children.Add(_pageText);
        Content = root;
        UpdateButtons();
        UpdatePageText();
    }

    private void UpdatePageText()
    {
        _pageText.Text = PageText;
        _pageText.Visibility = string.IsNullOrEmpty(PageText) ? Visibility.Collapsed : Visibility.Visible;
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

    public ICommand? PageChangedCommand
    {
        get => (ICommand?)GetValue(PageChangedCommandProperty);
        set => SetValue(PageChangedCommandProperty, value);
    }

    public bool ShowFirstLastButtons
    {
        get => (bool)GetValue(ShowFirstLastButtonsProperty);
        set => SetValue(ShowFirstLastButtonsProperty, value);
    }

    public ICommand? FirstPageCommand
    {
        get => (ICommand?)GetValue(FirstPageCommandProperty);
        set => SetValue(FirstPageCommandProperty, value);
    }

    public ICommand? LastPageCommand
    {
        get => (ICommand?)GetValue(LastPageCommandProperty);
        set => SetValue(LastPageCommandProperty, value);
    }

    public string PageText
    {
        get => (string)GetValue(PageTextProperty);
        set => SetValue(PageTextProperty, value);
    }

    public bool CanGoPreviousPage
    {
        get => (bool)GetValue(CanGoPreviousPageProperty);
        set => SetValue(CanGoPreviousPageProperty, value);
    }

    public bool CanGoNextPage
    {
        get => (bool)GetValue(CanGoNextPageProperty);
        set => SetValue(CanGoNextPageProperty, value);
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

    private static void OnShowFirstLastButtonsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((Pagination)d).UpdateButtons();

    private static void OnPageTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((Pagination)d).UpdatePageText();

    private void UpdateButtons()
    {
        if (_pages is null)
        {
            return;
        }

        _pages.Children.Clear();
        if (ShowFirstLastButtons)
        {
            _pages.Children.Add(CreateButton(
                "|<",
                1,
                "Trang đầu",
                CurrentPage > 1,
                FirstPageCommand));
        }
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
        if (ShowFirstLastButtons)
        {
            _pages.Children.Add(CreateButton(
                ">|",
                PageCount,
                "Trang cuối",
                CurrentPage < PageCount,
                LastPageCommand));
        }
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

    private Button CreateButton(object content, int page, string automationName, bool isEnabled, ICommand? directCommand = null)
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
        button.Click += (_, _) =>
        {
            var previousPage = CurrentPage;
            SetCurrentValue(CurrentPageProperty, page);
            var command = directCommand ?? PageChangedCommand;
            if (CurrentPage != previousPage && command?.CanExecute(CurrentPage) == true)
            {
                command.Execute(CurrentPage);
            }
        };
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
