using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WPF_Premium.Controls;
using WPF_Premium.Themes;

namespace WPF_Premium_Demo;

public partial class ExtendedComponentsPage : UserControl
{
    public ExtendedComponentsPage()
    {
        InitializeComponent();

        DemoBreadcrumb.ItemsSource = new[]
        {
            new BreadcrumbItem { Title = "Trang chủ", Tag = "home" },
            new BreadcrumbItem { Title = "Thư viện UI", Tag = "library" },
            new BreadcrumbItem { Title = "Thành phần mở rộng", Tag = "extended", IsCurrent = true }
        };

        DemoSegmented.ItemsSource = new[] { "Ngày", "Tuần", "Tháng", "Năm" };
        UpdateSegmentedStatus();

        DemoTagInput.Tags = new ObservableCollection<string> { "WPF", "MVVM", "UI" };
        UpdateTagStatus();

        DemoTimeline.ItemsSource = new[]
        {
            new TimelineItem
            {
                Title = "Khởi tạo dự án",
                Description = "Thiết lập cấu trúc WPF_Premium.Core và hệ thống theme.",
                Timestamp = "09:00",
                Status = TimelineStatus.Success
            },
            new TimelineItem
            {
                Title = "Tối ưu điều khiển",
                Description = "Sửa lỗi tập trung bàn phím RatingControl và xếp chồng ToastService.",
                Timestamp = "11:30",
                Status = TimelineStatus.Info
            },
            new TimelineItem
            {
                Title = "Kiểm tra tự động",
                Description = "Đang chờ chạy kiểm thử toàn bộ giao diện.",
                Timestamp = "14:15",
                Status = TimelineStatus.Warning
            },
            new TimelineItem
            {
                Title = "Cảnh báo lỗi mạng",
                Description = "Mất kết nối tạm thời tới dịch vụ đồng bộ.",
                Timestamp = "15:00",
                Status = TimelineStatus.Destructive
            }
        };
    }

    private void AccentPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string hex } && Application.Current is not null)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            ThemeManager.SetAccentColor(Application.Current, color);
        }
    }

    private void AccentReset_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current is not null)
        {
            ThemeManager.ResetAccentColor(Application.Current);
        }
    }

    private void ResetBanners_Click(object sender, RoutedEventArgs e)
    {
        InfoBanner.IsOpen = true;
        SuccessBanner.IsOpen = true;
        WarningBanner.IsOpen = true;
        DestructiveBanner.IsOpen = true;
    }

    private void InfoBanner_ActionClicked(object? sender, EventArgs e)
    {
        if (Window.GetWindow(this) is { } owner)
        {
            ToastService.Show(owner, "Đã mở chi tiết cập nhật WPF_Premium.", ToastKind.Info);
        }
    }

    private void DemoBreadcrumb_ItemClicked(object? sender, object e)
    {
        BreadcrumbStatusText.Text = $"Đã chọn mục điều hướng: {e}";
    }

    private void DemoSegmented_SelectionChanged(object? sender, EventArgs e) => UpdateSegmentedStatus();

    private void UpdateSegmentedStatus()
    {
        SegmentedStatusText.Text = $"Đang chọn: {DemoSegmented.SelectedItem ?? "(Chưa chọn)"} (Index: {DemoSegmented.SelectedIndex})";
    }

    private void DemoNumeric_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (NumericStatusText is not null)
        {
            NumericStatusText.Text = $"Giá trị: {e.NewValue:0.##}";
        }
    }

    private void DemoTagInput_TagsChanged(object? sender, EventArgs e) => UpdateTagStatus();

    private void UpdateTagStatus()
    {
        var tags = DemoTagInput.Tags;
        TagStatusText.Text = $"Số thẻ ({tags.Count}): {string.Join(", ", tags)}";
    }

    private void SkeletonToggle_Changed(object sender, RoutedEventArgs e)
    {
        var active = SkeletonActiveToggle.IsChecked == true;
        SkeletonAvatar.IsActive = active;
        SkeletonLine1.IsActive = active;
        SkeletonLine2.IsActive = active;
        SkeletonCard.IsActive = active;
    }

    private void ShowStackedToasts_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is not { } owner)
        {
            return;
        }

        ToastService.Show(owner, "Thông báo 1: Bắt đầu đồng bộ dữ liệu.", ToastKind.Info, TimeSpan.FromSeconds(5));
        ToastService.Show(owner, "Thông báo 2: Đã lưu 128 bản ghi thành công.", ToastKind.Success, TimeSpan.FromSeconds(5));
        ToastService.Show(owner, "Thông báo 3: Phát hiện 2 tệp cần xác nhận.", ToastKind.Warning, TimeSpan.FromSeconds(5));
    }
}
