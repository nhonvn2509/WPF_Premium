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
using MagickImage = ImageMagick.MagickImage;
using MagickFormat = ImageMagick.MagickFormat;
using UglyToad.PdfPig;
using Sdcb.LibRaw;

namespace WPF_Premium.Controls;

public partial class FilePreviewControl : UserControl
{
    private static readonly Lazy<LibVLCSharp.Shared.LibVLC> SharedVlc = new(() => { LibVLCSharp.Shared.Core.Initialize(); return new LibVLCSharp.Shared.LibVLC(enableDebugLogs: false); });
    private string? _filePath;
    private Image? _image;
    private MediaElement? _media;
    private MediaPlayer? _audioPlayer;
    private LibVLCSharp.Shared.MediaPlayer? _vlcVideoPlayer;
    private LibVLCSharp.Shared.Media? _vlcMedia;
    private LibVLCSharp.WPF.VideoView? _vlcVideoView;
    private DispatcherTimer? _audioPositionTimer;
    private DispatcherTimer? _videoPositionTimer;
    private bool _updatingAudioSeek;
    private TextBox? _textView;
    private double _zoom = 1;
    private bool _magnifier;
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
        _filePath = System.IO.Path.GetFullPath(path); _zoom = 1; _magnifier = false;
        FileNameText.Text = System.IO.Path.GetFileName(_filePath);
        var info = new FileInfo(_filePath);
        FileInfoText.Text = $"{System.IO.Path.GetExtension(path).TrimStart('.').ToUpperInvariant()} · {FormatSize(info.Length)} · {info.LastWriteTime:g}";
        StopCurrentMedia();
        CloseMagnifier();
        ContentHost.Children.Clear(); _image = null; _media = null; _textView = null;
        ImageTools.Visibility = TextTools.Visibility = Visibility.Collapsed; ZoomText.Visibility = Visibility.Visible;
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        try
        {
            if (IsImage(ext)) ShowImage(path);
            else if (IsAudio(ext)) ShowAudio(path);
            else if (IsVideo(ext)) ShowVideo(path);
            else ShowText(path, ext);
        }
        catch (Exception ex) { ShowMessage($"Không thể tạo bản xem trước: {ex.Message}"); }
    }

    private void ShowImage(string path)
    {
        var viewer = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, CanContentScroll = false };
        _image = new Image { Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, RenderTransformOrigin = new Point(.5, .5) };
        AutomationProperties.SetAutomationId(_image, "PreviewImage");
        _image.RenderTransform = new ScaleTransform(1, 1); _image.MouseMove += Image_MouseMove; _image.MouseLeave += Image_MouseLeave;
        var status = new TextBlock { Text = "Đang giải mã hình ảnh…", Foreground = (Brush)FindResource("MutedForegroundBrush"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetAutomationId(status, "PreviewImageStatus");
        var imageHost = new Grid(); imageHost.Children.Add(_image); imageHost.Children.Add(status);
        viewer.Content = imageHost; ContentHost.Children.Add(viewer); ImageTools.Visibility = Visibility.Visible; ZoomText.Text = "100%";
        _ = Task.Run(() => DecodeImage(path)).ContinueWith(task => Dispatcher.BeginInvoke(() =>
        {
            if (task.IsCompletedSuccessfully)
            {
                _image!.Source = task.Result;
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
        var bytes = File.ReadAllBytes(path); var n = Math.Min(800, Math.Max(120, (int)(ActualWidth - 70))); var stride = Math.Max(1, bytes.Length / n);
        for (var i = 0; i < n; i++) { var amp = .08 + (bytes[Math.Min(i * stride, bytes.Length - 1)] / 255d) * .78; var h = amp * 145; wave.Children.Add(new Line { X1 = i, X2 = i, Y1 = (180 - h) / 2, Y2 = (180 + h) / 2, Stroke = new SolidColorBrush(Color.FromRgb(76, 164, 245)), StrokeThickness = 1.2 }); }
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
        AutomationProperties.SetAutomationId(audioStatus, "AudioWaveform");
        AutomationProperties.SetName(audioStatus, "Dạng sóng âm thanh");
        var seek = new Slider { Minimum = 0, Maximum = 100, Value = 0, VerticalAlignment = VerticalAlignment.Center, MinWidth = 160, IsEnabled = false };
        AutomationProperties.SetAutomationId(seek, "AudioSeekSlider");
        var playbackTime = new TextBlock
        {
            Text = "00:00",
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetAutomationId(playbackTime, "AudioPlaybackTimeText");
        _audioPlayer = new MediaPlayer { Volume = .7 };
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
        _ = Task.Run(() => SharedVlc.Value).ContinueWith(task => Dispatcher.BeginInvoke(() =>
        {
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
        if (ext == ".docx") content = ReadDocx(path);
        else if (ext == ".xlsx" || ext == ".xlsm") content = ReadXlsx(path);
        else if (ext == ".pdf") content = ReadPdfText(path);
        else if (ext is ".odt" or ".ott") content = ReadOpenDocument(path, "content.xml");
        else if (ext is ".ods") content = ReadOpenDocument(path, "content.xml");
        else if (ext is ".odp") content = ReadOpenDocument(path, "content.xml");
        else if (ext is ".epub") content = ReadEbookArchive(path);
        else if (ext is ".lrf") content = "Tệp sách LRF (Sony Broadband eBook). Định dạng nhị phân này chưa có bộ giải mã nội dung tích hợp; bạn vẫn có thể mở bằng ứng dụng mặc định hoặc xem thông tin tệp ở phía trên.";
        else if (ext is ".djvu" or ".djv") content = "Tài liệu DjVu nhiều trang. Bộ kết xuất DjVu chưa được tích hợp trên máy này; hãy dùng nút Mở bằng ứng dụng để xem đầy đủ tài liệu.";
        else if (ext is ".doc" or ".xls" or ".ppt") content = ReadLegacyOffice(path, ext);
        else if (ext == ".doc" || ext == ".xls" || ext == ".ppt" || ext == ".pptx") content = "Định dạng Office này cần ứng dụng/engine Office để render chính xác. Hãy dùng 'Mở bằng ứng dụng'.";
        else if (ext == ".eml") content = ReadEml(path);
        else if (ext == ".json") { using var doc = JsonDocument.Parse(File.ReadAllText(path)); content = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }); }
        else if (ext == ".zip") content = string.Join(Environment.NewLine, ZipFile.OpenRead(path).Entries.Select(e => $"{e.FullName}    {FormatSize(e.Length)}"));
        else content = File.ReadAllText(path, DetectEncoding(path));
        _textView = new TextBox { Text = content, IsReadOnly = true, AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new FontFamily("Consolas"), FontSize = 13, Padding = new Thickness(16), BorderThickness = new Thickness(0), Background = Brushes.Transparent };
        AutomationProperties.SetAutomationId(_textView, "PreviewTextContent");
        ContentHost.Children.Add(_textView); TextTools.Visibility = Visibility.Visible; ZoomText.Visibility = Visibility.Collapsed;
    }

    private static string ReadDocx(string path) { using var z = ZipFile.OpenRead(path); using var s = z.GetEntry("word/document.xml")!.Open(); var d = System.Xml.Linq.XDocument.Load(s); return string.Join(Environment.NewLine, d.Descendants().Where(x => x.Name.LocalName == "p").Select(p => string.Concat(p.Descendants().Where(x => x.Name.LocalName == "t").Select(t => t.Value)))); }
    private static string ReadPdfText(string path)
    {
        try
        {
            using var document = PdfDocument.Open(path);
            return string.Join(Environment.NewLine + Environment.NewLine, document.GetPages().Select(page => $"Trang {page.Number}{Environment.NewLine}{page.Text}"));
        }
        catch (Exception ex) { return $"Không thể trích xuất văn bản PDF: {ex.Message}"; }
    }
    private static string ReadOpenDocument(string path, string entryName)
    {
        using var archive = ZipFile.OpenRead(path);
        var entry = archive.GetEntry(entryName);
        if (entry is null) return "Không tìm thấy nội dung trong tài liệu OpenDocument.";
        using var stream = entry.Open();
        var xml = System.Xml.Linq.XDocument.Load(stream);
        return string.Join(Environment.NewLine, xml.Descendants().Where(x => x.Name.LocalName is "p" or "h").Select(x => x.Value));
    }
    private static string ReadEbookArchive(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        return string.Join(Environment.NewLine + Environment.NewLine, archive.Entries.Where(e => e.FullName.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase) || e.FullName.EndsWith(".html", StringComparison.OrdinalIgnoreCase)).Select(e => { using var reader = new StreamReader(e.Open()); var doc = System.Xml.Linq.XDocument.Parse(reader.ReadToEnd(), System.Xml.Linq.LoadOptions.None); return string.Join(" ", doc.DescendantNodes().OfType<System.Xml.Linq.XText>().Select(t => t.Value.Trim()).Where(t => t.Length > 0)); }));
    }
    private static string ReadLegacyOffice(string path, string extension)
    {
        try
        {
            var type = Type.GetType("NPOI.HWPF.HWPFDocument, NPOI", false);
            if (extension == ".doc" && type is not null) { using var stream = File.OpenRead(path); dynamic doc = Activator.CreateInstance(type, stream)!; return (string)doc.GetText().Text; }
        }
        catch (Exception ex) { return $"Không thể đọc tài liệu Office cũ: {ex.Message}"; }
        return "Không thể trích xuất văn bản từ định dạng Office cũ này. Hãy mở bằng ứng dụng mặc định.";
    }
    private static string ReadXlsx(string path) { using var z = ZipFile.OpenRead(path); var strings = new List<string>(); var se = z.GetEntry("xl/sharedStrings.xml"); if (se != null) { using var s = se.Open(); var d = System.Xml.Linq.XDocument.Load(s); strings = d.Descendants().Where(x => x.Name.LocalName == "si").Select(si => string.Concat(si.Descendants().Where(x => x.Name.LocalName == "t").Select(t => t.Value))).ToList(); } var sheet = z.GetEntry("xl/worksheets/sheet1.xml"); if (sheet == null) return "Không tìm thấy sheet đầu tiên."; using var stream = sheet.Open(); var xml = System.Xml.Linq.XDocument.Load(stream); return string.Join(Environment.NewLine, xml.Descendants().Where(x => x.Name.LocalName == "row").Select(row => string.Join("\t", row.Elements().Select(c => { var v = c.Elements().FirstOrDefault(x => x.Name.LocalName == "v")?.Value ?? ""; return c.Attribute("t")?.Value == "s" && int.TryParse(v, out var i) && i < strings.Count ? strings[i] : v; })))); }
    private static string ReadEml(string path) { var raw = File.ReadAllText(path, Encoding.UTF8); var split = raw.IndexOf("\r\n\r\n", StringComparison.Ordinal); if (split < 0) split = raw.IndexOf("\n\n", StringComparison.Ordinal); return split < 0 ? raw : raw[..split] + Environment.NewLine + Environment.NewLine + raw[(split + (raw.AsSpan(split).StartsWith("\r\n\r\n") ? 4 : 2))..]; }
    private static string ExtractPdfText(string path) { try { var bytes = File.ReadAllBytes(path); return Encoding.Latin1.GetString(bytes).Split(new[] { "stream", "endstream" }, StringSplitOptions.None).SelectMany(s => System.Text.RegularExpressions.Regex.Matches(s, @"\(([^()]*)\)").Select(m => m.Groups[1].Value)).Where(s => s.Any(char.IsLetter)).Take(300).Aggregate(new StringBuilder(), (b, s) => b.AppendLine(s)).ToString(); } catch { return ""; } }
    private static Encoding DetectEncoding(string path) { using var f = File.OpenRead(path); Span<byte> b = stackalloc byte[4]; var n = f.Read(b); if (n >= 2 && b[0] == 0xff && b[1] == 0xfe) return Encoding.Unicode; if (n >= 2 && b[0] == 0xfe && b[1] == 0xff) return Encoding.BigEndianUnicode; if (n >= 3 && b[0] == 0xef && b[1] == 0xbb && b[2] == 0xbf) return Encoding.UTF8; return new UTF8Encoding(false, true); }
    private void ShowMessage(string message) { ContentHost.Children.Clear(); var text = new TextBlock { Text = message, Margin = new Thickness(24), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Foreground = (Brush)FindResource("MutedForegroundBrush") }; AutomationProperties.SetAutomationId(text, "PreviewLoadError"); ContentHost.Children.Add(text); }
    private static bool IsImage(string e) => new[] { ".bmp", ".cr2", ".cur", ".dds", ".dng", ".erf", ".exr", ".fts", ".gif", ".hdr", ".heic", ".heif", ".ico", ".jfif", ".jp2", ".jpe", ".jpeg", ".jpg", ".jps", ".mng", ".nef", ".nrw", ".orf", ".pam", ".pbm", ".pcd", ".pcx", ".pef", ".pes", ".pfm", ".pgm", ".picon", ".pict", ".png", ".pnm", ".ppm", ".psd", ".raf", ".ras", ".rw2", ".sfw", ".sgi", ".svg", ".tga", ".tif", ".tiff", ".wbmp", ".webp", ".wpg", ".x3f", ".xbm", ".xcf", ".xpm", ".xwd", ".wdp", ".jxr" }.Contains(e);
    private static bool IsRawImage(string e) => new[] { ".cr2", ".dng", ".erf", ".nef", ".nrw", ".orf", ".pef", ".raf", ".rw2", ".x3f" }.Contains(e, StringComparer.OrdinalIgnoreCase);
    private static bool IsAudio(string e) => new[] { ".8svx", ".aac", ".ac3", ".aiff", ".amb", ".au", ".avr", ".caf", ".cdda", ".cvs", ".cvsd", ".cvu", ".dts", ".dvms", ".fap", ".flac", ".fssd", ".gsrt", ".hcom", ".htk", ".ima", ".ircam", ".m4a", ".m4r", ".maud", ".mp2", ".mp3", ".nist", ".oga", ".ogg", ".opus", ".paf", ".prc", ".pvf", ".ra", ".sd2", ".sln", ".smp", ".snd", ".sndr", ".sndt", ".sou", ".sph", ".spx", ".tta", ".txw", ".vms", ".voc", ".vox", ".w64", ".wav", ".wma", ".wv", ".wve" }.Contains(e);
    private static bool IsVideo(string e) => new[] { ".3gp", ".asf", ".avi", ".f4v", ".flv", ".hevc", ".m2ts", ".m2v", ".m4v", ".mjpeg", ".mkv", ".mov", ".mp4", ".mpeg", ".mpg", ".mts", ".mxf", ".ogv", ".rm", ".swf", ".ts", ".vob", ".webm", ".wmv", ".wtv" }.Contains(e);
    private static string FormatSize(long n) => n < 1024 ? $"{n} B" : n < 1048576 ? $"{n / 1024d:0.0} KB" : $"{n / 1048576d:0.0} MB";
    private void StopCurrentMedia()
    {
        _videoPositionTimer?.Stop();
        _videoPositionTimer = null;
        _audioPositionTimer?.Stop();
        _audioPositionTimer = null;
        _audioPlayer?.Close();
        _audioPlayer = null;
        _media?.Stop();
        _media = null;
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
