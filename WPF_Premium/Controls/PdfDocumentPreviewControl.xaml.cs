using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PDFtoImage;

namespace WPF_Premium.Controls;

public partial class PdfDocumentPreviewControl : UserControl
{
    private const double BasePageWidth = 820d;
    private byte[]? _pdfBytes;
    private int _totalPages;
    private int _currentPage = 1;
    private double _zoom = 1.0;
    private int _rotationDegrees;
    private string _engineLabel = "PDFium";
    private CancellationTokenSource? _renderCts;
    private readonly List<PageSlot> _pageSlots = [];
    private readonly HashSet<int> _renderedPages = [];
    private readonly HashSet<int> _renderingPages = [];
    private readonly Dictionary<int, Image> _thumbnailImages = [];
    private bool _updatingThumbnailSelection;

    public PdfDocumentPreviewControl()
    {
        InitializeComponent();
        Unloaded += (_, _) => CancelRendering();
    }

    public void CancelRendering()
    {
        _renderCts?.Cancel();
        _renderCts?.Dispose();
        _renderCts = null;
    }

    public async Task LoadPdfAsync(string path, string engineLabel = "PDFium", CancellationToken externalToken = default)
    {
        CancelRendering();
        _engineLabel = engineLabel;
        _zoom = 1.0;
        _rotationDegrees = 0;
        _currentPage = 1;
        _pageSlots.Clear();
        _renderedPages.Clear();
        _renderingPages.Clear();
        _thumbnailImages.Clear();
        PagesPanel.Children.Clear();
        ThumbnailsList.Items.Clear();

        LoadingText.Text = "Đang tải tài liệu…";
        LoadingText.Visibility = Visibility.Visible;

        var cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        _renderCts = cts;
        var token = cts.Token;

        try
        {
            var bytes = await File.ReadAllBytesAsync(path, token);
            var sizes = await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                var count = Conversion.GetPageCount(bytes);
                var pageSizes = new List<(double Width, double Height)>(count);
                for (var i = 0; i < count; i++)
                {
                    var s = Conversion.GetPageSize(bytes, page: i);
                    pageSizes.Add((s.Width, s.Height));
                }
                return pageSizes;
            }, token);

            if (token.IsCancellationRequested)
            {
                return;
            }

            _pdfBytes = bytes;
            _totalPages = sizes.Count;
            LoadingText.Visibility = Visibility.Collapsed;

            for (var i = 0; i < _totalPages; i++)
            {
                var rawW = Math.Max(1d, sizes[i].Width);
                var rawH = Math.Max(1d, sizes[i].Height);
                var aspect = rawH / rawW;

                var img = new Image
                {
                    Stretch = Stretch.Uniform,
                    Width = BasePageWidth * _zoom,
                    Height = BasePageWidth * _zoom * aspect
                };

                var placeholderText = new TextBlock
                {
                    Text = $"Trang {i + 1}",
                    Foreground = Brushes.Gray,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var host = new Grid();
                host.Children.Add(placeholderText);
                host.Children.Add(img);

                var border = new Border
                {
                    Background = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0xC8, 0xCE, 0xD6)),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 0, 14),
                    Padding = new Thickness(4),
                    Child = host
                };

                PagesPanel.Children.Add(border);
                _pageSlots.Add(new PageSlot(i, border, img, placeholderText, aspect));

                var thumbImg = new Image
                {
                    Width = 108,
                    Height = Math.Max(60, 108 * aspect),
                    Stretch = Stretch.Uniform
                };
                var thumbStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(4) };
                thumbStack.Children.Add(new Border
                {
                    Background = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0xD0, 0xD5, 0xDD)),
                    BorderThickness = new Thickness(1),
                    Child = thumbImg
                });
                thumbStack.Children.Add(new TextBlock
                {
                    Text = $"{i + 1}",
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 3, 0, 0)
                });
                ThumbnailsList.Items.Add(thumbStack);
                _thumbnailImages[i] = thumbImg;
            }

            UpdateToolbarState();
            await RenderVisiblePagesAsync(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            LoadingText.Text = Previews.PreviewPasswordDetector.IsPasswordException(ex)
                ? Previews.PreviewPasswordDetector.PasswordProtectedMessage
                : $"Không thể mở tài liệu: {ex.Message}";
            LoadingText.Visibility = Visibility.Visible;
        }
    }

    private async Task RenderVisiblePagesAsync(CancellationToken token)
    {
        if (_pdfBytes is null || _totalPages == 0 || token.IsCancellationRequested)
        {
            return;
        }

        var centerIndex = Math.Clamp(_currentPage - 1, 0, _totalPages - 1);
        var start = Math.Max(0, centerIndex - 2);
        var end = Math.Min(_totalPages - 1, centerIndex + 4);
        var dpi = _zoom > 1.35 ? 200 : 150;

        for (var i = start; i <= end; i++)
        {
            if (token.IsCancellationRequested) return;
            await EnsurePageRenderedAsync(i, dpi, token);
        }
    }

    private async Task EnsurePageRenderedAsync(int pageIndex, int dpi, CancellationToken token)
    {
        if (_pdfBytes is null || _renderedPages.Contains(pageIndex) || !_renderingPages.Add(pageIndex))
        {
            return;
        }

        try
        {
            var pdfBytes = _pdfBytes;
            var rotation = _rotationDegrees switch
            {
                90 => PdfRotation.Rotate90,
                180 => PdfRotation.Rotate180,
                270 => PdfRotation.Rotate270,
                _ => PdfRotation.Rotate0
            };

            var bitmap = await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                using var stream = new MemoryStream();
                var options = new PDFtoImage.RenderOptions(Dpi: dpi, Rotation: rotation);
                Conversion.SavePng(stream, pdfBytes, page: pageIndex, options: options);
                stream.Position = 0;
                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.StreamSource = stream;
                img.EndInit();
                img.Freeze();
                return img;
            }, token);

            if (token.IsCancellationRequested || pageIndex >= _pageSlots.Count)
            {
                return;
            }

            var slot = _pageSlots[pageIndex];
            slot.Image.Source = bitmap;
            slot.Placeholder.Visibility = Visibility.Collapsed;
            _renderedPages.Add(pageIndex);

            if (_thumbnailImages.TryGetValue(pageIndex, out var thumbImg) && thumbImg.Source is null)
            {
                thumbImg.Source = bitmap;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        finally
        {
            _renderingPages.Remove(pageIndex);
        }
    }

    private async Task RenderVisibleThumbnailsAsync(CancellationToken token)
    {
        if (_pdfBytes is null || ThumbnailsSidebar.Visibility != Visibility.Visible)
        {
            return;
        }

        var count = Math.Min(_totalPages, 60);
        for (var i = 0; i < count; i++)
        {
            if (token.IsCancellationRequested || ThumbnailsSidebar.Visibility != Visibility.Visible) return;
            if (_thumbnailImages.TryGetValue(i, out var thumb) && thumb.Source is not null) continue;

            var pageIndex = i;
            var pdfBytes = _pdfBytes;
            try
            {
                var bitmap = await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    using var stream = new MemoryStream();
                    Conversion.SavePng(stream, pdfBytes, page: pageIndex, options: new PDFtoImage.RenderOptions(Dpi: 48));
                    stream.Position = 0;
                    var img = new BitmapImage();
                    img.BeginInit();
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.StreamSource = stream;
                    img.EndInit();
                    img.Freeze();
                    return img;
                }, token);

                if (!token.IsCancellationRequested && _thumbnailImages.TryGetValue(pageIndex, out var target))
                {
                    target.Source = bitmap;
                }
            }
            catch
            {
                break;
            }
        }
    }

    private void ApplyZoomAndRotationLayout()
    {
        var swapped = _rotationDegrees % 180 != 0;
        foreach (var slot in _pageSlots)
        {
            var effectiveAspect = swapped ? 1.0 / Math.Max(0.1, slot.AspectRatio) : slot.AspectRatio;
            var targetWidth = BasePageWidth * _zoom;
            slot.Image.Width = targetWidth;
            slot.Image.Height = targetWidth * effectiveAspect;
        }

        ZoomLevelText.Text = $"{_zoom:P0}";
    }

    private void UpdateToolbarState()
    {
        TotalPagesText.Text = $"/ {Math.Max(1, _totalPages)}";
        PageNumberBox.Text = _currentPage.ToString();
        FirstPageButton.IsEnabled = _currentPage > 1;
        PrevPageButton.IsEnabled = _currentPage > 1;
        NextPageButton.IsEnabled = _currentPage < _totalPages;
        LastPageButton.IsEnabled = _currentPage < _totalPages;
        ZoomLevelText.Text = $"{_zoom:P0}";
        DocumentInfoText.Text = $"{_engineLabel} · {_totalPages} trang";

        _updatingThumbnailSelection = true;
        try
        {
            if (_currentPage >= 1 && _currentPage <= ThumbnailsList.Items.Count)
            {
                ThumbnailsList.SelectedIndex = _currentPage - 1;
            }
        }
        finally
        {
            _updatingThumbnailSelection = false;
        }
    }

    private void GoToPage(int page)
    {
        if (_totalPages == 0) return;
        _currentPage = Math.Clamp(page, 1, _totalPages);
        var slot = _pageSlots[_currentPage - 1];
        slot.Container.BringIntoView();
        UpdateToolbarState();
        if (_renderCts is not null)
        {
            _ = RenderVisiblePagesAsync(_renderCts.Token);
        }
    }

    private void FirstPage_Click(object sender, RoutedEventArgs e) => GoToPage(1);
    private void PrevPage_Click(object sender, RoutedEventArgs e) => GoToPage(_currentPage - 1);
    private void NextPage_Click(object sender, RoutedEventArgs e) => GoToPage(_currentPage + 1);
    private void LastPage_Click(object sender, RoutedEventArgs e) => GoToPage(_totalPages);

    private void PageNumberBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && int.TryParse(PageNumberBox.Text.Trim(), out var page))
        {
            GoToPage(page);
            e.Handled = true;
        }
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => SetZoom(_zoom * 1.2);
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => SetZoom(_zoom / 1.2);

    private void FitWidth_Click(object sender, RoutedEventArgs e)
    {
        var viewportW = Math.Max(320d, PagesScrollViewer.ViewportWidth - 48d);
        SetZoom(viewportW / BasePageWidth);
    }

    private void FitPage_Click(object sender, RoutedEventArgs e)
    {
        if (_pageSlots.Count == 0) return;
        var slot = _pageSlots[Math.Clamp(_currentPage - 1, 0, _pageSlots.Count - 1)];
        var swapped = _rotationDegrees % 180 != 0;
        var effectiveAspect = swapped ? 1.0 / Math.Max(0.1, slot.AspectRatio) : slot.AspectRatio;
        var viewportW = Math.Max(320d, PagesScrollViewer.ViewportWidth - 48d);
        var viewportH = Math.Max(320d, PagesScrollViewer.ViewportHeight - 40d);
        var zW = viewportW / BasePageWidth;
        var zH = viewportH / (BasePageWidth * effectiveAspect);
        SetZoom(Math.Min(zW, zH));
    }

    private void SetZoom(double zoom)
    {
        var prevZoom = _zoom;
        _zoom = Math.Clamp(zoom, 0.35, 3.0);
        ApplyZoomAndRotationLayout();
        if ((_zoom > 1.35 && prevZoom <= 1.35) || (_zoom <= 1.35 && prevZoom > 1.35))
        {
            _renderedPages.Clear();
        }
        if (_renderCts is not null)
        {
            _ = RenderVisiblePagesAsync(_renderCts.Token);
        }
    }

    private void RotateLeft_Click(object sender, RoutedEventArgs e) => Rotate(-90);
    private void RotateRight_Click(object sender, RoutedEventArgs e) => Rotate(90);

    private void Rotate(int delta)
    {
        _rotationDegrees = (_rotationDegrees + delta + 360) % 360;
        _renderedPages.Clear();
        ApplyZoomAndRotationLayout();
        if (_renderCts is not null)
        {
            _ = RenderVisiblePagesAsync(_renderCts.Token);
        }
    }

    private void ThumbnailsToggle_Click(object sender, RoutedEventArgs e)
    {
        ThumbnailsSidebar.Visibility = ThumbnailsToggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        if (ThumbnailsSidebar.Visibility == Visibility.Visible && _renderCts is not null)
        {
            _ = RenderVisibleThumbnailsAsync(_renderCts.Token);
        }
    }

    private void ThumbnailsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingThumbnailSelection || ThumbnailsList.SelectedIndex < 0)
        {
            return;
        }

        GoToPage(ThumbnailsList.SelectedIndex + 1);
    }

    private void PagesScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_pageSlots.Count == 0 || PagesScrollViewer.ViewportHeight <= 0)
        {
            return;
        }

        var viewportMid = PagesScrollViewer.VerticalOffset + PagesScrollViewer.ViewportHeight * 0.4;
        var detectedPage = _currentPage;
        for (var i = 0; i < _pageSlots.Count; i++)
        {
            var top = _pageSlots[i].Container.TransformToAncestor(PagesPanel).Transform(new Point(0, 0)).Y;
            var bottom = top + _pageSlots[i].Container.ActualHeight;
            if (viewportMid >= top && viewportMid <= bottom + 14)
            {
                detectedPage = i + 1;
                break;
            }
        }

        if (detectedPage != _currentPage)
        {
            _currentPage = detectedPage;
            UpdateToolbarState();
            if (_renderCts is not null)
            {
                _ = RenderVisiblePagesAsync(_renderCts.Token);
            }
        }
    }

    private void PagesScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            SetZoom(_zoom * (e.Delta > 0 ? 1.12 : 1.0 / 1.12));
            e.Handled = true;
        }
    }

    private sealed record PageSlot(int Index, Border Container, Image Image, TextBlock Placeholder, double AspectRatio);
}
