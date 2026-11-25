using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using MahApps.Metro.IconPacks;

namespace WPF_Premium.Controls.Previews;

internal sealed class AudioPreviewHandler : IFilePreviewHandler
{
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".8svx", ".aac", ".ac3", ".aiff", ".amb", ".amr", ".au", ".avr", ".caf", ".cdda", ".cvs", ".cvsd",
        ".cvu", ".dts", ".dvms", ".fap", ".flac", ".fssd", ".gsrt", ".hcom", ".htk", ".ima", ".ircam",
        ".m4a", ".m4b", ".m4r", ".maud", ".mid", ".midi", ".mp2", ".mp3", ".nist", ".oga", ".ogg", ".opus",
        ".paf", ".pcm", ".prc", ".pvf", ".ra", ".sd2", ".sln", ".smp", ".snd", ".sndr", ".sndt", ".sou",
        ".sph", ".spx", ".tta", ".txw", ".vms", ".voc", ".vox", ".w64", ".wav", ".wma", ".wv", ".wve"
    };

    public bool CanHandle(PreviewContext context) =>
        AudioExtensions.Contains(context.Extension) ||
        (context.Extension == ".a_voc" && WavWaveformReader.HasWavHeader(context.FilePath));

    public IPreviewSession CreateSession(PreviewContext context, IPreviewHost host)
    {
        host.SetToolbarMode(PreviewToolbarMode.None);
        return new AudioPreviewSession(context.FilePath, context.Extension, host);
    }

    private sealed class AudioPreviewSession : IPreviewSession
    {
        private readonly Grid _root = new() { Margin = new Thickness(18) };
        private readonly DispatcherTimer _timer;
        private readonly MediaPlayer? _wpfPlayer;
        private LibVLCSharp.Shared.MediaPlayer? _vlcPlayer;
        private LibVLCSharp.Shared.Media? _vlcMedia;
        private bool _disposed;
        private bool _updatingSeek;

        public FrameworkElement View => _root;

        public AudioPreviewSession(string path, string extension, IPreviewHost host)
        {
            _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var isMidi = extension is ".mid" or ".midi";
            var useVlcAudio = !isMidi && extension is not ".wav";
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
                Color.FromRgb(76, 164, 245), Color.FromRgb(52, 211, 153), Color.FromRgb(251, 191, 36),
                Color.FromRgb(167, 139, 250), Color.FromRgb(244, 114, 182), Color.FromRgb(56, 189, 248)
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
                    trackGrid.Children.Add(new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(180, 15, 18, 24)),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(6, 2, 6, 2),
                        Margin = new Thickness(8, 6, 0, 0),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        IsHitTestVisible = false,
                        Child = new TextBlock { Text = $"Kênh {ch + 1}", FontSize = 11, Foreground = new SolidColorBrush(channelColors[ch % channelColors.Length]) }
                    });
                }
                trackBorder.Child = trackGrid;
                tracksPanel.Children.Add(trackBorder);
            }

            var overlayCanvas = new Canvas { IsHitTestVisible = false, ClipToBounds = true };
            var selectionRect = new Rectangle { Fill = new SolidColorBrush(Color.FromArgb(65, 59, 130, 246)), Stroke = new SolidColorBrush(Color.FromRgb(96, 165, 250)), StrokeThickness = 1, Visibility = Visibility.Collapsed };
            var playheadLine = new Line { X1 = 0, X2 = 0, Y1 = 0, Y2 = totalWaveHeight, Stroke = new SolidColorBrush(Color.FromRgb(248, 113, 113)), StrokeThickness = 1.6, Visibility = isMidi ? Visibility.Collapsed : Visibility.Visible };
            overlayCanvas.Children.Add(selectionRect);
            overlayCanvas.Children.Add(playheadLine);
            waveSurface.Children.Add(tracksPanel);
            waveSurface.Children.Add(overlayCanvas);

            const double timelineHeight = 24d;
            var timelineCanvas = new Canvas { ClipToBounds = true, IsHitTestVisible = false };
            var timelineOverlayCanvas = new Canvas { ClipToBounds = true, IsHitTestVisible = false };
            var timelineSelectionRect = new Rectangle { Fill = new SolidColorBrush(Color.FromArgb(90, 59, 130, 246)), Height = timelineHeight, Visibility = Visibility.Collapsed };
            var timelinePlayhead = new Polygon { Points = [new Point(-5, 0), new Point(5, 0), new Point(0, 8)], Fill = new SolidColorBrush(Color.FromRgb(248, 113, 113)), Visibility = isMidi ? Visibility.Collapsed : Visibility.Visible };
            timelineOverlayCanvas.Children.Add(timelineSelectionRect);
            timelineOverlayCanvas.Children.Add(timelinePlayhead);

            var timelineHost = new Grid { Height = timelineHeight, Background = new SolidColorBrush(Color.FromRgb(18, 21, 28)), ClipToBounds = true, Cursor = isMidi ? Cursors.Arrow : Cursors.Hand, Visibility = isMidi ? Visibility.Collapsed : Visibility.Visible };
            timelineHost.Children.Add(timelineCanvas);
            timelineHost.Children.Add(timelineOverlayCanvas);
            var timelineBorder = new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(42, 48, 60)), BorderThickness = new Thickness(0, 0, 0, 1), Child = timelineHost, Visibility = isMidi ? Visibility.Collapsed : Visibility.Visible };
            AutomationProperties.SetAutomationId(timelineBorder, "AudioTimeline");
            AutomationProperties.SetName(timelineBorder, "Thanh thời gian âm thanh");

            var waveLayout = new Grid();
            waveLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            waveLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(timelineBorder, 0);
            waveLayout.Children.Add(timelineBorder);
            UIElement waveBody = channelCount > 3 ? new ScrollViewer { MaxHeight = 360, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = waveSurface } : waveSurface;
            Grid.SetRow(waveBody, 1);
            waveLayout.Children.Add(waveBody);

            var waveFrame = new Border { BorderBrush = host.TryFindBrush("BorderBrush") ?? Brushes.Gray, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), ClipToBounds = true, Child = waveLayout };
            AutomationProperties.SetAutomationId(waveFrame, "AudioWaveform");
            AutomationProperties.SetName(waveFrame, "Dạng sóng âm thanh");
            _root.Children.Add(waveFrame);

            TimeSpan? rangeStart = null;
            TimeSpan? rangeEnd = null;
            var totalDuration = waveData.Duration > TimeSpan.Zero ? waveData.Duration : TimeSpan.Zero;

            var outlineStyle = host.TryFindStyle("ButtonOutline");
            var primaryStyle = host.TryFindStyle("ButtonPrimary");
            var toggleStyle = host.TryFindStyle("Toggle");
            var mutedBrush = host.TryFindBrush("MutedForegroundBrush") ?? Brushes.Gray;

            var audioStatus = new TextBlock
            {
                Text = isMidi ? "Định dạng MIDI không có dạng sóng trực quan. Hãy mở tệp bằng ứng dụng mặc định." : "Nhấn/kéo trên sóng để tua · Giữ Shift + kéo (hoặc bật Chọn khoảng) để phát đoạn",
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0),
                Foreground = mutedBrush
            };
            AutomationProperties.SetAutomationId(audioStatus, "AudioStatusText");

            var seek = new Slider { Minimum = 0, Maximum = Math.Max(1, totalDuration.TotalSeconds), Value = 0, VerticalAlignment = VerticalAlignment.Center, MinWidth = 160, IsEnabled = !isMidi };
            AutomationProperties.SetAutomationId(seek, "AudioSeekSlider");
            var playbackTime = new TextBlock { Text = totalDuration > TimeSpan.Zero ? $"00:00 / {totalDuration:mm\\:ss}" : "00:00", Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetAutomationId(playbackTime, "AudioPlaybackTimeText");

            var rangeSelectToggle = new ToggleButton { Content = "Chọn khoảng", Style = toggleStyle, Margin = new Thickness(10, 0, 0, 0), Padding = new Thickness(8, 3, 8, 3), IsEnabled = !isMidi };
            AutomationProperties.SetAutomationId(rangeSelectToggle, "AudioRangeSelectToggle");
            var loopRangeToggle = new ToggleButton { Content = "Lặp khoảng", Style = toggleStyle, IsChecked = true, Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(8, 3, 8, 3), Visibility = Visibility.Collapsed };
            var clearRangeButton = new Button { Content = "Xóa khoảng", Style = outlineStyle, Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(8, 3, 8, 3), Visibility = Visibility.Collapsed };
            var rangeText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Foreground = host.TryFindBrush("TraceActiveBrush") ?? Brushes.DodgerBlue, Visibility = Visibility.Collapsed };

            if (!useVlcAudio && !isMidi) _wpfPlayer = new MediaPlayer { Volume = .7 };
            var isAudioPlaying = false;

            TimeSpan GetPosition() => useVlcAudio
                ? (_vlcPlayer is { Time: >= 0 } ? TimeSpan.FromMilliseconds(_vlcPlayer.Time) : TimeSpan.Zero)
                : (_wpfPlayer?.Position ?? TimeSpan.Zero);

            void UpdateOverlays()
            {
                var width = waveSurface.ActualWidth;
                var height = waveSurface.ActualHeight > 0 ? waveSurface.ActualHeight : totalWaveHeight;
                if (width <= 0 || totalDuration <= TimeSpan.Zero) return;
                var currentSec = useVlcAudio ? (_vlcPlayer is { Time: >= 0 } ? _vlcPlayer.Time / 1000d : seek.Value) : (_wpfPlayer?.Position.TotalSeconds ?? seek.Value);
                var x = Math.Clamp(currentSec / totalDuration.TotalSeconds, 0, 1) * width;
                playheadLine.X1 = playheadLine.X2 = x;
                playheadLine.Y2 = height;
                Canvas.SetLeft(timelinePlayhead, x);
                Canvas.SetTop(timelinePlayhead, 0);

                if (rangeStart is TimeSpan rs && rangeEnd is TimeSpan re && re > rs)
                {
                    var x1 = Math.Clamp(rs.TotalSeconds / totalDuration.TotalSeconds, 0, 1) * width;
                    var x2 = Math.Clamp(re.TotalSeconds / totalDuration.TotalSeconds, 0, 1) * width;
                    Canvas.SetLeft(selectionRect, x1); Canvas.SetTop(selectionRect, 0);
                    selectionRect.Width = Math.Max(2, x2 - x1); selectionRect.Height = height; selectionRect.Visibility = Visibility.Visible;
                    Canvas.SetLeft(timelineSelectionRect, x1); Canvas.SetTop(timelineSelectionRect, 0);
                    timelineSelectionRect.Width = Math.Max(2, x2 - x1); timelineSelectionRect.Visibility = Visibility.Visible;
                    rangeText.Text = $"Khoảng: {rs:mm\\:ss\\.f} – {re:mm\\:ss\\.f}";
                    rangeText.Visibility = clearRangeButton.Visibility = loopRangeToggle.Visibility = Visibility.Visible;
                }
                else
                {
                    selectionRect.Visibility = timelineSelectionRect.Visibility = rangeText.Visibility = clearRangeButton.Visibility = loopRangeToggle.Visibility = Visibility.Collapsed;
                }
            }

            void RenderTimeline(double width)
            {
                timelineCanvas.Children.Clear();
                if (isMidi || width < 20 || totalDuration <= TimeSpan.Zero) return;
                var totalSeconds = totalDuration.TotalSeconds;
                double[] steps = [0.2, 0.5, 1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600];
                var majorStep = steps.FirstOrDefault(s => width * s / totalSeconds >= 72d, steps[^1]);
                var minorStep = majorStep / 5d;
                var tickBrush = new SolidColorBrush(Color.FromArgb(140, 148, 163, 184)); tickBrush.Freeze();
                var minorTickBrush = new SolidColorBrush(Color.FromArgb(70, 148, 163, 184)); minorTickBrush.Freeze();
                var labelBrush = new SolidColorBrush(Color.FromRgb(180, 190, 208)); labelBrush.Freeze();

                var maxTicks = (int)Math.Floor(totalSeconds / minorStep);
                for (var idx = 0; idx <= maxTicks; idx++)
                {
                    var sec = idx * minorStep;
                    var x = sec / totalSeconds * width;
                    var isMajor = idx % 5 == 0;
                    timelineCanvas.Children.Add(new Line { X1 = x, X2 = x, Y1 = isMajor ? timelineHeight - 10 : timelineHeight - 5, Y2 = timelineHeight, Stroke = isMajor ? tickBrush : minorTickBrush, StrokeThickness = 1 });
                    if (isMajor && x < width - 28)
                    {
                        var ts = TimeSpan.FromSeconds(sec);
                        var lbl = new TextBlock { Text = ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : majorStep < 1 ? ts.ToString(@"mm\:ss\.f") : ts.ToString(@"mm\:ss"), FontSize = 10, Foreground = labelBrush };
                        Canvas.SetLeft(lbl, x + 3); Canvas.SetTop(lbl, 2);
                        timelineCanvas.Children.Add(lbl);
                    }
                }
            }

            void RenderWaveform()
            {
                var width = waveSurface.ActualWidth;
                if (width < 20) return;
                for (var ch = 0; ch < channelCanvases.Count; ch++)
                {
                    var canvas = channelCanvases[ch];
                    canvas.Children.Clear();
                    var peaks = waveData.ChannelPeaks[Math.Min(ch, waveData.ChannelPeaks.Length - 1)];
                    var midY = channelTrackHeight / 2d;
                    var centerBrush = new SolidColorBrush(Color.FromArgb(70, 148, 163, 184)); centerBrush.Freeze();
                    canvas.Children.Add(new Line { X1 = 0, X2 = width, Y1 = midY, Y2 = midY, Stroke = centerBrush, StrokeThickness = 1 });
                    var strokeBrush = new SolidColorBrush(channelColors[ch % channelColors.Length]); strokeBrush.Freeze();
                    var bars = Math.Max(40, Math.Min(peaks.Length, (int)(width / 3d)));
                    var actualStep = width / bars;
                    for (var i = 0; i < bars; i++)
                    {
                        var startIdx = (int)((double)i / bars * peaks.Length);
                        var endIdx = Math.Max(startIdx + 1, (int)((double)(i + 1) / bars * peaks.Length));
                        float peak = 0;
                        for (var j = startIdx; j < endIdx && j < peaks.Length; j++) peak = Math.Max(peak, peaks[j]);
                        var h = (0.05 + peak * 0.86) * (channelTrackHeight - 10);
                        var x = i * actualStep + actualStep * 0.5;
                        canvas.Children.Add(new Line { X1 = x, X2 = x, Y1 = midY - h / 2d, Y2 = midY + h / 2d, Stroke = strokeBrush, StrokeThickness = Math.Max(1.2, actualStep * 0.65) });
                    }
                }
                RenderTimeline(width);
                UpdateOverlays();
            }

            waveSurface.SizeChanged += (_, _) => RenderWaveform();

            TimeSpan PositionFromX(double x) =>
                TimeSpan.FromSeconds(Math.Clamp(x / Math.Max(1, waveSurface.ActualWidth), 0, 1) * Math.Max(0.1, totalDuration.TotalSeconds));

            void SeekToPosition(TimeSpan target)
            {
                if (totalDuration > TimeSpan.Zero) target = TimeSpan.FromSeconds(Math.Clamp(target.TotalSeconds, 0, totalDuration.TotalSeconds));
                _updatingSeek = true;
                if (useVlcAudio) { if (_vlcPlayer is not null) _vlcPlayer.Time = (long)target.TotalMilliseconds; }
                else if (_wpfPlayer is not null) _wpfPlayer.Position = target;
                seek.Value = Math.Clamp(target.TotalSeconds, seek.Minimum, seek.Maximum);
                playbackTime.Text = totalDuration > TimeSpan.Zero ? $"{target:mm\\:ss} / {totalDuration:mm\\:ss}" : target.ToString(@"mm\:ss");
                _updatingSeek = false;
                UpdateOverlays();
            }

            var isDraggingWave = false;
            var isDraggingRange = false;
            double rangeAnchorX = 0;
            void AttachPointer(UIElement target)
            {
                target.MouseLeftButtonDown += (_, e) =>
                {
                    if (isMidi || totalDuration <= TimeSpan.Zero) return;
                    var pos = e.GetPosition(target);
                    target.CaptureMouse();
                    isDraggingWave = true;
                    isDraggingRange = rangeSelectToggle.IsChecked == true || (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
                    if (isDraggingRange) { rangeAnchorX = pos.X; rangeStart = rangeEnd = PositionFromX(pos.X); UpdateOverlays(); }
                    else SeekToPosition(PositionFromX(pos.X));
                    e.Handled = true;
                };
                target.MouseMove += (_, e) =>
                {
                    if (!isDraggingWave || totalDuration <= TimeSpan.Zero) return;
                    var pos = e.GetPosition(target);
                    if (isDraggingRange) { var t1 = PositionFromX(rangeAnchorX); var t2 = PositionFromX(pos.X); rangeStart = t1 <= t2 ? t1 : t2; rangeEnd = t1 <= t2 ? t2 : t1; UpdateOverlays(); }
                    else SeekToPosition(PositionFromX(pos.X));
                };
                target.MouseLeftButtonUp += (_, e) =>
                {
                    if (!isDraggingWave) return;
                    isDraggingWave = false;
                    target.ReleaseMouseCapture();
                    if (isDraggingRange && rangeStart is TimeSpan rs && rangeEnd is TimeSpan re)
                    {
                        if ((re - rs).TotalMilliseconds < 120) { rangeStart = rangeEnd = null; SeekToPosition(PositionFromX(e.GetPosition(target).X)); }
                        else SeekToPosition(rs);
                        UpdateOverlays();
                    }
                    isDraggingRange = false;
                };
            }
            AttachPointer(waveSurface);
            AttachPointer(timelineHost);
            clearRangeButton.Click += (_, _) => { rangeStart = rangeEnd = null; UpdateOverlays(); };

            var playIcon = new PackIconMaterial { Kind = PackIconMaterialKind.Play };
            var play = new Button { Content = playIcon, Style = primaryStyle, ToolTip = "Phát / tạm dừng", Margin = new Thickness(0, 0, 6, 0) };
            AutomationProperties.SetName(play, "Phát");
            AutomationProperties.SetAutomationId(play, "AudioPlayButton");
            var stop = new Button { Content = new PackIconMaterial { Kind = PackIconMaterialKind.Stop }, ToolTip = "Dừng", Style = outlineStyle, Margin = new Thickness(0, 0, 6, 0) };
            AutomationProperties.SetName(stop, "Dừng");
            AutomationProperties.SetAutomationId(stop, "AudioStopButton");
            var skipBack = new Button { Content = "-5s", ToolTip = "Tua lùi 5 giây", Style = outlineStyle, Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 0, 4, 0), IsEnabled = !isMidi };
            skipBack.Click += (_, _) => SeekToPosition(GetPosition() - TimeSpan.FromSeconds(5));
            var skipForward = new Button { Content = "+5s", ToolTip = "Tua tới 5 giây", Style = outlineStyle, Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 0, 10, 0), IsEnabled = !isMidi };
            skipForward.Click += (_, _) => SeekToPosition(GetPosition() + TimeSpan.FromSeconds(5));

            void SetPlaying(bool playing) { isAudioPlaying = playing; playIcon.Kind = playing ? PackIconMaterialKind.Pause : PackIconMaterialKind.Play; AutomationProperties.SetName(play, playing ? "Tạm dừng" : "Phát"); }

            if (_wpfPlayer is not null)
            {
                _wpfPlayer.MediaOpened += (_, _) =>
                {
                    if (_wpfPlayer.NaturalDuration.HasTimeSpan)
                    {
                        totalDuration = _wpfPlayer.NaturalDuration.TimeSpan;
                        seek.Maximum = Math.Max(1, totalDuration.TotalSeconds);
                        seek.IsEnabled = true;
                        playbackTime.Text = $"00:00 / {totalDuration:mm\\:ss}";
                        RenderTimeline(waveSurface.ActualWidth);
                        UpdateOverlays();
                    }
                };
                _wpfPlayer.MediaEnded += (_, _) =>
                {
                    if (rangeStart is TimeSpan rs && rangeEnd is TimeSpan re && re > rs && loopRangeToggle.IsChecked == true) { SeekToPosition(rs); _wpfPlayer.Play(); return; }
                    SetPlaying(false); _wpfPlayer.Position = TimeSpan.Zero; seek.Value = 0; UpdateOverlays();
                };
                _wpfPlayer.MediaFailed += (_, _) => { audioStatus.Text = "Không thể phát định dạng âm thanh này. Hãy mở tệp bằng ứng dụng mặc định."; audioStatus.Foreground = host.TryFindBrush("DestructiveBrush") ?? Brushes.Red; };
            }

            play.Click += (_, _) =>
            {
                if (isAudioPlaying) { if (useVlcAudio) _vlcPlayer?.Pause(); else _wpfPlayer?.Pause(); SetPlaying(false); }
                else
                {
                    if (rangeStart is TimeSpan rs && rangeEnd is TimeSpan re && re > rs) { var cur = GetPosition(); if (cur < rs || cur >= re) SeekToPosition(rs); }
                    if (useVlcAudio) _vlcPlayer?.Play(); else _wpfPlayer?.Play();
                    SetPlaying(true);
                }
            };
            stop.Click += (_, _) => { if (useVlcAudio) _vlcPlayer?.Stop(); else _wpfPlayer?.Stop(); SeekToPosition(rangeStart ?? TimeSpan.Zero); SetPlaying(false); };
            seek.ValueChanged += (_, _) => { if (!_updatingSeek && Math.Abs(GetPosition().TotalSeconds - seek.Value) > 0.5) SeekToPosition(TimeSpan.FromSeconds(seek.Value)); };

            var isMuted = false;
            var muteIcon = new PackIconMaterial { Kind = PackIconMaterialKind.VolumeHigh };
            var muteBtn = new Button { Content = muteIcon, Style = outlineStyle, ToolTip = "Tắt / bật tiếng", Margin = new Thickness(10, 0, 4, 0), Padding = new Thickness(6, 3, 6, 3) };
            var volume = new Slider { Minimum = 0, Maximum = 1, Value = .7, Width = 80, Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(volume, "Âm lượng");
            void ApplyVol() { var eff = isMuted ? 0 : volume.Value; if (_wpfPlayer is not null) _wpfPlayer.Volume = eff; if (_vlcPlayer is not null) _vlcPlayer.Volume = (int)(eff * 100); muteIcon.Kind = isMuted || eff == 0 ? PackIconMaterialKind.VolumeOff : PackIconMaterialKind.VolumeHigh; }
            muteBtn.Click += (_, _) => { isMuted = !isMuted; ApplyVol(); };
            volume.ValueChanged += (_, _) => { isMuted = false; ApplyVol(); };

            var speedCombo = new ComboBox { Width = 68, Height = 27, Margin = new Thickness(8, 0, 0, 0), ToolTip = "Tốc độ phát", VerticalAlignment = VerticalAlignment.Center, IsEnabled = !isMidi };
            foreach (var sp in new[] { "0.5x", "0.75x", "1.0x", "1.25x", "1.5x", "2.0x" }) speedCombo.Items.Add(sp);
            speedCombo.SelectedIndex = 2;
            speedCombo.SelectionChanged += (_, _) => { if (speedCombo.SelectedItem is string s && double.TryParse(s.TrimEnd('x'), CultureInfo.InvariantCulture, out var rate)) { if (_wpfPlayer is not null) _wpfPlayer.SpeedRatio = rate; _vlcPlayer?.SetRate((float)rate); } };

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _timer.Tick += (_, _) =>
            {
                if (seek.IsMouseCaptureWithin) return;
                if (!useVlcAudio && (_wpfPlayer is null || !_wpfPlayer.NaturalDuration.HasTimeSpan)) return;
                if (useVlcAudio && (_vlcPlayer is null || _vlcPlayer.Length <= 0)) return;
                var pos = GetPosition();
                if (isAudioPlaying && rangeStart is TimeSpan rs && rangeEnd is TimeSpan re && re > rs && pos >= re)
                {
                    if (loopRangeToggle.IsChecked == true) SeekToPosition(rs);
                    else { if (useVlcAudio) _vlcPlayer?.Pause(); else _wpfPlayer?.Pause(); SetPlaying(false); SeekToPosition(rs); }
                    return;
                }
                _updatingSeek = true;
                seek.Value = Math.Clamp(pos.TotalSeconds, seek.Minimum, seek.Maximum);
                playbackTime.Text = totalDuration > TimeSpan.Zero ? $"{pos:mm\\:ss} / {totalDuration:mm\\:ss}" : pos.ToString(@"mm\:ss");
                _updatingSeek = false;
                UpdateOverlays();
            };
            _timer.Start();

            var controls = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 12, 0, 0) };
            foreach (UIElement el in new UIElement[] { play, stop, skipBack, skipForward, seek, playbackTime, muteBtn, volume, speedCombo, rangeSelectToggle, loopRangeToggle, clearRangeButton, rangeText })
                controls.Children.Add(el);
            Grid.SetRow(controls, 1); _root.Children.Add(controls);
            Grid.SetRow(audioStatus, 2); _root.Children.Add(audioStatus);

            if (_wpfPlayer is not null) _wpfPlayer.Open(new Uri(path));
            else if (useVlcAudio)
            {
                _ = Task.Run(() => host.SharedVlc).ContinueWith(task => _root.Dispatcher.BeginInvoke(() =>
                {
                    if (_disposed) return;
                    try
                    {
                        if (!task.IsCompletedSuccessfully) throw new InvalidOperationException("Không thể khởi tạo LibVLC.", task.Exception);
                        _vlcPlayer = new LibVLCSharp.Shared.MediaPlayer(task.Result) { Volume = 70 };
                        _vlcMedia = new LibVLCSharp.Shared.Media(task.Result, new Uri(path));
                        _vlcPlayer.Media = _vlcMedia;
                        _vlcPlayer.LengthChanged += (_, args) => _root.Dispatcher.BeginInvoke(() => { if (args.Length > 0) { totalDuration = TimeSpan.FromMilliseconds(args.Length); seek.Maximum = Math.Max(1, totalDuration.TotalSeconds); seek.IsEnabled = true; playbackTime.Text = $"00:00 / {totalDuration:mm\\:ss}"; RenderTimeline(waveSurface.ActualWidth); UpdateOverlays(); } });
                        _vlcPlayer.EndReached += (_, _) => _root.Dispatcher.BeginInvoke(() => { if (rangeStart is TimeSpan rs && rangeEnd is TimeSpan re && re > rs && loopRangeToggle.IsChecked == true) { _vlcPlayer.Stop(); _vlcPlayer.Play(); _vlcPlayer.Time = (long)rs.TotalMilliseconds; return; } SetPlaying(false); seek.Value = 0; UpdateOverlays(); });
                        _vlcPlayer.EncounteredError += (_, _) => _root.Dispatcher.BeginInvoke(() => { audioStatus.Text = "Không thể phát định dạng âm thanh này. Hãy mở tệp bằng ứng dụng mặc định."; audioStatus.Foreground = host.TryFindBrush("DestructiveBrush") ?? Brushes.Red; });
                        seek.IsEnabled = true;
                    }
                    catch (Exception ex) { audioStatus.Text = $"Không thể mở âm thanh: {ex.Message}"; }
                }));
            }

            host.SetStatusText(waveData.SampleRate > 0
                ? (waveData.BitsPerSample > 0 ? $"{channelCount} kênh · {waveData.SampleRate} Hz · {waveData.BitsPerSample}-bit" : $"{channelCount} kênh · {waveData.SampleRate} Hz")
                : "Dạng sóng trực quan");
        }

        public void Dispose()
        {
            _disposed = true;
            _timer.Stop();
            _wpfPlayer?.Close();
            _vlcPlayer?.Stop();
            _vlcPlayer?.Dispose();
            _vlcMedia?.Dispose();
        }
    }
}
