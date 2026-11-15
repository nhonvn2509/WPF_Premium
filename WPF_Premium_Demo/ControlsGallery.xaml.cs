using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Globalization;
using Microsoft.Data.Sqlite;
using WPF_Premium.Controls;

namespace WPF_Premium_Demo;

public partial class ControlsGallery : UserControl
{
    private const string CodeBehindMarker = "// Code-behind";

    private static readonly IReadOnlyDictionary<string, string> PageSections = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Page.TreeView"] = nameof(TreeViewSection),
        ["Page.Toolbar"] = nameof(ToolbarSection),
        ["Page.Accordion"] = nameof(AccordionSection),
        ["Page.Alert"] = nameof(AlertSection),
        ["Page.FormControls"] = nameof(FormSection),
        ["Page.Calendar"] = nameof(CalendarSection),
        ["Page.Selectors"] = nameof(SelectorsSection),
        ["Page.Menus"] = nameof(MenusSection),
        ["Page.ButtonsGallery"] = nameof(ButtonsSection),
        ["Page.Pagination"] = nameof(PaginationSection),
        ["Page.Table"] = nameof(TableSection),
        ["Page.DataTable"] = nameof(DataTableSection),
        ["Page.DataInsights"] = nameof(DataInsightsSection),
        ["Page.Badge"] = nameof(BadgeSection),
        ["Page.Divider"] = nameof(DividerSection),
        ["Page.StepBar"] = nameof(StepBarSection),
        ["Page.Waterfall"] = nameof(WaterfallPanelSection),
        ["Page.Charts"] = nameof(ChartsSection),
        ["Page.Trace"] = nameof(TraceSection),
        ["Page.Overlays"] = nameof(OverlaysSection),
        ["Page.Docking"] = nameof(DockingSection),
        ["Page.OfflineMap"] = nameof(OfflineMapSection)
    };

    private readonly Dictionary<string, Expander> _codePanels = new(StringComparer.Ordinal);
    private readonly SqliteConnection _sampleDatabaseAnchor;
    private Border? _activeSection;

    public ControlsGallery()
    {
        InitializeComponent();
        SizeChanged += ControlsGallery_SizeChanged;
        var connectionString = $"Data Source=WpfPremiumDemo_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _sampleDatabaseAnchor = new SqliteConnection(connectionString);
        _sampleDatabaseAnchor.Open();
        InitializeSampleDatabase();
        ProductsDataTable.ConnectionFactory = () => new SqliteConnection(connectionString);
        ProductsDataTable.Query = "SELECT Id, Name, Category, Price, Stock, Status FROM Products";
        ProductsDataTable.SearchableColumns.Add("Name");
        ProductsDataTable.SearchableColumns.Add("Category");
        ProductsDataTable.SearchableColumns.Add("Status");
        foreach (var column in new[] { "Name", "Category", "Price", "Stock", "Status" })
        {
            ProductsDataTable.SortableColumns.Add(column);
        }
        Loaded += async (_, _) => await ProductsDataTable.RefreshAsync();
        OfflineMapPreview.Locations = new[]
        {
            new OfflineMapLocation(21.0285, 105.8542, "Hồ Gươm", "ho-guom"),
            new OfflineMapLocation(21.0368, 105.8342, "Lăng Chủ tịch Hồ Chí Minh", "lang-bac"),
            new OfflineMapLocation(21.0245, 105.8412, "Nhà hát Lớn Hà Nội", "nha-hat-lon")
        };
        Table.ItemsSource = new[]
        {
            new { Name = "Ngăn xếp", Status = "Sẵn sàng" },
            new { Name = "Lịch", Status = "Sẵn sàng" },
            new { Name = "Cây điều hướng", Status = "Đang xem xét" },
            new { Name = "Thanh công cụ", Status = "Sẵn sàng" },
            new { Name = "Bảng dữ liệu", Status = "Đã cập nhật" },
            new { Name = "Nhãn trạng thái", Status = "Sẵn sàng" },
            new { Name = "Tiến trình nhiều bước", Status = "Đang xử lý" },
            new { Name = "Bản đồ ngoại tuyến", Status = "Sẵn sàng" }
        };
        FrameworkPicker.ItemsSource = new[] { "WPF", "WinUI", "Avalonia" };
        TechnologyMultiSelect.ItemsSource = new[] { "WPF", "WinUI", "Avalonia", "Blazor", "MAUI" };
        ToolbarFontPicker.ItemsSource = new[] { "Aptos", "Arial", "Calibri", "Times New Roman" };
        ToolbarFontPicker.SelectedItem = "Aptos";
        ToolbarFontSizePicker.ItemsSource = new[] { "10", "11", "12", "14", "16", "18", "24", "36" };
        ToolbarFontSizePicker.SelectedItem = "14";
        SingleDateTimePicker.SelectedDateTime = DateTime.Now;
        DateTimeRangePicker.StartDateTime = DateTime.Today;
        DateTimeRangePicker.EndDateTime = DateTime.Today.AddHours(23).AddMinutes(59).AddSeconds(59);

        var months = new[] { "Thg 1", "Thg 2", "Thg 3", "Thg 4", "Thg 5", "Thg 6" };
        LineChart.Labels = months;
        LineChart.Series =
        [
            new ChartSeries("Doanh thu", new[] { 12d, 18, 15, 24, 21, 30 }),
            new ChartSeries("Chi phí", new[] { 8d, 10, 11, 14, 13, 17 })
        ];
        AreaChart.Labels = months;
        AreaChart.Series = [new ChartSeries("Người dùng", new[] { 16d, 22, 20, 32, 29, 38 })];
        ColumnChart.Labels = months;
        ColumnChart.Series =
        [
            new ChartSeries("Năm nay", new[] { 10d, 14, 18, 16, 24, 28 }),
            new ChartSeries("Năm trước", new[] { 8d, 12, 14, 15, 18, 20 })
        ];
        BarChart.Labels = new[] { "Nền tảng", "Thiết kế", "Dữ liệu", "Cốt lõi" };
        BarChart.Series = [new ChartSeries("Công việc", new[] { 12d, 8, 15, 6 })];
        ScatterChart.Series =
        [
            new ChartSeries(
                "Độ trễ",
                new[] { 14d, 23, 18, 31, 26, 37, 29 },
                new[] { 1d, 2.2, 3.1, 4.8, 6, 7.4, 8.2 })
        ];
        DoughnutChart.Series =
        [
            new ChartSeries("Tìm kiếm", new[] { 42d }),
            new ChartSeries("Trực tiếp", new[] { 28d }),
            new ChartSeries("Giới thiệu", new[] { 18d }),
            new ChartSeries("Mạng xã hội", new[] { 12d })
        ];
    }

    private void InitializeSampleDatabase()
    {
        using var command = _sampleDatabaseAnchor.CreateCommand();
        command.CommandText = """
            CREATE TABLE Products (
                Id INTEGER PRIMARY KEY,
                Name TEXT NOT NULL,
                Category TEXT NOT NULL,
                Price REAL NOT NULL,
                Stock INTEGER NOT NULL,
                Status TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();

        var products = new (string Name, string Category, double Price, int Stock, string Status)[]
        {
            ("Bàn phím cơ K87", "Phụ kiện", 1850000, 24, "Đang bán"),
            ("Chuột không dây Pro", "Phụ kiện", 920000, 18, "Đang bán"),
            ("Màn hình 27 inch QHD", "Màn hình", 6450000, 7, "Sắp hết"),
            ("Đế tản nhiệt laptop", "Phụ kiện", 540000, 32, "Đang bán"),
            ("Tai nghe Studio", "Âm thanh", 2350000, 12, "Đang bán"),
            ("Webcam Full HD", "Phụ kiện", 1250000, 9, "Sắp hết"),
            ("Loa Bluetooth Mini", "Âm thanh", 790000, 0, "Hết hàng"),
            ("Màn hình 24 inch IPS", "Màn hình", 3890000, 16, "Đang bán"),
            ("Adapter USB-C 7-in-1", "Kết nối", 1150000, 21, "Đang bán"),
            ("Cáp USB-C bọc dù", "Kết nối", 280000, 46, "Đang bán"),
            ("Hub USB-C Pro", "Kết nối", 1590000, 5, "Sắp hết"),
            ("Micro thu âm USB", "Âm thanh", 2750000, 8, "Sắp hết"),
            ("Bàn phím không dây", "Phụ kiện", 1120000, 14, "Đang bán"),
            ("Chuột công thái học", "Phụ kiện", 1480000, 11, "Đang bán"),
            ("Tai nghe chống ồn", "Âm thanh", 4250000, 4, "Sắp hết"),
            ("Adapter HDMI 4K", "Kết nối", 460000, 30, "Đang bán"),
            ("Màn hình 32 inch UHD", "Màn hình", 9850000, 3, "Sắp hết"),
            ("Loa kiểm âm Compact", "Âm thanh", 3650000, 6, "Sắp hết"),
            ("Cáp HDMI 2.1", "Kết nối", 350000, 40, "Đang bán"),
            ("Giá đỡ màn hình", "Màn hình", 890000, 13, "Đang bán"),
            ("Adapter mạng Gigabit", "Kết nối", 620000, 17, "Đang bán"),
            ("Bàn di chuột XL", "Phụ kiện", 320000, 28, "Đang bán"),
            ("Tai nghe USB-C", "Âm thanh", 990000, 0, "Hết hàng"),
            ("Màn hình di động 15 inch", "Màn hình", 5290000, 10, "Đang bán")
        };
        using var insert = _sampleDatabaseAnchor.CreateCommand();
        insert.CommandText = """
            INSERT INTO Products (Name, Category, Price, Stock, Status)
            VALUES ($name, $category, $price, $stock, $status);
            """;
        var name = insert.CreateParameter();
        name.ParameterName = "$name";
        insert.Parameters.Add(name);
        var category = insert.CreateParameter();
        category.ParameterName = "$category";
        insert.Parameters.Add(category);
        var price = insert.CreateParameter();
        price.ParameterName = "$price";
        insert.Parameters.Add(price);
        var stock = insert.CreateParameter();
        stock.ParameterName = "$stock";
        insert.Parameters.Add(stock);
        var status = insert.CreateParameter();
        status.ParameterName = "$status";
        insert.Parameters.Add(status);

        foreach (var product in products)
        {
            name.Value = product.Name;
            category.Value = product.Category;
            price.Value = product.Price;
            stock.Value = product.Stock;
            status.Value = product.Status;
            insert.ExecuteNonQuery();
        }
    }

    private void ProductRating_ValueChanged(object sender, RoutedPropertyChangedEventArgs<int> e)
    {
        if (ProductRatingValue is not null)
        {
            ProductRatingValue.Text = $"{e.NewValue} / {ProductRating.Maximum}";
        }
    }

    public void ShowPage(string pageKey, string exampleTitle, string source)
    {
        if (!PageSections.TryGetValue(pageKey, out var sectionName) ||
            FindName(sectionName) is not Border activeSection ||
            activeSection.Child is not StackPanel sectionContent)
        {
            throw new InvalidOperationException($"Gallery section for '{pageKey}' is not configured.");
        }

        foreach (var name in PageSections.Values)
        {
            if (FindName(name) is FrameworkElement section)
            {
                section.Visibility = ReferenceEquals(section, activeSection)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        _activeSection = activeSection;
        UpdateActiveSectionWidth();
        if (!_codePanels.TryGetValue(pageKey, out var codePanel))
        {
            codePanel = CreateCodePanel(exampleTitle, source);
            sectionContent.Children.Add(codePanel);
            _codePanels.Add(pageKey, codePanel);
        }
    }

    private void ControlsGallery_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateActiveSectionWidth();
    }

    private void UpdateActiveSectionWidth()
    {
        if (_activeSection is null || GalleryRoot.ActualWidth <= 0)
        {
            return;
        }

        var margin = _activeSection.Margin;
        _activeSection.Width = Math.Max(0, GalleryRoot.ActualWidth - margin.Left - margin.Right);
    }

    public static Expander CreateCodePanel(string exampleTitle, string source)
    {
        var title = new TextBlock
        {
            Text = $"Ví dụ: {exampleTitle}",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 6)
        };
        AutomationProperties.SetAutomationId(title, "CodeSampleTitle");

        var codeContent = new StackPanel();
        codeContent.Children.Add(title);

        var separatorIndex = source.IndexOf(CodeBehindMarker, StringComparison.Ordinal);
        if (separatorIndex >= 0)
        {
            var xamlSource = source[..separatorIndex].Trim();
            var codeBehindSource = source[(separatorIndex + CodeBehindMarker.Length)..].Trim();
            var tabs = new TabControl();
            tabs.Items.Add(new TabItem
            {
                Header = "XAML",
                Content = CreateCodeEditor(xamlSource)
            });
            tabs.Items.Add(new TabItem
            {
                Header = "C#",
                Content = CreateCodeEditor(codeBehindSource)
            });
            codeContent.Children.Add(tabs);
        }
        else
        {
            codeContent.Children.Add(CreateCodeEditor(source));
        }

        var expander = new Expander
        {
            Header = "Xem mã nguồn",
            Margin = new Thickness(0, 10, 0, 0),
            Content = codeContent
        };
        expander.Expanded += (_, _) =>
        {
            codeContent.BeginAnimation(
                UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(130))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.Stop
                },
                HandoffBehavior.SnapshotAndReplace);
        };
        expander.SetResourceReference(StyleProperty, "Collapsible");
        AutomationProperties.SetAutomationId(expander, "CodeSampleToggle");
        return expander;
    }

    private static UIElement CreateCodeEditor(string source)
    {
        var codeText = new TextBox
        {
            Text = source,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            Padding = new Thickness(8),
            MaxHeight = 320
        };
        codeText.SetResourceReference(Control.BackgroundProperty, "BackgroundBrush");
        codeText.SetResourceReference(Control.ForegroundProperty, "ForegroundBrush");
        codeText.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
        AutomationProperties.SetAutomationId(codeText, "CodeSampleText");

        var copyButton = new Button
        {
            Content = "Sao chép mã",
            Padding = new Thickness(10, 5, 10, 5),
            Margin = new Thickness(0, 0, 0, 6),
            HorizontalAlignment = HorizontalAlignment.Right,
            ToolTip = "Sao chép đoạn mã này vào bộ nhớ tạm"
        };
        copyButton.SetResourceReference(FrameworkElement.StyleProperty, "ButtonOutline");
        AutomationProperties.SetAutomationId(copyButton, "CopyCodeButton");
        copyButton.Click += (_, _) => CopyCodeToClipboard(codeText);

        var editor = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(copyButton, Dock.Top);
        editor.Children.Add(copyButton);
        editor.Children.Add(codeText);
        return editor;
    }

    private static void CopyCodeToClipboard(TextBox codeText)
    {
        Clipboard.SetText(codeText.Text);
        var owner = Window.GetWindow(codeText)
            ?? throw new InvalidOperationException("The code sample must be hosted in a Window.");
        ToastService.Show(owner, "Đã sao chép mã vào bộ nhớ tạm.", ToastKind.Success);
    }

    public void NavigateToSection(string sectionName)
    {
        if (FindName(sectionName) is not FrameworkElement section)
        {
            throw new InvalidOperationException($"Gallery section '{sectionName}' was not found.");
        }

        section.BringIntoView();
    }

    private void ValidateForm_Click(object sender, RoutedEventArgs e)
    {
        ProjectNameField.Error = string.IsNullOrWhiteSpace(ProjectNameInput.Text)
            ? "Vui lòng nhập tên dự án."
            : null;
        RetryCountField.Error = !int.TryParse(RetryCountInput.Text, out var retryCount) || retryCount < 1
            ? "Vui lòng nhập số nguyên dương."
            : null;
        EmailField.Error = !EmailInput.Text.Contains('@')
            ? "Vui lòng nhập địa chỉ thư điện tử hợp lệ."
            : null;
        DescriptionField.Error = string.IsNullOrWhiteSpace(DescriptionInput.Text)
            ? "Vui lòng nhập mô tả."
            : null;
        KeywordField.Error = string.IsNullOrWhiteSpace(KeywordInput.Text)
            ? "Vui lòng nhập từ khóa."
            : null;
        TechnologyField.Error = TechnologyMultiSelect.SelectedItems.Count == 0
            ? "Vui lòng chọn ít nhất một công nghệ."
            : null;
        TermsField.Error = TermsInput.IsChecked == true
            ? null
            : "Vui lòng đồng ý với điều khoản để tiếp tục.";
        DeploymentField.Error = StandardDeploymentInput.IsChecked == true || CompactDeploymentInput.IsChecked == true
            ? null
            : "Vui lòng chọn chế độ triển khai.";
        FrameworkField.Error = FrameworkPicker.SelectedItem is null
            ? "Vui lòng chọn nền tảng."
            : null;
        PhoneField.Error = PhoneInput.Value.Length != 12
            ? "Vui lòng nhập đủ 12 chữ số của số điện thoại."
            : null;
        BirthDateField.Error = BirthDateInput.Value.Length != 8 ||
                               !DateTime.TryParseExact(
                                   BirthDateInput.Value,
                                   "ddMMyyyy",
                                   CultureInfo.InvariantCulture,
                                   DateTimeStyles.None,
                                   out _)
            ? "Vui lòng nhập ngày hợp lệ theo định dạng ngày/tháng/năm."
            : null;
    }

    private void OpenPanel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string panelTag })
        {
            return;
        }

        var panelParts = panelTag.Split(':', 2);
        var panelType = panelParts[0];
        var direction = panelParts.Length == 2
            ? Enum.Parse<OverlayDirection>(panelParts[1])
            : panelType == "Sheet" ? OverlayDirection.Bottom : OverlayDirection.Right;
        var owner = Window.GetWindow(this)
            ?? throw new InvalidOperationException("The component gallery must be hosted in a Window.");
        var directionText = direction switch
        {
            OverlayDirection.Left => "trái",
            OverlayDirection.Top => "trên",
            OverlayDirection.Right => "phải",
            OverlayDirection.Bottom => "dưới",
            _ => "phải"
        };
        var panelTitle = panelType switch
        {
            "AlertDialog" => "Hộp cảnh báo",
            "Drawer" => "Ngăn trượt",
            "Sheet" => "Bảng trượt",
            _ => "Hộp thoại"
        };
        object content = panelType is "Dialog" or "Drawer" or "Sheet"
            ? CreateValidationForm()
            : new TextBlock
            {
                Text = $"{panelTitle} hiển thị phía trên cửa sổ hiện tại.",
                TextWrapping = TextWrapping.Wrap
            };

        switch (panelType)
        {
            case "Dialog":
                DialogService.ShowDialog(owner, "Hộp thoại", content);
                break;
            case "AlertDialog":
                DialogService.ShowAlertDialog(owner, "Bỏ các thay đổi?", content);
                break;
            case "Drawer":
                DialogService.ShowDrawer(owner, $"Ngăn trượt - {directionText}", content, direction);
                break;
            case "Sheet":
                DialogService.ShowSheet(owner, $"Bảng trượt - {directionText}", content, direction);
                break;
            default:
                throw new InvalidOperationException($"Unknown panel type '{panelType}'.");
        }
    }

    private UIElement CreateValidationForm()
    {
        var form = new StackPanel { Margin = new Thickness(2) };
        var nameInput = new TextBox { Tag = "Tên dự án" };
        var nameField = AddFormField(form, "Tên dự án", nameInput);
        var emailInput = new TextBox { Tag = "ten@vidu.vn" };
        var emailField = AddFormField(form, "Thư điện tử", emailInput);
        var countInput = new TextBox { Tag = "Số nguyên dương" };
        var countField = AddFormField(form, "Số lần thử lại", countInput);
        var phoneInput = new FormattedInputControl
        {
            Mask = "+## (###) ###-####",
            Placeholder = "+84 (___) ___-____"
        };
        var phoneField = AddFormField(form, "Số điện thoại", phoneInput);
        var dateInput = new FormattedInputControl
        {
            Mask = "##/##/####",
            Placeholder = "ngày/tháng/năm"
        };
        var dateField = AddFormField(form, "Ngày", dateInput);
        var keywordInput = new TextBox { Tag = "Từ khóa tìm kiếm" };
        var keywordGroup = new DockPanel { LastChildFill = true };
        var keywordButton = new Button
        {
            Content = "Tìm",
            Style = (Style)FindResource("ButtonSecondary"),
            Padding = new Thickness(8, 4, 8, 4)
        };
        DockPanel.SetDock(keywordButton, Dock.Right);
        keywordGroup.Children.Add(keywordButton);
        keywordGroup.Children.Add(keywordInput);
        var keywordField = AddFormField(form, "Nhóm ô nhập", keywordGroup);
        var descriptionInput = new TextBox
        {
            Tag = "Mô tả dự án",
            Style = (Style)FindResource("Textarea"),
            AcceptsReturn = true,
            MinHeight = 64
        };
        var descriptionField = AddFormField(form, "Mô tả", descriptionInput);
        var termsInput = new CheckBox { Content = "Tôi đồng ý với điều khoản" };
        var termsField = AddFormField(form, "Điều khoản", termsInput);
        var radioInput = new StackPanel();
        var standardOption = new RadioButton { Content = "Tiêu chuẩn", GroupName = "OverlayValidationPlan" };
        var compactOption = new RadioButton { Content = "Thu gọn", GroupName = "OverlayValidationPlan" };
        radioInput.Children.Add(standardOption);
        radioInput.Children.Add(compactOption);
        var radioField = AddFormField(form, "Chế độ triển khai", radioInput);
        var frameworkInput = new ComboBoxControl
        {
            Placeholder = "Chọn nền tảng",
            ItemsSource = new[] { "WPF", "Avalonia", "WinUI" },
            Style = (Style)FindResource("Combobox")
        };
        var frameworkField = AddFormField(form, "Nền tảng", frameworkInput);
        var technologyInput = new MultiSelectControl
        {
            Placeholder = "Chọn công nghệ",
            ItemsSource = new[] { "WPF", "Avalonia", "WinUI", "MAUI" },
            Style = (Style)FindResource("MultiSelect"),
            MinHeight = 42
        };
        var technologyField = AddFormField(form, "Công nghệ", technologyInput);
        var status = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap
        };
        var validateButton = new Button
        {
            Content = "Kiểm tra biểu mẫu",
            Style = (Style)FindResource("ButtonPrimary"),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        validateButton.Click += (_, _) =>
        {
            nameField.Error = string.IsNullOrWhiteSpace(nameInput.Text) ? "Vui lòng nhập tên dự án." : null;
            emailField.Error = !emailInput.Text.Contains('@') ? "Vui lòng nhập địa chỉ thư điện tử hợp lệ." : null;
            countField.Error = !int.TryParse(countInput.Text, out var count) || count < 1
                ? "Vui lòng nhập số nguyên dương."
                : null;
            phoneField.Error = phoneInput.Value.Length != 12 ? "Vui lòng nhập đủ số điện thoại." : null;
            dateField.Error = dateInput.Value.Length != 8 ||
                              !DateTime.TryParseExact(dateInput.Value, "ddMMyyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                ? "Vui lòng nhập ngày hợp lệ."
                : null;
            keywordField.Error = string.IsNullOrWhiteSpace(keywordInput.Text) ? "Vui lòng nhập từ khóa." : null;
            descriptionField.Error = string.IsNullOrWhiteSpace(descriptionInput.Text) ? "Vui lòng nhập mô tả." : null;
            termsField.Error = termsInput.IsChecked == true ? null : "Vui lòng đồng ý với điều khoản.";
            radioField.Error = standardOption.IsChecked == true || compactOption.IsChecked == true
                ? null
                : "Vui lòng chọn chế độ triển khai.";
            frameworkField.Error = frameworkInput.SelectedItem is null ? "Vui lòng chọn nền tảng." : null;
            technologyField.Error = technologyInput.SelectedItems.Count > 0 ? null : "Vui lòng chọn ít nhất một công nghệ.";

            var isValid = new[]
            {
                nameField, emailField, countField, phoneField, dateField, keywordField,
                descriptionField, termsField, radioField, frameworkField, technologyField
            }.All(field => !field.HasError);
            status.Text = isValid
                ? "Biểu mẫu hợp lệ. Tất cả trường đã được kiểm tra."
                : "Vui lòng sửa các trường được đánh dấu trước khi tiếp tục.";
            status.Foreground = (Brush)FindResource(isValid ? "PrimaryBrush" : "DestructiveBrush");
        };
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.Children.Add(new ScrollViewer
        {
            Content = form,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        });

        var actions = new DockPanel { Margin = new Thickness(2, 10, 2, 2) };
        DockPanel.SetDock(validateButton, Dock.Right);
        actions.Children.Add(validateButton);
        actions.Children.Add(status);
        var footer = new Border
        {
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 10, 0, 0),
            Child = actions
        };
        Grid.SetRow(footer, 1);
        layout.Children.Add(footer);
        return layout;
    }

    private Field AddFormField(Panel parent, string label, UIElement input)
    {
        var field = new Field
        {
            Label = label,
            IsRequired = true,
            Content = input,
            Style = (Style)FindResource("Field")
        };
        parent.Children.Add(field);
        return field;
    }

    private void ShowToast_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this)
            ?? throw new InvalidOperationException("The component gallery must be hosted in a Window.");
        ToastService.Show(owner, "Đã lưu các thay đổi.", ToastKind.Success);
    }

    private void OpenDropdown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.IsOpen = true;
        }
    }

    private void SplitButton_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this)
            ?? throw new InvalidOperationException("The component gallery must be hosted in a Window.");
        ToastService.Show(owner, "Đã chọn thao tác xuất bản.", ToastKind.Success);
    }

    private void SplitButtonMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Header: string action })
        {
            return;
        }

        var owner = Window.GetWindow(this)
            ?? throw new InvalidOperationException("The component gallery must be hosted in a Window.");
        ToastService.Show(owner, $"Đã chọn: {action}.", ToastKind.Success);
    }
}
