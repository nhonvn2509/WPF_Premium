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
            if (context.Extension == ".zip" && PreviewPasswordDetector.IsEncryptedZipFile(context.FilePath))
            {
                host.SetToolbarMode(PreviewToolbarMode.None);
                host.SetStatusText("Có mật khẩu");
                return new SimplePreviewSession(CreatePasswordPromptView(context.FilePath, host));
            }

            host.SetToolbarMode(PreviewToolbarMode.None);
            host.SetStatusText(statusLabel);
            var element = factory(context, () => host.ShowTextFallback(context.FilePath, context.Extension));
            return new SimplePreviewSession((FrameworkElement)element);
        }
        catch (Exception ex) when (PreviewPasswordDetector.IsPasswordException(ex))
        {
            host.SetToolbarMode(PreviewToolbarMode.None);
            host.SetStatusText("Có mật khẩu");
            return new SimplePreviewSession(CreatePasswordPromptView(context.FilePath, host));
        }
        catch
        {
            return new TextPreviewHandler().CreateSession(context, host);
        }
    }

    private static FrameworkElement CreatePasswordPromptView(string filePath, IPreviewHost host)
    {
        var stack = new System.Windows.Controls.StackPanel
        {
            Margin = new Thickness(24),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            MaxWidth = 520
        };
        stack.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = PreviewPasswordDetector.PasswordProtectedMessage,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Foreground = host.TryFindBrush("MutedForegroundBrush") ?? System.Windows.Media.Brushes.Gray
        });
        var openBtn = new System.Windows.Controls.Button
        {
            Content = "Mở bằng ứng dụng mặc định",
            Margin = new Thickness(0, 12, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(12, 5, 12, 5)
        };
        if (host.TryFindStyle("ButtonOutline") is Style outlineStyle)
        {
            openBtn.Style = outlineStyle;
        }
        openBtn.Click += (_, _) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(filePath) { UseShellExecute = true });
            }
            catch
            {
            }
        };
        stack.Children.Add(openBtn);
        return stack;
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
