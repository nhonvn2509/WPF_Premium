using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using MahApps.Metro.IconPacks;
using Microsoft.Win32;
using PDFtoImage;
using MagickImage = ImageMagick.MagickImage;
using MagickFormat = ImageMagick.MagickFormat;
using Sdcb.LibRaw;

namespace WPF_Premium.Controls;

public partial class FilePreviewControl : UserControl
{
    private const int MaxTextPreviewCharacters = 1_000_000;
    private const int MaxArchivePreviewEntries = 10_000;
    private const int MaxEbookPreviewEntries = 1_000;
    private const long MaxJsonPreviewBytes = 8 * 1024 * 1024;
    private static readonly Lazy<LibVLCSharp.Shared.LibVLC> SharedVlc = new(() => { LibVLCSharp.Shared.Core.Initialize(); return new LibVLCSharp.Shared.LibVLC(enableDebugLogs: false); });
    private string? _filePath;
    private Image? _image;
    private ScaleTransform? _imageScale;
    private RotateTransform? _imageRotation;
    private MediaElement? _media;
    private MediaPlayer? _audioPlayer;
    private LibVLCSharp.Shared.MediaPlayer? _vlcVideoPlayer;
    private LibVLCSharp.Shared.Media? _vlcMedia;
    private LibVLCSharp.WPF.VideoView? _vlcVideoView;
    private CancellationTokenSource? _documentRenderCts;
    private long _documentNavigationGeneration;
    private DispatcherTimer? _audioPositionTimer;
    private DispatcherTimer? _videoPositionTimer;
    private bool _updatingAudioSeek;
    private TextBox? _textView;
    private string? _textFilePath;
    private string _textFileExt = string.Empty;
    private double _zoom = 1;
    private bool _magnifier;
    private bool _imageFillMode;
    private FrameworkElement? _customContent;
    private long _previewGeneration;
    private ToolTip? _magnifierToolTip;
    private Image? _magnifierPreview;

    public FilePreviewControl()
    {
        InitializeComponent();
        UpdateChromeVisibility();
        AllowDrop = true;
        Drop += OnDrop;
        Unloaded += (_, _) => StopCurrentMedia();
    }

    public static readonly DependencyProperty ShowChromeProperty =
        DependencyProperty.Register(
            nameof(ShowChrome),
            typeof(bool),
            typeof(FilePreviewControl),
            new PropertyMetadata(true, OnShowChromeChanged));

    public bool ShowChrome
    {
        get => (bool)GetValue(ShowChromeProperty);
        set => SetValue(ShowChromeProperty, value);
    }

    public string? FilePath
    {
        get => _filePath;
        set { if (string.IsNullOrWhiteSpace(value)) return; LoadFile(value); }
    }

    public double ImageZoom => _zoom;

    public void Browse()
    {
        var dialog = new OpenFileDialog { Title = "Chọn tệp xem trước", Filter = "Tất cả tệp (*.*)|*.*" };
        if (dialog.ShowDialog() == true) LoadFile(dialog.FileName);
    }

    public void LoadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            ClearPreview();
            ShowMessage("Không tìm thấy tệp.");
            return;
        }

        StopCurrentMedia();
        var generation = _previewGeneration;
        _filePath = System.IO.Path.GetFullPath(path); _zoom = 1; _magnifier = false;
        _imageFillMode = false;
        var fileName = System.IO.Path.GetFileName(_filePath);
        var extension = System.IO.Path.GetExtension(_filePath);
        FileNameText.Text = System.IO.Path.GetFileNameWithoutExtension(_filePath);
        FileExtensionText.Text = extension.ToUpperInvariant();
        var info = new FileInfo(_filePath);
        FileSizeText.Text = FormatSize(info.Length);
        AutomationProperties.SetName(FileNameText, fileName);
        CloseDocumentBrowser();
        CloseMagnifier();
        ContentHost.Children.Clear();
        _image = null;
        _imageScale = null;
        _imageRotation = null;
        _media = null;
        _textView = null;
        ImageTools.Visibility = TextTools.Visibility = Visibility.Collapsed; ZoomText.Visibility = Visibility.Visible;
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        try
        {
            if (IsImage(ext)) ShowImage(path);
            else if (IsAudio(ext) || (ext == ".a_voc" && WavWaveformReader.HasWavHeader(path))) ShowAudio(path);
            else if (IsVideo(ext)) ShowVideo(path);
            else if (ext == ".pdf")
            {
                if (IsValidPdfHeader(path)) ShowPdfDocument(path);
                else ShowText(path, ext);
            }
            else if (IsOfficeDocument(ext)) ShowOfficeDocument(path, ext);
            else ShowText(path, ext);
        }
        catch (Exception ex) { ShowMessage($"Không thể tạo bản xem trước: {ex.Message}"); }
    }

    public void SetCustomContent(FrameworkElement content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Parent is not null && !ReferenceEquals(content.Parent, ContentHost))
        {
            throw new InvalidOperationException("Custom preview content must be detached before it is assigned.");
        }

        _customContent = content;
        ShowCustomContent();
    }

    public void ShowCustomContent()
    {
        if (_customContent is not null && ReferenceEquals(_customContent.Parent, ContentHost))
        {
            return;
        }

        StopCurrentMedia();
        CloseDocumentBrowser();
        CloseMagnifier();
        _filePath = null;
        _image = null;
        _imageScale = null;
        _imageRotation = null;
        _media = null;
        _textView = null;
        FileNameText.Text = "Chưa chọn tệp";
        FileExtensionText.Text = string.Empty;
        FileSizeText.Text = string.Empty;
        ImageTools.Visibility = TextTools.Visibility = Visibility.Collapsed;
        ZoomText.Visibility = Visibility.Visible;
        ZoomText.Text = "100%";
        _zoom = 1;
        _imageFillMode = false;
        ContentHost.Children.Clear();
        if (_customContent is null)
        {
            EmptyText.Visibility = Visibility.Visible;
            ContentHost.Children.Add(EmptyText);
            return;
        }

        _customContent.Visibility = Visibility.Visible;
        ContentHost.Children.Add(_customContent);
    }

    public void ClearPreview()
    {
        StopCurrentMedia();
        CloseDocumentBrowser();
        CloseMagnifier();
        _filePath = null;
        _image = null;
        _imageScale = null;
        _imageRotation = null;
        _media = null;
        _textView = null;
        FileNameText.Text = "Chưa chọn tệp";
        FileExtensionText.Text = string.Empty;
        FileSizeText.Text = string.Empty;
        ImageTools.Visibility = TextTools.Visibility = Visibility.Collapsed;
        ZoomText.Visibility = Visibility.Visible;
        ZoomText.Text = "100%";
        _zoom = 1;
        _magnifier = false;
        _imageFillMode = false;
        ContentHost.Children.Clear();
        EmptyText.Visibility = Visibility.Visible;
        ContentHost.Children.Add(EmptyText);
    }

    public void SetImageZoom(double zoom)
    {
        if (_image is null)
        {
            return;
        }

        SetZoom(Math.Clamp(zoom, 0.2, 5));
    }

    public void ToggleImageFit()
    {
        if (_image is null)
        {
            return;
        }

        _imageFillMode = !_imageFillMode;
        _image.Stretch = _imageFillMode ? Stretch.UniformToFill : Stretch.Uniform;
    }

    public void RotateImage()
    {
        if (_imageRotation is null)
        {
            return;
        }

        _imageRotation.Angle = (_imageRotation.Angle + 90) % 360;
    }

    public void ResetImage()
    {
        if (_image is null || _imageScale is null || _imageRotation is null)
        {
            return;
        }

        _imageScale.ScaleX = _imageScale.ScaleY = 1;
        _imageRotation.Angle = 0;
        _imageFillMode = false;
        _image.Stretch = Stretch.Uniform;
        ZoomText.Text = "100%";
    }

    private static void OnShowChromeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        ((FilePreviewControl)dependencyObject).UpdateChromeVisibility();
    }

    private void UpdateChromeVisibility()
    {
        if (HeaderPanel is null || ToolsPanel is null)
        {
            return;
        }

        var visibility = ShowChrome ? Visibility.Visible : Visibility.Collapsed;
        HeaderPanel.Visibility = visibility;
        ToolsPanel.Visibility = visibility;
    }

    private void ShowPdfDocument(string path)
    {
        var navGen = ++_documentNavigationGeneration;
        _documentRenderCts?.Cancel();
        _documentRenderCts?.Dispose();
        var cts = new CancellationTokenSource();
        _documentRenderCts = cts;

        var pagesPanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(16)
        };
        var scrollViewer = new ScrollViewer
        {
            Content = pagesPanel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = new SolidColorBrush(Color.FromRgb(0xDF, 0xE3, 0xE8))
        };
        AutomationProperties.SetAutomationId(scrollViewer, "PreviewDocumentBrowser");
        AutomationProperties.SetName(scrollViewer, "Trình xem PDF");

        ContentHost.Children.Clear();
        ContentHost.Children.Add(scrollViewer);
        ZoomText.Text = "PDF";

        _ = RenderPdfPagesAsync(path, pagesPanel, navGen, cts.Token);
    }

    private async Task RenderPdfPagesAsync(string path, StackPanel pagesPanel, long navGen, CancellationToken cancellationToken)
    {
        try
        {
            var pdfBytes = await File.ReadAllBytesAsync(path, cancellationToken);
            var totalPages = await Task.Run(() => Conversion.GetPageCount(pdfBytes), cancellationToken);
            var maxPages = Math.Min(totalPages, 50);
            var renderOptions = new PDFtoImage.RenderOptions(Dpi: 150);

            for (var i = 0; i < maxPages; i++)
            {
                if (cancellationToken.IsCancellationRequested || navGen != _documentNavigationGeneration) return;

                var pageIndex = i;
                var bitmap = await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var stream = new MemoryStream();
                    Conversion.SavePng(stream, pdfBytes, page: pageIndex, options: renderOptions);
                    stream.Position = 0;
                    var img = new BitmapImage();
                    img.BeginInit();
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.StreamSource = stream;
                    img.EndInit();
                    img.Freeze();
                    return img;
                }, cancellationToken);

                if (cancellationToken.IsCancellationRequested || navGen != _documentNavigationGeneration) return;

                var pageBorder = new Border
                {
                    Background = Brushes.White,
                    Margin = new Thickness(0, 0, 0, 12),
                    Padding = new Thickness(4),
                    Child = new Image
                    {
                        Source = bitmap,
                        Stretch = Stretch.Uniform,
                        MaxWidth = 960
                    }
                };
                pagesPanel.Children.Add(pageBorder);
            }

            if (totalPages > maxPages && !cancellationToken.IsCancellationRequested && navGen == _documentNavigationGeneration)
            {
                pagesPanel.Children.Add(new TextBlock
                {
                    Text = $"Đã giới hạn phần xem trước ở {maxPages} / {totalPages} trang.",
                    Foreground = (Brush)FindResource("MutedForegroundBrush"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 8, 0, 16)
                });
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (cancellationToken.IsCancellationRequested || navGen != _documentNavigationGeneration) return;
            ShowMessage($"Không thể xem trước PDF (PDFium): {ex.Message}");
        }
    }

    private void ShowOfficeDocument(string path, string extension)
    {
        if (LibreOfficePreviewConverter.IsAvailable)
        {
            _ = ShowOfficeViaLibreOfficeAsync(path, extension);
            return;
        }

        ShowOfficeFallback(path, extension);
    }

    private async Task ShowOfficeViaLibreOfficeAsync(string path, string extension)
    {
        var navGen = ++_documentNavigationGeneration;
        _documentRenderCts?.Cancel();
        _documentRenderCts?.Dispose();
        var cts = new CancellationTokenSource();
        _documentRenderCts = cts;

        ShowMessage("Đang kết xuất tài liệu bằng LibreOffice + PDFium...");
        ZoomText.Text = "Office";

        try
        {
            var pdfPath = await LibreOfficePreviewConverter.ConvertToPdfAsync(path, cts.Token);
            if (cts.IsCancellationRequested || navGen != _documentNavigationGeneration)
            {
                return;
            }

            if (!string.IsNullOrEmpty(pdfPath) && File.Exists(pdfPath))
            {
                var pagesPanel = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(16)
                };
                var scrollViewer = new ScrollViewer
                {
                    Content = pagesPanel,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Background = new SolidColorBrush(Color.FromRgb(0xDF, 0xE3, 0xE8))
                };
                AutomationProperties.SetAutomationId(scrollViewer, "PreviewDocumentBrowser");
                AutomationProperties.SetName(scrollViewer, "Bản xem trước LibreOffice + PDFium");

                ContentHost.Children.Clear();
                ContentHost.Children.Add(scrollViewer);
                ZoomText.Text = "PDFium";
                await RenderPdfPagesAsync(pdfPath, pagesPanel, navGen, cts.Token);
                return;
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            return;
        }
        catch
        {
        }

        if (!cts.IsCancellationRequested && navGen == _documentNavigationGeneration)
        {
            ShowOfficeFallback(path, extension);
        }
    }

    private void ShowOfficeFallback(string path, string extension)
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
            ContentHost.Children.Clear();
            ContentHost.Children.Add(viewer);
            ZoomText.Text = "Office";
        }
        catch (Exception) when (extension is ".doc" or ".xls" or ".ppt")
        {
            ShowText(path, extension);
        }
        catch (Exception ex)
        {
            ShowMessage($"Không thể tạo bản xem trước: {ex.Message}");
        }
    }

    private void CloseDocumentBrowser()
    {
        ++_documentNavigationGeneration;
        _documentRenderCts?.Cancel();
        _documentRenderCts?.Dispose();
        _documentRenderCts = null;
    }

    private static bool IsValidPdfHeader(string path)
    {
        try
        {
            Span<byte> header = stackalloc byte[5];
            using var stream = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
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

    private static bool IsOfficeDocument(string extension) =>
        extension is ".docx" or ".docm" or ".dot" or ".dotx" or ".xlsx" or ".xlsm" or ".pptx" or ".pptm" or
            ".doc" or ".xls" or ".ppt" or ".odt" or ".ott" or ".ods" or ".ots" or ".odp" or ".otp" or ".rtf";

    private void ShowImage(string path)
    {
        var viewer = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, CanContentScroll = false };
        var image = new Image { Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, RenderTransformOrigin = new Point(.5, .5) };
        _image = image;
        AutomationProperties.SetAutomationId(_image, "PreviewImage");
        _imageScale = new ScaleTransform(1, 1);
        _imageRotation = new RotateTransform(0);
        var transform = new TransformGroup();
        transform.Children.Add(_imageScale);
        transform.Children.Add(_imageRotation);
        _image.RenderTransform = transform;
        _image.MouseMove += Image_MouseMove;
        _image.MouseLeave += Image_MouseLeave;
        _image.PreviewMouseWheel += Image_PreviewMouseWheel;
        var status = new TextBlock { Text = "Đang giải mã hình ảnh…", Foreground = (Brush)FindResource("MutedForegroundBrush"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetAutomationId(status, "PreviewImageStatus");
        var imageHost = new Grid(); imageHost.Children.Add(image); imageHost.Children.Add(status);
        viewer.Content = imageHost; ContentHost.Children.Add(viewer); ImageTools.Visibility = Visibility.Visible; ZoomText.Text = "100%";
        _ = Task.Run(() => DecodeImage(path)).ContinueWith(task => Dispatcher.BeginInvoke(() =>
        {
            if (!ReferenceEquals(_image, image)) return;
            if (task.IsCompletedSuccessfully)
            {
                image.Source = task.Result;
                status.Visibility = Visibility.Collapsed;
            }
            else
            {
                status.Text = $"Không thể giải mã ảnh: {task.Exception?.GetBaseException().Message ?? "định dạng không được hỗ trợ"}. Hãy mở bằng ứng dụng mặc định.";
            }
        }));
    }

    private static BitmapSource DecodeImage(string path)
    {
        try
        {
            var image = new MagickImage(path);
            image.AutoOrient();
            if (image.Width > 2400 || image.Height > 2400) image.Resize(new ImageMagick.MagickGeometry(2400, 2400) { IgnoreAspectRatio = false });
            using var memory = new MemoryStream();
            image.Format = MagickFormat.Png;
            image.Write(memory);
            memory.Position = 0;
            var bitmap = new BitmapImage();
            bitmap.BeginInit(); bitmap.StreamSource = memory; bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.EndInit(); bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            try
            {
                if (IsRawImage(System.IO.Path.GetExtension(path)))
                {
                    using var raw = RawContext.OpenFile(path);
                    raw.Unpack();
                    raw.DcrawProcess(parameters => parameters.HalfSize = true);
                    using var processed = raw.MakeDcrawMemoryImage();
                    processed.SwapRGB();
                    var width = checked((int)processed.Width);
                    var height = checked((int)processed.Height);
                    var stride = checked(width * 3);
                    var pixels = new byte[checked(stride * height)];
                    System.Runtime.InteropServices.Marshal.Copy(processed.DataPointer, pixels, 0, pixels.Length);
                    var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr24, null, pixels, stride);
                    bitmap.Freeze();
                    return bitmap;
                }
            }
            catch { }
            try
            {
                var bytes = File.ReadAllBytes(path);
                for (var start = 0; start < bytes.Length - 4; start++)
                {
                    if (bytes[start] != 0xff || bytes[start + 1] != 0xd8) continue;
                    for (var end = start + 2; end < bytes.Length - 1; end++)
                    {
                        if (bytes[end] != 0xff || bytes[end + 1] != 0xd9) continue;
                        using var memory = new MemoryStream(bytes, start, end + 2 - start, writable: false);
                        var thumbnail = new BitmapImage(); thumbnail.BeginInit(); thumbnail.StreamSource = memory; thumbnail.CacheOption = BitmapCacheOption.OnLoad; thumbnail.EndInit(); thumbnail.Freeze(); return thumbnail;
                    }
                }
            }
            catch { }
            var fallback = new BitmapImage(); fallback.BeginInit(); fallback.UriSource = new Uri(path); fallback.CacheOption = BitmapCacheOption.OnLoad; fallback.EndInit(); fallback.Freeze(); return fallback;
        }
    }

    private void ShowAudio(string path)
    {
        var root = new Grid { Margin = new Thickness(18) };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
        var isMidi = extension is ".mid" or ".midi";
        var waveData = isMidi
            ? new WavWaveformData(1, 0, 0, TimeSpan.Zero, [new float[240]], false)
            : WavWaveformReader.Read(path, 800);

        var channelCount = Math.Max(1, waveData.ChannelCount);
        var channelTrackHeight = channelCount == 1 ? 180d : Math.Max(68d, Math.Min(120d, 320d / channelCount));
        var totalWaveHeight = channelTrackHeight * channelCount + Math.Max(0, channelCount - 1) * 6d;

        var waveSurface = new Grid
        {
            MinHeight = totalWaveHeight,
            Background = new SolidColorBrush(Color.FromRgb(25, 28, 35)),
            ClipToBounds = true,
            Cursor = isMidi ? Cursors.Arrow : Cursors.Hand
        };

        var tracksPanel = new StackPanel();
        var channelCanvases = new List<Canvas>(channelCount);
        Color[] channelColors =
        [
            Color.FromRgb(76, 164, 245),
            Color.FromRgb(52, 211, 153),
            Color.FromRgb(251, 191, 36),
            Color.FromRgb(167, 139, 250),
            Color.FromRgb(244, 114, 182),
            Color.FromRgb(56, 189, 248)
        ];

        for (var ch = 0; ch < channelCount; ch++)
        {
            var trackBorder = new Border
            {
                Height = channelTrackHeight,
                Margin = ch == 0 ? new Thickness(0) : new Thickness(0, 6, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(20, 23, 30)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(42, 48, 60)),
                BorderThickness = new Thickness(0, 0, 0, ch < channelCount - 1 ? 1 : 0)
            };

            var trackGrid = new Grid();
            var canvas = new Canvas { ClipToBounds = true };
            channelCanvases.Add(canvas);
            trackGrid.Children.Add(canvas);

            if (channelCount > 1)
            {
                var label = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(180, 15, 18, 24)),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(8, 6, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    IsHitTestVisible = false,
                    Child = new TextBlock
                    {
                        Text = $"Kênh {ch + 1}",
                        FontSize = 11,
                        Foreground = new SolidColorBrush(channelColors[ch % channelColors.Length])
                    }
                };
                trackGrid.Children.Add(label);
            }

            trackBorder.Child = trackGrid;
            tracksPanel.Children.Add(trackBorder);
        }

        var overlayCanvas = new Canvas { IsHitTestVisible = false, ClipToBounds = true };
        var selectionRect = new System.Windows.Shapes.Rectangle
        {
            Fill = new SolidColorBrush(Color.FromArgb(65, 59, 130, 246)),
            Stroke = new SolidColorBrush(Color.FromRgb(96, 165, 250)),
            StrokeThickness = 1,
            Visibility = Visibility.Collapsed
        };
        var playheadLine = new Line
        {
            X1 = 0,
            X2 = 0,
            Y1 = 0,
            Y2 = totalWaveHeight,
            Stroke = new SolidColorBrush(Color.FromRgb(248, 113, 113)),
            StrokeThickness = 1.6,
            Visibility = isMidi ? Visibility.Collapsed : Visibility.Visible
        };
        overlayCanvas.Children.Add(selectionRect);
        overlayCanvas.Children.Add(playheadLine);

        waveSurface.Children.Add(tracksPanel);
        waveSurface.Children.Add(overlayCanvas);

        const double timelineHeight = 24d;
        var timelineCanvas = new Canvas { ClipToBounds = true, IsHitTestVisible = false };
        var timelineOverlayCanvas = new Canvas { ClipToBounds = true, IsHitTestVisible = false };
        var timelineSelectionRect = new System.Windows.Shapes.Rectangle
        {
            Fill = new SolidColorBrush(Color.FromArgb(90, 59, 130, 246)),
            Height = timelineHeight,
            Visibility = Visibility.Collapsed
        };
        var timelinePlayhead = new Polygon
        {
            Points = [new Point(-5, 0), new Point(5, 0), new Point(0, 8)],
            Fill = new SolidColorBrush(Color.FromRgb(248, 113, 113)),
            Visibility = isMidi ? Visibility.Collapsed : Visibility.Visible
        };
        timelineOverlayCanvas.Children.Add(timelineSelectionRect);
        timelineOverlayCanvas.Children.Add(timelinePlayhead);

        var timelineHost = new Grid
        {
            Height = timelineHeight,
            Background = new SolidColorBrush(Color.FromRgb(18, 21, 28)),
            ClipToBounds = true,
            Cursor = isMidi ? Cursors.Arrow : Cursors.Hand,
            Visibility = isMidi ? Visibility.Collapsed : Visibility.Visible
        };
        timelineHost.Children.Add(timelineCanvas);
        timelineHost.Children.Add(timelineOverlayCanvas);
        var timelineBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(42, 48, 60)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = timelineHost,
            Visibility = isMidi ? Visibility.Collapsed : Visibility.Visible
        };
        AutomationProperties.SetAutomationId(timelineBorder, "AudioTimeline");
        AutomationProperties.SetName(timelineBorder, "Thanh thời gian âm thanh");

        var waveLayout = new Grid();
        waveLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        waveLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(timelineBorder, 0);
        waveLayout.Children.Add(timelineBorder);
        UIElement waveBody = channelCount > 3
            ? new ScrollViewer { MaxHeight = 360, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = waveSurface }
            : waveSurface;
        Grid.SetRow(waveBody, 1);
        waveLayout.Children.Add(waveBody);

        var waveFrame = new Border
        {
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            Child = waveLayout
        };
        AutomationProperties.SetAutomationId(waveFrame, "AudioWaveform");
        AutomationProperties.SetName(waveFrame, "Dạng sóng âm thanh");
        root.Children.Add(waveFrame);

        TimeSpan? rangeStart = null;
        TimeSpan? rangeEnd = null;
        var totalDuration = waveData.Duration > TimeSpan.Zero ? waveData.Duration : TimeSpan.Zero;

        var controls = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 12, 0, 0) };
        var audioStatus = new TextBlock
        {
            Text = isMidi
                ? "Định dạng MIDI không có dạng sóng trực quan. Hãy mở tệp bằng ứng dụng mặc định."
                : "Nhấn/kéo trên sóng để tua · Giữ Shift + kéo (hoặc bật Chọn khoảng) để phát đoạn",
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0),
            Foreground = (Brush)FindResource("MutedForegroundBrush")
        };
        AutomationProperties.SetAutomationId(audioStatus, "AudioStatusText");
        AutomationProperties.SetName(audioStatus, "Trạng thái âm thanh");

        var seek = new Slider { Minimum = 0, Maximum = Math.Max(1, totalDuration.TotalSeconds), Value = 0, VerticalAlignment = VerticalAlignment.Center, MinWidth = 160, IsEnabled = !isMidi };
        AutomationProperties.SetAutomationId(seek, "AudioSeekSlider");
        var playbackTime = new TextBlock
        {
            Text = totalDuration > TimeSpan.Zero ? $"00:00 / {totalDuration:mm\\:ss}" : "00:00",
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetAutomationId(playbackTime, "AudioPlaybackTimeText");

        var rangeSelectToggle = new ToggleButton
        {
            Content = "Chọn khoảng",
            Style = (Style)FindResource("Toggle"),
            ToolTip = "Bật để kéo chọn khoảng phát trực tiếp trên sóng (hoặc giữ Shift + kéo)",
            Margin = new Thickness(10, 0, 0, 0),
            Padding = new Thickness(8, 3, 8, 3),
            IsEnabled = !isMidi
        };
        AutomationProperties.SetAutomationId(rangeSelectToggle, "AudioRangeSelectToggle");

        var loopRangeToggle = new ToggleButton
        {
            Content = "Lặp khoảng",
            Style = (Style)FindResource("Toggle"),
            IsChecked = true,
            ToolTip = "Tự động lặp lại trong khoảng đã chọn",
            Margin = new Thickness(6, 0, 0, 0),
            Padding = new Thickness(8, 3, 8, 3),
            Visibility = Visibility.Collapsed
        };

        var clearRangeButton = new Button
        {
            Content = "Xóa khoảng",
            Style = (Style)FindResource("ButtonOutline"),
            Margin = new Thickness(6, 0, 0, 0),
            Padding = new Thickness(8, 3, 8, 3),
            Visibility = Visibility.Collapsed
        };

        var rangeText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            Foreground = (Brush)FindResource("TraceActiveBrush"),
            Visibility = Visibility.Collapsed
        };

        void UpdateOverlayVisuals()
        {
            var width = waveSurface.ActualWidth;
            var height = waveSurface.ActualHeight > 0 ? waveSurface.ActualHeight : totalWaveHeight;
            if (width <= 0 || totalDuration <= TimeSpan.Zero)
            {
                return;
            }

            var currentSec = _audioPlayer?.Position.TotalSeconds ?? seek.Value;
            var x = Math.Clamp(currentSec / totalDuration.TotalSeconds, 0, 1) * width;
            playheadLine.X1 = x;
            playheadLine.X2 = x;
            playheadLine.Y2 = height;
            Canvas.SetLeft(timelinePlayhead, x);
            Canvas.SetTop(timelinePlayhead, 0);

            if (rangeStart is TimeSpan rs && rangeEnd is TimeSpan re && re > rs)
            {
                var x1 = Math.Clamp(rs.TotalSeconds / totalDuration.TotalSeconds, 0, 1) * width;
                var x2 = Math.Clamp(re.TotalSeconds / totalDuration.TotalSeconds, 0, 1) * width;
                Canvas.SetLeft(selectionRect, x1);
                Canvas.SetTop(selectionRect, 0);
                selectionRect.Width = Math.Max(2, x2 - x1);
                selectionRect.Height = height;
                selectionRect.Visibility = Visibility.Visible;
                Canvas.SetLeft(timelineSelectionRect, x1);
                Canvas.SetTop(timelineSelectionRect, 0);
                timelineSelectionRect.Width = Math.Max(2, x2 - x1);
                timelineSelectionRect.Visibility = Visibility.Visible;
                rangeText.Text = $"Khoảng: {rs:mm\\:ss\\.f} – {re:mm\\:ss\\.f}";
                rangeText.Visibility = Visibility.Visible;
                clearRangeButton.Visibility = Visibility.Visible;
                loopRangeToggle.Visibility = Visibility.Visible;
            }
            else
            {
                selectionRect.Visibility = Visibility.Collapsed;
                timelineSelectionRect.Visibility = Visibility.Collapsed;
                rangeText.Visibility = Visibility.Collapsed;
                clearRangeButton.Visibility = Visibility.Collapsed;
                loopRangeToggle.Visibility = Visibility.Collapsed;
            }
        }

        void RenderTimeline(double width)
        {
            timelineCanvas.Children.Clear();
            if (isMidi || width < 20 || totalDuration <= TimeSpan.Zero)
            {
                return;
            }

            var totalSeconds = totalDuration.TotalSeconds;
            double[] candidateSteps = [0.2, 0.5, 1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600];
            var majorStep = candidateSteps[^1];
            foreach (var step in candidateSteps)
            {
                if (width * step / totalSeconds >= 72d)
                {
                    majorStep = step;
                    break;
                }
            }

            const int minorSubdivisions = 5;
            var minorStep = majorStep / minorSubdivisions;
            var tickBrush = new SolidColorBrush(Color.FromArgb(140, 148, 163, 184));
            tickBrush.Freeze();
            var minorTickBrush = new SolidColorBrush(Color.FromArgb(70, 148, 163, 184));
            minorTickBrush.Freeze();
            var labelBrush = new SolidColorBrush(Color.FromRgb(180, 190, 208));
            labelBrush.Freeze();

            var maxTicks = (int)Math.Floor(totalSeconds / minorStep);
            for (var idx = 0; idx <= maxTicks; idx++)
            {
                var sec = idx * minorStep;
                var x = sec / totalSeconds * width;
                var isMajor = idx % minorSubdivisions == 0;

                timelineCanvas.Children.Add(new Line
                {
                    X1 = x,
                    X2 = x,
                    Y1 = isMajor ? timelineHeight - 9 : timelineHeight - 4,
                    Y2 = timelineHeight,
                    Stroke = isMajor ? tickBrush : minorTickBrush,
                    StrokeThickness = 1
                });

                if (isMajor && x <= width - 28)
                {
                    var ts = TimeSpan.FromSeconds(sec);
                    var labelText = totalSeconds < 10 && majorStep < 1
                        ? $"{sec:0.0}s"
                        : ts.TotalHours >= 1
                            ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                            : $"{(int)ts.TotalMinutes:D2}:{ts.Seconds:D2}";
                    var label = new TextBlock
                    {
                        Text = labelText,
                        FontSize = 10,
                        Foreground = labelBrush,
                        IsHitTestVisible = false
                    };
                    Canvas.SetLeft(label, x + 3);
                    Canvas.SetTop(label, 2);
                    timelineCanvas.Children.Add(label);
                }
            }
        }

        void RenderWaveform()
        {
            if (isMidi) return;
            var width = waveSurface.ActualWidth;
            if (width < 20) return;

            var barStep = 3d;
            var barCount = Math.Max(24, (int)(width / barStep));
            var actualStep = width / barCount;

            for (var ch = 0; ch < channelCount; ch++)
            {
                var canvas = channelCanvases[ch];
                canvas.Children.Clear();

                var midY = channelTrackHeight / 2d;
                canvas.Children.Add(new Line
                {
                    X1 = 0,
                    X2 = width,
                    Y1 = midY,
                    Y2 = midY,
                    Stroke = new SolidColorBrush(Color.FromArgb(70, 148, 163, 184)),
                    StrokeThickness = 1
                });

                var peaks = waveData.ChannelPeaks[ch];
                var strokeBrush = new SolidColorBrush(channelColors[ch % channelColors.Length]);
                strokeBrush.Freeze();

                for (var i = 0; i < barCount; i++)
                {
                    var startBucket = i * peaks.Length / barCount;
                    var endBucket = Math.Max(startBucket + 1, (i + 1) * peaks.Length / barCount);
                    var peak = 0f;
                    for (var b = startBucket; b < endBucket && b < peaks.Length; b++)
                    {
                        if (peaks[b] > peak) peak = peaks[b];
                    }

                    var amp = 0.05 + peak * 0.86;
                    var h = amp * (channelTrackHeight - 10);
                    var x = i * actualStep + actualStep * 0.5;
                    canvas.Children.Add(new Line
                    {
                        X1 = x,
                        X2 = x,
                        Y1 = midY - h / 2d,
                        Y2 = midY + h / 2d,
                        Stroke = strokeBrush,
                        StrokeThickness = Math.Max(1.2, actualStep * 0.65)
                    });
                }
            }

            RenderTimeline(width);
            UpdateOverlayVisuals();
        }

        waveSurface.SizeChanged += (_, _) => RenderWaveform();

        _audioPlayer = new MediaPlayer { Volume = .7 };
        var isAudioPlaying = false;

        TimeSpan PositionFromX(double x)
        {
            var width = Math.Max(1, waveSurface.ActualWidth);
            var ratio = Math.Clamp(x / width, 0, 1);
            return TimeSpan.FromSeconds(ratio * Math.Max(0.1, totalDuration.TotalSeconds));
        }

        void SeekToPosition(TimeSpan target)
        {
            if (totalDuration > TimeSpan.Zero)
            {
                target = TimeSpan.FromSeconds(Math.Clamp(target.TotalSeconds, 0, totalDuration.TotalSeconds));
            }
            _updatingAudioSeek = true;
            _audioPlayer.Position = target;
            seek.Value = Math.Clamp(target.TotalSeconds, seek.Minimum, seek.Maximum);
            playbackTime.Text = totalDuration > TimeSpan.Zero ? $"{target:mm\\:ss} / {totalDuration:mm\\:ss}" : target.ToString(@"mm\:ss");
            _updatingAudioSeek = false;
            UpdateOverlayVisuals();
        }

        var isDraggingWave = false;
        var isDraggingRange = false;
        double rangeAnchorX = 0;

        void AttachWavePointerHandlers(UIElement target)
        {
            target.MouseLeftButtonDown += (_, e) =>
            {
                if (isMidi || totalDuration <= TimeSpan.Zero) return;
                var pos = e.GetPosition(target);
                target.CaptureMouse();
                isDraggingWave = true;
                isDraggingRange = rangeSelectToggle.IsChecked == true || (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
                if (isDraggingRange)
                {
                    rangeAnchorX = pos.X;
                    var t = PositionFromX(pos.X);
                    rangeStart = t;
                    rangeEnd = t;
                    UpdateOverlayVisuals();
                }
                else
                {
                    SeekToPosition(PositionFromX(pos.X));
                }
                e.Handled = true;
            };

            target.MouseMove += (_, e) =>
            {
                if (!isDraggingWave || totalDuration <= TimeSpan.Zero) return;
                var pos = e.GetPosition(target);
                if (isDraggingRange)
                {
                    var t1 = PositionFromX(rangeAnchorX);
                    var t2 = PositionFromX(pos.X);
                    rangeStart = t1 <= t2 ? t1 : t2;
                    rangeEnd = t1 <= t2 ? t2 : t1;
                    UpdateOverlayVisuals();
                }
                else
                {
                    SeekToPosition(PositionFromX(pos.X));
                }
            };

            target.MouseLeftButtonUp += (_, e) =>
            {
                if (!isDraggingWave) return;
                isDraggingWave = false;
                target.ReleaseMouseCapture();
                if (isDraggingRange && rangeStart is TimeSpan rs && rangeEnd is TimeSpan re)
                {
                    if ((re - rs).TotalMilliseconds < 120)
                    {
                        rangeStart = null;
                        rangeEnd = null;
                        SeekToPosition(PositionFromX(e.GetPosition(target).X));
                    }
                    else
                    {
                        SeekToPosition(rs);
                    }
                    UpdateOverlayVisuals();
                }
                isDraggingRange = false;
            };
        }

        AttachWavePointerHandlers(waveSurface);
        AttachWavePointerHandlers(timelineHost);

        clearRangeButton.Click += (_, _) =>
        {
            rangeStart = null;
            rangeEnd = null;
            UpdateOverlayVisuals();
        };

        _audioPlayer.MediaOpened += (_, _) =>
        {
            if (_audioPlayer.NaturalDuration.HasTimeSpan)
            {
                totalDuration = _audioPlayer.NaturalDuration.TimeSpan;
                seek.Maximum = Math.Max(1, totalDuration.TotalSeconds);
                seek.IsEnabled = true;
                playbackTime.Text = $"00:00 / {totalDuration:mm\\:ss}";
                RenderTimeline(waveSurface.ActualWidth);
                UpdateOverlayVisuals();
            }
        };
        _audioPlayer.MediaEnded += (_, _) =>
        {
            if (rangeStart is TimeSpan rs && rangeEnd is TimeSpan re && re > rs && loopRangeToggle.IsChecked == true)
            {
                SeekToPosition(rs);
                _audioPlayer.Play();
                return;
            }
            isAudioPlaying = false;
            _audioPlayer.Position = TimeSpan.Zero;
            seek.Value = 0;
            UpdateOverlayVisuals();
        };
        _audioPlayer.MediaFailed += (_, _) =>
        {
            audioStatus.Text = "Không thể phát định dạng âm thanh này. Hãy mở tệp bằng ứng dụng mặc định.";
            audioStatus.Foreground = (Brush)FindResource("DestructiveBrush");
        };

        var playIcon = new PackIconMaterial { Kind = PackIconMaterialKind.Play };
        var play = new Button { Content = playIcon, Style = (Style)FindResource("ButtonPrimary"), ToolTip = "Phát / tạm dừng", Margin = new Thickness(0, 0, 8, 0) };
        AutomationProperties.SetName(play, "Phát");
        play.Click += (_, _) =>
        {
            if (isAudioPlaying)
            {
                _audioPlayer!.Pause();
                playIcon.Kind = PackIconMaterialKind.Play;
                AutomationProperties.SetName(play, "Phát");
                isAudioPlaying = false;
            }
            else
            {
                if (rangeStart is TimeSpan rs && rangeEnd is TimeSpan re && re > rs)
                {
                    var cur = _audioPlayer!.Position;
                    if (cur < rs || cur >= re)
                    {
                        SeekToPosition(rs);
                    }
                }
                _audioPlayer!.Play();
                playIcon.Kind = PackIconMaterialKind.Pause;
                AutomationProperties.SetName(play, "Tạm dừng");
                isAudioPlaying = true;
            }
        };
        AutomationProperties.SetAutomationId(play, "AudioPlayButton");
        var stop = new Button
        {
            Content = new PackIconMaterial { Kind = PackIconMaterialKind.Stop },
            ToolTip = "Dừng",
            Style = (Style)FindResource("ButtonOutline"),
            Margin = new Thickness(0, 0, 14, 0)
        };
        AutomationProperties.SetName(stop, "Dừng");
        stop.Click += (_, _) =>
        {
            _audioPlayer!.Stop();
            SeekToPosition(rangeStart ?? TimeSpan.Zero);
            playIcon.Kind = PackIconMaterialKind.Play;
            AutomationProperties.SetName(play, "Phát");
            isAudioPlaying = false;
        };
        AutomationProperties.SetAutomationId(stop, "AudioStopButton");
        seek.ValueChanged += (_, _) =>
        {
            if (!_updatingAudioSeek && _audioPlayer is not null && Math.Abs(_audioPlayer.Position.TotalSeconds - seek.Value) > 0.5)
            {
                _audioPlayer.Position = TimeSpan.FromSeconds(seek.Value);
                UpdateOverlayVisuals();
            }
        };
        var volume = new Slider { Minimum = 0, Maximum = 1, Value = .7, Width = 80, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(volume, "Âm lượng");
        volume.ValueChanged += (_, _) => { if (_audioPlayer is not null) _audioPlayer.Volume = volume.Value; };
        _audioPositionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _audioPositionTimer.Tick += (_, _) =>
        {
            if (_audioPlayer is null || !_audioPlayer.NaturalDuration.HasTimeSpan || seek.IsMouseCaptureWithin)
            {
                return;
            }

            var position = _audioPlayer.Position;
            if (isAudioPlaying && rangeStart is TimeSpan rs && rangeEnd is TimeSpan re && re > rs && position >= re)
            {
                if (loopRangeToggle.IsChecked == true)
                {
                    SeekToPosition(rs);
                }
                else
                {
                    _audioPlayer.Pause();
                    playIcon.Kind = PackIconMaterialKind.Play;
                    AutomationProperties.SetName(play, "Phát");
                    isAudioPlaying = false;
                    SeekToPosition(rs);
                }
                return;
            }

            _updatingAudioSeek = true;
            seek.Value = Math.Clamp(position.TotalSeconds, seek.Minimum, seek.Maximum);
            playbackTime.Text = totalDuration > TimeSpan.Zero ? $"{position:mm\\:ss} / {totalDuration:mm\\:ss}" : position.ToString(@"mm\:ss");
            _updatingAudioSeek = false;
            UpdateOverlayVisuals();
        };
        _audioPositionTimer.Start();
        controls.Children.Add(play);
        controls.Children.Add(stop);
        controls.Children.Add(seek);
        controls.Children.Add(playbackTime);
        controls.Children.Add(volume);
        controls.Children.Add(rangeSelectToggle);
        controls.Children.Add(loopRangeToggle);
        controls.Children.Add(clearRangeButton);
        controls.Children.Add(rangeText);

        Grid.SetRow(controls, 1);
        root.Children.Add(controls);
        Grid.SetRow(audioStatus, 2);
        root.Children.Add(audioStatus);
        ContentHost.Children.Add(root);
        _audioPlayer.Open(new Uri(path));
        ZoomText.Text = waveData.SampleRate > 0
            ? (waveData.BitsPerSample > 0
                ? $"{channelCount} kênh · {waveData.SampleRate} Hz · {waveData.BitsPerSample}-bit"
                : $"{channelCount} kênh · {waveData.SampleRate} Hz")
            : "Dạng sóng trực quan";
    }

    private void ShowVideo(string path)
    {
        var grid = new Grid { Background = Brushes.Black };
        var status = new TextBlock { Text = "Đang tải bộ giải mã video…", Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(10), Background = new SolidColorBrush(Color.FromArgb(210, 25, 28, 35)) };
        AutomationProperties.SetAutomationId(status, "VideoPreviewStatus");
        grid.Children.Add(status);
        var bar = new DockPanel { VerticalAlignment = VerticalAlignment.Bottom, Background = new SolidColorBrush(Color.FromArgb(210, 20, 20, 24)), Margin = new Thickness(14), LastChildFill = true };
        var isVideoPlaying = false;
        var videoPlayIcon = new PackIconMaterial { Kind = PackIconMaterialKind.Play };
        var play = new Button { Content = videoPlayIcon, Style = (Style)FindResource("ButtonPrimary"), ToolTip = "Phát / tạm dừng", Margin = new Thickness(0, 0, 10, 0) };
        AutomationProperties.SetName(play, "Phát");
        play.Click += (_, _) =>
        {
            if (isVideoPlaying)
            {
                _vlcVideoPlayer?.Pause();
                videoPlayIcon.Kind = PackIconMaterialKind.Play;
                AutomationProperties.SetName(play, "Phát");
                isVideoPlaying = false;
            }
            else
            {
                _vlcVideoPlayer?.Play();
                videoPlayIcon.Kind = PackIconMaterialKind.Pause;
                AutomationProperties.SetName(play, "Tạm dừng");
                isVideoPlaying = true;
            }
        };
        AutomationProperties.SetAutomationId(play, "VideoPlayButton");
        play.IsEnabled = false;
        var seek = new Slider { Minimum = 0, Maximum = 100, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetAutomationId(seek, "VideoSeekSlider");
        seek.ValueChanged += (_, _) => { if (_vlcVideoPlayer is { Length: > 0 } player && Math.Abs(player.Time / 1000d - seek.Value) > 1) player.Time = (long)(seek.Value * 1000); };
        var vol = new Slider { Minimum = 0, Maximum = 1, Value = .7, Width = 90, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }; vol.ValueChanged += (_, _) => { if (_vlcVideoPlayer != null) _vlcVideoPlayer.Volume = (int)(vol.Value * 100); };
        AutomationProperties.SetAutomationId(vol, "VideoVolumeSlider");
        _videoPositionTimer?.Stop();
        _videoPositionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _videoPositionTimer.Tick += (_, _) => { if (_vlcVideoPlayer is { Length: > 0 } player && !seek.IsMouseCaptureWithin) { seek.Maximum = Math.Max(1, player.Length / 1000d); seek.Value = Math.Clamp(player.Time / 1000d, 0, seek.Maximum); } };
        _videoPositionTimer.Start();
        DockPanel.SetDock(play, Dock.Left); DockPanel.SetDock(vol, Dock.Right); bar.Children.Add(play); bar.Children.Add(vol); bar.Children.Add(seek); grid.Children.Add(bar); ContentHost.Children.Add(grid); ZoomText.Text = "Video";
        var generation = _previewGeneration;
        _ = Task.Run(() => SharedVlc.Value).ContinueWith(task => Dispatcher.BeginInvoke(() =>
        {
            if (generation != _previewGeneration) return;
            try
            {
                if (!task.IsCompletedSuccessfully) throw new InvalidOperationException("Không thể khởi tạo bộ giải mã.", task.Exception);
                _vlcVideoPlayer = new LibVLCSharp.Shared.MediaPlayer(task.Result);
                _vlcVideoView = new LibVLCSharp.WPF.VideoView { MediaPlayer = _vlcVideoPlayer, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
                grid.Children.Insert(0, _vlcVideoView);
                _vlcMedia = new LibVLCSharp.Shared.Media(task.Result, new Uri(path));
                _vlcVideoPlayer.Media = _vlcMedia;
                _vlcVideoPlayer.Volume = 70;
                _vlcVideoPlayer.Playing += (_, _) => Dispatcher.BeginInvoke(() => status.Visibility = Visibility.Collapsed);
                _vlcVideoPlayer.EncounteredError += (_, _) => Dispatcher.BeginInvoke(() => { status.Text = "Không thể giải mã video trên máy này. Hãy mở bằng ứng dụng mặc định."; status.Visibility = Visibility.Visible; });
                _vlcVideoPlayer.Play();
                play.IsEnabled = true;
                isVideoPlaying = true;
                videoPlayIcon.Kind = PackIconMaterialKind.Pause;
            }
            catch (Exception ex) { status.Text = $"Không thể mở video: {ex.Message}"; }
        }));
    }

    private void ShowText(string path, string ext)
    {
        _textFilePath = path;
        _textFileExt = ext;
        ReadableStringsToggle.IsChecked = false;
        HexAsciiToggle.IsChecked = false;
        var content = BuildTextPreviewContent(path, ext, includeReadableStrings: false, includeHexAscii: false);
        _textView = new TextBox
        {
            Text = content,
            IsReadOnly = true,
            AcceptsReturn = true,
            AcceptsTab = true,
            TextWrapping = WordWrapToggle.IsChecked == true ? TextWrapping.Wrap : TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            Padding = new Thickness(16),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent
        };
        AutomationProperties.SetAutomationId(_textView, "PreviewTextContent");
        ContentHost.Children.Add(_textView);
        TextTools.Visibility = Visibility.Visible;
        ZoomText.Visibility = Visibility.Collapsed;
    }

    private void TextDisplayMode_Changed(object sender, RoutedEventArgs e)
    {
        if (_textView is null || string.IsNullOrEmpty(_textFilePath))
        {
            return;
        }

        _textView.Text = BuildTextPreviewContent(
            _textFilePath,
            _textFileExt,
            ReadableStringsToggle.IsChecked == true,
            HexAsciiToggle.IsChecked == true);
    }

    private static string BuildTextPreviewContent(string path, string ext, bool includeReadableStrings, bool includeHexAscii)
    {
        if (includeReadableStrings || includeHexAscii)
        {
            return ReadBinaryPreview(path, ext, includeReadableStrings, includeHexAscii);
        }

        if (ext == ".epub") return "Không đọc được EPUB";
        if (ext is ".azw3" or ".fb2") return $"Không đọc được {ext.TrimStart('.').ToUpperInvariant()}";
        if (ext == ".ttf") return $"Tệp phông chữ: {System.IO.Path.GetFileName(path)}";
        if (ext is ".lrf") return "Tệp sách LRF (Sony Broadband eBook). Định dạng nhị phân này chưa có bộ giải mã nội dung tích hợp; bạn vẫn có thể mở bằng ứng dụng mặc định hoặc xem thông tin tệp ở phía trên.";
        if (ext is ".djvu" or ".djv") return "Tài liệu DjVu nhiều trang. Bộ kết xuất DjVu chưa được tích hợp trên máy này; hãy dùng nút Mở bằng ứng dụng để xem đầy đủ tài liệu.";
        if (ext is ".doc" or ".xls" or ".ppt") return ReadLegacyOffice(path, ext);
        if (ext == ".eml") return ReadEml(path);
        if (ext == ".json") return ReadJsonPreview(path);
        if (ext == ".zip") return ReadZipArchive(path);
        return ReadNotepadTextPreview(path);
    }

    private static string ReadEbookArchive(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var content = new StringBuilder();
        var count = 0;
        foreach (var entry in archive.Entries.Where(e => e.FullName.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase) || e.FullName.EndsWith(".html", StringComparison.OrdinalIgnoreCase)))
        {
            if (count++ >= MaxEbookPreviewEntries) break;
            if (entry.Length > 8 * 1024 * 1024) continue;
            using var reader = new StreamReader(entry.Open());
            var text = reader.ReadToEnd();
            text = System.Net.WebUtility.HtmlDecode(text);
            text = System.Text.RegularExpressions.Regex.Replace(text, "\\s+", " ").Trim();
            if (text.Length == 0) continue;
            if (content.Length > 0) content.Append(Environment.NewLine).Append(Environment.NewLine);
            content.Append(text);
            if (content.Length >= MaxTextPreviewCharacters) break;
        }

        return LimitPreview(content.ToString(), MaxTextPreviewCharacters, "EPUB");
    }
    private static string ReadZipArchive(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var content = new StringBuilder();
        var count = 0;
        var truncated = false;
        foreach (var entry in archive.Entries)
        {
            if (count++ >= MaxArchivePreviewEntries)
            {
                truncated = true;
                break;
            }
            if (content.Length > 0) content.AppendLine();
            content.Append(entry.FullName).Append("    ").Append(FormatSize(entry.Length));
        }

        return truncated
            ? content.AppendLine().Append("… [đã giới hạn 10.000 mục]").ToString()
            : content.ToString();
    }
    private static string ReadJsonPreview(string path)
    {
        if (new FileInfo(path).Length > MaxJsonPreviewBytes)
            return "Tệp JSON quá lớn để xem trước (giới hạn 8 MB). Hãy mở bằng ứng dụng mặc định.";

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return LimitPreview(JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }), MaxTextPreviewCharacters, "JSON");
    }
    private static string ReadNotepadTextPreview(string path)
    {
        string text;
        try
        {
            var encoding = DetectEncoding(path);
            text = ReadTextPreview(path, encoding);
        }
        catch (DecoderFallbackException)
        {
            // Fall back to single-byte ANSI/Latin1 decoding like Notepad when UTF-8 validation fails.
            text = ReadTextPreview(path, Encoding.Latin1);
        }

        return text.Contains('\0') ? text.Replace('\0', ' ') : text;
    }

    private static string ReadBinaryPreview(string path, string ext, bool includeReadableStrings, bool includeHexAscii)
    {
        const int maxDumpBytes = 32 * 1024;
        var fileInfo = new FileInfo(path);
        using var stream = File.OpenRead(path);
        var toRead = (int)Math.Min(maxDumpBytes, fileInfo.Length);
        var bytes = new byte[toRead];
        var read = stream.Read(bytes, 0, toRead);

        var sb = new StringBuilder();
        var description = ext switch
        {
            ".gpsidt" => "Gói dữ liệu định danh / tọa độ VSAT (.GPSIDT)",
            ".a_voc" => "Dữ liệu thoại / âm thanh thô (.A_VOC)",
            ".unk" => "Tệp dữ liệu nhị phân chưa xác định (.UNK)",
            ".x3" => "Tệp dữ liệu phiên (.X3)",
            ".777" => "Tệp dữ liệu phiên (.777)",
            _ => $"Tệp dữ liệu ({ext.ToUpperInvariant()})"
        };

        sb.AppendLine($"Định dạng: {description}");
        sb.AppendLine($"Kích thước: {FormatSize(fileInfo.Length)} ({fileInfo.Length:N0} bytes)");

        if (includeReadableStrings)
        {
            var strings = ExtractPrintableStrings(bytes.AsSpan(0, read));
            sb.AppendLine();
            if (strings.Count > 0)
            {
                sb.AppendLine("Chuỗi ký tự đọc được:");
                foreach (var s in strings.Take(40))
                {
                    sb.AppendLine($"  • {s}");
                }
            }
            else
            {
                sb.AppendLine("Chuỗi ký tự đọc được: (không có)");
            }
        }

        if (includeHexAscii)
        {
            sb.AppendLine();
            sb.AppendLine("Nội dung Hex / ASCII:");
            for (var offset = 0; offset < read; offset += 16)
            {
                sb.Append($"{offset:X8}  ");
                for (var i = 0; i < 16; i++)
                {
                    if (offset + i < read)
                    {
                        sb.Append($"{bytes[offset + i]:X2} ");
                    }
                    else
                    {
                        sb.Append("   ");
                    }
                    if (i == 7) sb.Append(' ');
                }

                sb.Append(" |");
                for (var i = 0; i < 16 && offset + i < read; i++)
                {
                    var b = bytes[offset + i];
                    sb.Append(b is >= 32 and <= 126 ? (char)b : '.');
                }
                sb.AppendLine("|");
            }

            if (fileInfo.Length > maxDumpBytes)
            {
                sb.AppendLine().Append($"… [đã giới hạn hiển thị {maxDumpBytes:N0} bytes đầu]");
            }
        }

        return sb.ToString();
    }

    private static List<string> ExtractPrintableStrings(ReadOnlySpan<byte> bytes)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        foreach (var b in bytes)
        {
            if (b is >= 32 and <= 126)
            {
                current.Append((char)b);
            }
            else
            {
                if (current.Length >= 4)
                {
                    result.Add(current.ToString());
                }
                current.Clear();
            }
        }

        if (current.Length >= 4)
        {
            result.Add(current.ToString());
        }

        return result;
    }

    private static string ReadTextPreview(string path, Encoding encoding)
    {
        using var reader = new StreamReader(path, encoding, detectEncodingFromByteOrderMarks: true);
        var buffer = new char[8192];
        var content = new StringBuilder();
        while (content.Length < MaxTextPreviewCharacters)
        {
            var read = reader.ReadBlock(buffer, 0, Math.Min(buffer.Length, MaxTextPreviewCharacters - content.Length));
            if (read == 0) break;
            content.Append(buffer, 0, read);
        }

        return content.Length == MaxTextPreviewCharacters && reader.Read() != -1
            ? content.Append($"{Environment.NewLine}{Environment.NewLine}… [đã giới hạn phần xem trước văn bản ở {MaxTextPreviewCharacters:N0} ký tự]").ToString()
            : content.ToString();
    }
    private static string LimitPreview(string content, int limit, string kind) =>
        content.Length <= limit ? content : content[..limit] + $"{Environment.NewLine}{Environment.NewLine}… [đã giới hạn phần xem trước {kind} ở {limit:N0} ký tự]";
    private static string ReadLegacyOffice(string path, string extension)
    {
        try
        {
            var type = Type.GetType("DotnetPoi.HWPF.UserModel.HWPFDocument, DotnetPoi.Legacy", false);
            if (extension == ".doc" && type is not null) { using var stream = File.OpenRead(path); dynamic doc = Activator.CreateInstance(type, stream)!; return (string)doc.getText(); }
        }
        catch (Exception ex) { return $"Không thể đọc tài liệu Office cũ: {ex.GetBaseException().Message}"; }
        return "Không thể trích xuất văn bản từ định dạng Office cũ này. Hãy mở bằng ứng dụng mặc định.";
    }
    private static string ReadEml(string path) { var raw = File.ReadAllText(path, Encoding.UTF8); var split = raw.IndexOf("\r\n\r\n", StringComparison.Ordinal); if (split < 0) split = raw.IndexOf("\n\n", StringComparison.Ordinal); return split < 0 ? raw : raw[..split] + Environment.NewLine + Environment.NewLine + raw[(split + (raw.AsSpan(split).StartsWith("\r\n\r\n") ? 4 : 2))..]; }
    private static Encoding DetectEncoding(string path) { using var f = File.OpenRead(path); Span<byte> b = stackalloc byte[4]; var n = f.Read(b); if (n >= 2 && b[0] == 0xff && b[1] == 0xfe) return Encoding.Unicode; if (n >= 2 && b[0] == 0xfe && b[1] == 0xff) return Encoding.BigEndianUnicode; if (n >= 3 && b[0] == 0xef && b[1] == 0xbb && b[2] == 0xbf) return Encoding.UTF8; return new UTF8Encoding(false, true); }
    private void ShowMessage(string message) { ContentHost.Children.Clear(); var text = new TextBlock { Text = message, Margin = new Thickness(24), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Foreground = (Brush)FindResource("MutedForegroundBrush") }; AutomationProperties.SetAutomationId(text, "PreviewLoadError"); ContentHost.Children.Add(text); }
    private static bool IsImage(string e) => new[] { ".bmp", ".cr2", ".cur", ".dds", ".dng", ".erf", ".exr", ".fts", ".gif", ".hdr", ".heic", ".heif", ".ico", ".jfif", ".jp2", ".jpe", ".jpeg", ".jpg", ".jps", ".mng", ".nef", ".nrw", ".orf", ".pam", ".pbm", ".pcd", ".pcx", ".pef", ".pes", ".pfm", ".pgm", ".picon", ".pict", ".png", ".pnm", ".ppm", ".psd", ".raf", ".ras", ".rw2", ".sfw", ".sgi", ".svg", ".tga", ".tif", ".tiff", ".wbmp", ".webp", ".wpg", ".x3f", ".xbm", ".xcf", ".xpm", ".xwd", ".wdp", ".jxr" }.Contains(e);
    private static bool IsRawImage(string e) => new[] { ".cr2", ".dng", ".erf", ".nef", ".nrw", ".orf", ".pef", ".raf", ".rw2", ".x3f" }.Contains(e, StringComparer.OrdinalIgnoreCase);
    private static bool IsAudio(string e) => new[] { ".8svx", ".aac", ".ac3", ".aiff", ".amb", ".au", ".avr", ".caf", ".cdda", ".cvs", ".cvsd", ".cvu", ".dts", ".dvms", ".fap", ".flac", ".fssd", ".gsrt", ".hcom", ".htk", ".ima", ".ircam", ".m4a", ".m4r", ".maud", ".mp2", ".mp3", ".nist", ".oga", ".ogg", ".opus", ".paf", ".prc", ".pvf", ".ra", ".sd2", ".sln", ".smp", ".snd", ".sndr", ".sndt", ".sou", ".sph", ".spx", ".tta", ".txw", ".vms", ".voc", ".vox", ".w64", ".wav", ".wma", ".wv", ".wve" }.Contains(e);
    private static bool IsVideo(string e) => new[] { ".3gp", ".asf", ".avi", ".f4v", ".flv", ".hevc", ".m2ts", ".m2v", ".m4v", ".mjpeg", ".mkv", ".mov", ".mp4", ".mpeg", ".mpg", ".mts", ".mxf", ".ogv", ".rm", ".swf", ".ts", ".vob", ".webm", ".wmv", ".wtv" }.Contains(e);
    private static string FormatSize(long n) => n < 1024 ? $"{n} B" : n < 1048576 ? $"{n / 1024d:0.0} KB" : $"{n / 1048576d:0.0} MB";
    private void StopCurrentMedia()
    {
        _previewGeneration++;
        _videoPositionTimer?.Stop();
        _videoPositionTimer = null;
        _audioPositionTimer?.Stop();
        _audioPositionTimer = null;
        _audioPlayer?.Close();
        _audioPlayer = null;
        _media?.Stop();
        _media = null;
        _vlcVideoPlayer?.Stop();
        _vlcVideoPlayer?.Dispose();
        _vlcVideoPlayer = null;
        _vlcMedia?.Dispose();
        _vlcMedia = null;
        _vlcVideoView?.Dispose();
        _vlcVideoView = null;
    }
    private void OnDrop(object sender, DragEventArgs e) { if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0) LoadFile(files[0]); }
    private void ZoomIn_Click(object s, RoutedEventArgs e) => SetZoom(Math.Min(6, _zoom * 1.2)); private void ZoomOut_Click(object s, RoutedEventArgs e) => SetZoom(Math.Max(.15, _zoom / 1.2)); private void ZoomReset_Click(object s, RoutedEventArgs e) => SetZoom(1);
    private void SetZoom(double z) { _zoom = z; if (_imageScale is not null) _imageScale.ScaleX = _imageScale.ScaleY = z; ZoomText.Text = $"{z:P0}"; }
    private void Image_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        SetImageZoom(_zoom + (e.Delta > 0 ? 0.1 : -0.1));
        e.Handled = true;
    }
    private void Magnifier_Click(object s, RoutedEventArgs e)
    {
        _magnifier = MagnifierToggle.IsChecked == true;
        if (_image != null) _image.Cursor = _magnifier ? Cursors.Cross : Cursors.Arrow;
        if (!_magnifier) CloseMagnifier();
    }

    private void Image_MouseMove(object s, MouseEventArgs e)
    {
        if (!_magnifier || _image?.Source is not BitmapSource b) return;
        var p = e.GetPosition(_image); var x = (int)(p.X * b.PixelWidth / Math.Max(1, _image.ActualWidth)); var y = (int)(p.Y * b.PixelHeight / Math.Max(1, _image.ActualHeight));
        if (x < 0 || y < 0 || x >= b.PixelWidth || y >= b.PixelHeight) return;
        const int cropSize = 48; var left = Math.Clamp(x - cropSize / 2, 0, Math.Max(0, b.PixelWidth - cropSize)); var top = Math.Clamp(y - cropSize / 2, 0, Math.Max(0, b.PixelHeight - cropSize));
        var crop = new CroppedBitmap(b, new Int32Rect(left, top, Math.Min(cropSize, b.PixelWidth - left), Math.Min(cropSize, b.PixelHeight - top)));
        if (_magnifierToolTip is null)
        {
            _magnifierPreview = new Image { Width = 192, Height = 192, Stretch = Stretch.Fill, SnapsToDevicePixels = true };
            var magnifierBorder = new Border
            {
                BorderThickness = new Thickness(2),
                Child = _magnifierPreview
            };
            magnifierBorder.SetResourceReference(Border.BorderBrushProperty, "FocusBrush");
            _magnifierToolTip = new ToolTip
            {
                Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse,
                PlacementTarget = _image,
                StaysOpen = true,
                Content = magnifierBorder
            };
        }

        _magnifierPreview!.Source = crop;
        if (!_magnifierToolTip.IsOpen) _magnifierToolTip.IsOpen = true;
    }

    private void Image_MouseLeave(object sender, MouseEventArgs e) => CloseMagnifier();

    private void CloseMagnifier()
    {
        if (_magnifierToolTip is not null) _magnifierToolTip.IsOpen = false;
        _magnifierToolTip = null;
        _magnifierPreview = null;
    }
    private void FindNext_Click(object s, RoutedEventArgs e) => FindNext(); private void SearchBox_KeyDown(object s, KeyEventArgs e) { if (e.Key == Key.Enter) FindNext(); }
    private void FindNext() { if (_textView == null || string.IsNullOrEmpty(SearchBox.Text)) return; var start = Math.Max(0, _textView.SelectionStart + _textView.SelectionLength); var i = _textView.Text.IndexOf(SearchBox.Text, start, StringComparison.CurrentCultureIgnoreCase); if (i < 0) i = _textView.Text.IndexOf(SearchBox.Text, StringComparison.CurrentCultureIgnoreCase); if (i >= 0) { _textView.Focus(); _textView.Select(i, SearchBox.Text.Length); } }
    private void WordWrap_Changed(object s, RoutedEventArgs e) { if (_textView != null) _textView.TextWrapping = WordWrapToggle.IsChecked == true ? TextWrapping.Wrap : TextWrapping.NoWrap; }
    private void CopyText_Click(object s, RoutedEventArgs e) { if (_textView != null) Clipboard.SetText(_textView.SelectedText.Length > 0 ? _textView.SelectedText : _textView.Text); }
    private void OpenDefault_Click(object s, RoutedEventArgs e) => Launch(_filePath); private void OpenFolder_Click(object s, RoutedEventArgs e) { if (_filePath != null) try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_filePath}\"") { UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show(ex.Message, "Không thể mở thư mục", MessageBoxButton.OK, MessageBoxImage.Error); } }
    private static void Launch(string? arg) { if (arg == null) return; try { Process.Start(new ProcessStartInfo(arg) { UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show(ex.Message, "Không thể mở tệp", MessageBoxButton.OK, MessageBoxImage.Error); } }
}
