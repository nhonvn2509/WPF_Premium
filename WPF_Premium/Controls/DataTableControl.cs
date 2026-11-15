using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace WPF_Premium.Controls;

public enum DataTableSqlDialect
{
    SqlServer,
    Sqlite,
    PostgreSql,
    MySql
}

public sealed class DataTableLoadFailedEventArgs(Exception exception) : EventArgs
{
    public Exception Exception { get; } = exception;
}

public sealed class DataTableControl : UserControl
{
    private readonly SearchBox _searchBox;
    private readonly Table _table;
    private readonly Pagination _pagination;
    private readonly TextBlock _summary;
    private readonly TextBlock _errorText;
    private readonly DispatcherTimer _searchTimer;
    private CancellationTokenSource? _activeLoad;
    private string? _sortColumn;
    private ListSortDirection _sortDirection = ListSortDirection.Ascending;
    private bool _synchronizingPager;

    public static readonly DependencyProperty SearchTextProperty =
        DependencyProperty.Register(
            nameof(SearchText),
            typeof(string),
            typeof(DataTableControl),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty PageSizeProperty =
        DependencyProperty.Register(
            nameof(PageSize),
            typeof(int),
            typeof(DataTableControl),
            new PropertyMetadata(10, OnPageSizeChanged, CoercePageSize));

    public static readonly DependencyProperty CurrentPageProperty =
        DependencyProperty.Register(
            nameof(CurrentPage),
            typeof(int),
            typeof(DataTableControl),
            new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCurrentPageChanged));

    public ObservableCollection<DataGridColumn> Columns { get; } = new();
    public ObservableCollection<string> SearchableColumns { get; } = new();
    public ObservableCollection<string> SortableColumns { get; } = new();
    public IDictionary<string, object?> QueryParameters { get; } = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    public DataTableControl()
    {
        _searchBox = new SearchBox
        {
            Width = 320,
            HorizontalAlignment = HorizontalAlignment.Left,
            Placeholder = "Tìm kiếm..."
        };
        AutomationProperties.SetAutomationId(_searchBox, "DataTableSearch");
        _searchBox.TextChanged += SearchBox_TextChanged;

        _summary = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        _summary.SetResourceReference(ForegroundProperty, "MutedForegroundBrush");
        AutomationProperties.SetAutomationId(_summary, "DataTableSummary");

        var header = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_summary, 1);
        header.Children.Add(_searchBox);
        header.Children.Add(_summary);

        _table = new Table
        {
            MinHeight = 140,
            MaxHeight = 440,
            CanUserSortColumns = true
        };
        _table.SetResourceReference(FrameworkElement.StyleProperty, "Table");
        AutomationProperties.SetAutomationId(_table, "DataTableGrid");
        _table.Sorting += Table_Sorting;
        Columns.CollectionChanged += Columns_CollectionChanged;

        _pagination = new Pagination
        {
            PageCount = 1,
            CurrentPage = 1,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0)
        };
        _pagination.SetResourceReference(FrameworkElement.StyleProperty, "Pagination");
        AutomationProperties.SetAutomationId(_pagination, "DataTablePagination");
        _pagination.PageChanged += Pagination_PageChanged;

        _errorText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 8, 0, 0)
        };
        _errorText.SetResourceReference(ForegroundProperty, "DestructiveBrush");

        var layout = new DockPanel();
        DockPanel.SetDock(_pagination, Dock.Bottom);
        DockPanel.SetDock(_errorText, Dock.Bottom);
        DockPanel.SetDock(header, Dock.Top);
        layout.Children.Add(_pagination);
        layout.Children.Add(_errorText);
        layout.Children.Add(header);
        layout.Children.Add(_table);
        Content = layout;

        _searchTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _searchTimer.Tick += SearchTimer_Tick;
        Unloaded += (_, _) => _activeLoad?.Cancel();
    }

    public Func<DbConnection>? ConnectionFactory { get; set; }
    public string Query { get; set; } = string.Empty;
    public string? CountQuery { get; set; }
    public DataTableSqlDialect SqlDialect { get; set; } = DataTableSqlDialect.SqlServer;

    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public int PageSize
    {
        get => (int)GetValue(PageSizeProperty);
        set => SetValue(PageSizeProperty, value);
    }

    public int CurrentPage
    {
        get => (int)GetValue(CurrentPageProperty);
        set => SetValue(CurrentPageProperty, value);
    }

    public long TotalItems { get; private set; }

    public event EventHandler<DataTableLoadFailedEventArgs>? LoadFailed;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        _activeLoad?.Cancel();
        var loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeLoad = loadCancellation;
        _errorText.Visibility = Visibility.Collapsed;
        _summary.Text = "Đang tải dữ liệu...";

        try
        {
            if (ConnectionFactory is null)
            {
                throw new InvalidOperationException("ConnectionFactory phải được cấu hình trước khi tải dữ liệu.");
            }
            if (string.IsNullOrWhiteSpace(Query))
            {
                throw new InvalidOperationException("Query phải chứa một câu SELECT cơ sở.");
            }
            if (PageSize < 1)
            {
                throw new InvalidOperationException("PageSize phải lớn hơn 0.");
            }

            var token = loadCancellation.Token;
            await using var connection = ConnectionFactory();
            await connection.OpenAsync(token);

            var whereClause = BuildSearchClause(out var searchParameter);
            var countSql = string.IsNullOrWhiteSpace(CountQuery)
                ? $"SELECT COUNT(*) FROM ({NormalizeQuery(Query)}) AS premium_source{whereClause}"
                : $"{CountQuery!.Trim().TrimEnd(';')}{whereClause}";

            await using var countCommand = connection.CreateCommand();
            countCommand.CommandText = countSql;
            AddParameters(countCommand, searchParameter, null);
            var countResult = await countCommand.ExecuteScalarAsync(token);
            var totalItems = Convert.ToInt64(countResult, System.Globalization.CultureInfo.InvariantCulture);
            var pageCount = Math.Max(1L, (totalItems + PageSize - 1) / PageSize);
            var pageCountInt = (int)Math.Min(int.MaxValue, pageCount);
            _synchronizingPager = true;
            CurrentPage = Math.Clamp(CurrentPage, 1, pageCountInt);
            _pagination.PageCount = pageCountInt;
            _pagination.CurrentPage = CurrentPage;
            _synchronizingPager = false;

            await using var pageCommand = connection.CreateCommand();
            pageCommand.CommandText = BuildPageQuery(whereClause);
            AddParameters(pageCommand, searchParameter, (long)(CurrentPage - 1) * PageSize);
            await using var reader = await pageCommand.ExecuteReaderAsync(token);
            var result = await ReadTableAsync(reader, token);

            _table.ItemsSource = result.DefaultView;
            _table.BeginAnimation(
                UIElement.OpacityProperty,
                new DoubleAnimation(0.82, 1, TimeSpan.FromMilliseconds(120))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.Stop
                },
                HandoffBehavior.SnapshotAndReplace);
            TotalItems = totalItems;
            _summary.Text = $"{totalItems:N0} bản ghi · trang {CurrentPage}/{pageCountInt}";
            _errorText.Visibility = Visibility.Collapsed;
        }
        catch (OperationCanceledException) when (loadCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _synchronizingPager = false;
            _summary.Text = "Không thể tải dữ liệu";
            _errorText.Text = exception.Message;
            _errorText.Visibility = Visibility.Visible;
            LoadFailed?.Invoke(this, new DataTableLoadFailedEventArgs(exception));
        }
        finally
        {
            if (ReferenceEquals(_activeLoad, loadCancellation))
            {
                _activeLoad = null;
            }
            loadCancellation.Dispose();
        }
    }

    private static object CoercePageSize(DependencyObject d, object baseValue) =>
        Math.Clamp((int)baseValue, 1, 500);

    private static void OnPageSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (DataTableControl)d;
        control.CurrentPage = 1;
        control.QueueRefresh();
    }

    private static void OnCurrentPageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (DataTableControl)d;
        if (control._synchronizingPager || control._pagination is null)
        {
            return;
        }

        control._synchronizingPager = true;
        control._pagination.CurrentPage = (int)e.NewValue;
        control._synchronizingPager = false;
    }

    private void Columns_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (DataGridColumn column in e.OldItems)
            {
                _table.Columns.Remove(column);
            }
        }
        if (e.NewItems is not null)
        {
            foreach (DataGridColumn column in e.NewItems)
            {
                _table.Columns.Add(column);
            }
        }
    }

    private void SearchBox_TextChanged(object? sender, EventArgs e)
    {
        SearchText = _searchBox.Text;
        CurrentPage = 1;
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private async void SearchTimer_Tick(object? sender, EventArgs e)
    {
        _searchTimer.Stop();
        await RefreshAsync();
    }

    private async void Pagination_PageChanged(object? sender, RoutedPropertyChangedEventArgs<int> e)
    {
        if (_synchronizingPager)
        {
            return;
        }
        CurrentPage = e.NewValue;
        await RefreshAsync();
    }

    private void Table_Sorting(object? sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        var columnName = e.Column.SortMemberPath;
        if (string.IsNullOrWhiteSpace(columnName) ||
            !SortableColumns.Contains(columnName, StringComparer.OrdinalIgnoreCase))
        {
            _summary.Text = "Sắp xếp chỉ bật cho các cột trong SortableColumns.";
            return;
        }

        foreach (var column in _table.Columns)
        {
            column.SortDirection = null;
        }

        _sortDirection = _sortColumn == columnName && _sortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;
        _sortColumn = columnName;
        e.Column.SortDirection = _sortDirection;
        CurrentPage = 1;
        _pagination.CurrentPage = 1;
        QueueRefresh();
    }

    private void QueueRefresh()
    {
        if (IsLoaded)
        {
            _ = RefreshAsync();
        }
    }

    private string BuildSearchClause(out string? searchParameter)
    {
        searchParameter = null;
        if (string.IsNullOrWhiteSpace(SearchText) || SearchableColumns.Count == 0)
        {
            return string.Empty;
        }

        searchParameter = $"%{SearchText.Replace("~", "~~", StringComparison.Ordinal).Replace("%", "~%", StringComparison.Ordinal).Replace("_", "~_", StringComparison.Ordinal)}%";
        var conditions = SearchableColumns
            .Select(column => $"{QuoteIdentifier(column)} LIKE @__search ESCAPE '~'")
            .ToArray();
        return $" WHERE ({string.Join(" OR ", conditions)})";
    }

    private string BuildPageQuery(string whereClause)
    {
        var orderBy = string.IsNullOrWhiteSpace(_sortColumn)
            ? "1"
            : $"{QuoteIdentifier(_sortColumn)} {(_sortDirection == ListSortDirection.Ascending ? "ASC" : "DESC")}";
        var source = $"SELECT * FROM ({NormalizeQuery(Query)}) AS premium_source{whereClause} ORDER BY {orderBy}";
        return SqlDialect == DataTableSqlDialect.SqlServer
            ? $"{source} OFFSET @__offset ROWS FETCH NEXT @__pageSize ROWS ONLY"
            : $"{source} LIMIT @__pageSize OFFSET @__offset";
    }

    private void AddParameters(DbCommand command, string? searchParameter, long? offset)
    {
        foreach (var (name, value) in QueryParameters)
        {
            AddParameter(command, NormalizeParameterName(name), value);
        }
        if (searchParameter is not null)
        {
            AddParameter(command, "@__search", searchParameter);
        }
        if (offset.HasValue)
        {
            AddParameter(command, "@__offset", offset.Value);
            AddParameter(command, "@__pageSize", PageSize);
        }
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private string QuoteIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier) || identifier.Contains('.'))
        {
            throw new InvalidOperationException($"Column name '{identifier}' must be a single output-column name.");
        }

        return SqlDialect switch
        {
            DataTableSqlDialect.SqlServer => $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]",
            DataTableSqlDialect.MySql => $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`",
            _ => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
        };
    }

    private static string NormalizeQuery(string query) => query.Trim().TrimEnd(';');

    private static string NormalizeParameterName(string name) =>
        name.StartsWith('@') || name.StartsWith(':') || name.StartsWith('$') ? name : $"@{name}";

    private static async Task<System.Data.DataTable> ReadTableAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        var result = new System.Data.DataTable();
        for (var index = 0; index < reader.FieldCount; index++)
        {
            var type = reader.GetFieldType(index);
            result.Columns.Add(reader.GetName(index), type);
        }

        while (await reader.ReadAsync(cancellationToken))
        {
            var values = new object[reader.FieldCount];
            reader.GetValues(values);
            result.Rows.Add(values);
        }

        return result;
    }
}
