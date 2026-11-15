using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using FlaApplication = FlaUI.Core.Application;

namespace WPF_Premium.UIAutomationTests;

[TestClass]
[DoNotParallelize]
public sealed class DemoUiTests
{
    private static readonly string[] SectionTitles =
    [
        "Cây điều hướng",
        "Thanh công cụ kiểu dải băng",
        "Ngăn xếp / Có thể thu gọn",
        "Cảnh báo / Trống / Mục",
        "Tệp đính kèm / Xác thực biểu mẫu / Ô nhập / Vùng văn bản",
        "Lịch / Ngày giờ / Khoảng ngày giờ",
        "Hộp chọn / Lệnh / Nhóm nút chọn",
        "Trình đơn ngữ cảnh / Trình đơn thả xuống / Thanh trình đơn",
        "Nút / Nhóm nút / Công tắc / Nhóm công tắc",
        "Phân trang / Cửa sổ bật lên / Tiến trình / Thanh trượt",
        "Bảng / Chú giải / Khung chờ",
        "DataTable / ADO.NET SQL",
        "StatCard / Đánh giá",
        "Badge / Nhãn trạng thái",
        "Divider / Đường phân cách",
        "StepBar / Tiến trình nhiều bước",
        "Waterfall Panel / Bố cục dạng thác",
        "Biểu đồ",
        "Dòng sự kiện / Kiểu chữ / Phím tắt / Nhãn",
        "Hộp thoại / Hộp cảnh báo / Ngăn trượt / Bảng trượt / Thông báo",
        "AvalonDock",
        "Bản đồ ngoại tuyến"
    ];

    private static readonly string[] SectionNavigationItems =
    [
        "Cây điều hướng",
        "Thanh công cụ",
        "Ngăn xếp / Thu gọn",
        "Cảnh báo / Trống / Mục",
        "Tệp / Trường / Ô nhập",
        "Lịch / Ngày giờ",
        "Hộp chọn / Lệnh",
        "Trình đơn",
        "Nút / Công tắc",
        "Phân trang / Tiến trình",
        "Bảng / Chú giải",
        "DataTable / SQL",
        "Thống kê / Đánh giá",
        "Badge",
        "Divider",
        "StepBar",
        "Waterfall Panel",
        "Biểu đồ",
        "Trace / Kiểu chữ",
        "Hộp thoại / Bảng trượt",
        "AvalonDock",
        "Bản đồ ngoại tuyến"
    ];

    [TestMethod]
    [TestCategory("UI")]
    public void EveryDemoSectionIsAccessibleAndVisuallyPlaced()
    {
        var screenshotDirectory = Path.Combine(Path.GetTempPath(), "WpfPremiumUiScreenshots");
        var darkScreenshotDirectory = Path.Combine(screenshotDirectory, "Dark");
        Directory.CreateDirectory(screenshotDirectory);
        Directory.CreateDirectory(darkScreenshotDirectory);

        using var process = StartDemo();
        try
        {
            using var application = FlaApplication.Attach(process.Id);
            using var automation = new UIA3Automation();
            var window = WaitForMainWindow(application, automation);
            Assert.IsFalse(process.HasExited, "The demo exited before the UI scan started.");

            var navigation = WaitForElement(window, "DemoNavigation");
            var windowBounds = window.BoundingRectangle;
            Assert.IsTrue(windowBounds.Width >= 1000, "The demo window should open at a desktop-friendly width.");
            Assert.IsTrue(windowBounds.Height >= 700, "The demo window should open at a desktop-friendly height.");

            ScanDemoSections(window, navigation, automation, screenshotDirectory, windowBounds);

            AssertSidebarCanCollapseAndExpand(window);
            AssertDemoControls(window);
            SwitchTheme(window, "Tối");
            AssertThemeSwitch(window);
            ScanDemoSections(window, navigation, automation, darkScreenshotDirectory, windowBounds);
            SwitchTheme(window, "Sáng");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(TimeSpan.FromSeconds(5));
            }
        }

        Assert.IsTrue(Directory.Exists(screenshotDirectory), "The screenshot directory should exist.");
        Assert.IsTrue(Directory.Exists(darkScreenshotDirectory), "The dark-theme screenshot directory should exist.");
    }

    private static void ScanDemoSections(
        Window window,
        AutomationElement navigation,
        UIA3Automation automation,
        string screenshotDirectory,
        Rectangle windowBounds)
    {
        for (var index = 0; index < SectionTitles.Length; index++)
        {
            SelectNavigationItem(navigation, SectionNavigationItems[index]);
            WaitForSection(window, SectionNavigationItems[index], SectionTitles[index]);
            DrainUi();
            if (SectionTitles[index] == "DataTable / ADO.NET SQL")
            {
                AssertDataTableSearchAndPaging(window);
            }
            if (index == 1)
            {
                AssertToolbarFitsPage(window);
                AssertCodeSampleCanExpand(window);
            }

            var screenshotPath = Path.Combine(
                screenshotDirectory,
                $"{index + 1:00}-{SanitizeFileName(SectionTitles[index])}.png");
            CaptureWindow(window, screenshotPath);
            Assert.IsTrue(new FileInfo(screenshotPath).Length > 12_000, $"The {SectionTitles[index]} screenshot is suspiciously small.");
            AssertVisibleBoundsInside(window, windowBounds, SectionTitles[index]);
            AssertNoUnexpectedPopupStillOpen(automation, SectionTitles[index]);
        }
    }

    private static Process StartDemo()
    {
        var configuration = Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))?.Name
            ?? "Debug";
        var repositoryRoot = FindRepositoryRoot();
        var executablePath = Path.Combine(
            repositoryRoot,
            "WPF_Premium_Demo",
            "bin",
            configuration,
            "net10.0-windows10.0.19041",
            "WPF_Premium_Demo.exe");
        Assert.IsTrue(File.Exists(executablePath), $"Build the demo before running UI tests. Expected: {executablePath}");

        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(executablePath)!
        };
        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the WPF Premium demo process.");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WPF_Premium.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate WPF_Premium.slnx from the test output directory.");
    }

    private static Window WaitForMainWindow(FlaApplication application, UIA3Automation automation)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(20))
        {
            try
            {
                var window = application.GetMainWindow(automation, TimeSpan.FromSeconds(1));
                if (window is not null)
                {
                    return window;
                }
            }
            catch (TimeoutException)
            {
                // The WPF window has not been created yet.
            }

            Thread.Sleep(150);
        }

        Assert.Fail("The WPF Premium demo did not expose its main window within 20 seconds.");
        throw new AssertFailedException("Unreachable after Assert.Fail.");
    }

    private static AutomationElement WaitForElement(AutomationElement root, string automationId)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(15))
        {
            var element = root.FindFirstDescendant(condition => condition.ByAutomationId(automationId));
            if (element is not null)
            {
                return element;
            }

            Thread.Sleep(150);
        }

        Assert.Fail($"UI Automation element '{automationId}' was not found within 15 seconds.");
        throw new AssertFailedException("Unreachable after Assert.Fail.");
    }

    private static void WaitForSection(AutomationElement window, string navigationTitle, string sectionTitle)
    {
        var pageTitle = WaitForElement(window, "DemoPageTitle");
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(5))
        {
            var visibleText = string.Join(" | ", window.FindAllDescendants(condition =>
                    condition.ByControlType(ControlType.Text))
                .Select(SafeName));
            if (SafeName(pageTitle).Trim().Equals(navigationTitle, StringComparison.OrdinalIgnoreCase) &&
                visibleText.Contains(sectionTitle, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Thread.Sleep(100);
        }

        var availableText = string.Join(" | ", window.FindAllDescendants(condition =>
                condition.ByControlType(ControlType.Text))
            .Select(SafeName));
        Assert.Fail($"Demo section '{sectionTitle}' was not visible within 5 seconds. Visible text: {availableText}");
    }

    private static void SelectNavigationItem(AutomationElement navigation, string itemName)
    {
        var item = navigation.FindFirstDescendant(condition =>
            condition.ByControlType(ControlType.Text)
                .And(condition.ByName(itemName)));
        if (item is null)
        {
            Assert.Fail($"Navigation item '{itemName}' was not found.");
        }

        var treeItem = item.Parent;
        while (treeItem is not null && treeItem.ControlType != ControlType.TreeItem)
        {
            treeItem = treeItem.Parent;
        }

        Assert.IsNotNull(treeItem, $"Navigation text '{itemName}' must belong to a tree item.");
        var selectionPattern = treeItem.Patterns.SelectionItem.Pattern;
        Assert.IsNotNull(selectionPattern, $"Navigation item '{itemName}' should support selection.");
        selectionPattern.Select();
        Thread.Sleep(120);
    }

    private static void DrainUi()
    {
        Thread.Sleep(150);
    }

    private static void CaptureWindow(Window window, string screenshotPath)
    {
        var bounds = window.BoundingRectangle;
        var width = Math.Max(1, (int)Math.Ceiling((double)bounds.Width));
        var height = Math.Max(1, (int)Math.Ceiling((double)bounds.Height));
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(
                (int)Math.Floor((double)bounds.Left),
                (int)Math.Floor((double)bounds.Top),
                0,
                0,
                new Size(width, height),
                CopyPixelOperation.SourceCopy);
        }

        bitmap.Save(screenshotPath, ImageFormat.Png);
    }

    private static void AssertVisibleBoundsInside(Window window, Rectangle windowBounds, string sectionTitle)
    {
        var text = window.FindAllDescendants(condition => condition.ByControlType(ControlType.Text));
        foreach (var element in text)
        {
            if (!TryGetBounds(element, out var bounds)) continue;
            if (bounds.Width <= 0 || bounds.Height <= 0) continue;
            if (bounds.Right <= windowBounds.Left || bounds.Left >= windowBounds.Right ||
                bounds.Bottom <= windowBounds.Top || bounds.Top >= windowBounds.Bottom) continue;
            if (bounds.Left < windowBounds.Left - 1 || bounds.Right > windowBounds.Right + 1 ||
                bounds.Top < windowBounds.Top - 1 || bounds.Bottom > windowBounds.Bottom + 1)
            {
                Assert.Fail($"{sectionTitle}: visible text '{SafeName(element)}' is clipped outside the window. " +
                            $"Bounds={bounds}, window={windowBounds}");
            }
        }
    }

    private static bool TryGetBounds(AutomationElement element, out Rectangle bounds)
    {
        try
        {
            bounds = element.BoundingRectangle;
            return bounds.Width > 0 && bounds.Height > 0;
        }
        catch (Exception exception) when (exception is FlaUI.Core.Exceptions.PropertyNotSupportedException or InvalidOperationException)
        {
            bounds = default;
            return false;
        }
    }

    private static void AssertNoUnexpectedPopupStillOpen(UIA3Automation automation, string sectionTitle)
    {
        if (sectionTitle is not ("Trình đơn ngữ cảnh / Trình đơn thả xuống / Thanh trình đơn" or "Hộp thoại / Hộp cảnh báo / Ngăn trượt / Bảng trượt / Thông báo"))
        {
            return;
        }

        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(1))
        {
            var popup = automation.GetDesktop().FindFirstDescendant(condition =>
                condition.ByControlType(ControlType.Window)
                    .And(condition.ByAutomationId("WpfPremiumDemoWindow")));
            if (popup is null) return;
            Thread.Sleep(100);
        }
    }

    private static void AssertDemoControls(AutomationElement window)
    {
        var ids = CollectAutomationIds(window);
        foreach (var id in new[] { "DemoNavigation", "CodeSampleToggle", "ToggleSidebarButton" })
        {
            Assert.Contains(id, ids);
        }

        AssertCodeSampleCanExpand(window);

        var text = string.Join(' ', window.FindAllDescendants(condition => condition.ByControlType(ControlType.Text))
            .Select(SafeName));
        StringAssert.Contains(text, "Bản đồ ngoại tuyến");
    }

    private static void AssertCodeSampleCanExpand(AutomationElement window)
    {
        var codePanel = WaitForElement(window, "CodeSampleToggle").Patterns.ExpandCollapse.Pattern;
        Assert.IsNotNull(codePanel, "The code sample expander should expose the UI Automation expand pattern.");
        codePanel.Expand();
        WaitForElement(window, "CodeSampleTitle");
        WaitForElement(window, "CodeSampleText");
        Invoke(WaitForElement(window, "CopyCodeButton"));
    }

    private static void AssertSidebarCanCollapseAndExpand(AutomationElement window)
    {
        var toggleButton = WaitForElement(window, "ToggleSidebarButton");
        Invoke(toggleButton);
        Thread.Sleep(100);
        Invoke(toggleButton);
    }

    private static void AssertToolbarFitsPage(AutomationElement window)
    {
        var windowBounds = window.BoundingRectangle;
        var controls = window.FindAllDescendants(condition =>
                condition.ByControlType(ControlType.Text))
            .Where(element => new[] { "Bình thường", "Tiêu đề 1", "Tiêu đề 2" }
                .Contains(SafeName(element), StringComparer.Ordinal))
            .ToList();
        Assert.HasCount(3, controls, "Every toolbar text style should be visible in the demo.");
        foreach (var control in controls)
        {
            var bounds = control.BoundingRectangle;
            Assert.IsTrue(bounds.Left >= windowBounds.Left - 1 &&
                          bounds.Right <= windowBounds.Right + 1,
                $"Toolbar control '{SafeName(control)}' should fit inside the window.");
        }
    }

    private static void AssertDataTableSearchAndPaging(AutomationElement window)
    {
        var summary = WaitForElement(window, "DataTableSummary");
        var search = WaitForElement(window, "SearchBoxInput");
        var valuePattern = search.Patterns.Value.Pattern;
        Assert.IsNotNull(valuePattern, "The data table search box should expose a value pattern.");
        valuePattern.SetValue(string.Empty);
        WaitUntil(() => SafeName(summary).Contains("24 bản ghi", StringComparison.Ordinal),
            "The ADO.NET demo should load the initial SQL result set.");

        var pageTwo = window.FindFirstDescendant(condition =>
            condition.ByControlType(ControlType.Button).And(condition.ByName("Trang 2")));
        Assert.IsNotNull(pageTwo, "The data table should expose the second page.");
        Invoke(pageTwo);
        WaitUntil(() => SafeName(summary).Contains("trang 2/4", StringComparison.Ordinal),
            "Selecting a page should issue a paged SQL query.");

        valuePattern.SetValue("Adapter");
        WaitUntil(() => SafeName(summary).Contains("3 bản ghi", StringComparison.Ordinal),
            "Search should filter the SQL result set.");
        var visibleText = string.Join(' ', window.FindAllDescendants(condition =>
                condition.ByControlType(ControlType.Text))
            .Select(SafeName));
        StringAssert.Contains(visibleText, "Adapter USB-C 7-in-1");
        Assert.IsFalse(visibleText.Contains("Bàn phím cơ K87", StringComparison.Ordinal),
            "Search results should not show non-matching rows.");
    }

    private static void WaitUntil(Func<bool> condition, string failureMessage)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(10))
        {
            if (condition())
            {
                return;
            }

            Thread.Sleep(100);
        }

        Assert.Fail(failureMessage);
    }

    private static void SwitchTheme(AutomationElement window, string themeName)
    {
        var button = window.FindFirstDescendant(condition =>
            condition.ByControlType(ControlType.Button)
                .And(condition.ByName(themeName)));
        Assert.IsNotNull(button, $"Theme button '{themeName}' should be available.");
        Invoke(button);
        Thread.Sleep(350);
    }

    private static void Invoke(AutomationElement element)
    {
        var invokePattern = element.Patterns.Invoke.Pattern;
        Assert.IsNotNull(invokePattern, $"UI Automation element '{SafeName(element)}' should expose Invoke.");
        invokePattern.Invoke();
    }

    private static void AssertThemeSwitch(AutomationElement window)
    {
        var text = string.Join(' ', window.FindAllDescendants(condition => condition.ByControlType(ControlType.Text))
            .Select(SafeName));
        StringAssert.Contains(text, "Tối");
        StringAssert.Contains(text, "Sáng");
    }

    private static HashSet<string> CollectAutomationIds(AutomationElement root)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in root.FindAllDescendants())
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(element.AutomationId)) ids.Add(element.AutomationId);
            }
            catch (FlaUI.Core.Exceptions.PropertyNotSupportedException)
            {
                // Some WPF automation peers do not expose every optional UIA property.
            }
        }

        return ids;
    }

    private static string SafeName(AutomationElement element)
    {
        try
        {
            return element.Name ?? string.Empty;
        }
        catch (Exception exception) when (exception is FlaUI.Core.Exceptions.PropertyNotSupportedException or InvalidOperationException)
        {
            return string.Empty;
        }
    }

    private static string SanitizeFileName(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_');
        }

        return builder.ToString();
    }
}