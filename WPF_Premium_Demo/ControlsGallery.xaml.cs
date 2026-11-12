using System.Windows;
using System.Windows.Controls;
using WPF_Premium.Controls;

namespace WPF_Premium_Demo;

public partial class ControlsGallery : UserControl
{
    public ControlsGallery()
    {
        InitializeComponent();
        Table.ItemsSource = new[]
        {
            new { Name = "Ngăn xếp", Status = "Sẵn sàng" },
            new { Name = "Lịch", Status = "Sẵn sàng" }
        };
        FrameworkPicker.ItemsSource = new[] { "WPF", "WinUI", "Avalonia" };
        TechnologyMultiSelect.ItemsSource = new[] { "WPF", "WinUI", "Avalonia", "Blazor", "MAUI" };
        TechnologyMultiSelect.SelectedItems.Add("WPF");
        TechnologyMultiSelect.SelectedItems.Add("Avalonia");
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

    private void ValidateForm_Click(object sender, RoutedEventArgs e)
    {
        ProjectNameField.Error = string.IsNullOrWhiteSpace(ProjectNameInput.Text)
            ? "Vui lòng nhập tên dự án."
            : null;
        RetryCountField.Error = !int.TryParse(RetryCountInput.Text, out var retryCount) || retryCount < 1
            ? "Vui lòng nhập số nguyên dương."
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
        var content = new TextBlock
        {
            Text = panelType is "Drawer" or "Sheet"
                ? $"{panelTitle} xuất hiện từ cạnh {directionText} của cửa sổ."
                : $"{panelTitle} hiển thị phía trên cửa sổ hiện tại.",
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
