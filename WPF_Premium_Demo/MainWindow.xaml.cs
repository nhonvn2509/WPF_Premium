using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MahApps.Metro.IconPacks;
using WPF_Premium.Controls;
using WPF_Premium.Themes;

namespace WPF_Premium_Demo;

public partial class MainWindow : PremiumWindow
{
    private sealed record PageInfo(string Key, string Title, string Icon, Type PageType, bool IsGallery);
    private sealed record DemoExample(string Title, string Source);

    private static readonly IReadOnlyDictionary<string, PageInfo> PageRegistry = new Dictionary<string, PageInfo>
    {
        ["Page.Appearance"] = new("Page.Appearance", "Giao diện", "ThemeLightDark", typeof(AppearancePage), false),
        ["Page.Buttons"] = new("Page.Buttons", "Nút", "GestureTapButton", typeof(ButtonsPage), false),
        ["Page.Forms"] = new("Page.Forms", "Biểu mẫu", "FormSelect", typeof(FormsPage), false),
        ["Page.Tabs"] = new("Page.Tabs", "Thẻ", "Tab", typeof(TabsPage), false),
        ["Page.FilePreview"] = new("Page.FilePreview", "Xem trước tệp", "FileEyeOutline", typeof(FilePreviewPage), false),
        ["Page.Dialogs"] = new("Page.Dialogs", "Hộp thoại", "MessageTextOutline", typeof(DialogsPage), false),

        ["Page.TreeView"] = new("Page.TreeView", "Cây điều hướng", "TreeOutline", typeof(TreeViewPage), true),
        ["Page.Toolbar"] = new("Page.Toolbar", "Thanh công cụ", "ToolbarOutline", typeof(ToolbarPage), true),
        ["Page.Accordion"] = new("Page.Accordion", "Ngăn xếp / Thu gọn", "CollapseAllOutline", typeof(AccordionPage), true),
        ["Page.Alert"] = new("Page.Alert", "Cảnh báo / Trống / Mục", "AlertOutline", typeof(AlertPage), true),
        ["Page.FormControls"] = new("Page.FormControls", "Tệp / Trường / Ô nhập", "FormTextboxOutline", typeof(FormControlsPage), true),
        ["Page.Calendar"] = new("Page.Calendar", "Lịch / Ngày giờ", "CalendarMonthOutline", typeof(CalendarPage), true),
        ["Page.Selectors"] = new("Page.Selectors", "Hộp chọn / Lệnh", "CheckboxMultipleOutline", typeof(SelectorsPage), true),
        ["Page.Menus"] = new("Page.Menus", "Trình đơn", "MenuOutline", typeof(MenusPage), true),
        ["Page.ButtonsGallery"] = new("Page.ButtonsGallery", "Nút / Công tắc", "GestureTapButton", typeof(ButtonsGalleryPage), true),
        ["Page.Pagination"] = new("Page.Pagination", "Phân trang / Tiến trình", "Pagination", typeof(PaginationPage), true),
        ["Page.Table"] = new("Page.Table", "Bảng / Chú giải", "TableOutline", typeof(TablePage), true),
        ["Page.Badge"] = new("Page.Badge", "Badge", "BadgeOutline", typeof(BadgePage), true),
        ["Page.Divider"] = new("Page.Divider", "Divider", "MinusBoxOutline", typeof(DividerPage), true),
        ["Page.StepBar"] = new("Page.StepBar", "StepBar", "ProgressHelper", typeof(StepBarPage), true),
        ["Page.Waterfall"] = new("Page.Waterfall", "Waterfall Panel", "ViewColumnOutline", typeof(WaterfallPage), true),
        ["Page.Charts"] = new("Page.Charts", "Biểu đồ", "ChartLine", typeof(ChartsPage), true),
        ["Page.Trace"] = new("Page.Trace", "Trace / Kiểu chữ", "TimelineOutline", typeof(TracePage), true),
        ["Page.Overlays"] = new("Page.Overlays", "Hộp thoại / Bảng trượt", "WindowOpen", typeof(OverlaysPage), true),
        ["Page.Docking"] = new("Page.Docking", "AvalonDock", "WindowRestore", typeof(DockingPage), true),
        ["Page.OfflineMap"] = new("Page.OfflineMap", "Bản đồ ngoại tuyến", "MapOutline", typeof(OfflineMapPage), true),
    };

    private static readonly IReadOnlyDictionary<string, DemoExample> DemoExamples = new Dictionary<string, DemoExample>
    {
        ["Page.Appearance"] = new("Giao diện", """
            <premium:AppearanceSettingsControl />
            <Button Content="Sáng" Tag="Light" Click="SetTheme_Click" />
            <Button Content="Tối" Tag="Dark" Click="SetTheme_Click" />

            // Code-behind
            ThemeManager.SetTheme(Application.Current!, WpfPremiumTheme.Dark);
            """),
        ["Page.Buttons"] = new("Nút", """
            <Button Content="Chính" />
            <Button Content="Phụ" Style="{StaticResource ButtonSecondary}" />
            <Button Content="Viền" Style="{StaticResource ButtonOutline}" />
            <Button Content="Trong suốt" Style="{StaticResource ButtonGhost}" />
            <Button Content="Nguy hiểm" Style="{StaticResource ButtonDestructive}" />
            <Button Content="Đã tắt" IsEnabled="False" />
            """),
        ["Page.Forms"] = new("Biểu mẫu", """
            <TextBox Text="name@example.com" />
            <CheckBox Content="Tôi đồng ý với các điều khoản" IsChecked="True" />
            <ToggleButton Style="{StaticResource Switch}" IsChecked="True" />
            """),
        ["Page.Tabs"] = new("Thẻ", """
            <TabControl>
                <TabItem Header="Tổng quan">
                    <TextBlock Text="Nội dung tổng quan" />
                </TabItem>
                <TabItem Header="Cài đặt">
                    <TextBlock Text="Nội dung cài đặt" />
                </TabItem>
            </TabControl>
            """),
        ["Page.FilePreview"] = new("Xem trước tệp", """
            <premium:FilePreviewControl x:Name="FilePreview" MinHeight="440" />

            // Code-behind
            FilePreview.LoadFile(filePath);
            """),
        ["Page.Dialogs"] = new("Hộp thoại", """
            <Button Content="Mở hộp thoại" Click="OpenDialog_Click" />

            // Code-behind
            DialogService.ShowDialog(this, "Hộp thoại", content);
            """),
        ["Page.TreeView"] = new("Cây điều hướng", """
            <TreeView Style="{StaticResource TreeViewStyle}">
                <TreeViewItem Header="Không gian làm việc" IsExpanded="True">
                    <TreeViewItem Header="src">
                        <TreeViewItem Header="Controls" />
                    </TreeViewItem>
                </TreeViewItem>
            </TreeView>
            """),
        ["Page.Toolbar"] = new("Thanh công cụ", """
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
        ["Page.Accordion"] = new("Ngăn xếp / Thu gọn", """
            <Expander Header="WPF Premium là gì?" Style="{StaticResource Accordion}">
                <TextBlock Text="Nội dung có thể mở rộng." Margin="12" />
            </Expander>
            <Expander Header="Thông tin nâng cao" Style="{StaticResource Collapsible}">
                <TextBlock Text="Thông tin chi tiết." Margin="12" />
            </Expander>
            """),
        ["Page.Alert"] = new("Cảnh báo / Trống / Mục", """
            <ContentControl Style="{StaticResource Alert}" Content="Phiên làm việc đang hoạt động." />
            <ContentControl Style="{StaticResource AlertDestructive}" Content="Không thể lưu thay đổi." />
            <ContentControl Style="{StaticResource Empty}" Content="Không có kết quả." />
            <ContentControl Style="{StaticResource Item}" Content="Mục danh sách" />
            """),
        ["Page.FormControls"] = new("Tệp / Trường / Ô nhập", """
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
        ["Page.Calendar"] = new("Lịch / Ngày giờ", """
            <controls:CalendarControl Style="{StaticResource Calendar}" />
            <Separator Style="{StaticResource Marker}" />
            <controls:DateTimeCalendar Style="{StaticResource DateTimeCalendar}" />
            <controls:DateTimeRangeCalendar Style="{StaticResource DateTimeRangeCalendar}" />
            """),
        ["Page.Selectors"] = new("Hộp chọn / Lệnh", """
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
        ["Page.Menus"] = new("Trình đơn", """
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
        ["Page.ButtonsGallery"] = new("Nút / Công tắc", """
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
        ["Page.Pagination"] = new("Phân trang / Tiến trình", """
            <controls:Pagination PageCount="12" CurrentPage="5"
                                 Style="{StaticResource Pagination}" />
            <Popup Style="{StaticResource Popover}">
                <TextBlock Text="Nội dung cửa sổ bật lên" />
            </Popup>
            <ProgressBar Style="{StaticResource Progress}" Value="68" />
            <Slider Style="{StaticResource Slider}" Value="40" />
            """),
        ["Page.Table"] = new("Bảng / Chú giải", """
            <controls:Table Style="{StaticResource Table}">
                <controls:Table.Columns>
                    <DataGridTextColumn Header="Tên" Binding="{Binding Name}" />
                    <DataGridTextColumn Header="Trạng thái" Binding="{Binding Status}" />
                </controls:Table.Columns>
            </controls:Table>
            <Button Content="Xem chú giải" ToolTip="Nội dung chú giải" />
            <Border Style="{StaticResource Skeleton}" Height="14" Width="220" />
            """),
        ["Page.Badge"] = new("Badge", """
            <controls:Badge Content="Mới" />
            <controls:Badge Content="Hoàn tất" Variant="Success" />
            <controls:Badge Content="Chờ xử lý" Variant="Warning" />
            <controls:Badge Content="Thất bại" Variant="Destructive" />
            """),
        ["Page.Divider"] = new("Divider", """
            <controls:Divider />
            <controls:Divider Orientation="Vertical" />
            """),
        ["Page.StepBar"] = new("StepBar", """
            <controls:StepBar CurrentStep="1">
                <controls:StepBarItem Content="Thông tin" />
                <controls:StepBarItem Content="Cấu hình" />
                <controls:StepBarItem Content="Xác nhận" />
                <controls:StepBarItem Content="Hoàn tất" />
            </controls:StepBar>
            """),
        ["Page.Waterfall"] = new("Waterfall Panel", """
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
        ["Page.Charts"] = new("Biểu đồ", """
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
        ["Page.Trace"] = new("Trace / Kiểu chữ", """
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
        ["Page.Overlays"] = new("Hộp thoại / Bảng trượt", """
            <Button Content="Mở ngăn trượt" Click="OpenPanel_Click" />

            // Code-behind
            DialogService.ShowDialog(owner, "Hộp thoại", content);
            DialogService.ShowAlertDialog(owner, "Bỏ các thay đổi?", content);
            DialogService.ShowDrawer(owner, "Ngăn trượt", content, OverlayDirection.Left);
            DialogService.ShowSheet(owner, "Bảng trượt", content, OverlayDirection.Bottom);
            ToastService.Show(owner, "Đã lưu thay đổi.", ToastKind.Success);
            """),
        ["Page.Docking"] = new("AvalonDock", """
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
        ["Page.OfflineMap"] = new("Bản đồ ngoại tuyến", """
            <controls:OfflineMapControl x:Name="MapView" Height="360" />

            // Code-behind
            MapView.Locations = new[]
            {
                new OfflineMapLocation(21.0285, 105.8542, "Hồ Gươm"),
                new OfflineMapLocation(21.0368, 105.8342, "Lăng Chủ tịch Hồ Chí Minh")
            };
            """),
    };

    private bool _isSidebarCollapsed;
    private string _currentPageKey = "Page.Appearance";
    private Dictionary<TreeViewItem, string> _navItemVisibility = new();

    public MainWindow()
    {
        InitializeComponent();
        NavTree.SelectedItemChanged += NavTree_SelectedItemChanged;
        SearchBox.TextChanged += SearchBox_TextChanged;
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Build navigation item visibility map
        BuildNavItemVisibilityMap(NavTree.Items);
        
        // Select first item by default
        NavAppearance.IsSelected = true;
        OpenPage("Page.Appearance");
    }

    private void BuildNavItemVisibilityMap(ItemCollection items)
    {
        foreach (var item in items)
        {
            if (item is TreeViewItem tvi)
            {
                if (tvi.Tag is string pageKey)
                {
                    _navItemVisibility[tvi] = pageKey;
                }
                BuildNavItemVisibilityMap(tvi.Items);
            }
        }
    }

    private void NavTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not TreeViewItem { Tag: string pageKey }) return;
        OpenPage(pageKey);
    }

    private void OpenPage(string pageKey)
    {
        if (!PageRegistry.TryGetValue(pageKey, out var pageInfo))
        {
            return;
        }

        _currentPageKey = pageKey;
        var content = CreatePageContent(pageInfo);
        PageContent.Content = content;

        ContentHeaderTitle.Text = pageInfo.Title;
        PageTitle.Text = pageInfo.Title;
        UpdateRightPanel(pageKey);
    }

    private UIElement CreatePageContent(PageInfo pageInfo)
    {
        if (pageInfo.IsGallery)
        {
            var gallery = new ControlsGallery();
            return new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = gallery
            };
        }

        return Activator.CreateInstance(pageInfo.PageType) as UIElement
            ?? new TextBlock { Text = "Không thể tạo trang", Foreground = (Brush)FindResource("DestructiveBrush") };
    }

    private void UpdateRightPanel(string pageKey)
    {
        if (RightPanelContent == null) return;
        RightPanelContent.Children.Clear();

        if (DemoExamples.TryGetValue(pageKey, out var example))
        {
            RightPanelContent.Children.Add(new TextBlock
            {
                Text = $"Ví dụ: {example.Title}",
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 8)
            });
            RightPanelContent.Children.Add(new TextBox
            {
                Text = example.Source,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 11,
                Padding = new Thickness(8),
                MinHeight = 200,
                Background = (Brush)FindResource("BackgroundBrush"),
                BorderBrush = (Brush)FindResource("BorderBrush")
            });
        }
        else
        {
            RightPanelContent.Children.Add(new TextBlock
            {
                Text = "Mã ví dụ",
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 8)
            });
            RightPanelContent.Children.Add(new TextBlock
            {
                Text = "Không có mã ví dụ cho trang này.",
                FontSize = 12,
                Foreground = (Brush)FindResource("MutedForegroundBrush"),
                TextWrapping = TextWrapping.Wrap
            });
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (SearchBox == null) return;
        var searchText = SearchBox.Text?.ToLower() ?? "";
        
        if (string.IsNullOrWhiteSpace(searchText))
        {
            // Show all items
            foreach (var kvp in _navItemVisibility)
            {
                kvp.Key.Visibility = Visibility.Visible;
            }
        }
        else
        {
            foreach (var kvp in _navItemVisibility)
            {
                var pageKey = kvp.Value;
                if (PageRegistry.TryGetValue(pageKey, out var pageInfo))
                {
                    var title = pageInfo.Title.ToLower();
                    var key = pageKey.ToLower();
                    var matches = title.Contains(searchText) || key.Contains(searchText);
                    kvp.Key.Visibility = matches ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }
    }

    private void SyncNavigationSelection(string pageKey)
    {
        var item = FindNavTreeItem(NavTree.Items, pageKey);
        if (item != null)
        {
            item.IsSelected = true;
            item.BringIntoView();
        }
    }

    private TreeViewItem? FindNavTreeItem(ItemCollection items, string pageKey)
    {
        foreach (var item in items)
        {
            if (item is TreeViewItem tvi)
            {
                if (tvi.Tag as string == pageKey)
                    return tvi;

                var found = FindNavTreeItem(tvi.Items, pageKey);
                if (found != null)
                    return found;
            }
        }
        return null;
    }

    private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
    {
        _isSidebarCollapsed = !_isSidebarCollapsed;
        SidebarColumn.Width = _isSidebarCollapsed ? new GridLength(60) : new GridLength(280);

        var icon = ToggleSidebarButton.Content as PackIconMaterial;
        if (icon != null)
        {
            icon.Kind = _isSidebarCollapsed ? PackIconMaterialKind.ChevronRight : PackIconMaterialKind.ChevronLeft;
        }
        ToggleSidebarButton.ToolTip = _isSidebarCollapsed ? "Mở rộng thanh bên" : "Thu gọn thanh bên";
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
        DialogService.ShowDialog(this, "Hộp thoại phương thức", content);
    }

    private void ShowCodeButton_Click(object sender, RoutedEventArgs e)
    {
        if (RightPanelColumn.Width.Value > 0)
        {
            CloseRightPanel_Click(sender, e);
        }
        else
        {
            OpenRightPanel_Click(sender, e);
        }
    }

    private void OpenRightPanel_Click(object sender, RoutedEventArgs e)
    {
        RightPanelColumn.Width = new GridLength(320);
        RightPanel.Visibility = Visibility.Visible;
        UpdateRightPanel(_currentPageKey);
    }

    private void CloseRightPanel_Click(object sender, RoutedEventArgs e)
    {
        RightPanel.Visibility = Visibility.Collapsed;
        RightPanelColumn.Width = new GridLength(0);
    }
}