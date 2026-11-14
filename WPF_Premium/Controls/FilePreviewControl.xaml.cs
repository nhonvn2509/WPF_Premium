using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using MahApps.Metro.IconPacks;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Wpf;
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
    private MediaElement? _media;
    private MediaPlayer? _audioPlayer;
    private LibVLCSharp.Shared.MediaPlayer? _vlcVideoPlayer;
    private LibVLCSharp.Shared.Media? _vlcMedia;
    private LibVLCSharp.WPF.VideoView? _vlcVideoView;
    private WebView2? _documentBrowser;
    private DispatcherTimer? _audioPositionTimer;
    private DispatcherTimer? _videoPositionTimer;
    private bool _updatingAudioSeek;
    private TextBox? _textView;
    private double _zoom = 1;
    private bool _magnifier;
    private long _previewGeneration;
    private ToolTip? _magnifierToolTip;
    private Image? _magnifierPreview;

    public FilePreviewControl()
    {
        InitializeComponent();
        AllowDrop = true;
        Drop += OnDrop;
        Unloaded += (_, _) => StopCurrentMedia();
    }

    public string? FilePath
    {
        get => _filePath;
        set { if (string.IsNullOrWhiteSpace(value)) return; LoadFile(value); }
    }

    public void Browse()
    {
        var dialog = new OpenFileDialog { Title = "Chọn tệp xem trước", Filter = "Tất cả tệp (*.*)|*.*" };
        if (dialog.ShowDialog() == true) LoadFile(dialog.FileName);
    }

    public void LoadFile(string path)
    {
        if (!File.Exists(path)) { ShowMessage("Không tìm thấy tệp."); return; }
        StopCurrentMedia();
        var generation = _previewGeneration;
        _filePath = System.IO.Path.GetFullPath(path); _zoom = 1; _magnifier = false;
        var fileName = System.IO.Path.GetFileName(_filePath);
        var extension = System.IO.Path.GetExtension(_filePath);
        FileNameText.Text = System.IO.Path.GetFileNameWithoutExtension(_filePath);
        FileExtensionText.Text = extension.ToUpperInvariant();
        var info = new FileInfo(_filePath);
        FileSizeText.Text = FormatSize(info.Length);
        AutomationProperties.SetName(FileNameText, fileName);
        CloseDocumentBrowser();
        CloseMagnifier();
        ContentHost.Children.Clear(); _image = null; _media = null; _textView = null;
        ImageTools.Visibility = TextTools.Visibility = Visibility.Collapsed; ZoomText.Visibility = Visibility.Visible;
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        try
        {
            if (IsImage(ext)) ShowImage(path);
            else if (IsAudio(ext)) ShowAudio(path);
            else if (IsVideo(ext)) ShowVideo(path);
            else if (ext == ".pdf") ShowDocument(path, null);
            else if (IsOfficeDocument(ext)) ShowOfficeDocument(path, ext);
            else ShowText(path, ext);
        }
        catch (Exception ex) { ShowMessage($"Không thể tạo bản xem trước: {ex.Message}"); }
    }

    private void ShowDocument(string path, string? html)
    {
        var browser = new WebView2();
        AutomationProperties.SetAutomationId(browser, "PreviewDocumentBrowser");
        AutomationProperties.SetName(browser, html is null ? "Trình xem PDF" : "Bản xem trước tài liệu Office");
        RoutedEventHandler loaded = null!;
        loaded = async (_, _) =>
        {
            browser.Loaded -= loaded;
            if (ReferenceEquals(_documentBrowser, browser))
                await InitializeDocumentBrowserAsync(browser, path, html);
        };
        browser.Loaded += loaded;
        _documentBrowser = browser;
        ContentHost.Children.Add(browser);
        ZoomText.Text = html is null ? "PDF" : "Office";
    }

    private void ShowOfficeDocument(string path, string extension)
    {
        try
        {
            ShowDocument(path, OfficePreviewHtmlRenderer.Render(path));
        }
        catch (Exception) when (extension is ".doc" or ".xls" or ".ppt")
        {
            ShowText(path, extension);
        }
    }

    private async Task InitializeDocumentBrowserAsync(WebView2 browser, string path, string? html)
    {
        try
        {
            await browser.EnsureCoreWebView2Async();
            browser.CoreWebView2.Settings.IsScriptEnabled = html is null;
            browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            browser.CoreWebView2.NavigationStarting += (_, args) =>
            {
                if (!Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri)) return;
                if (uri.IsFile &&
                    !System.IO.Path.GetFullPath(uri.LocalPath).Equals(System.IO.Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
                {
                    args.Cancel = true;
                }
                else if (uri.Scheme is "http" or "https" or "mailto" or "ftp")
                {
                    args.Cancel = true;
                }
            };
            if (html is null) browser.Source = new Uri(path);
            else browser.NavigateToString(html);
        }
        catch (Exception ex)
        {
            if (!ReferenceEquals(_documentBrowser, browser)) return;
            ContentHost.Children.Clear();
            _documentBrowser = null;
            ShowMessage($"Không thể khởi tạo trình xem tài liệu: {ex.Message}");
        }
    }

    private void CloseDocumentBrowser()
    {
        _documentBrowser?.Dispose();
        _documentBrowser = null;
    }

    private static bool IsOfficeDocument(string extension) =>
        extension is ".docx" or ".docm" or ".xlsx" or ".xlsm" or ".pptx" or
            ".doc" or ".xls" or ".ppt" or ".odt" or ".ott" or ".ods" or ".odp";

    private void ShowImage(string path)
    {
        var viewer = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, CanContentScroll = false };
        var image = new Image { Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, RenderTransformOrigin = new Point(.5, .5) };
        _image = image;
        AutomationProperties.SetAutomationId(_image, "PreviewImage");
        _image.RenderTransform = new ScaleTransform(1, 1); _image.MouseMove += Image_MouseMove; _image.MouseLeave += Image_MouseLeave;
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
        var root = new Grid { Margin = new Thickness(22) }; root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var wave = new Canvas { Height = 180, Background = new SolidColorBrush(Color.FromRgb(25, 28, 35)), ClipToBounds = true };
        AutomationProperties.SetAutomationId(wave, "AudioWaveform");
        AutomationProperties.SetName(wave, "Dạng sóng âm thanh");
        var extension = System.IO.Path.GetExtension(path);
        var isMidi = extension is ".mid" or ".midi";
        var n = Math.Min(800, Math.Max(120, (int)(ActualWidth - 70)));
        if (!isMidi)
        {
            using var audio = File.OpenRead(path);
            var length = audio.Length;
            for (var i = 0; i < n; i++)
            {
                var sample = 0;
                if (length > 0)
                {
                    audio.Position = (long)i * length / n;
                    sample = Math.Max(0, audio.ReadByte());
                }

                var amp = .08 + (sample / 255d) * .78;
                var h = amp * 145;
                wave.Children.Add(new Line { X1 = i, X2 = i, Y1 = (180 - h) / 2, Y2 = (180 + h) / 2, Stroke = new SolidColorBrush(Color.FromRgb(76, 164, 245)), StrokeThickness = 1.2 });
            }
        }
        root.Children.Add(wave);
        var controls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 12, 0, 0) };
        var audioStatus = new TextBlock
        {
            Text = "Âm thanh · kéo thanh để tua",
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 250,
            Foreground = (Brush)FindResource("MutedForegroundBrush")
        };
        AutomationProperties.SetAutomationId(audioStatus, "AudioStatusText");
        AutomationProperties.SetName(audioStatus, "Trạng thái âm thanh");
        var seek = new Slider { Minimum = 0, Maximum = 100, Value = 0, VerticalAlignment = VerticalAlignment.Center, MinWidth = 160, IsEnabled = !isMidi };
        AutomationProperties.SetAutomationId(seek, "AudioSeekSlider");
        var playbackTime = new TextBlock
        {
            Text = "00:00",
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetAutomationId(playbackTime, "AudioPlaybackTimeText");
        _audioPlayer = new MediaPlayer { Volume = .7 };
        if (isMidi)
        {
            audioStatus.Text = "Định dạng MIDI không có dạng sóng trực quan. Hãy mở tệp bằng ứng dụng mặc định.";
        }
        var isAudioPlaying = false;
        _audioPlayer.MediaOpened += (_, _) =>
        {
            if (_audioPlayer.NaturalDuration.HasTimeSpan)
            {
                seek.Maximum = Math.Max(1, _audioPlayer.NaturalDuration.TimeSpan.TotalSeconds);
                seek.IsEnabled = true;
            }
        };
        _audioPlayer.MediaEnded += (_, _) =>
        {
            isAudioPlaying = false;
            _audioPlayer.Position = TimeSpan.Zero;
            seek.Value = 0;
        };
        _audioPlayer.MediaFailed += (_, _) =>
        {
            audioStatus.Text = "Không thể phát định dạng âm thanh này. Hãy mở tệp bằng ứng dụng mặc định.";
            audioStatus.Foreground = (Brush)FindResource("DestructiveBrush");
        };
        var playIcon = new PackIconMaterial { Kind = PackIconMaterialKind.Play };
        var play = new Button { Content = playIcon, ToolTip = "Phát / tạm dừng", Margin = new Thickness(0, 0, 8, 0) };
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
                _audioPlayer!.Play();
                playIcon.Kind = PackIconMaterialKind.Pause;
                AutomationProperties.SetName(play, "Tạm dừng");
                isAudioPlaying = true;
            }
        };
        AutomationProperties.SetAutomationId(play, "AudioPlayButton");
        var stop = new Button { Content = new PackIconMaterial { Kind = PackIconMaterialKind.Stop }, ToolTip = "Dừng", Style = (Style)FindResource("ButtonOutline"), Margin = new Thickness(0, 0, 14, 0) }; AutomationProperties.SetName(stop, "Dừng"); stop.Click += (_, _) => { _audioPlayer!.Stop(); _audioPlayer.Position = TimeSpan.Zero; seek.Value = 0; playIcon.Kind = PackIconMaterialKind.Play; AutomationProperties.SetName(play, "Phát"); isAudioPlaying = false; };
        AutomationProperties.SetAutomationId(stop, "AudioStopButton");
        seek.ValueChanged += (_, _) =>
        {
            if (!_updatingAudioSeek && _audioPlayer is not null && Math.Abs(_audioPlayer.Position.TotalSeconds - seek.Value) > 1)
            {
                _audioPlayer.Position = TimeSpan.FromSeconds(seek.Value);
            }
        };
        var volume = new Slider { Minimum = 0, Maximum = 1, Value = .7, Width = 80, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(volume, "Âm lượng");
        volume.ValueChanged += (_, _) => { if (_audioPlayer is not null) _audioPlayer.Volume = volume.Value; };
        _audioPositionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _audioPositionTimer.Tick += (_, _) =>
        {
            if (_audioPlayer is null || !_audioPlayer.NaturalDuration.HasTimeSpan || seek.IsMouseCaptureWithin)
            {
                return;
            }

            _updatingAudioSeek = true;
            var position = _audioPlayer.Position;
            seek.Value = Math.Clamp(position.TotalSeconds, seek.Minimum, seek.Maximum);
            playbackTime.Text = position.ToString(@"mm\:ss");
            _updatingAudioSeek = false;
        };
        _audioPositionTimer.Start();
        controls.Children.Add(play);
        controls.Children.Add(stop);
        controls.Children.Add(seek);
        controls.Children.Add(playbackTime);
        controls.Children.Add(volume);
        controls.Children.Add(audioStatus);
        Grid.SetRow(controls, 1); root.Children.Add(controls); ContentHost.Children.Add(root);
        _audioPlayer.Open(new Uri(path));
        ZoomText.Text = "Dạng sóng trực quan";
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
        var play = new Button { Content = videoPlayIcon, ToolTip = "Phát / tạm dừng", Margin = new Thickness(0, 0, 10, 0) };
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
        string content;
        if (ext == ".epub") content = "Không đọc được EPUB";
        else if (ext is ".azw3" or ".fb2") content = $"Không đọc được {ext.TrimStart('.').ToUpperInvariant()}";
        else if (ext == ".ttf") content = $"Tệp phông chữ: {System.IO.Path.GetFileName(path)}";
        else if (ext is ".lrf") content = "Tệp sách LRF (Sony Broadband eBook). Định dạng nhị phân này chưa có bộ giải mã nội dung tích hợp; bạn vẫn có thể mở bằng ứng dụng mặc định hoặc xem thông tin tệp ở phía trên.";
        else if (ext is ".djvu" or ".djv") content = "Tài liệu DjVu nhiều trang. Bộ kết xuất DjVu chưa được tích hợp trên máy này; hãy dùng nút Mở bằng ứng dụng để xem đầy đủ tài liệu.";
        else if (ext is ".doc" or ".xls" or ".ppt") content = ReadLegacyOffice(path, ext);
        else if (ext == ".eml") content = ReadEml(path);
        else if (ext == ".json") content = ReadJsonPreview(path);
        else if (ext == ".zip") content = ReadZipArchive(path);
        else content = ReadTextPreview(path, DetectEncoding(path));
        _textView = new TextBox { Text = content, IsReadOnly = true, AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new FontFamily("Consolas"), FontSize = 13, Padding = new Thickness(16), BorderThickness = new Thickness(0), Background = Brushes.Transparent };
        AutomationProperties.SetAutomationId(_textView, "PreviewTextContent");
        ContentHost.Children.Add(_textView); TextTools.Visibility = Visibility.Visible; ZoomText.Visibility = Visibility.Collapsed;
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
    private void SetZoom(double z) { _zoom = z; if (_image?.RenderTransform is ScaleTransform t) t.ScaleX = t.ScaleY = z; ZoomText.Text = $"{z:P0}"; }
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
            _magnifierToolTip = new ToolTip
            {
                Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse,
                PlacementTarget = _image,
                StaysOpen = true,
                Content = new Border
                {
                    BorderBrush = Brushes.DodgerBlue,
                    BorderThickness = new Thickness(2),
                    Child = _magnifierPreview
                }
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
