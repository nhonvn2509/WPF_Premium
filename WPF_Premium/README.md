# WPF Premium

WPF Premium là thư viện điều khiển giao diện cho WPF trên .NET 10, gồm các kiểu dùng lại được và điều khiển tùy chỉnh cho lịch, lựa chọn có tìm kiếm, biểu đồ, bảng, bố cục docking và hộp thoại.
Thư viện dùng biểu tượng Material từ `MahApps.Metro.IconPacks`; gói phụ thuộc này được khai báo sẵn. AvalonDock được cung cấp bởi `Dirkster.AvalonDock` và có giao diện WPF Premium đi kèm.

Cài gói NuGet `WPF_Premium` (hoặc thêm tham chiếu dự án), sau đó gộp từ điển giao diện một lần trong ứng dụng sử dụng thư viện:

```powershell
dotnet add package WPF_Premium --version 1.0.0
```

## Giao diện và tùy biến

`WpfPremium.xaml` là giao diện sáng mặc định; `WpfPremiumDark.xaml` cung cấp bảng màu tối. Cả hai dùng các tài nguyên động, nên khi đổi giao diện thì những điều khiển đang hiển thị cũng được cập nhật. Dùng `ThemeManager` để đổi giao diện lúc chạy:

```csharp
using WPF_Premium.Themes;

ThemeManager.SetTheme(Application.Current!, WpfPremiumTheme.Dark);
ThemeManager.SetTheme(Application.Current!, WpfPremiumTheme.Light);
```

Để cố định giao diện, gộp từ điển mong muốn một lần trong `App.xaml`:

```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="/WPF_Premium;component/Themes/WpfPremiumDark.xaml" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

Thay bằng `WpfPremium.xaml` để bắt đầu với giao diện sáng. Ứng dụng minh họa khởi chạy ở chế độ sáng và có nút đổi giao diện trên thanh đầu trang.

Có thể tùy biến màu bằng cách khai báo lại các khóa tài nguyên động như `BackgroundBrush`, `ForegroundBrush`, `PrimaryBrush`, `BorderBrush` và `FocusBrush` trong từ điển tài nguyên của ứng dụng. Khai báo từ điển tùy biến sau từ điển WPF Premium để giá trị của ứng dụng được ưu tiên.

Default styles apply to `Button`, `TextBox`, `CheckBox`, `TabControl`, `TabItem`, `Label`, `ContextMenu`, `Menu`, `ProgressBar`, and `ToolTip`. Calendar, combobox, and table use WPF Premium controls rather than the stock WPF visuals.

### Cài đặt kiểu chữ và kích thước

Thêm `AppearanceSettingsControl` vào thanh công cụ hoặc trang cài đặt. Dialog cho phép đổi phông chữ, cỡ chữ từ 9 đến 32 pt và kích thước thành phần `sm`, `md`, `lg`; các kiểu dùng tài nguyên động sẽ cập nhật ngay sau khi áp dụng.

```xml
<controls:AppearanceSettingsControl />
```

Ứng dụng có thể áp dụng cấu hình từ mã và đọc lại cấu hình đang dùng:

```csharp
AppearanceSettingsManager.Apply(
    Application.Current!,
    new AppearanceSettings("Segoe UI", 15, ComponentSize.Large));

var current = AppearanceSettingsManager.GetCurrent(Application.Current!);
```

Các ứng dụng dùng control cần tham chiếu thư viện và gộp `WpfPremium.xaml` hoặc `WpfPremiumDark.xaml` như phần trên. `AppearanceSettingsManager.SettingsChanged` phát thông báo sau mỗi lần áp dụng cấu hình.

## Danh mục thành phần

| Component | WPF type / resource |
| --- | --- |
| Accordion, Collapsible | `Expander`, styles `Accordion`, `Collapsible` |
| Appearance settings | `controls:AppearanceSettingsControl`; runtime API `AppearanceSettingsManager` với `ComponentSize.Small`, `Medium`, `Large` |
| Alert | `ContentControl`, styles `Alert`, `AlertDestructive` |
| Alert dialog, Dialog | `DialogService.ShowAlertDialog` and `DialogService.ShowDialog` |
| AvalonDock | AvalonDock `DockingManager`, style `WpfPremiumDockingManager` and theme resource `WpfPremiumDockingTheme` |
| Attachment | `controls:AttachmentControl`, style `Attachment` |
| Badge | `controls:Badge`, style `Badge`; variants `Primary`, `Secondary`, `Success`, `Warning`, `Destructive`, and `Outline` |
| Button | `Button` default style; keys `Button`, `ButtonPrimary`, `ButtonSecondary`, `ButtonOutline`, `ButtonGhost`, `ButtonDestructive`, `ButtonLink` |
| Button group | `StackPanel`, style `ButtonGroup` |
| Split button | `controls:SplitButton`, style `SplitButton`; supports `Command`, `CommandParameter`, `Click`, `ContextMenu`, and a configurable `ButtonStyle` |
| Toolbar | `controls:Toolbar`, style `Toolbar`; arrange reusable controls in horizontal `ToolbarGroup`s separated by `ToolbarSeparator` |
| Calendar | `controls:CalendarControl`, style `Calendar` |
| Charts | `controls:ChartControl`, backed by LiveCharts2 |
| DateTimeCalendar | `controls:DateTimeCalendar` |
| DateTimeRangeCalendar | `controls:DateTimeRangeCalendar`; `DatTimeRangeCalendar` is also accepted as a style-key alias |
| Checkbox | `CheckBox` default style; key `Checkbox` |
| Combobox | `controls:ComboBoxControl`, style `Combobox` |
| Multi-select | `controls:MultiSelectControl`, style `MultiSelect`; supports search, display templates, and bindable `SelectedItems` |
| Command | `ListBox`, style `Command` |
| Context menu, Dropdown menu | `ContextMenu`, styles `ContextMenu`, `DropdownMenu` |
| Divider | `controls:Divider`, style `Divider`; supports horizontal and vertical orientation |
| Drawer, Sheet | `DialogService.ShowDrawer` and `DialogService.ShowSheet` |
| Empty | `ContentControl`, style `Empty` |
| Field validation | `controls:Field` wraps any input or input group and displays invalid state with a red frame and legend |
| File preview | `controls:FilePreviewControl`; embeds PDF viewing and visual previews for Office/OpenDocument files |
| Input | `TextBox` default style; key `Input` |
| Input group | `DockPanel`, style `InputGroup` |
| Formatted input | `controls:FormattedInputControl` with a mask and placeholder |
| Item | `ContentControl`, style `Item` |
| Kbd | `ContentControl`, style `Kbd` |
| Label | `Label`, style `Label` |
| Marker | `Separator`, style `Marker` |
| Menubar | `Menu`, style `Menubar` |
| Pagination | `controls:Pagination` with two-way `CurrentPage`, `PageCount`, and `PageChanged` |
| Popover | `Popup`, style `Popover` |
| Progress | `ProgressBar`, style `Progress` |
| Radio group | `StackPanel`, style `RadioGroup` |
| Skeleton | `Border`, style `Skeleton` |
| Slider | `Slider`, style `Slider` |
| StepBar | `controls:StepBar`; set `CurrentStep` to a zero-based step index |
| Switch | `ToggleButton`, style `Switch` |
| Table | `controls:Table`, style `Table` |
| Tabs | `TabControl`, style `Tabs` |
| Textarea | `TextBox`, style `Textarea` |
| Toast | `ToastService` and the `Toast` border style |
| Tree view | `TreeView` and `TreeViewItem`, styles `TreeViewStyle` and `TreeViewItemStyle`; expandable rows use Material icons, and headers can include `PackIconMaterial` icons |
| Toggle, Toggle group | `ToggleButton` style `Toggle`; `UniformGrid` style `ToggleGroup` |
| Tooltip | `ToolTip` default style; style key `Tooltip` |
| Trace | `ItemsControl`, style `Trace`, with `TraceEntry` items |
| Typography | `TextBlock` styles `Typography`, `TypographyH1`, `TypographyH2`, `TypographyH3`, `TypographyMuted` |
| Waterfall panel | `controls:WaterfallPanel`; set `ColumnCount`, `HorizontalSpacing`, and `VerticalSpacing` |

Other components compose WPF primitives with the library's shared compact styles and resource tokens.

## Điều khiển tùy chỉnh

Khai báo không gian tên thư viện trong XAML:

```xml
xmlns:controls="clr-namespace:WPF_Premium.Controls;assembly=WPF_Premium"
```

Dùng trực tiếp bộ biểu tượng Material trong XAML:

```xml
xmlns:iconPacks="http://metro.mahapps.com/winfx/xaml/iconpacks"
<iconPacks:PackIconMaterial Kind="Calendar" />
```

Sử dụng AvalonDock cùng giao diện WPF Premium có sẵn:

```xml
xmlns:avalonDock="clr-namespace:AvalonDock;assembly=AvalonDock"
<avalonDock:DockingManager Style="{StaticResource WpfPremiumDockingManager}">
    <!-- Khai báo LayoutRoot, các ngăn và tài liệu tại đây. -->
</avalonDock:DockingManager>
```

`Themes/WpfPremiumAvalonDock.xaml` được gộp bởi `Themes/WpfPremium.xaml`. Style `WpfPremiumDockingManager` áp dụng theme và bảng màu WPF Premium, đồng thời thay menu ngữ cảnh mặc định bằng menu có thể bản địa hóa. Thêm từ điển ngôn ngữ vào tài nguyên ứng dụng sau giao diện WPF Premium:

```xml
<ResourceDictionary Source="/WPF_Premium;component/Resources/AvalonDock.vi.xaml" />
```

`Resources/AvalonDock.vi.xaml` cung cấp các khóa `AvalonDock.*`; menu ngữ cảnh của tài liệu và các ngăn có thể ghim dùng các khóa này qua tài nguyên động. Các menu dùng style `DropdownMenu` để đồng bộ màu sắc, viền và trạng thái rê chuột với giao diện dự án.

`CalendarControl` tự vẽ lịch tháng và hỗ trợ chọn ngày đơn hoặc khoảng ngày qua các thuộc tính `SelectionMode`, `SelectedDate`, `RangeStartDate` và `RangeEndDate`. `DateTimeCalendar` là trường chọn ngày giờ gọn, mở lịch và bộ chọn giờ tùy chỉnh, đồng thời cung cấp thuộc tính `SelectedDateTime` hỗ trợ liên kết hai chiều. `DateTimeRangeCalendar` mở bộ chọn khoảng ngày hai tháng, có mốc chọn nhanh, giờ bắt đầu/kết thúc và thao tác Áp dụng/Hủy/Xóa. Hai thuộc tính `StartDateTime` và `EndDateTime` được cập nhật khi nhấn Áp dụng.

`ComboBoxControl` là hộp chọn dạng bật lên có tìm kiếm, với các thuộc tính `ItemsSource`, `SelectedItem`, `DisplayMemberPath`, `ItemTemplate`, `Placeholder` và `SearchEnabled`. Điều khiển hỗ trợ danh sách có thể quan sát và liên kết lựa chọn hai chiều.

`MultiSelectControl` dùng danh sách bật lên có tìm kiếm và cho phép chọn nhiều mục mà không đóng danh sách. Liên kết `SelectedItems` với `IList` có thể thay đổi như `ObservableCollection<T>`; phần tóm tắt hiển thị tên mục hoặc số mục đã chọn.

```xml
<controls:MultiSelectControl ItemsSource="{Binding FrameworkOptions}"
                             SelectedItems="{Binding SelectedFrameworks, Mode=TwoWay}"
                             DisplayMemberPath="Name"
                             Placeholder="Select frameworks"
                             Style="{StaticResource MultiSelect}" />
```

`SplitButton` runs its main action through `Command` or `Click` and opens its `ContextMenu` from the arrow. Use `ButtonStyle` to choose an existing button variant.

```xml
<controls:SplitButton Content="Publish"
                      Style="{StaticResource SplitButton}"
                      ButtonStyle="{StaticResource ButtonOutline}"
                      Command="{Binding PublishCommand}">
    <controls:SplitButton.ContextMenu>
        <ContextMenu Style="{StaticResource DropdownMenu}"
                     DataContext="{Binding PlacementTarget.DataContext, RelativeSource={RelativeSource Self}}">
            <MenuItem Header="Publish and notify" Command="{Binding PublishAndNotifyCommand}" />
            <MenuItem Header="Schedule publish" Command="{Binding SchedulePublishCommand}" />
        </ContextMenu>
    </controls:SplitButton.ContextMenu>
</controls:SplitButton>
```

`Toolbar` is a horizontally scrollable container for composing buttons, selectors, split buttons, and other controls into grouped tool sections.

```xml
<controls:Toolbar Style="{StaticResource Toolbar}">
    <StackPanel Style="{StaticResource ToolbarGroup}">
        <Button Content="Bold" Style="{StaticResource ToolbarIconButton}" />
        <Button Content="Italic" Style="{StaticResource ToolbarIconButton}" />
    </StackPanel>
    <Separator Style="{StaticResource ToolbarSeparator}" />
    <StackPanel Style="{StaticResource ToolbarGroup}">
        <Button Content="Find" Style="{StaticResource ToolbarButton}" />
    </StackPanel>
</controls:Toolbar>
```

`Table` derives from `DataGrid` for standard column binding and selection behavior, with library-owned compact row, header, and cell styles. Declare columns explicitly using `controls:Table.Columns`.

`ChartControl` supports line, area, column, horizontal bar, stacked column/bar, step line, scatter, pie, and doughnut charts. Set `Kind`, `Series`, and optional category `Labels`; pie and doughnut data uses one non-negative value per named series. Observable series and label collections refresh the chart, and LiveCharts observes mutable value collections for Cartesian series. Chart colors refresh when `ThemeManager` switches between light and dark.

```xml
<controls:ChartControl Kind="Line"
                       Title="Monthly revenue"
                       Labels="{Binding Months}"
                       Series="{Binding RevenueSeries}"
                       Height="300" />
```

`ChartSeries` is constructed from a display name and an `IReadOnlyList<double>`. Scatter series optionally accept a matching X-value list; if omitted, point indexes are used. The theme provides five chart colors (`ChartColor1Brush` through `ChartColor5Brush`) and uses the active theme's foreground, border, and surface colors for axes, legends, and tooltips. LiveCharts2 requires Windows 10 version 2004 (build 19041) or later.

`DialogService.ShowDialog`, `ShowAlertDialog`, `ShowDrawer`, and `ShowSheet` create owner-modal overlays with close and Escape handling. Drawer and sheet edges can be selected with `OverlayDirection.Left`, `Top`, `Right`, or `Bottom`; the defaults are right for a drawer and bottom for a sheet. The alert dialog returns `true` for confirmation and `false` for cancellation; other overlays return `null` when closed.

```csharp
DialogService.ShowDrawer(owner, "Details", content, OverlayDirection.Left);
DialogService.ShowSheet(owner, "Filters", content, OverlayDirection.Top);
```

`Field` is a `ContentControl` that can wrap any input or group of inputs, including text, numeric text, checkbox, radio, combo, date/time, and attachment controls. Its `Error` property can be set directly or bound to a view-model error; a non-empty error displays in the frame legend and turns the frame red. `IsRequired` only marks the field visually; validation rules remain owned by the application.

```xml
<controls:Field Label="Project name"
                IsRequired="True"
                Error="{Binding ProjectNameError}">
    <TextBox Text="{Binding ProjectName, UpdateSourceTrigger=PropertyChanged}" />
</controls:Field>
```

WPF binding validation errors can also drive the field automatically. Enable `NotifyOnValidationError` on the child bindings so the field can observe bubbled validation events:

```xml
<controls:Field Label="Retry count" IsRequired="True">
    <TextBox Text="{Binding RetryCount,
                            ValidatesOnDataErrors=True,
                            NotifyOnValidationError=True}" />
</controls:Field>
```

Cùng thành phần `Field` có thể bao quanh nhóm hộp kiểm, nhóm nút chọn hoặc nhiều điều khiển. Khi có lỗi liên kết, lỗi đầu tiên được hiển thị; thuộc tính `Description` dùng làm dòng hướng dẫn khi trường hợp lệ.

`Pagination` keeps `CurrentPage` within `1..PageCount`, provides previous/next and a bounded set of page buttons, and raises the bubbling `PageChanged` routed event. Bind `CurrentPage` two-way for MVVM use.

`StepBar.CurrentStep` is zero-based; completed, current, and upcoming steps receive distinct theme-aware markers. `WaterfallPanel` assigns each visible child to the currently shortest column and lays out children without requiring fixed heights.

`AttachmentControl.Files` is an `ObservableCollection<string>` of selected local file paths. The control opens the system file picker and supports removing a selected path; it does not copy, upload, or delete files.

`FilePreviewControl` opens PDF files in the embedded WebView2 PDF viewer and renders DOC/DOCX, XLS/XLSX, PPT/PPTX, and ODT/ODS/ODP locally as pagelike documents, spreadsheet tables, or slides. Preview generation does not upload documents or require a network connection. WebView2 Runtime must be installed; Office layout is an approximation and advanced features such as spreadsheet recalculation, animations, and some embedded objects may differ from Microsoft Office or WPS.

`ToastService.Show(owner, message, kind, duration)` displays a non-modal toast and returns its `Window`. `kind` can be `ToastKind.Default`, `ToastKind.Success`, or `ToastKind.Destructive`; duration must be positive.

`Trace` renders a compact, expandable activity timeline for in-progress AI work. `TraceEntryKind` covers thought, plan, queue, reasoning, sources, stack trace, subagent, task, terminal, test results, tool, chain-of-thought, and error patterns. Entries can include duration, active state, added/removed line counts, command/output blocks, child steps, linked sources, status, details, and optional navigation/action commands:

```xml
<ItemsControl Style="{StaticResource Trace}" ItemsSource="{Binding TraceItems}">
    <controls:TraceEntry Kind="Tool"
                         Title="Ran pnpm --version"
                         Icon="WrenchOutline"
                         Command="pnpm --version"
                         Output="9.12.0"
                         IsExpanded="True" />
</ItemsControl>
```

## Theme tokens

Colors are exposed as dynamic resources, including `BackgroundBrush`, `ForegroundBrush`, `CardBrush`, `MutedBrush`, `MutedForegroundBrush`, `BorderBrush`, `PrimaryBrush`, `SecondaryBrush`, `DestructiveBrush`, `SuccessBrush`, `WarningBrush`, `TraceActiveBrush`, and `ChartColor1Brush` through `ChartColor5Brush`. Override a token in application resources to customize the theme.

## Bản đồ ngoại tuyến

`OfflineMapControl` có sẵn bản đồ thế giới ngoại tuyến từ dữ liệu địa lý tích hợp; không cần cung cấp tệp bản đồ hoặc kết nối mạng. Ứng dụng chỉ cần truyền danh sách vị trí WGS84 (vĩ độ, kinh độ):

```xml
<controls:OfflineMapControl x:Name="MapView" Height="420" />
```

```csharp
MapView.Locations = new[]
{
    new OfflineMapLocation(21.0285, 105.8542, "Hồ Gươm", "ho-guom"),
    new OfflineMapLocation(21.0368, 105.8342, "Lăng Chủ tịch Hồ Chí Minh")
};
```

Có thể gán một `ObservableCollection<OfflineMapLocation>` để tự cập nhật các điểm khi danh sách thay đổi. Tọa độ sai phạm vi được bỏ qua. Điều khiển có nút phóng to, thu nhỏ và đưa tất cả vị trí vào khung nhìn. Nếu cần, `MapFilePath` có thể trỏ đến MBTiles cục bộ; cách hiển thị nhãn/biểu tượng đã được kết xuất sẵn trong MBTiles sẽ giữ nguyên.

Nhãn trên bản đồ tích hợp được phân cấp theo mức thu phóng: bản đồ tổng quan ưu tiên địa danh lớn, còn tên tỉnh, thành phố, đảo và địa hình biển lần lượt xuất hiện khi phóng gần để giảm chồng lấp.

Bản đồ tích hợp dùng dữ liệu [Natural Earth 1:10m](https://www.naturalearthdata.com/downloads/), gồm đường bờ, quốc gia, hơn 4.500 vùng hành chính cấp 1, địa danh, đảo nhỏ, các vùng biển, rạn san hô/đảo san hô và các dải địa hình đáy biển từ 0 đến 10.000 m. Khi phóng to, đường bờ đảo nhỏ được vẽ chi tiết hơn và có thêm nhãn đảo, đá, rạn, bãi cạn/bãi ngầm từ [GeoNames](https://www.geonames.org/), theo giấy phép [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). GeoNames ghi công dữ liệu gốc như sau: “This work is based on data from GeoNames.org.” Dữ liệu đảo và tên địa danh được cung cấp nguyên trạng, có thể chưa đầy đủ hoặc có sai số. Lớp đáy biển là các đường đẳng sâu khái quát tạo từ SRTM Plus; lớp rạn san hô Natural Earth chỉ gồm các rạn lớn từ WDB2. Các lớp này giúp định hướng trực quan ở quy mô bản đồ thế giới, không thể hiện đầy đủ mọi rạn, bãi cạn, điểm đo độ sâu hay chướng ngại hàng hải, và không phải hải đồ hàng hải chính thức. Không dùng để điều hướng hoặc đánh giá an toàn hàng hải. Dữ liệu Natural Earth thuộc phạm vi công cộng. Các vùng biển và biên giới hiển thị chỉ mang tính tham khảo, không phải ranh giới chủ quyền, EEZ hay dữ liệu pháp lý.
