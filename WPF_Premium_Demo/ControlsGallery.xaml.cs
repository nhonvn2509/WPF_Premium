using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Globalization;
using Microsoft.Win32;
using WPF_Premium.Controls;

namespace WPF_Premium_Demo;

public partial class ControlsGallery : UserControl
{
    public ControlsGallery()
    {
        InitializeComponent();
        OfflineMapPreview.Locations = new[]
        {
            new OfflineMapLocation(21.0285, 105.8542, "Hồ Gươm", "ho-guom"),
            new OfflineMapLocation(21.0368, 105.8342, "Lăng Chủ tịch Hồ Chí Minh", "lang-bac"),
            new OfflineMapLocation(21.0245, 105.8412, "Nhà hát Lớn Hà Nội", "nha-hat-lon")
        };
        Table.ItemsSource = new[]
        {
            new { Name = "Ngăn xếp", Status = "Sẵn sàng" },
            new { Name = "Lịch", Status = "Sẵn sàng" }
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

    private void ChooseOfflineMap_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Chọn dữ liệu bản đồ ngoại tuyến",
            Filter = "Cơ sở dữ liệu bản đồ MBTiles (*.mbtiles)|*.mbtiles|Tất cả các tệp (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            OfflineMapPreview.MapFilePath = dialog.FileName;
        }
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
            Margin = new Thickness(0, 4, 0, 10),
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
        form.Children.Add(status);
        form.Children.Add(validateButton);
        return new ScrollViewer
        {
            Content = form,
            MaxHeight = 580,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
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
