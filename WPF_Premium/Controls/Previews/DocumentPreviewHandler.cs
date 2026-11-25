using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace WPF_Premium.Controls.Previews;

internal sealed class DocumentPreviewHandler : IFilePreviewHandler
{
    private static readonly HashSet<string> OfficeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".docx", ".docm", ".dot", ".dotx", ".xlsx", ".xlsm", ".pptx", ".pptm",
        ".doc", ".xls", ".ppt", ".odt", ".ott", ".ods", ".ots", ".odp", ".otp", ".rtf"
    };

    public bool CanHandle(PreviewContext context) =>
        (context.Extension == ".pdf" && IsValidPdfHeader(context.FilePath)) ||
        OfficeExtensions.Contains(context.Extension);

    public IPreviewSession CreateSession(PreviewContext context, IPreviewHost host)
    {
        host.SetToolbarMode(PreviewToolbarMode.None);
        var session = new DocumentPreviewSession(context.FilePath, context.Extension, host);
        session.Start();
        return session;
    }

    private static bool IsValidPdfHeader(string path)
    {
        try
        {
            Span<byte> header = stackalloc byte[5];
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return stream.Read(header) == 5 &&
                   header[0] == (byte)'%' &&
                   header[1] == (byte)'P' &&
                   header[2] == (byte)'D' &&
                   header[3] == (byte)'F' &&
                   header[4] == (byte)'-';
        }
        catch
        {
            return false;
        }
    }

    private sealed class DocumentPreviewSession(string path, string extension, IPreviewHost host) : IPreviewSession
    {
        private readonly Grid _container = new();
        private readonly CancellationTokenSource _cts = new();
        private PdfDocumentPreviewControl? _pdfControl;

        public FrameworkElement View => _container;

        public void Start()
        {
            if (extension == ".pdf")
            {
                LoadPdf(path, "PDFium");
                return;
            }

            if (PreviewPasswordDetector.IsEncryptedOfficeFile(path, extension))
            {
                host.SetStatusText("Có mật khẩu");
                host.ShowMessage(PreviewPasswordDetector.PasswordProtectedMessage);
                return;
            }

            if (LibreOfficePreviewConverter.IsAvailable)
            {
                _ = ConvertAndLoadOfficeAsync();
                return;
            }

            ShowOfficeFallback();
        }

        private void LoadPdf(string pdfPath, string engineLabel)
        {
            _pdfControl?.CancelRendering();
            _pdfControl = new PdfDocumentPreviewControl();
            _container.Children.Clear();
            _container.Children.Add(_pdfControl);
            host.SetStatusText(engineLabel);
            _ = _pdfControl.LoadPdfAsync(pdfPath, engineLabel, _cts.Token);
        }

        private async Task ConvertAndLoadOfficeAsync()
        {
            host.SetStatusText("Office");
            _container.Children.Clear();
            _container.Children.Add(new TextBlock
            {
                Text = "Đang kết xuất tài liệu bằng LibreOffice + PDFium...",
                Margin = new Thickness(24),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = host.TryFindBrush("MutedForegroundBrush") ?? Brushes.Gray
            });

            try
            {
                var pdfPath = await LibreOfficePreviewConverter.ConvertToPdfAsync(path, _cts.Token);
                if (_cts.IsCancellationRequested) return;
                if (!string.IsNullOrEmpty(pdfPath) && File.Exists(pdfPath))
                {
                    LoadPdf(pdfPath, "LibreOffice + PDFium");
                    return;
                }
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                return;
            }
            catch
            {
            }

            if (!_cts.IsCancellationRequested)
            {
                ShowOfficeFallback();
            }
        }

        private void ShowOfficeFallback()
        {
            if (extension is ".xlsx" or ".xlsm" or ".xls")
            {
                try
                {
                    _container.Children.Clear();
                    _container.Children.Add(RichPreviewHelpers.CreateSpreadsheetPreview(
                        path,
                        extension,
                        () => host.ShowTextFallback(path, extension),
                        extension is ".xlsx" or ".xlsm" ? RenderOfficeDocumentView : null));
                    host.SetStatusText("Bảng tính");
                    return;
                }
                catch
                {
                }
            }

            RenderOfficeDocumentView();
        }

        private void RenderOfficeDocumentView()
        {
            try
            {
                var document = OfficePreviewFlowDocumentRenderer.Render(path);
                var viewer = new FlowDocumentScrollViewer
                {
                    Document = document,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    IsToolBarVisible = false,
                    Background = Brushes.Transparent
                };
                AutomationProperties.SetAutomationId(viewer, "PreviewDocumentBrowser");
                AutomationProperties.SetName(viewer, "Bản xem trước tài liệu Office");

                var officeRoot = new DockPanel { LastChildFill = true };
                var toolbarBorder = new Border
                {
                    Padding = new Thickness(8, 4, 8, 4),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    BorderBrush = host.TryFindBrush("BorderBrush") ?? Brushes.Gray,
                    Background = host.TryFindBrush("CardBrush") ?? Brushes.Transparent
                };
                var toolbar = new DockPanel { LastChildFill = false };
                var outlineStyle = host.TryFindStyle("ButtonOutline");

                var zoomOut = new Button { Content = "-", Style = outlineStyle, Width = 26, Height = 26, Padding = new Thickness(2), Margin = new Thickness(0, 0, 4, 0), ToolTip = "Thu nhỏ cỡ chữ" };
                var zoomIn = new Button { Content = "+", Style = outlineStyle, Width = 26, Height = 26, Padding = new Thickness(2), Margin = new Thickness(0, 0, 8, 0), ToolTip = "Phóng to cỡ chữ" };
                var fontLabel = new TextBlock { Text = "100%", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0), Foreground = host.TryFindBrush("MutedForegroundBrush") ?? Brushes.Gray };
                zoomOut.Click += (_, _) => { document.FontSize = Math.Max(9, document.FontSize - 1); fontLabel.Text = $"{Math.Round(document.FontSize / 14.0 * 100)}%"; };
                zoomIn.Click += (_, _) => { document.FontSize = Math.Min(28, document.FontSize + 1); fontLabel.Text = $"{Math.Round(document.FontSize / 14.0 * 100)}%"; };

                var copyAllBtn = new Button { Content = "Sao chép văn bản", Style = outlineStyle, Height = 26, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0), ToolTip = "Sao chép toàn bộ nội dung văn bản tài liệu" };
                copyAllBtn.Click += (_, _) =>
                {
                    var range = new System.Windows.Documents.TextRange(document.ContentStart, document.ContentEnd);
                    Clipboard.SetText(range.Text);
                };

                var rawBtn = new Button { Content = "Xem văn bản thô", Style = outlineStyle, Height = 26, Padding = new Thickness(8, 2, 8, 2), ToolTip = "Chuyển sang trình xem văn bản / Hex" };
                rawBtn.Click += (_, _) => host.ShowTextFallback(path, extension);
                DockPanel.SetDock(rawBtn, Dock.Right);

                toolbar.Children.Add(rawBtn);
                toolbar.Children.Add(zoomOut);
                toolbar.Children.Add(zoomIn);
                toolbar.Children.Add(fontLabel);
                toolbar.Children.Add(copyAllBtn);
                toolbarBorder.Child = toolbar;

                DockPanel.SetDock(toolbarBorder, Dock.Top);
                officeRoot.Children.Add(toolbarBorder);
                officeRoot.Children.Add(viewer);

                _container.Children.Clear();
                _container.Children.Add(officeRoot);
                host.SetStatusText("Office");
            }
            catch (Exception ex) when (PreviewPasswordDetector.IsPasswordException(ex))
            {
                host.SetStatusText("Có mật khẩu");
                host.ShowMessage(PreviewPasswordDetector.PasswordProtectedMessage);
            }
            catch (Exception) when (extension is ".doc" or ".xls" or ".ppt")
            {
                host.ShowTextFallback(path, extension);
            }
            catch (Exception ex)
            {
                host.ShowMessage($"Không thể tạo bản xem trước: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _pdfControl?.CancelRendering();
        }
    }
}
