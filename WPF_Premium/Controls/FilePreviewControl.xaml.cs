using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using WPF_Premium.Controls.Previews;

namespace WPF_Premium.Controls;

public partial class FilePreviewControl : UserControl, IPreviewHost
{
    private static readonly Lazy<LibVLCSharp.Shared.LibVLC> LazySharedVlc = new(() =>
    {
        LibVLCSharp.Shared.Core.Initialize();
        return new LibVLCSharp.Shared.LibVLC(enableDebugLogs: false);
    });

    private static readonly PreviewHandlerRegistry Registry = new(
    [
        new ImagePreviewHandler(),
        new AudioPreviewHandler(),
        new VideoPreviewHandler(),
        new DocumentPreviewHandler(),
        .. StructuredPreviewHandlers.CreateAll(),
        new TextPreviewHandler()
    ]);

    private string? _filePath;
    private IPreviewSession? _activeSession;
    private FrameworkElement? _customContent;

    public FilePreviewControl()
    {
        InitializeComponent();
        UpdateChromeVisibility();
        AllowDrop = true;
        Drop += OnDrop;
        Unloaded += (_, _) => DisposeActiveSession();
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
        set { if (!string.IsNullOrWhiteSpace(value)) LoadFile(value); }
    }

    public double ImageZoom => (_activeSession as ImagePreviewHandler.ImagePreviewSession)?.Zoom ?? 1;

    LibVLCSharp.Shared.LibVLC IPreviewHost.SharedVlc => LazySharedVlc.Value;

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

        var context = PreviewContext.FromPath(path);
        LoadWithHandler(context, Registry.Resolve(context));
    }

    public void LoadWebFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            ClearPreview();
            ShowMessage("Không tìm thấy tệp.");
            return;
        }

        var context = PreviewContext.FromPath(path);
        var webHandler = StructuredPreviewHandlers.CreateAll().Last();
        LoadWithHandler(context, webHandler);
    }

    private void LoadWithHandler(PreviewContext context, IFilePreviewHandler handler)
    {
        DisposeActiveSession();
        _filePath = context.FilePath;
        UpdateHeaderMetadata(context.FilePath);
        ContentHost.Children.Clear();
        CheckerboardToggle.IsChecked = false;
        MagnifierToggle.IsChecked = false;
        ReadableStringsToggle.IsChecked = false;
        HexAsciiToggle.IsChecked = false;

        try
        {
            _activeSession = handler.CreateSession(context, this);
            ContentHost.Children.Add(_activeSession.View);
        }
        catch (Exception ex)
        {
            ShowMessage($"Không thể tạo bản xem trước: {ex.Message}");
        }
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

        DisposeActiveSession();
        _filePath = null;
        FileNameText.Text = "Chưa chọn tệp";
        FileExtensionText.Text = string.Empty;
        FileSizeText.Text = string.Empty;
        SetToolbarMode(PreviewToolbarMode.None);
        SetStatusText("100%");
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
        DisposeActiveSession();
        _filePath = null;
        FileNameText.Text = "Chưa chọn tệp";
        FileExtensionText.Text = string.Empty;
        FileSizeText.Text = string.Empty;
        SetToolbarMode(PreviewToolbarMode.None);
        SetStatusText("100%");
        ContentHost.Children.Clear();
        EmptyText.Visibility = Visibility.Visible;
        ContentHost.Children.Add(EmptyText);
    }

    public void SetImageZoom(double zoom) => (_activeSession as ImagePreviewHandler.ImagePreviewSession)?.SetZoom(zoom);
    public void ToggleImageFit() => (_activeSession as ImagePreviewHandler.ImagePreviewSession)?.ToggleFit();
    public void RotateImage() => (_activeSession as ImagePreviewHandler.ImagePreviewSession)?.RotateBy(90);
    public void ResetImage() => (_activeSession as ImagePreviewHandler.ImagePreviewSession)?.Reset();

    public void SetStatusText(string text)
    {
        ZoomText.Text = text;
        ZoomText.Visibility = Visibility.Visible;
    }

    public void SetToolbarMode(PreviewToolbarMode mode)
    {
        ImageTools.Visibility = mode == PreviewToolbarMode.Image ? Visibility.Visible : Visibility.Collapsed;
        TextTools.Visibility = mode == PreviewToolbarMode.Text ? Visibility.Visible : Visibility.Collapsed;
        ZoomText.Visibility = mode == PreviewToolbarMode.Text ? Visibility.Collapsed : Visibility.Visible;
        if (ShowChrome)
        {
            ToolsPanel.Visibility = mode == PreviewToolbarMode.None ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    public void ShowTextFallback(string path, string extension)
    {
        var context = new PreviewContext(path, extension);
        LoadWithHandler(context, new TextPreviewHandler());
    }

    public void ShowMessage(string message)
    {
        ContentHost.Children.Clear();
        var text = new TextBlock
        {
            Text = message,
            Margin = new Thickness(24),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = TryFindBrush("MutedForegroundBrush") ?? Brushes.Gray
        };
        AutomationProperties.SetAutomationId(text, "PreviewLoadError");
        ContentHost.Children.Add(text);
    }

    public Style? TryFindStyle(string resourceKey) => TryFindResource(resourceKey) as Style;
    public Brush? TryFindBrush(string resourceKey) => TryFindResource(resourceKey) as Brush;

    private void UpdateHeaderMetadata(string fullPath)
    {
        var fileName = Path.GetFileName(fullPath);
        var extension = Path.GetExtension(fullPath);
        FileNameText.Text = Path.GetFileNameWithoutExtension(fullPath);
        FileExtensionText.Text = extension.ToUpperInvariant();
        FileSizeText.Text = FormatSize(new FileInfo(fullPath).Length);
        AutomationProperties.SetName(FileNameText, fileName);
    }

    private void DisposeActiveSession()
    {
        _activeSession?.Dispose();
        _activeSession = null;
    }

    private static void OnShowChromeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((FilePreviewControl)d).UpdateChromeVisibility();

    private void UpdateChromeVisibility()
    {
        if (HeaderPanel is null || ToolsPanel is null) return;
        var visibility = ShowChrome ? Visibility.Visible : Visibility.Collapsed;
        HeaderPanel.Visibility = visibility;
        ToolsPanel.Visibility = visibility;
    }

    private void TextDisplayMode_Changed(object sender, RoutedEventArgs e) =>
        (_activeSession as TextPreviewHandler.TextPreviewSession)?.Refresh(
            ReadableStringsToggle.IsChecked == true,
            HexAsciiToggle.IsChecked == true);

    private void ZoomIn_Click(object s, RoutedEventArgs e) => SetImageZoom(Math.Min(6, ImageZoom * 1.2));
    private void ZoomOut_Click(object s, RoutedEventArgs e) => SetImageZoom(Math.Max(.15, ImageZoom / 1.2));
    private void ZoomReset_Click(object s, RoutedEventArgs e) => ResetImage();
    private void ImageFit_Click(object s, RoutedEventArgs e) => (_activeSession as ImagePreviewHandler.ImagePreviewSession)?.SetFit();
    private void ImageFill_Click(object s, RoutedEventArgs e) => (_activeSession as ImagePreviewHandler.ImagePreviewSession)?.SetFill();
    private void ImageRotateLeft_Click(object s, RoutedEventArgs e) => (_activeSession as ImagePreviewHandler.ImagePreviewSession)?.RotateBy(-90);
    private void ImageRotateRight_Click(object s, RoutedEventArgs e) => (_activeSession as ImagePreviewHandler.ImagePreviewSession)?.RotateBy(90);
    private void ImageFlipHorizontal_Click(object s, RoutedEventArgs e) => (_activeSession as ImagePreviewHandler.ImagePreviewSession)?.FlipHorizontal();
    private void ImageFlipVertical_Click(object s, RoutedEventArgs e) => (_activeSession as ImagePreviewHandler.ImagePreviewSession)?.FlipVertical();
    private void Checkerboard_Click(object s, RoutedEventArgs e) => (_activeSession as ImagePreviewHandler.ImagePreviewSession)?.SetCheckerboard(CheckerboardToggle.IsChecked == true);
    private void Magnifier_Click(object s, RoutedEventArgs e) => (_activeSession as ImagePreviewHandler.ImagePreviewSession)?.SetMagnifier(MagnifierToggle.IsChecked == true);

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0) LoadFile(files[0]);
    }

    private void OpenDefault_Click(object s, RoutedEventArgs e) => Launch(_filePath);

    private void OpenFolder_Click(object s, RoutedEventArgs e)
    {
        if (_filePath is null) return;
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_filePath}\"") { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Không thể mở thư mục", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private static void Launch(string? arg)
    {
        if (arg is null) return;
        try { Process.Start(new ProcessStartInfo(arg) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Không thể mở tệp", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private static string FormatSize(long n) =>
        n < 1024 ? $"{n} B" : n < 1048576 ? $"{n / 1024d:0.0} KB" : $"{n / 1048576d:0.0} MB";
}
