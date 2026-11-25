using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Sdcb.LibRaw;
using MagickFormat = ImageMagick.MagickFormat;
using MagickImage = ImageMagick.MagickImage;

namespace WPF_Premium.Controls.Previews;

internal sealed class ImagePreviewHandler : IFilePreviewHandler
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp", ".cr2", ".cur", ".dds", ".dng", ".erf", ".exr", ".fts", ".gif", ".hdr", ".heic", ".heif",
        ".ico", ".jfif", ".jp2", ".jpe", ".jpeg", ".jpg", ".jps", ".mng", ".nef", ".nrw", ".orf", ".pam",
        ".pbm", ".pcd", ".pcx", ".pef", ".pes", ".pfm", ".pgm", ".picon", ".pict", ".png", ".pnm", ".ppm",
        ".psd", ".raf", ".ras", ".rw2", ".sfw", ".sgi", ".svg", ".tga", ".tif", ".tiff", ".wbmp", ".webp",
        ".wpg", ".x3f", ".xbm", ".xcf", ".xpm", ".xwd", ".wdp", ".jxr"
    };

    private static readonly HashSet<string> RawExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cr2", ".dng", ".erf", ".nef", ".nrw", ".orf", ".pef", ".raf", ".rw2", ".x3f"
    };

    public bool CanHandle(PreviewContext context) => ImageExtensions.Contains(context.Extension);

    public IPreviewSession CreateSession(PreviewContext context, IPreviewHost host)
    {
        host.SetToolbarMode(PreviewToolbarMode.Image);
        host.SetStatusText("100%");
        return new ImagePreviewSession(context.FilePath, host);
    }

    internal sealed class ImagePreviewSession : IPreviewSession
    {
        private readonly IPreviewHost _host;
        private readonly ScrollViewer _scrollViewer;
        private readonly Grid _imageHost;
        private readonly Image _image;
        private readonly ScaleTransform _scaleTransform = new(1, 1);
        private readonly RotateTransform _rotateTransform = new(0);
        private readonly TextBlock _statusText;
        private ToolTip? _magnifierToolTip;
        private Image? _magnifierPreview;
        private bool _disposed;
        private double _zoom = 1;
        private bool _flipX;
        private bool _flipY;
        private bool _magnifier;
        private bool _fillMode;
        private bool _isPanning;
        private Point _panStartPoint;
        private double _panStartH;
        private double _panStartV;

        public FrameworkElement View => _scrollViewer;
        public double Zoom => _zoom;

        public ImagePreviewSession(string path, IPreviewHost host)
        {
            _host = host;
            _scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                CanContentScroll = false
            };

            _image = new Image
            {
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Point(.5, .5)
            };
            AutomationProperties.SetAutomationId(_image, "PreviewImage");

            var transform = new TransformGroup();
            transform.Children.Add(_scaleTransform);
            transform.Children.Add(_rotateTransform);
            _image.RenderTransform = transform;
            _image.MouseMove += OnMouseMove;
            _image.MouseLeave += (_, _) => CloseMagnifier();
            _image.PreviewMouseWheel += OnMouseWheel;
            _image.MouseLeftButtonDown += OnMouseDown;
            _image.MouseLeftButtonUp += OnMouseUp;

            _statusText = new TextBlock
            {
                Text = "Đang giải mã hình ảnh…",
                Foreground = host.TryFindBrush("MutedForegroundBrush") ?? Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            AutomationProperties.SetAutomationId(_statusText, "PreviewImageStatus");

            _imageHost = new Grid();
            _imageHost.Children.Add(_image);
            _imageHost.Children.Add(_statusText);
            _scrollViewer.Content = _imageHost;

            _ = Task.Run(() => DecodeImage(path)).ContinueWith(task => _scrollViewer.Dispatcher.BeginInvoke(() =>
            {
                if (_disposed) return;
                if (task.IsCompletedSuccessfully)
                {
                    _image.Source = task.Result;
                    _statusText.Visibility = Visibility.Collapsed;
                    _host.SetStatusText($"100% · {task.Result.PixelWidth}×{task.Result.PixelHeight} px");
                }
                else
                {
                    var errMsg = task.Exception?.GetBaseException().Message?.TrimEnd('.') ?? "định dạng không được hỗ trợ";
                    _statusText.Text = $"Không thể giải mã ảnh: {errMsg}. Hãy mở bằng ứng dụng mặc định.";
                }
            }));
        }

        public void SetZoom(double zoom)
        {
            _zoom = Math.Clamp(zoom, 0.15, 6);
            ApplyScale();
            _host.SetStatusText(_image.Source is BitmapSource bs
                ? $"{_zoom:P0} · {bs.PixelWidth}×{bs.PixelHeight} px"
                : $"{_zoom:P0}");
        }

        public void SetFit() { _fillMode = false; _image.Stretch = Stretch.Uniform; SetZoom(1); }
        public void SetFill() { _fillMode = true; _image.Stretch = Stretch.UniformToFill; SetZoom(1); }
        public void ToggleFit() { _fillMode = !_fillMode; _image.Stretch = _fillMode ? Stretch.UniformToFill : Stretch.Uniform; }
        public void RotateBy(double delta) => _rotateTransform.Angle = (_rotateTransform.Angle + delta + 360) % 360;
        public void FlipHorizontal() { _flipX = !_flipX; ApplyScale(); }
        public void FlipVertical() { _flipY = !_flipY; ApplyScale(); }

        public void Reset()
        {
            _flipX = false;
            _flipY = false;
            _rotateTransform.Angle = 0;
            _fillMode = false;
            _image.Stretch = Stretch.Uniform;
            SetZoom(1);
        }

        public void SetMagnifier(bool enabled)
        {
            _magnifier = enabled;
            _image.Cursor = enabled ? Cursors.Cross : Cursors.Arrow;
            if (!enabled) CloseMagnifier();
        }

        public void SetCheckerboard(bool enabled)
        {
            if (!enabled)
            {
                _imageHost.Background = Brushes.Transparent;
                return;
            }

            var group = new DrawingGroup();
            group.Children.Add(new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(0, 0, 16, 16))));
            var gray = new SolidColorBrush(Color.FromRgb(0xDC, 0xDF, 0xE4));
            var rects = new GeometryGroup();
            rects.Children.Add(new RectangleGeometry(new Rect(0, 0, 8, 8)));
            rects.Children.Add(new RectangleGeometry(new Rect(8, 8, 8, 8)));
            group.Children.Add(new GeometryDrawing(gray, null, rects));
            _imageHost.Background = new DrawingBrush(group)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 16, 16),
                ViewportUnits = BrushMappingMode.Absolute
            };
        }

        private void ApplyScale()
        {
            _scaleTransform.ScaleX = _zoom * (_flipX ? -1 : 1);
            _scaleTransform.ScaleY = _zoom * (_flipY ? -1 : 1);
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            SetZoom(Math.Clamp(_zoom + (e.Delta > 0 ? 0.1 : -0.1), 0.2, 5));
            e.Handled = true;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_magnifier) return;
            _isPanning = true;
            _panStartPoint = e.GetPosition(_scrollViewer);
            _panStartH = _scrollViewer.HorizontalOffset;
            _panStartV = _scrollViewer.VerticalOffset;
            _image.CaptureMouse();
            _image.Cursor = Cursors.Hand;
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isPanning) return;
            _isPanning = false;
            _image.ReleaseMouseCapture();
            _image.Cursor = _magnifier ? Cursors.Cross : Cursors.Arrow;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                var cur = e.GetPosition(_scrollViewer);
                _scrollViewer.ScrollToHorizontalOffset(_panStartH - (cur.X - _panStartPoint.X));
                _scrollViewer.ScrollToVerticalOffset(_panStartV - (cur.Y - _panStartPoint.Y));
                return;
            }

            if (!_magnifier || _image.Source is not BitmapSource b) return;
            var p = e.GetPosition(_image);
            var x = (int)(p.X * b.PixelWidth / Math.Max(1, _image.ActualWidth));
            var y = (int)(p.Y * b.PixelHeight / Math.Max(1, _image.ActualHeight));
            if (x < 0 || y < 0 || x >= b.PixelWidth || y >= b.PixelHeight) return;

            const int cropSize = 48;
            var left = Math.Clamp(x - cropSize / 2, 0, Math.Max(0, b.PixelWidth - cropSize));
            var top = Math.Clamp(y - cropSize / 2, 0, Math.Max(0, b.PixelHeight - cropSize));
            var crop = new CroppedBitmap(b, new Int32Rect(left, top, Math.Min(cropSize, b.PixelWidth - left), Math.Min(cropSize, b.PixelHeight - top)));
            if (_magnifierToolTip is null)
            {
                _magnifierPreview = new Image { Width = 192, Height = 192, Stretch = Stretch.Fill, SnapsToDevicePixels = true };
                var border = new Border { BorderThickness = new Thickness(2), Child = _magnifierPreview };
                border.SetResourceReference(Border.BorderBrushProperty, "FocusBrush");
                _magnifierToolTip = new ToolTip
                {
                    Placement = PlacementMode.Mouse,
                    PlacementTarget = _image,
                    StaysOpen = true,
                    Content = border
                };
            }

            _magnifierPreview!.Source = crop;
            if (!_magnifierToolTip.IsOpen) _magnifierToolTip.IsOpen = true;
        }

        private void CloseMagnifier()
        {
            if (_magnifierToolTip is not null) _magnifierToolTip.IsOpen = false;
            _magnifierToolTip = null;
            _magnifierPreview = null;
        }

        public void Dispose()
        {
            _disposed = true;
            CloseMagnifier();
        }

        private static BitmapSource DecodeImage(string path)
        {
            try
            {
                using var image = new MagickImage(path);
                image.AutoOrient();
                if (image.Width > 2400 || image.Height > 2400)
                {
                    image.Resize(new ImageMagick.MagickGeometry(2400, 2400) { IgnoreAspectRatio = false });
                }
                using var memory = new MemoryStream();
                image.Format = MagickFormat.Png;
                image.Write(memory);
                memory.Position = 0;
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = memory;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                try
                {
                    if (RawExtensions.Contains(Path.GetExtension(path)))
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
                        Marshal.Copy(processed.DataPointer, pixels, 0, pixels.Length);
                        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr24, null, pixels, stride);
                        bitmap.Freeze();
                        return bitmap;
                    }
                }
                catch { }

                var fallback = new BitmapImage();
                fallback.BeginInit();
                fallback.UriSource = new Uri(path);
                fallback.CacheOption = BitmapCacheOption.OnLoad;
                fallback.EndInit();
                fallback.Freeze();
                return fallback;
            }
        }
    }
}
