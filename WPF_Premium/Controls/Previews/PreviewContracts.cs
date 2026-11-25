using System.IO;
using System.Windows;
using System.Windows.Media;

namespace WPF_Premium.Controls.Previews;

public sealed record PreviewContext(string FilePath, string Extension)
{
    public static PreviewContext FromPath(string path) =>
        new(Path.GetFullPath(path), Path.GetExtension(path).ToLowerInvariant());
}

public interface IPreviewHost
{
    void SetStatusText(string text);
    void SetToolbarMode(PreviewToolbarMode mode);
    void ShowTextFallback(string path, string extension);
    void ShowMessage(string message);
    Style? TryFindStyle(string resourceKey);
    Brush? TryFindBrush(string resourceKey);
    LibVLCSharp.Shared.LibVLC SharedVlc { get; }
}

public enum PreviewToolbarMode
{
    None,
    Image,
    Text
}

public interface IPreviewSession : IDisposable
{
    FrameworkElement View { get; }
}

public interface IFilePreviewHandler
{
    bool CanHandle(PreviewContext context);
    IPreviewSession CreateSession(PreviewContext context, IPreviewHost host);
}

public sealed class PreviewHandlerRegistry(IEnumerable<IFilePreviewHandler> handlers)
{
    private readonly IReadOnlyList<IFilePreviewHandler> _handlers = [.. handlers];

    public IFilePreviewHandler Resolve(PreviewContext context) =>
        _handlers.First(h => h.CanHandle(context));
}
