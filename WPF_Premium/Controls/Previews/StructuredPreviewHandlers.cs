using System.Windows;

namespace WPF_Premium.Controls.Previews;

internal sealed class StructuredPreviewHandler(
    Func<PreviewContext, bool> canHandle,
    string statusLabel,
    Func<PreviewContext, Action, UIElement> factory) : IFilePreviewHandler
{
    public bool CanHandle(PreviewContext context) => canHandle(context);

    public IPreviewSession CreateSession(PreviewContext context, IPreviewHost host)
    {
        try
        {
            host.SetToolbarMode(PreviewToolbarMode.None);
            host.SetStatusText(statusLabel);
            var element = factory(context, () => host.ShowTextFallback(context.FilePath, context.Extension));
            return new SimplePreviewSession((FrameworkElement)element);
        }
        catch
        {
            return new TextPreviewHandler().CreateSession(context, host);
        }
    }

    private sealed class SimplePreviewSession(FrameworkElement view) : IPreviewSession
    {
        public FrameworkElement View => view;
        public void Dispose() { }
    }
}

internal static class StructuredPreviewHandlers
{
    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".zst"
    };

    public static IEnumerable<IFilePreviewHandler> CreateAll()
    {
        yield return new StructuredPreviewHandler(
            ctx => ArchiveExtensions.Contains(ctx.Extension),
            "Lưu trữ",
            (ctx, fallback) => RichPreviewHelpers.CreateArchivePreview(ctx.FilePath, fallback));

        yield return new StructuredPreviewHandler(
            ctx => ctx.Extension is ".eml" or ".mbox",
            "Email",
            (ctx, fallback) => RichPreviewHelpers.CreateEmailPreview(ctx.FilePath, fallback));

        yield return new StructuredPreviewHandler(
            ctx => ctx.Extension == ".md",
            "Markdown",
            (ctx, fallback) => RichPreviewHelpers.CreateMarkdownPreview(ctx.FilePath, fallback));

        yield return new StructuredPreviewHandler(
            ctx => ctx.Extension is ".csv" or ".tsv",
            "Bảng tính",
            (ctx, fallback) => RichPreviewHelpers.CreateSpreadsheetPreview(ctx.FilePath, ctx.Extension, fallback));

        yield return new StructuredPreviewHandler(
            ctx => ctx.Extension is ".html" or ".htm" or ".mht" or ".mhtml",
            "Web",
            (ctx, fallback) => RichPreviewHelpers.CreateWebPreview(ctx.FilePath, fallback));
    }
}
