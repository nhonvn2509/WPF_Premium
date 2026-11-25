using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MahApps.Metro.IconPacks;

namespace WPF_Premium.Controls.Previews;

internal sealed class VideoPreviewHandler : IFilePreviewHandler
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".3gp", ".asf", ".avi", ".f4v", ".flv", ".hevc", ".m2ts", ".m2v", ".m4v", ".mjpeg", ".mkv",
        ".mov", ".mp4", ".mpeg", ".mpg", ".mts", ".mxf", ".ogv", ".rm", ".swf", ".ts", ".vob",
        ".webm", ".wmv", ".wtv"
    };

    public bool CanHandle(PreviewContext context) => VideoExtensions.Contains(context.Extension);

    public IPreviewSession CreateSession(PreviewContext context, IPreviewHost host)
    {
        host.SetToolbarMode(PreviewToolbarMode.None);
        host.SetStatusText("Video");
        return new VideoPreviewSession(context.FilePath, host);
    }

    private sealed class VideoPreviewSession : IPreviewSession
    {
        private readonly Grid _grid = new() { Background = Brushes.Black };
        private readonly DispatcherTimer _timer;
        private LibVLCSharp.Shared.MediaPlayer? _player;
        private LibVLCSharp.Shared.Media? _media;
        private LibVLCSharp.WPF.VideoView? _videoView;
        private bool _disposed;

        public FrameworkElement View => _grid;

        public VideoPreviewSession(string path, IPreviewHost host)
        {
            var primaryStyle = host.TryFindStyle("ButtonPrimary");
            var outlineStyle = host.TryFindStyle("ButtonOutline");

            var status = new TextBlock
            {
                Text = "Đang tải bộ giải mã video…",
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(10),
                Background = new SolidColorBrush(Color.FromArgb(210, 25, 28, 35))
            };
            AutomationProperties.SetAutomationId(status, "VideoPreviewStatus");
            _grid.Children.Add(status);

            var isPlaying = false;
            var playIcon = new PackIconMaterial { Kind = PackIconMaterialKind.Play };
            var play = new Button { Content = playIcon, Style = primaryStyle, ToolTip = "Phát / tạm dừng", Margin = new Thickness(0, 0, 6, 0), IsEnabled = false };
            AutomationProperties.SetName(play, "Phát");
            AutomationProperties.SetAutomationId(play, "VideoPlayButton");

            var stopBtn = new Button { Content = new PackIconMaterial { Kind = PackIconMaterialKind.Stop }, Style = outlineStyle, ToolTip = "Dừng", Margin = new Thickness(0, 0, 6, 0), IsEnabled = false };
            var backBtn = new Button { Content = "-10s", Style = outlineStyle, Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 0, 4, 0), ToolTip = "Tua lùi 10 giây", IsEnabled = false };
            var fwdBtn = new Button { Content = "+10s", Style = outlineStyle, Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 0, 6, 0), ToolTip = "Tua tới 10 giây", IsEnabled = false };
            var stepBtn = new Button { Content = "Khung tiếp", Style = outlineStyle, Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 0, 6, 0), ToolTip = "Tới khung hình kế tiếp", IsEnabled = false };
            var snapBtn = new Button { Content = "Chụp hình", Style = outlineStyle, Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 0, 8, 0), ToolTip = "Chụp khung hình hiện tại vào Clipboard", IsEnabled = false };
            var timeText = new TextBlock { Text = "00:00 / 00:00", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0), FontSize = 12 };
            var seek = new Slider { Minimum = 0, Maximum = 100, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 6) };
            AutomationProperties.SetAutomationId(seek, "VideoSeekSlider");
            seek.ValueChanged += (_, _) => { if (_player is { Length: > 0 } p && Math.Abs(p.Time / 1000d - seek.Value) > 1) p.Time = (long)(seek.Value * 1000); };

            var isMuted = false;
            var muteIcon = new PackIconMaterial { Kind = PackIconMaterialKind.VolumeHigh };
            var muteBtn = new Button { Content = muteIcon, Style = outlineStyle, Padding = new Thickness(6, 3, 6, 3), Margin = new Thickness(0, 0, 4, 0), ToolTip = "Tắt / bật tiếng" };
            var vol = new Slider { Minimum = 0, Maximum = 1, Value = .7, Width = 80, Margin = new Thickness(4, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetAutomationId(vol, "VideoVolumeSlider");
            void ApplyVol()
            {
                var eff = isMuted ? 0 : vol.Value;
                if (_player is not null) _player.Volume = (int)(eff * 100);
                muteIcon.Kind = isMuted || eff == 0 ? PackIconMaterialKind.VolumeOff : PackIconMaterialKind.VolumeHigh;
            }
            muteBtn.Click += (_, _) => { isMuted = !isMuted; ApplyVol(); };
            vol.ValueChanged += (_, _) => { isMuted = false; ApplyVol(); };

            var speedCombo = new ComboBox { Width = 66, Height = 26, VerticalAlignment = VerticalAlignment.Center, ToolTip = "Tốc độ phát" };
            foreach (var sp in new[] { "0.5x", "0.75x", "1.0x", "1.25x", "1.5x", "2.0x" }) speedCombo.Items.Add(sp);
            speedCombo.SelectedIndex = 2;
            speedCombo.SelectionChanged += (_, _) =>
            {
                if (speedCombo.SelectedItem is string s && float.TryParse(s.TrimEnd('x'), CultureInfo.InvariantCulture, out var rate))
                {
                    _player?.SetRate(rate);
                }
            };

            play.Click += (_, _) =>
            {
                if (isPlaying)
                {
                    _player?.Pause();
                    playIcon.Kind = PackIconMaterialKind.Play;
                    AutomationProperties.SetName(play, "Phát");
                    isPlaying = false;
                }
                else
                {
                    _player?.Play();
                    playIcon.Kind = PackIconMaterialKind.Pause;
                    AutomationProperties.SetName(play, "Tạm dừng");
                    isPlaying = true;
                }
            };
            stopBtn.Click += (_, _) => { _player?.Stop(); playIcon.Kind = PackIconMaterialKind.Play; AutomationProperties.SetName(play, "Phát"); isPlaying = false; seek.Value = 0; };
            backBtn.Click += (_, _) => { if (_player is { Length: > 0 } p) p.Time = Math.Max(0, p.Time - 10_000); };
            fwdBtn.Click += (_, _) => { if (_player is { Length: > 0 } p) p.Time = Math.Min(p.Length, p.Time + 10_000); };
            stepBtn.Click += (_, _) => { _player?.NextFrame(); playIcon.Kind = PackIconMaterialKind.Play; isPlaying = false; };
            snapBtn.Click += (_, _) =>
            {
                if (_player is null) return;
                var snapPath = Path.Combine(Path.GetTempPath(), $"vsat_snap_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                if (_player.TakeSnapshot(0, snapPath, 0, 0) && File.Exists(snapPath))
                {
                    try { Clipboard.SetImage(new BitmapImage(new Uri(snapPath))); host.SetStatusText("Đã chụp khung hình"); } catch { }
                }
            };

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _timer.Tick += (_, _) =>
            {
                if (_player is { Length: > 0 } p && !seek.IsMouseCaptureWithin)
                {
                    seek.Maximum = Math.Max(1, p.Length / 1000d);
                    seek.Value = Math.Clamp(p.Time / 1000d, 0, seek.Maximum);
                    timeText.Text = $"{TimeSpan.FromMilliseconds(Math.Max(0, p.Time)):mm\\:ss} / {TimeSpan.FromMilliseconds(p.Length):mm\\:ss}";
                }
            };
            _timer.Start();

            var barLayout = new Grid();
            barLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            barLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var controlsRow = new DockPanel { LastChildFill = false };
            var left = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            foreach (UIElement el in new UIElement[] { play, stopBtn, backBtn, fwdBtn, stepBtn, snapBtn, timeText }) left.Children.Add(el);
            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            foreach (UIElement el in new UIElement[] { muteBtn, vol, speedCombo }) right.Children.Add(el);
            DockPanel.SetDock(right, Dock.Right);
            controlsRow.Children.Add(right);
            controlsRow.Children.Add(left);
            Grid.SetRow(seek, 0);
            Grid.SetRow(controlsRow, 1);
            barLayout.Children.Add(seek);
            barLayout.Children.Add(controlsRow);

            _grid.Children.Add(new Border
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = new SolidColorBrush(Color.FromArgb(220, 18, 20, 26)),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(12),
                CornerRadius = new CornerRadius(6),
                Child = barLayout
            });

            _ = Task.Run(() => host.SharedVlc).ContinueWith(task => _grid.Dispatcher.BeginInvoke(() =>
            {
                if (_disposed) return;
                try
                {
                    if (!task.IsCompletedSuccessfully) throw new InvalidOperationException("Không thể khởi tạo bộ giải mã.", task.Exception);
                    _player = new LibVLCSharp.Shared.MediaPlayer(task.Result) { Volume = 70 };
                    _videoView = new LibVLCSharp.WPF.VideoView { MediaPlayer = _player, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
                    _grid.Children.Insert(0, _videoView);
                    _media = new LibVLCSharp.Shared.Media(task.Result, new Uri(path));
                    _player.Media = _media;
                    _player.Playing += (_, _) => _grid.Dispatcher.BeginInvoke(() => status.Visibility = Visibility.Collapsed);
                    _player.EncounteredError += (_, _) => _grid.Dispatcher.BeginInvoke(() =>
                    {
                        status.Text = "Không thể giải mã video trên máy này. Hãy mở bằng ứng dụng mặc định.";
                        status.Visibility = Visibility.Visible;
                    });
                    _player.Play();
                    play.IsEnabled = stopBtn.IsEnabled = backBtn.IsEnabled = fwdBtn.IsEnabled = stepBtn.IsEnabled = snapBtn.IsEnabled = true;
                    isPlaying = true;
                    playIcon.Kind = PackIconMaterialKind.Pause;
                }
                catch (Exception ex)
                {
                    status.Text = $"Không thể mở video: {ex.Message}";
                }
            }));
        }

        public void Dispose()
        {
            _disposed = true;
            _timer.Stop();
            _player?.Stop();
            _player?.Dispose();
            _media?.Dispose();
            _videoView?.Dispose();
        }
    }
}
