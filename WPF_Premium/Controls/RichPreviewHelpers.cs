using System.Data;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using DocumentFormat.OpenXml.Packaging;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using MimeKit;
using SharpCompress.Archives;

namespace WPF_Premium.Controls;

internal static class RichPreviewHelpers
{
    private static readonly Lazy<bool> IsWebView2Available = new(() =>
    {
        try
        {
            var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            return !string.IsNullOrWhiteSpace(version);
        }
        catch
        {
            return false;
        }
    });

    public static UIElement CreateArchivePreview(string path, Action showRawFallback)
    {
        var entries = new List<ArchiveItemRow>();
        long totalRaw = 0;
        long totalCompressed = 0;
        var fileCount = 0;
        var dirCount = 0;

        using (var archive = ArchiveFactory.Open(path))
        {
            foreach (var entry in archive.Entries)
            {
                if (entries.Count >= 10_000) break;
                if (entry.IsDirectory) dirCount++;
                else fileCount++;

                totalRaw += Math.Max(0, entry.Size);
                totalCompressed += Math.Max(0, entry.CompressedSize);
                var ratio = entry.Size > 0
                    ? $"{Math.Clamp(100.0 * (1.0 - (double)entry.CompressedSize / entry.Size), 0, 100):0}%"
                    : "-";
                entries.Add(new ArchiveItemRow(
                    entry.Key ?? string.Empty,
                    entry.IsDirectory ? "Thư mục" : "Tệp",
                    entry.IsDirectory ? "-" : FormatBytes(entry.Size),
                    entry.IsDirectory ? "-" : FormatBytes(entry.CompressedSize),
                    ratio,
                    entry.LastModifiedTime?.ToString("dd/MM/yyyy HH:mm") ?? "-"));
            }
        }

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var toolbar = new DockPanel { Margin = new Thickness(8, 6, 8, 6), LastChildFill = false };
        var searchBox = new SearchBox
        {
            Width = 240,
            Height = 27,
            Placeholder = "Tìm tên hoặc đường dẫn trong gói nén...",
            ToolTip = "Tìm kiếm theo tên hoặc đường dẫn trong gói nén"
        };
        var searchLabel = new TextBlock { Text = "Lọc:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
        var summary = new TextBlock
        {
            Text = $"Tổng: {fileCount:N0} tệp · {dirCount:N0} thư mục · Gốc: {FormatBytes(totalRaw)} · Nén: {FormatBytes(totalCompressed)}",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            Foreground = Application.Current?.TryFindResource("MutedForegroundBrush") as Brush ?? Brushes.Gray
        };

        var rawBtn = new Button
        {
            Content = "Xem danh sách văn bản",
            Padding = new Thickness(8, 3, 8, 3),
            Height = 27
        };
        if (Application.Current?.TryFindResource("ButtonOutline") is Style btnStyle) rawBtn.Style = btnStyle;
        rawBtn.Click += (_, _) => showRawFallback();
        DockPanel.SetDock(rawBtn, Dock.Right);

        toolbar.Children.Add(rawBtn);
        toolbar.Children.Add(searchLabel);
        toolbar.Children.Add(searchBox);
        toolbar.Children.Add(summary);

        var grid = new DataGrid
        {
            ItemsSource = entries,
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            AlternatingRowBackground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(16, 128, 128, 128))
        };
        ApplyThemedDataGrid(grid);
        grid.Columns.Add(new DataGridTextColumn { Header = "Đường dẫn", Binding = new Binding(nameof(ArchiveItemRow.Path)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Loại", Binding = new Binding(nameof(ArchiveItemRow.Kind)), Width = 80 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Kích thước", Binding = new Binding(nameof(ArchiveItemRow.Size)), Width = 100 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Sau nén", Binding = new Binding(nameof(ArchiveItemRow.CompressedSize)), Width = 100 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Tiết kiệm", Binding = new Binding(nameof(ArchiveItemRow.Ratio)), Width = 80 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Ngày sửa", Binding = new Binding(nameof(ArchiveItemRow.Modified)), Width = 130 });

        var view = CollectionViewSource.GetDefaultView(entries);
        searchBox.TextChanged += (_, _) =>
        {
            var q = (searchBox.Text ?? string.Empty).Trim();
            view.Filter = string.IsNullOrEmpty(q)
                ? null
                : obj => obj is ArchiveItemRow row && row.Path.Contains(q, StringComparison.OrdinalIgnoreCase);
        };

        var border = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = Application.Current?.TryFindResource("BorderBrush") as Brush ?? Brushes.LightGray,
            Child = toolbar
        };
        Grid.SetRow(border, 0);
        Grid.SetRow(grid, 1);
        root.Children.Add(border);
        root.Children.Add(grid);
        return root;
    }

    public static UIElement CreateEmailPreview(string path, Action showSource)
    {
        MimeMessage message;
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".mbox")
        {
            using var stream = File.OpenRead(path);
            var parser = new MimeParser(stream, MimeFormat.Mbox);
            message = parser.ParseMessage() ?? throw new InvalidDataException("Tệp MBOX không có thư nào.");
        }
        else
        {
            message = MimeMessage.Load(path);
        }

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var headerCard = new Border
        {
            Padding = new Thickness(14, 10, 14, 10),
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = Application.Current?.TryFindResource("BorderBrush") as Brush ?? Brushes.LightGray,
            Background = Application.Current?.TryFindResource("CardBrush") as Brush ?? Brushes.WhiteSmoke
        };

        var headerDock = new DockPanel { LastChildFill = true };
        var sourceBtn = new Button
        {
            Content = "Mã nguồn EML",
            Height = 27,
            Padding = new Thickness(10, 2, 10, 2),
            VerticalAlignment = VerticalAlignment.Top
        };
        if (Application.Current?.TryFindResource("ButtonOutline") is Style btnStyle) sourceBtn.Style = btnStyle;
        sourceBtn.Click += (_, _) => showSource();
        DockPanel.SetDock(sourceBtn, Dock.Right);
        headerDock.Children.Add(sourceBtn);

        var metaPanel = new StackPanel();
        metaPanel.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(message.Subject) ? "(Không có tiêu đề)" : message.Subject,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 12, 6)
        });
        metaPanel.Children.Add(MakeMetaLine("Từ:", message.From.ToString()));
        metaPanel.Children.Add(MakeMetaLine("Đến:", message.To.ToString()));
        if (message.Cc.Count > 0) metaPanel.Children.Add(MakeMetaLine("CC:", message.Cc.ToString()));
        metaPanel.Children.Add(MakeMetaLine("Ngày:", message.Date.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss")));

        var attachments = message.Attachments.OfType<MimePart>().Select(a => a.FileName ?? a.ContentType.Name ?? "đính kèm").ToList();
        if (attachments.Count > 0)
        {
            metaPanel.Children.Add(MakeMetaLine($"Đính kèm ({attachments.Count}):", string.Join(", ", attachments)));
        }

        headerDock.Children.Add(metaPanel);
        headerCard.Child = headerDock;
        Grid.SetRow(headerCard, 0);
        root.Children.Add(headerCard);

        UIElement bodyElement;
        if (!string.IsNullOrWhiteSpace(message.HtmlBody))
        {
            bodyElement = CreateHtmlRendererElement(message.HtmlBody);
        }
        else
        {
            var textDoc = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI, Arial"),
                FontSize = 14,
                PagePadding = new Thickness(20),
                ColumnWidth = double.PositiveInfinity
            };
            textDoc.SetResourceReference(FlowDocument.BackgroundProperty, "BackgroundBrush");
            textDoc.SetResourceReference(FlowDocument.ForegroundProperty, "ForegroundBrush");
            textDoc.Blocks.Add(new Paragraph(new Run(message.TextBody ?? "(Thư không có nội dung)")));
            var viewer = new FlowDocumentScrollViewer
            {
                Document = textDoc,
                IsToolBarVisible = false,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            viewer.SetResourceReference(Control.BackgroundProperty, "BackgroundBrush");
            viewer.SetResourceReference(Control.ForegroundProperty, "ForegroundBrush");
            bodyElement = viewer;
        }

        Grid.SetRow(bodyElement, 1);
        root.Children.Add(bodyElement);
        return root;
    }

    public static UIElement CreateMarkdownPreview(string path, Action showSource)
    {
        var markdown = File.ReadAllText(path, Encoding.UTF8);
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var syntaxDoc = Markdig.Markdown.Parse(markdown, pipeline);

        var flowDoc = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI, Arial"),
            FontSize = 14,
            PagePadding = new Thickness(24),
            ColumnWidth = double.PositiveInfinity
        };
        flowDoc.SetResourceReference(FlowDocument.BackgroundProperty, "BackgroundBrush");
        flowDoc.SetResourceReference(FlowDocument.ForegroundProperty, "ForegroundBrush");

        foreach (var block in syntaxDoc)
        {
            var wpfBlock = ConvertMarkdownBlock(block);
            if (wpfBlock is not null) flowDoc.Blocks.Add(wpfBlock);
        }

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var bar = new DockPanel { Margin = new Thickness(8, 5, 8, 5), LastChildFill = false };
        var label = new TextBlock
        {
            Text = "Xem trước Markdown (Markdig)",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Application.Current?.TryFindResource("MutedForegroundBrush") as Brush ?? Brushes.Gray
        };
        var sourceBtn = new Button { Content = "Mã nguồn Markdown", Height = 27, Padding = new Thickness(10, 2, 10, 2) };
        if (Application.Current?.TryFindResource("ButtonOutline") is Style btnStyle) sourceBtn.Style = btnStyle;
        sourceBtn.Click += (_, _) => showSource();
        DockPanel.SetDock(sourceBtn, Dock.Right);
        bar.Children.Add(sourceBtn);
        bar.Children.Add(label);

        var barBorder = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = Application.Current?.TryFindResource("BorderBrush") as Brush ?? Brushes.LightGray,
            Child = bar
        };
        var viewer = new FlowDocumentScrollViewer
        {
            Document = flowDoc,
            IsToolBarVisible = false,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        viewer.SetResourceReference(Control.BackgroundProperty, "BackgroundBrush");
        viewer.SetResourceReference(Control.ForegroundProperty, "ForegroundBrush");

        Grid.SetRow(barBorder, 0);
        Grid.SetRow(viewer, 1);
        root.Children.Add(barBorder);
        root.Children.Add(viewer);
        return root;
    }

    public static UIElement CreateSpreadsheetPreview(string path, string extension, Action showSource, Action? showOfficeLayout = null)
    {
        var sheets = ReadSpreadsheetSheets(path, extension);
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var toolbar = new DockPanel { Margin = new Thickness(8, 5, 8, 5), LastChildFill = false };
        var sheetCombo = new ComboBox { Width = 170, Height = 27, Margin = new Thickness(0, 0, 8, 0), Visibility = sheets.Count > 1 ? Visibility.Visible : Visibility.Collapsed };
        foreach (var s in sheets) sheetCombo.Items.Add(s.Name);
        if (sheets.Count > 0) sheetCombo.SelectedIndex = 0;

        var filterBox = new SearchBox
        {
            Width = 220,
            Height = 27,
            Placeholder = "Lọc nhanh dòng trong bảng...",
            ToolTip = "Lọc nhanh các dòng trong bảng"
        };
        var infoText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
            Foreground = Application.Current?.TryFindResource("MutedForegroundBrush") as Brush ?? Brushes.Gray
        };

        var btnStyle = Application.Current?.TryFindResource("ButtonOutline") as Style;
        var sourceBtn = new Button { Content = "Xem dạng văn bản", Height = 27, Padding = new Thickness(10, 2, 10, 2) };
        if (btnStyle is not null) sourceBtn.Style = btnStyle;
        sourceBtn.Click += (_, _) => showSource();
        DockPanel.SetDock(sourceBtn, Dock.Right);

        var copyCsvBtn = new Button { Content = "Sao chép CSV", Height = 27, Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 0, 6, 0), ToolTip = "Sao chép các dòng đang hiển thị ra Clipboard dạng CSV" };
        if (btnStyle is not null) copyCsvBtn.Style = btnStyle;
        DockPanel.SetDock(copyCsvBtn, Dock.Right);

        toolbar.Children.Add(sourceBtn);
        toolbar.Children.Add(copyCsvBtn);
        if (showOfficeLayout is not null)
        {
            var layoutBtn = new Button { Content = "Bố cục gốc", Height = 27, Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 0, 6, 0), ToolTip = "Xem bố cục gốc (ô gộp, nhiều bảng, ảnh nhúng)" };
            if (btnStyle is not null) layoutBtn.Style = btnStyle;
            layoutBtn.Click += (_, _) => showOfficeLayout();
            DockPanel.SetDock(layoutBtn, Dock.Right);
            toolbar.Children.Add(layoutBtn);
        }
        toolbar.Children.Add(sheetCombo);
        toolbar.Children.Add(new TextBlock { Text = "Lọc:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        toolbar.Children.Add(filterBox);
        toolbar.Children.Add(infoText);

        var dataGrid = new DataGrid
        {
            IsReadOnly = true,
            CanUserAddRows = false,
            AutoGenerateColumns = true,
            GridLinesVisibility = DataGridGridLinesVisibility.All,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            AlternatingRowBackground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(16, 128, 128, 128))
        };
        ApplyThemedDataGrid(dataGrid);

        void BindSheet(int index)
        {
            if (index < 0 || index >= sheets.Count) return;
            var dt = sheets[index].Table;
            var dv = dt.DefaultView;
            dataGrid.ItemsSource = dv;
            infoText.Text = $"{dt.Rows.Count:N0} dòng · {dt.Columns.Count:N0} cột";
            var q = (filterBox.Text ?? string.Empty).Trim().Replace("'", "''");
            if (string.IsNullOrEmpty(q))
            {
                dv.RowFilter = string.Empty;
            }
            else
            {
                var conditions = dt.Columns.Cast<DataColumn>()
                    .Select(c => $"CONVERT([{c.ColumnName.Replace("]", "]]")}], 'System.String') LIKE '%{q}%'");
                dv.RowFilter = string.Join(" OR ", conditions);
            }
        }

        sheetCombo.SelectionChanged += (_, _) => BindSheet(sheetCombo.SelectedIndex);
        filterBox.TextChanged += (_, _) => BindSheet(Math.Max(0, sheetCombo.SelectedIndex));
        copyCsvBtn.Click += (_, _) =>
        {
            if (dataGrid.ItemsSource is DataView dv && dv.Table is { } tbl)
            {
                var csv = new StringBuilder();
                csv.AppendLine(string.Join(",", tbl.Columns.Cast<DataColumn>().Select(c => $"\"{c.ColumnName.Replace("\"", "\"\"")}\"")));
                foreach (DataRowView rv in dv)
                {
                    csv.AppendLine(string.Join(",", tbl.Columns.Cast<DataColumn>().Select(c => $"\"{Convert.ToString(rv[c.ColumnName])?.Replace("\"", "\"\"")}\"")));
                }
                Clipboard.SetText(csv.ToString());
            }
        };
        if (sheets.Count > 0) BindSheet(0);

        var barBorder = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = Application.Current?.TryFindResource("BorderBrush") as Brush ?? Brushes.LightGray,
            Child = toolbar
        };
        Grid.SetRow(barBorder, 0);
        Grid.SetRow(dataGrid, 1);
        root.Children.Add(barBorder);
        root.Children.Add(dataGrid);
        return root;
    }

    public static UIElement CreateWebPreview(string path, Action showSource)
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var toolbar = new DockPanel { Margin = new Thickness(8, 5, 8, 5), LastChildFill = false };
        var engineBadge = new TextBlock
        {
            Text = IsWebView2Available.Value ? "Trình duyệt WebView2 (Edge)" : "Chế độ đọc HTML ngoại tuyến",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Application.Current?.TryFindResource("MutedForegroundBrush") as Brush ?? Brushes.Gray
        };

        var sourceBtn = new Button { Content = "Mã nguồn HTML", Height = 27, Padding = new Thickness(10, 2, 10, 2) };
        var btnStyle = Application.Current?.TryFindResource("ButtonOutline") as Style;
        if (btnStyle is not null) sourceBtn.Style = btnStyle;
        sourceBtn.Click += (_, _) => showSource();
        DockPanel.SetDock(sourceBtn, Dock.Right);
        toolbar.Children.Add(sourceBtn);

        UIElement viewer;
        if (IsWebView2Available.Value)
        {
            var webView = new WebView2();
            var reloadBtn = new Button { Content = "Tải lại", Height = 27, Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 0, 6, 0) };
            if (btnStyle is not null) reloadBtn.Style = btnStyle;
            reloadBtn.Click += (_, _) =>
            {
                try { webView.Reload(); } catch { }
            };
            toolbar.Children.Add(reloadBtn);
            toolbar.Children.Add(engineBadge);

            webView.Loaded += async (_, _) =>
            {
                try
                {
                    await webView.EnsureCoreWebView2Async();
                    webView.Source = new Uri(Path.GetFullPath(path));
                }
                catch
                {
                }
            };
            viewer = webView;
        }
        else
        {
            toolbar.Children.Add(engineBadge);
            var html = File.ReadAllText(path, Encoding.UTF8);
            viewer = CreateOfflineHtmlFlowViewer(html);
        }

        var barBorder = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = Application.Current?.TryFindResource("BorderBrush") as Brush ?? Brushes.LightGray,
            Child = toolbar
        };
        Grid.SetRow(barBorder, 0);
        Grid.SetRow(viewer, 1);
        root.Children.Add(barBorder);
        root.Children.Add(viewer);
        return root;
    }

    private static void ApplyThemedDataGrid(DataGrid grid)
    {
        grid.SetResourceReference(Control.BackgroundProperty, "CardBrush");
        grid.SetResourceReference(Control.ForegroundProperty, "ForegroundBrush");
        grid.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
        grid.SetResourceReference(DataGrid.RowBackgroundProperty, "CardBrush");
        grid.SetResourceReference(DataGrid.HorizontalGridLinesBrushProperty, "BorderBrush");
        grid.SetResourceReference(DataGrid.VerticalGridLinesBrushProperty, "BorderBrush");
        if (Application.Current?.TryFindResource("TableRow") is Style rowStyle) grid.RowStyle = rowStyle;
        if (Application.Current?.TryFindResource("TableCell") is Style cellStyle) grid.CellStyle = cellStyle;
        if (Application.Current?.TryFindResource("TableColumnHeader") is Style hdrStyle) grid.ColumnHeaderStyle = hdrStyle;
    }

    private static UIElement CreateHtmlRendererElement(string html)
    {
        if (IsWebView2Available.Value)
        {
            var webView = new WebView2();
            webView.Loaded += async (_, _) =>
            {
                try
                {
                    await webView.EnsureCoreWebView2Async();
                    webView.NavigateToString(html);
                }
                catch
                {
                }
            };
            return webView;
        }

        return CreateOfflineHtmlFlowViewer(html);
    }

    private static FlowDocumentScrollViewer CreateOfflineHtmlFlowViewer(string html)
    {
        var cleaned = Regex.Replace(html, @"<(script|style|head)\b[^>]*>[\s\S]*?</\1>", string.Empty, RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"</(p|div|li|tr|h[1-6]|section|article|blockquote)>", "\n", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"<t[dh]\b[^>]*>", " | ", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"<[^>]+>", string.Empty);
        var text = WebUtility.HtmlDecode(cleaned).Replace("\r\n", "\n");

        var doc = new FlowDocument
        {
            Background = Brushes.White,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x20, 0x21, 0x24)),
            FontFamily = new FontFamily("Segoe UI, Arial"),
            FontSize = 14,
            PagePadding = new Thickness(24),
            ColumnWidth = double.PositiveInfinity
        };

        foreach (var line in text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0))
        {
            doc.Blocks.Add(new Paragraph(new Run(line)) { Margin = new Thickness(0, 0, 0, 8), LineHeight = 22 });
        }

        if (doc.Blocks.Count == 0)
        {
            doc.Blocks.Add(new Paragraph(new Run("(Không có nội dung văn bản hiển thị)")) { Foreground = Brushes.Gray, FontStyle = FontStyles.Italic });
        }

        return new FlowDocumentScrollViewer
        {
            Document = doc,
            IsToolBarVisible = false,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = Brushes.White
        };
    }

    private static System.Windows.Documents.Block? ConvertMarkdownBlock(Markdig.Syntax.Block block)
    {
        switch (block)
        {
            case HeadingBlock heading:
                var hPara = new Paragraph
                {
                    FontSize = heading.Level switch { 1 => 22, 2 => 18, 3 => 16, _ => 14 },
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 12, 0, 6)
                };
                AppendInlines(hPara.Inlines, heading.Inline);
                return hPara;

            case ParagraphBlock para:
                var p = new Paragraph { Margin = new Thickness(0, 0, 0, 8), LineHeight = 22 };
                AppendInlines(p.Inlines, para.Inline);
                return p;

            case CodeBlock code:
                var codeText = code.Lines.ToString();
                var codeSection = new Section
                {
                    Padding = new Thickness(12),
                    Margin = new Thickness(0, 4, 0, 10),
                    Blocks =
                    {
                        new Paragraph(new Run(codeText))
                        {
                            FontFamily = new FontFamily("Consolas, Courier New"),
                            FontSize = 13,
                            Margin = new Thickness(0)
                        }
                    }
                };
                codeSection.SetResourceReference(TextElement.BackgroundProperty, "CardBrush");
                return codeSection;

            case ListBlock list:
                var wpfList = new System.Windows.Documents.List
                {
                    MarkerStyle = list.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
                    Margin = new Thickness(18, 0, 0, 8)
                };
                foreach (var item in list.OfType<ListItemBlock>())
                {
                    var li = new ListItem();
                    foreach (var sub in item)
                    {
                        var subBlock = ConvertMarkdownBlock(sub);
                        if (subBlock is not null) li.Blocks.Add(subBlock);
                    }
                    wpfList.ListItems.Add(li);
                }
                return wpfList;

            case QuoteBlock quote:
                var section = new Section
                {
                    BorderThickness = new Thickness(3, 0, 0, 0),
                    Padding = new Thickness(12, 2, 0, 2),
                    Margin = new Thickness(4, 4, 0, 8)
                };
                section.SetResourceReference(System.Windows.Documents.Block.BorderBrushProperty, "BorderBrush");
                section.SetResourceReference(TextElement.ForegroundProperty, "MutedForegroundBrush");
                foreach (var sub in quote)
                {
                    var subBlock = ConvertMarkdownBlock(sub);
                    if (subBlock is not null) section.Blocks.Add(subBlock);
                }
                return section;

            default:
                return null;
        }
    }

    private static void AppendInlines(InlineCollection target, ContainerInline? container)
    {
        if (container is null) return;
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline lit:
                    target.Add(new Run(lit.Content.ToString()));
                    break;
                case CodeInline code:
                    var inlineCode = new Run(code.Content)
                    {
                        FontFamily = new FontFamily("Consolas, Courier New")
                    };
                    inlineCode.SetResourceReference(TextElement.BackgroundProperty, "MutedBrush");
                    target.Add(inlineCode);
                    break;
                case EmphasisInline emph:
                    var span = new Span();
                    if (emph.DelimiterCount >= 2) span.FontWeight = FontWeights.Bold;
                    else span.FontStyle = FontStyles.Italic;
                    AppendInlines(span.Inlines, emph);
                    target.Add(span);
                    break;
                case LineBreakInline:
                    target.Add(new LineBreak());
                    break;
                case ContainerInline subContainer:
                    AppendInlines(target, subContainer);
                    break;
            }
        }
    }

    private static List<(string Name, DataTable Table)> ReadSpreadsheetSheets(string path, string extension)
    {
        if (extension is ".csv" or ".tsv")
        {
            var delimiter = extension == ".tsv" ? '\t' : ',';
            var dt = new DataTable("Dữ liệu");
            var lines = File.ReadLines(path, Encoding.UTF8).Take(5000).ToList();
            if (lines.Count == 0) return [("Dữ liệu", dt)];

            var headers = SplitDelimitedLine(lines[0], delimiter);
            for (var c = 0; c < headers.Count; c++)
            {
                var colName = string.IsNullOrWhiteSpace(headers[c]) ? $"Cột {c + 1}" : headers[c];
                while (dt.Columns.Contains(colName)) colName += "_";
                dt.Columns.Add(colName);
            }

            foreach (var line in lines.Skip(1))
            {
                var cells = SplitDelimitedLine(line, delimiter);
                while (dt.Columns.Count < cells.Count) dt.Columns.Add($"Cột {dt.Columns.Count + 1}");
                var row = dt.NewRow();
                for (var c = 0; c < cells.Count; c++) row[c] = cells[c];
                dt.Rows.Add(row);
            }

            return [("Dữ liệu", dt)];
        }

        if (extension is ".xlsx" or ".xlsm")
        {
            var result = new List<(string, DataTable)>();
            using var doc = SpreadsheetDocument.Open(path, false);
            var wbPart = doc.WorkbookPart;
            if (wbPart?.Workbook?.Sheets is null) return result;
            var sharedStrings = wbPart.SharedStringTablePart?.SharedStringTable?.Elements<DocumentFormat.OpenXml.Spreadsheet.SharedStringItem>().Select(i => i.InnerText).ToList();

            foreach (var sheet in wbPart.Workbook.Sheets.Elements<DocumentFormat.OpenXml.Spreadsheet.Sheet>())
            {
                var id = sheet.Id?.Value;
                if (string.IsNullOrEmpty(id) || wbPart.GetPartById(id) is not WorksheetPart wsPart) continue;
                var sheetName = sheet.Name?.Value ?? $"Trang {result.Count + 1}";
                result.AddRange(ExtractSheetTables(wsPart, sheetName, sharedStrings));
            }

            return result;
        }

        // Legacy .xls via DotnetPoi.Legacy
        var legacyResult = new List<(string, DataTable)>();
        using var fs = File.OpenRead(path);
        dynamic wb = new DotnetPoi.HSSF.UserModel.HSSFWorkbook(fs);
        int count = wb.NumberOfSheets;
        for (var s = 0; s < count; s++)
        {
            dynamic sheet = wb.GetSheetAt(s);
            string name = sheet.SheetName ?? $"Trang {s + 1}";
            var dt = new DataTable(name);
            int lastRow = Math.Min(2000, (int)sheet.LastRowNum);
            for (var r = 0; r <= lastRow; r++)
            {
                dynamic? row = sheet.GetRow(r);
                if (row is null) continue;
                int lastCell = (int)row.LastCellNum;
                while (dt.Columns.Count < lastCell) dt.Columns.Add(GetExcelColumnName(dt.Columns.Count));
                var dr = dt.NewRow();
                for (var c = 0; c < lastCell; c++)
                {
                    dynamic? cell = row.GetCell(c);
                    dr[c] = cell?.ToString() ?? string.Empty;
                }
                dt.Rows.Add(dr);
            }
            legacyResult.Add((name, dt));
        }

        return legacyResult;
    }

    private static List<(string Name, DataTable Table)> ExtractSheetTables(
        WorksheetPart wsPart,
        string sheetName,
        List<string>? sharedStrings)
    {
        var rows = wsPart.Worksheet?.Descendants<DocumentFormat.OpenXml.Spreadsheet.Row>().Take(2000).ToList() ?? [];
        var fullDt = new DataTable(sheetName);
        var grid = new Dictionary<(int Row, int Col), string>();
        var maxCols = 1;
        var parsedRows = new List<(int RowIdx, List<(int ColIdx, string Val)> Cells)>(rows.Count);

        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var rowIdx = r.RowIndex?.HasValue == true ? Math.Max(0, (int)r.RowIndex.Value - 1) : i;
            var cells = new List<(int ColIdx, string Val)>();
            var seqCol = 0;
            foreach (var cell in r.Elements<DocumentFormat.OpenXml.Spreadsheet.Cell>())
            {
                var colIdx = string.IsNullOrWhiteSpace(cell.CellReference?.Value)
                    ? seqCol
                    : GetColumnIndex(cell.CellReference!.Value);
                seqCol = colIdx + 1;
                if (colIdx + 1 > maxCols) maxCols = colIdx + 1;

                var val = ReadCellValue(cell, sharedStrings);
                cells.Add((colIdx, val));
                if (!string.IsNullOrWhiteSpace(val))
                {
                    grid[(rowIdx, colIdx)] = val;
                }
            }
            parsedRows.Add((rowIdx, cells));
        }

        for (var c = 0; c < maxCols; c++) fullDt.Columns.Add(GetExcelColumnName(c));
        foreach (var (_, cells) in parsedRows)
        {
            var dr = fullDt.NewRow();
            foreach (var (colIdx, val) in cells)
            {
                if (colIdx >= 0 && colIdx < fullDt.Columns.Count) dr[colIdx] = val;
            }
            fullDt.Rows.Add(dr);
        }

        var regions = DetectTableRegions(wsPart, grid);
        if (regions.Count < 2)
        {
            return [(sheetName, fullDt)];
        }

        var list = new List<(string, DataTable)>(regions.Count + 1)
        {
            ($"{sheetName} (Toàn bộ)", fullDt)
        };
        foreach (var (label, minR, maxR, minC, maxC) in regions)
        {
            var subName = $"{sheetName} — {label}";
            list.Add((subName, BuildSubTableDataTable(grid, minR, maxR, minC, maxC, subName)));
        }
        return list;
    }

    private static List<(string Label, int MinR, int MaxR, int MinC, int MaxC)> DetectTableRegions(
        WorksheetPart wsPart,
        Dictionary<(int Row, int Col), string> grid)
    {
        var regions = new List<(string Label, int MinR, int MaxR, int MinC, int MaxC)>();
        foreach (var tdp in wsPart.TableDefinitionParts)
        {
            var tbl = tdp.Table;
            if (TryParseCellRange(tbl?.Reference?.Value, out var r1, out var c1, out var r2, out var c2)
                && (r2 > r1 || c2 > c1))
            {
                var name = tbl?.DisplayName?.Value ?? tbl?.Name?.Value ?? tbl?.Reference?.Value ?? $"Bảng {regions.Count + 1}";
                regions.Add((name, r1, r2, c1, c2));
            }
        }
        if (regions.Count >= 2 || grid.Count == 0)
        {
            return regions.OrderBy(r => r.MinR).ThenBy(r => r.MinC).ToList();
        }

        // Fallback: split occupied cells into horizontal row bands, then vertical column bands.
        regions.Clear();
        var occupiedRows = grid.Keys.Select(k => k.Row).Distinct().OrderBy(r => r).ToList();
        foreach (var (bandR1, bandR2) in GetContiguousSpans(occupiedRows))
        {
            var bandCols = grid.Keys
                .Where(k => k.Row >= bandR1 && k.Row <= bandR2)
                .Select(k => k.Col)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            foreach (var (bandC1, bandC2) in GetContiguousSpans(bandCols))
            {
                var subRows = grid.Keys
                    .Where(k => k.Row >= bandR1 && k.Row <= bandR2 && k.Col >= bandC1 && k.Col <= bandC2)
                    .Select(k => k.Row)
                    .Distinct()
                    .OrderBy(r => r)
                    .ToList();

                foreach (var (subR1, subR2) in GetContiguousSpans(subRows))
                {
                    if (subR2 - subR1 < 1 || bandC2 - bandC1 < 1) continue;
                    var range = $"{GetExcelColumnName(bandC1)}{subR1 + 1}:{GetExcelColumnName(bandC2)}{subR2 + 1}";
                    regions.Add(($"Bảng {regions.Count + 1} ({range})", subR1, subR2, bandC1, bandC2));
                }
            }
        }

        return regions;
    }

    private static IEnumerable<(int Start, int End)> GetContiguousSpans(List<int> sortedIndices)
    {
        if (sortedIndices.Count == 0) yield break;
        var start = sortedIndices[0];
        var prev = sortedIndices[0];
        for (var i = 1; i < sortedIndices.Count; i++)
        {
            if (sortedIndices[i] != prev + 1)
            {
                yield return (start, prev);
                start = sortedIndices[i];
            }
            prev = sortedIndices[i];
        }
        yield return (start, prev);
    }

    private static DataTable BuildSubTableDataTable(
        Dictionary<(int Row, int Col), string> grid,
        int minR,
        int maxR,
        int minC,
        int maxC,
        string tableName)
    {
        var dt = new DataTable(tableName);
        var headerNonEmpty = 0;
        for (var c = minC; c <= maxC; c++)
        {
            if (grid.ContainsKey((minR, c))) headerNonEmpty++;
        }

        var useFirstRowAsHeader = headerNonEmpty >= 2 && maxR > minR;
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var c = minC; c <= maxC; c++)
        {
            var baseName = useFirstRowAsHeader && grid.TryGetValue((minR, c), out var h) && !string.IsNullOrWhiteSpace(h)
                ? h.Trim()
                : GetExcelColumnName(c);
            var uniqueName = baseName;
            var suffix = 2;
            while (!usedNames.Add(uniqueName))
            {
                uniqueName = $"{baseName} ({suffix++})";
            }
            dt.Columns.Add(uniqueName);
        }

        var startRow = useFirstRowAsHeader ? minR + 1 : minR;
        for (var r = startRow; r <= maxR; r++)
        {
            var dr = dt.NewRow();
            for (var c = minC; c <= maxC; c++)
            {
                if (grid.TryGetValue((r, c), out var val))
                {
                    dr[c - minC] = val;
                }
            }
            dt.Rows.Add(dr);
        }
        return dt;
    }

    private static bool TryParseCellRange(string? range, out int r1, out int c1, out int r2, out int c2)
    {
        r1 = c1 = r2 = c2 = 0;
        if (string.IsNullOrWhiteSpace(range)) return false;
        var parts = range.Split(':');
        if (parts.Length != 2) return false;
        return TryParseCellRef(parts[0], out r1, out c1) && TryParseCellRef(parts[1], out r2, out c2);
    }

    private static bool TryParseCellRef(string? cellRef, out int rowIdx, out int colIdx)
    {
        rowIdx = colIdx = 0;
        if (string.IsNullOrWhiteSpace(cellRef)) return false;
        var clean = cellRef.Trim().Replace("$", string.Empty);
        var split = 0;
        while (split < clean.Length && char.IsLetter(clean[split])) split++;
        if (split == 0 || split == clean.Length) return false;
        if (!int.TryParse(clean[split..], out var rowNum) || rowNum < 1) return false;
        colIdx = GetColumnIndex(clean);
        rowIdx = rowNum - 1;
        return true;
    }

    private static List<string> SplitDelimitedLine(string line, char delimiter)
    {
        var cells = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                else inQuotes = !inQuotes;
            }
            else if (ch == delimiter && !inQuotes)
            {
                cells.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }
        cells.Add(current.ToString());
        return cells;
    }

    private static string ReadCellValue(DocumentFormat.OpenXml.Spreadsheet.Cell cell, List<string>? sharedStrings)
    {
        if (cell.DataType?.Value == DocumentFormat.OpenXml.Spreadsheet.CellValues.InlineString)
        {
            return cell.InlineString?.InnerText ?? string.Empty;
        }
        var raw = cell.CellValue?.InnerText ?? string.Empty;
        if (cell.DataType?.Value == DocumentFormat.OpenXml.Spreadsheet.CellValues.SharedString && int.TryParse(raw, out var idx) && sharedStrings is not null && idx >= 0 && idx < sharedStrings.Count)
        {
            return sharedStrings[idx];
        }
        if (cell.DataType?.Value == DocumentFormat.OpenXml.Spreadsheet.CellValues.Boolean)
        {
            return raw == "1" ? "TRUE" : "FALSE";
        }
        return raw;
    }

    private static int GetColumnIndex(string? cellRef)
    {
        if (string.IsNullOrWhiteSpace(cellRef)) return 0;
        var col = 0;
        foreach (var ch in cellRef)
        {
            if (!char.IsLetter(ch)) break;
            col = col * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
        }
        return Math.Max(0, col - 1);
    }

    private static string GetExcelColumnName(int index)
    {
        var sb = new StringBuilder();
        var n = index + 1;
        while (n > 0)
        {
            var rem = (n - 1) % 26;
            sb.Insert(0, (char)('A' + rem));
            n = (n - 1) / 26;
        }
        return sb.ToString();
    }

    private static StackPanel MakeMetaLine(string label, string value)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
        p.Children.Add(new TextBlock
        {
            Text = label,
            Width = 96,
            FontWeight = FontWeights.SemiBold,
            Foreground = Application.Current?.TryFindResource("MutedForegroundBrush") as Brush ?? Brushes.Gray
        });
        p.Children.Add(new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap });
        return p;
    }

    private static string FormatBytes(long bytes) =>
        bytes < 1024 ? $"{bytes} B" : bytes < 1048576 ? $"{bytes / 1024d:0.0} KB" : $"{bytes / 1048576d:0.0} MB";

    private sealed record ArchiveItemRow(string Path, string Kind, string Size, string CompressedSize, string Ratio, string Modified);
}
