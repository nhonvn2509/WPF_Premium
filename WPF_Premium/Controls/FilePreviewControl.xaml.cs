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

namespace WPF_Premium.Controls;

public partial class FilePreviewControl : UserControl
{
    private string? _filePath;
    private Image? _image;
    private MediaElement? _media;
    private MediaPlayer? _audioPlayer;
    private DispatcherTimer? _audioPositionTimer;
    private bool _updatingAudioSeek;
    private TextBox? _textView;
    private double _zoom = 1;
    private bool _magnifier;

    public FilePreviewControl() { InitializeComponent(); AllowDrop = true; Drop += OnDrop; }

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
        _audioPositionTimer?.Stop();
        _audioPositionTimer = null;
        _audioPlayer?.Close();
        _audioPlayer = null;
        _media?.Stop();
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
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.UriSource = new Uri(path); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.EndInit(); bitmap.Freeze();
        var viewer = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, CanContentScroll = false };
        _image = new Image { Source = bitmap, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, RenderTransformOrigin = new Point(.5, .5) };
        AutomationProperties.SetAutomationId(_image, "PreviewImage");
        _image.RenderTransform = new ScaleTransform(1, 1); _image.MouseMove += Image_MouseMove;
        viewer.Content = _image; ContentHost.Children.Add(viewer); ImageTools.Visibility = Visibility.Visible; ZoomText.Text = "100%";
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
        _audioPlayer = new MediaPlayer { Volume = .7 };
        _audioPlayer.MediaOpened += (_, _) =>
        {
            if (_audioPlayer.NaturalDuration.HasTimeSpan)
            {
                seek.Maximum = Math.Max(1, _audioPlayer.NaturalDuration.TimeSpan.TotalSeconds);
                seek.IsEnabled = true;
            }
        };
        _audioPlayer.MediaFailed += (_, e) => ShowMessage($"Không thể phát tệp âm thanh: {e.ErrorException?.Message ?? "định dạng hoặc bộ giải mã không được hỗ trợ"}");
        _audioPlayer.Open(new Uri(path));
        var play = new Button { Content = new PackIconMaterial { Kind = PackIconMaterialKind.Play }, ToolTip = "Phát / tạm dừng", Margin = new Thickness(0, 0, 8, 0) }; AutomationProperties.SetName(play, "Phát hoặc tạm dừng"); play.Click += (_, _) => { if (_audioPlayer!.CanPause) _audioPlayer.Pause(); else _audioPlayer.Play(); };
        AutomationProperties.SetAutomationId(play, "AudioPlayButton");
        var stop = new Button { Content = new PackIconMaterial { Kind = PackIconMaterialKind.Stop }, ToolTip = "Dừng", Style = (Style)FindResource("ButtonOutline"), Margin = new Thickness(0, 0, 14, 0) }; AutomationProperties.SetName(stop, "Dừng"); stop.Click += (_, _) => { _audioPlayer!.Stop(); _audioPlayer.Position = TimeSpan.Zero; seek.Value = 0; };
        AutomationProperties.SetAutomationId(stop, "AudioStopButton");
        var seek = new Slider { Minimum = 0, Maximum = 100, Value = 0, VerticalAlignment = VerticalAlignment.Center, MinWidth = 160, IsEnabled = false };
        AutomationProperties.SetAutomationId(seek, "AudioSeekSlider");
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
            seek.Value = Math.Clamp(_audioPlayer.Position.TotalSeconds, seek.Minimum, seek.Maximum);
            _updatingAudioSeek = false;
        };
        _audioPositionTimer.Start();
        controls.Children.Add(play);
        controls.Children.Add(stop);
        controls.Children.Add(seek);
        controls.Children.Add(volume);
        var waveformDescription = new TextBlock
        {
            Text = "  Âm thanh · kéo thanh để tua",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)FindResource("MutedForegroundBrush")
        };
        AutomationProperties.SetAutomationId(waveformDescription, "AudioWaveform");
        AutomationProperties.SetName(waveformDescription, "Dạng sóng âm thanh");
        controls.Children.Add(waveformDescription);
        Grid.SetRow(controls, 1); root.Children.Add(controls); root.Children.Add(_media); ContentHost.Children.Add(root);
        ZoomText.Text = "Dạng sóng trực quan";
    }

    private void ShowVideo(string path)
    {
        var grid = new Grid { Background = Brushes.Black };
        _media = new MediaElement { Source = new Uri(path), LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Stop, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        grid.Children.Add(_media);
        var bar = new DockPanel { VerticalAlignment = VerticalAlignment.Bottom, Background = new SolidColorBrush(Color.FromArgb(210, 20, 20, 24)), Margin = new Thickness(14), LastChildFill = true };
        var play = new Button { Content = new PackIconMaterial { Kind = PackIconMaterialKind.Play }, ToolTip = "Phát / tạm dừng", Margin = new Thickness(0, 0, 10, 0) }; AutomationProperties.SetName(play, "Phát hoặc tạm dừng"); play.Click += (_, _) => { if (_media!.CanPause) _media.Pause(); else _media.Play(); };
        AutomationProperties.SetAutomationId(play, "VideoPlayButton");
        var seek = new Slider { Minimum = 0, Maximum = 100, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetAutomationId(seek, "VideoSeekSlider");
        _media.MediaOpened += (_, _) => { seek.Maximum = Math.Max(1, _media.NaturalDuration.TimeSpan.TotalSeconds); }; seek.ValueChanged += (_, _) => { if (_media!.NaturalDuration.HasTimeSpan && Math.Abs(_media.Position.TotalSeconds - seek.Value) > 1) _media.Position = TimeSpan.FromSeconds(seek.Value); };
        var vol = new Slider { Minimum = 0, Maximum = 1, Value = .7, Width = 90, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }; vol.ValueChanged += (_, _) => { if (_media != null) _media.Volume = vol.Value; };
        AutomationProperties.SetAutomationId(vol, "VideoVolumeSlider");
        DockPanel.SetDock(play, Dock.Left); DockPanel.SetDock(vol, Dock.Right); bar.Children.Add(play); bar.Children.Add(vol); bar.Children.Add(seek); grid.Children.Add(bar); ContentHost.Children.Add(grid); ZoomText.Text = "Video";
    }

    private void ShowText(string path, string ext)
    {
        string content;
        if (ext == ".docx") content = ReadDocx(path);
        else if (ext == ".xlsx" || ext == ".xlsm") content = ReadXlsx(path);
        else if (ext == ".pdf") content = "PDF cần bộ đọc PDF tích hợp để hiển thị nội dung. Hãy dùng 'Mở bằng ứng dụng' để xem đầy đủ.\n\n" + ExtractPdfText(path);
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
    private static string ReadXlsx(string path) { using var z = ZipFile.OpenRead(path); var strings = new List<string>(); var se = z.GetEntry("xl/sharedStrings.xml"); if (se != null) { using var s = se.Open(); var d = System.Xml.Linq.XDocument.Load(s); strings = d.Descendants().Where(x => x.Name.LocalName == "si").Select(si => string.Concat(si.Descendants().Where(x => x.Name.LocalName == "t").Select(t => t.Value))).ToList(); } var sheet = z.GetEntry("xl/worksheets/sheet1.xml"); if (sheet == null) return "Không tìm thấy sheet đầu tiên."; using var stream = sheet.Open(); var xml = System.Xml.Linq.XDocument.Load(stream); return string.Join(Environment.NewLine, xml.Descendants().Where(x => x.Name.LocalName == "row").Select(row => string.Join("\t", row.Elements().Select(c => { var v = c.Elements().FirstOrDefault(x => x.Name.LocalName == "v")?.Value ?? ""; return c.Attribute("t")?.Value == "s" && int.TryParse(v, out var i) && i < strings.Count ? strings[i] : v; })))); }
    private static string ReadEml(string path) { var raw = File.ReadAllText(path, Encoding.UTF8); var split = raw.IndexOf("\r\n\r\n", StringComparison.Ordinal); if (split < 0) split = raw.IndexOf("\n\n", StringComparison.Ordinal); return split < 0 ? raw : raw[..split] + Environment.NewLine + Environment.NewLine + raw[(split + (raw.AsSpan(split).StartsWith("\r\n\r\n") ? 4 : 2))..]; }
    private static string ExtractPdfText(string path) { try { var bytes = File.ReadAllBytes(path); return Encoding.Latin1.GetString(bytes).Split(new[] { "stream", "endstream" }, StringSplitOptions.None).SelectMany(s => System.Text.RegularExpressions.Regex.Matches(s, @"\(([^()]*)\)").Select(m => m.Groups[1].Value)).Where(s => s.Any(char.IsLetter)).Take(300).Aggregate(new StringBuilder(), (b, s) => b.AppendLine(s)).ToString(); } catch { return ""; } }
    private static Encoding DetectEncoding(string path) { using var f = File.OpenRead(path); Span<byte> b = stackalloc byte[4]; var n = f.Read(b); if (n >= 2 && b[0] == 0xff && b[1] == 0xfe) return Encoding.Unicode; if (n >= 2 && b[0] == 0xfe && b[1] == 0xff) return Encoding.BigEndianUnicode; if (n >= 3 && b[0] == 0xef && b[1] == 0xbb && b[2] == 0xbf) return Encoding.UTF8; return new UTF8Encoding(false, true); }
    private void ShowMessage(string message) { ContentHost.Children.Clear(); ContentHost.Children.Add(new TextBlock { Text = message, Margin = new Thickness(24), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Foreground = (Brush)FindResource("MutedForegroundBrush") }); }
    private static bool IsImage(string e) => new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff" }.Contains(e);
    private static bool IsAudio(string e) => new[] { ".mp3", ".wav", ".wma", ".m4a", ".aac", ".mid", ".midi" }.Contains(e);
    private static bool IsVideo(string e) => new[] { ".mp4", ".wmv", ".avi", ".mpeg", ".mpg", ".mov" }.Contains(e);
    private static string FormatSize(long n) => n < 1024 ? $"{n} B" : n < 1048576 ? $"{n / 1024d:0.0} KB" : $"{n / 1048576d:0.0} MB";
    private void OnDrop(object sender, DragEventArgs e) { if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0) LoadFile(files[0]); }
    private void ZoomIn_Click(object s, RoutedEventArgs e) => SetZoom(Math.Min(6, _zoom * 1.2)); private void ZoomOut_Click(object s, RoutedEventArgs e) => SetZoom(Math.Max(.15, _zoom / 1.2)); private void ZoomReset_Click(object s, RoutedEventArgs e) => SetZoom(1);
    private void SetZoom(double z) { _zoom = z; if (_image?.RenderTransform is ScaleTransform t) t.ScaleX = t.ScaleY = z; ZoomText.Text = $"{z:P0}"; }
    private void Magnifier_Click(object s, RoutedEventArgs e) { _magnifier = MagnifierToggle.IsChecked == true; if (_image != null) _image.Cursor = _magnifier ? Cursors.Cross : Cursors.Arrow; }
    private void Image_MouseMove(object s, MouseEventArgs e)
    {
        if (!_magnifier || _image?.Source is not BitmapSource b) return;
        var p = e.GetPosition(_image); var x = (int)(p.X * b.PixelWidth / Math.Max(1, _image.ActualWidth)); var y = (int)(p.Y * b.PixelHeight / Math.Max(1, _image.ActualHeight));
        if (x < 0 || y < 0 || x >= b.PixelWidth || y >= b.PixelHeight) return;
        const int cropSize = 48; var left = Math.Clamp(x - cropSize / 2, 0, Math.Max(0, b.PixelWidth - cropSize)); var top = Math.Clamp(y - cropSize / 2, 0, Math.Max(0, b.PixelHeight - cropSize));
        var crop = new CroppedBitmap(b, new Int32Rect(left, top, Math.Min(cropSize, b.PixelWidth - left), Math.Min(cropSize, b.PixelHeight - top)));
        ToolTip = new ToolTip { Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse, Content = new Border { BorderBrush = Brushes.DodgerBlue, BorderThickness = new Thickness(2), Child = new Image { Source = crop, Width = 192, Height = 192, Stretch = Stretch.Fill } }, IsOpen = true };
    }
    private void FindNext_Click(object s, RoutedEventArgs e) => FindNext(); private void SearchBox_KeyDown(object s, KeyEventArgs e) { if (e.Key == Key.Enter) FindNext(); }
    private void FindNext() { if (_textView == null || string.IsNullOrEmpty(SearchBox.Text)) return; var start = Math.Max(0, _textView.SelectionStart + _textView.SelectionLength); var i = _textView.Text.IndexOf(SearchBox.Text, start, StringComparison.CurrentCultureIgnoreCase); if (i < 0) i = _textView.Text.IndexOf(SearchBox.Text, StringComparison.CurrentCultureIgnoreCase); if (i >= 0) { _textView.Focus(); _textView.Select(i, SearchBox.Text.Length); } }
    private void WordWrap_Changed(object s, RoutedEventArgs e) { if (_textView != null) _textView.TextWrapping = WordWrapToggle.IsChecked == true ? TextWrapping.Wrap : TextWrapping.NoWrap; }
    private void CopyText_Click(object s, RoutedEventArgs e) { if (_textView != null) Clipboard.SetText(_textView.SelectedText.Length > 0 ? _textView.SelectedText : _textView.Text); }
    private void OpenDefault_Click(object s, RoutedEventArgs e) => Launch(_filePath); private void OpenFolder_Click(object s, RoutedEventArgs e) { if (_filePath != null) try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_filePath}\"") { UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show(ex.Message, "Không thể mở thư mục", MessageBoxButton.OK, MessageBoxImage.Error); } }
    private static void Launch(string? arg) { if (arg == null) return; try { Process.Start(new ProcessStartInfo(arg) { UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show(ex.Message, "Không thể mở tệp", MessageBoxButton.OK, MessageBoxImage.Error); } }
}
