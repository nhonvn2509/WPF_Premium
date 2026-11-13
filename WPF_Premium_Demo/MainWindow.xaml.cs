using System.Windows;
using System.Windows.Controls;
using WPF_Premium.Controls;
using WPF_Premium.Themes;

namespace WPF_Premium_Demo;

public partial class MainWindow : PremiumWindow
{
    private sealed record DemoExample(string Title, string Target, bool IsGallery, string Source);

    private static readonly IReadOnlyDictionary<string, DemoExample> DemoExamples = new Dictionary<string, DemoExample>
    {
        ["Main.Appearance"] = new("Giao diện", "ThemeSettingsHeading", false, """
            <premium:AppearanceSettingsControl />
            <Button Content="Sáng" Tag="Light" Click="SetTheme_Click" />
            <Button Content="Tối" Tag="Dark" Click="SetTheme_Click" />

            // Code-behind
            ThemeManager.SetTheme(Application.Current!, WpfPremiumTheme.Dark);
            """),
        ["Main.Buttons"] = new("Nút", "ButtonsHeading", false, """
            <Button Content="Chính" />
            <Button Content="Phụ" Style="{StaticResource ButtonSecondary}" />
            <Button Content="Viền" Style="{StaticResource ButtonOutline}" />
            <Button Content="Trong suốt" Style="{StaticResource ButtonGhost}" />
            <Button Content="Nguy hiểm" Style="{StaticResource ButtonDestructive}" />
            <Button Content="Đã tắt" IsEnabled="False" />
            """),
        ["Main.Form"] = new("Điều khiển biểu mẫu", "FormHeading", false, """
            <TextBox Text="name@example.com" />
            <CheckBox Content="Tôi đồng ý với các điều khoản" IsChecked="True" />
            <ToggleButton Style="{StaticResource Switch}" IsChecked="True" />
            """),
        ["Main.Tabs"] = new("Thẻ", "TabsHeading", false, """
            <TabControl>
                <TabItem Header="Tổng quan">
                    <TextBlock Text="Nội dung tổng quan" />
                </TabItem>
                <TabItem Header="Cài đặt">
                    <TextBlock Text="Nội dung cài đặt" />
                </TabItem>
            </TabControl>
            """),
        ["Main.FilePreview"] = new("Xem trước tệp", "FilePreviewHeading", false, """
            <premium:FilePreviewControl x:Name="FilePreview" MinHeight="440" />

            // Code-behind
            FilePreview.LoadFile(filePath);
            """),
        ["Main.Dialog"] = new("Hộp thoại", "DialogHeading", false, """
            <Button Content="Mở hộp thoại" Click="OpenDialog_Click" />

            // Code-behind
            DialogService.ShowDialog(this, "Hộp thoại", content);
            """),
        ["Gallery.TreeView"] = new("Cây điều hướng", "TreeViewSection", true, """
            <TreeView Style="{StaticResource TreeViewStyle}">
                <TreeViewItem Header="Không gian làm việc" IsExpanded="True">
                    <TreeViewItem Header="src">
                        <TreeViewItem Header="Controls" />
                    </TreeViewItem>
                </TreeViewItem>
            </TreeView>
            """),
        ["Gallery.Toolbar"] = new("Thanh công cụ", "ToolbarSection", true, """
            <controls:Toolbar Style="{StaticResource Toolbar}">
                <StackPanel Style="{StaticResource ToolbarGroup}">
                    <Button Content="Sao chép" Style="{StaticResource ToolbarButton}" />
                    <Button Content="Dán" Style="{StaticResource ToolbarIconButton}" />
                    <Button Content="Tiêu đề" Style="{StaticResource ToolbarStyleButton}" />
                </StackPanel>
                <Separator Style="{StaticResource ToolbarSeparator}" />
                <controls:ComboBoxControl Placeholder="Phông chữ"
                                          Style="{StaticResource Combobox}" />
                <controls:SplitButton Content="Sắp xếp" Style="{StaticResource SplitButton}" />
            </controls:Toolbar>
            """),
        ["Gallery.Accordion"] = new("Ngăn xếp / Thu gọn", "AccordionSection", true, """
            <Expander Header="WPF Premium là gì?" Style="{StaticResource Accordion}">
                <TextBlock Text="Nội dung có thể mở rộng." Margin="12" />
            </Expander>
            <Expander Header="Thông tin nâng cao" Style="{StaticResource Collapsible}">
                <TextBlock Text="Thông tin chi tiết." Margin="12" />
            </Expander>
            """),
        ["Gallery.Alert"] = new("Cảnh báo / Trống / Mục", "AlertSection", true, """
            <ContentControl Style="{StaticResource Alert}" Content="Phiên làm việc đang hoạt động." />
            <ContentControl Style="{StaticResource AlertDestructive}" Content="Không thể lưu thay đổi." />
            <ContentControl Style="{StaticResource Empty}" Content="Không có kết quả." />
            <ContentControl Style="{StaticResource Item}" Content="Mục danh sách" />
            """),
        ["Gallery.Form"] = new("Tệp / Trường / Ô nhập", "FormSection", true, """
            <controls:AttachmentControl Style="{StaticResource Attachment}" />
            <controls:Field Label="Tên dự án" IsRequired="True" Style="{StaticResource Field}">
                <TextBox />
            </controls:Field>
            <controls:Field Label="Mô tả" Style="{StaticResource Field}">
                <TextBox Style="{StaticResource Textarea}" AcceptsReturn="True" />
            </controls:Field>
            <controls:Field Label="Điều khoản" Style="{StaticResource Field}">
                <CheckBox Content="Tôi đồng ý với điều khoản" />
            </controls:Field>
            <controls:Field Label="Chế độ" Style="{StaticResource Field}">
                <RadioButton Content="Tiêu chuẩn" GroupName="Plan" />
            </controls:Field>
            <DockPanel Style="{StaticResource InputGroup}">
                <Button Content="Tìm" Style="{StaticResource ButtonSecondary}"
                        DockPanel.Dock="Right" />
                <TextBox Tag="Từ khóa tìm kiếm" />
            </DockPanel>
            <controls:FormattedInputControl Mask="+## (###) ###-####" />
            <controls:MultiSelectControl Placeholder="Chọn công nghệ"
                                         Style="{StaticResource MultiSelect}" />
            """),
        ["Gallery.Calendar"] = new("Lịch / Ngày giờ", "CalendarSection", true, """
            <controls:CalendarControl Style="{StaticResource Calendar}" />
            <Separator Style="{StaticResource Marker}" />
            <controls:DateTimeCalendar Style="{StaticResource DateTimeCalendar}" />
            <controls:DateTimeRangeCalendar Style="{StaticResource DateTimeRangeCalendar}" />
            """),
        ["Gallery.Selectors"] = new("Hộp chọn / Lệnh", "SelectorsSection", true, """
            <controls:ComboBoxControl Placeholder="Chọn nền tảng"
                                      Style="{StaticResource Combobox}" />
            <ListBox Style="{StaticResource Command}">
                <ListBoxItem Content="Tìm dự án" />
                <ListBoxItem Content="Mở cài đặt" />
            </ListBox>
            <StackPanel Style="{StaticResource RadioGroup}">
                <RadioButton Content="Mặc định" GroupName="Plan" />
                <RadioButton Content="Thu gọn" GroupName="Plan" />
            </StackPanel>
            """),
        ["Gallery.Menus"] = new("Trình đơn", "MenusSection", true, """
            <Button Content="Mở trình đơn">
                <Button.ContextMenu>
                    <ContextMenu Style="{StaticResource DropdownMenu}">
                        <MenuItem Header="Chỉnh sửa" />
                        <MenuItem Header="Xóa" />
                    </ContextMenu>
                </Button.ContextMenu>
            </Button>
            <Menu Style="{StaticResource Menubar}">
                <MenuItem Header="_Tệp">
                    <MenuItem Header="_Mới" />
                </MenuItem>
            </Menu>
            """),
        ["Gallery.Buttons"] = new("Nút / Công tắc", "ButtonsSection", true, """
            <StackPanel Style="{StaticResource ButtonGroup}">
                <Button Content="Trái" Style="{StaticResource ButtonOutline}" />
                <Button Content="Phải" Style="{StaticResource ButtonOutline}" />
            </StackPanel>
            <controls:SplitButton Content="Xuất bản" Style="{StaticResource SplitButton}" />
            <ToggleButton Content="Bật" Style="{StaticResource Toggle}" IsChecked="True" />
            <UniformGrid Style="{StaticResource ToggleGroup}" Columns="3">
                <ToggleButton Content="Trái" Style="{StaticResource Toggle}" />
                <ToggleButton Content="Giữa" Style="{StaticResource Toggle}" />
                <ToggleButton Content="Phải" Style="{StaticResource Toggle}" />
            </UniformGrid>
            <CheckBox Content="Hộp kiểm" IsChecked="True" />
            <ToggleButton Style="{StaticResource Switch}" IsChecked="True" />
            """),
        ["Gallery.Pagination"] = new("Phân trang / Tiến trình", "PaginationSection", true, """
            <controls:Pagination PageCount="12" CurrentPage="5"
                                 Style="{StaticResource Pagination}" />
            <Popup Style="{StaticResource Popover}">
                <TextBlock Text="Nội dung cửa sổ bật lên" />
            </Popup>
            <ProgressBar Style="{StaticResource Progress}" Value="68" />
            <Slider Style="{StaticResource Slider}" Value="40" />
            """),
        ["Gallery.Table"] = new("Bảng / Chú giải", "TableSection", true, """
            <controls:Table Style="{StaticResource Table}">
                <controls:Table.Columns>
                    <DataGridTextColumn Header="Tên" Binding="{Binding Name}" />
                    <DataGridTextColumn Header="Trạng thái" Binding="{Binding Status}" />
                </controls:Table.Columns>
            </controls:Table>
            <Button Content="Xem chú giải" ToolTip="Nội dung chú giải" />
            <Border Style="{StaticResource Skeleton}" Height="14" Width="220" />
            """),
        ["Gallery.Badge"] = new("Badge", "BadgeSection", true, """
            <controls:Badge Content="Mới" />
            <controls:Badge Content="Hoàn tất" Variant="Success" />
            <controls:Badge Content="Chờ xử lý" Variant="Warning" />
            <controls:Badge Content="Thất bại" Variant="Destructive" />
            """),
        ["Gallery.Divider"] = new("Divider", "DividerSection", true, """
            <controls:Divider />
            <controls:Divider Orientation="Vertical" />
            """),
        ["Gallery.StepBar"] = new("StepBar", "StepBarSection", true, """
            <controls:StepBar CurrentStep="1">
                <controls:StepBarItem Content="Thông tin" />
                <controls:StepBarItem Content="Cấu hình" />
                <controls:StepBarItem Content="Xác nhận" />
                <controls:StepBarItem Content="Hoàn tất" />
            </controls:StepBar>
            """),
        ["Gallery.WaterfallPanel"] = new("Waterfall Panel", "WaterfallPanelSection", true, """
            <controls:WaterfallPanel ColumnCount="3"
                                     HorizontalSpacing="12"
                                     VerticalSpacing="12">
                <Border Padding="12" Background="{DynamicResource MutedBrush}">
                    <TextBlock Text="Thẻ ngắn" TextWrapping="Wrap" />
                </Border>
                <Border Padding="12" Background="{DynamicResource MutedBrush}">
                    <TextBlock Text="Thẻ cao hơn với nội dung dài hơn."
                               TextWrapping="Wrap" />
                </Border>
                <Border Padding="12" Background="{DynamicResource MutedBrush}">
                    <TextBlock Text="Thẻ vừa" TextWrapping="Wrap" />
                </Border>
            </controls:WaterfallPanel>
            """),
        ["Gallery.Charts"] = new("Biểu đồ", "ChartsSection", true, """
            <controls:ChartControl x:Name="LineChart" Kind="Line"
                                   Title="Doanh thu theo tháng" />
            <controls:ChartControl Kind="Area" Title="Người dùng hoạt động" />
            <controls:ChartControl Kind="Column" Title="Đơn hàng theo tháng" />
            <controls:ChartControl Kind="Bar" Title="Công việc theo nhóm" />
            <controls:ChartControl Kind="Scatter" Title="Độ trễ" />
            <controls:ChartControl Kind="Doughnut" Title="Nguồn truy cập" />

            // Code-behind
            LineChart.Labels = new[] { "Thg 1", "Thg 2", "Thg 3" };
            LineChart.Series = [
                new ChartSeries("Doanh thu", new[] { 12d, 18, 15 })
            ];
            """),
        ["Gallery.Trace"] = new("Trace / Kiểu chữ", "TraceSection", true, """
            <ItemsControl Style="{StaticResource Trace}">
                <controls:TraceEntry Kind="Thought" Title="Đang xử lý"
                                     IsExpanded="True">
                    <controls:TraceEntry.Children>
                        <controls:TraceEntry Kind="Sources" Title="Đọc tài liệu" />
                    </controls:TraceEntry.Children>
                </controls:TraceEntry>
            </ItemsControl>
            <ContentControl Content="Ctrl + K" Style="{StaticResource Kbd}" />
            <TextBlock Text="Tiêu đề" Style="{StaticResource TypographyH2}" />
                <TextBlock Text="Văn bản mô tả phụ" Style="{StaticResource TypographyMuted}" />
            <Label Content="Nhãn" Style="{StaticResource Label}" />
            """),
        ["Gallery.Overlays"] = new("Hộp thoại / Bảng trượt", "OverlaysSection", true, """
            <Button Content="Mở ngăn trượt" Click="OpenPanel_Click" />

            // Code-behind
            DialogService.ShowDialog(owner, "Hộp thoại", content);
            DialogService.ShowAlertDialog(owner, "Bỏ các thay đổi?", content);
            DialogService.ShowDrawer(owner, "Ngăn trượt", content, OverlayDirection.Left);
            DialogService.ShowSheet(owner, "Bảng trượt", content, OverlayDirection.Bottom);
            ToastService.Show(owner, "Đã lưu thay đổi.", ToastKind.Success);
            """),
        ["Gallery.AvalonDock"] = new("AvalonDock", "DockingSection", true, """
            <avalonDock:DockingManager Style="{StaticResource WpfPremiumDockingManager}">
                <avalonDockLayout:LayoutRoot>
                    <avalonDockLayout:LayoutPanel>
                        <avalonDockLayout:LayoutAnchorablePane>
                            <avalonDockLayout:LayoutAnchorable Title="Trình khám phá" />
                        </avalonDockLayout:LayoutAnchorablePane>
                        <avalonDockLayout:LayoutDocumentPane>
                            <avalonDockLayout:LayoutDocument Title="Tài liệu" />
                        </avalonDockLayout:LayoutDocumentPane>
                    </avalonDockLayout:LayoutPanel>
                </avalonDockLayout:LayoutRoot>
            </avalonDock:DockingManager>
            """),
        ["Gallery.OfflineMap"] = new("Bản đồ ngoại tuyến", "OfflineMapSection", true, """
            <controls:OfflineMapControl x:Name="MapView" Height="360" />

            // Code-behind
            MapView.Locations = new[]
            {
                new OfflineMapLocation(21.0285, 105.8542, "Hồ Gươm"),
                new OfflineMapLocation(21.0368, 105.8342, "Lăng Chủ tịch Hồ Chí Minh")
            };
            """)
    };

    public MainWindow()
    {
        InitializeComponent();
        DemoNavigation.SelectedIndex = 0;

        var arguments = Environment.GetCommandLineArgs();
        var previewArgumentIndex = Array.FindIndex(arguments, argument => argument == "--preview");
        if (previewArgumentIndex >= 0 && previewArgumentIndex + 1 < arguments.Length)
        {
            var previewPath = arguments[previewArgumentIndex + 1];
            Loaded += (_, _) =>
            {
                FilePreview.LoadFile(previewPath);
                FilePreview.BringIntoView();
            };
        }
    }

    private void DemoNavigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DemoNavigation.SelectedItem is not ListBoxItem { Tag: string key }) return;
        if (!DemoExamples.TryGetValue(key, out var example))
        {
            throw new InvalidOperationException($"Unknown demo section '{key}'.");
        }

        CodeSampleTitle.Text = $"Ví dụ: {example.Title}";
        CodeSampleText.Text = example.Source;
        if (example.IsGallery)
        {
            ControlsGalleryPreview.NavigateToSection(example.Target);
            return;
        }

        if (FindName(example.Target) is not FrameworkElement target)
        {
            throw new InvalidOperationException($"Demo section '{example.Target}' was not found.");
        }

        target.BringIntoView();
    }

    private void SetTheme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string themeName })
        {
            throw new InvalidOperationException("Theme buttons must specify a theme name.");
        }

        var theme = themeName switch
        {
            "Light" => WpfPremiumTheme.Light,
            "Dark" => WpfPremiumTheme.Dark,
            _ => throw new InvalidOperationException($"Unknown theme '{themeName}'.")
        };
        var application = Application.Current
            ?? throw new InvalidOperationException("The WPF application is not available.");
        ThemeManager.SetTheme(application, theme);
    }

    private void OpenDialog_Click(object sender, RoutedEventArgs e)
    {
        var content = new TextBlock
        {
            Text = "Lớp phủ hộp thoại này được căn giữa cửa sổ cha.",
            TextWrapping = TextWrapping.Wrap
        };
        DialogService.ShowDialog(this, "Modal dialog", content);
    }

    private void ChoosePreviewFile_Click(object sender, RoutedEventArgs e) => FilePreview.Browse();
}
