using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace WPF_Premium.Controls.Previews;

internal static class PreviewPasswordDetector
{
    public const string PasswordProtectedMessage =
        "Tệp này sử dụng mật khẩu bảo vệ. Không thể xem trước trực tiếp — vui lòng mở bằng ứng dụng mặc định.";

    public static bool IsPasswordException(Exception? ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is CryptographicException)
            {
                return true;
            }

            var msg = current.Message;
            if (!string.IsNullOrEmpty(msg) &&
                (msg.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                 msg.Contains("encrypted", StringComparison.OrdinalIgnoreCase) ||
                 msg.Contains("mật khẩu", StringComparison.OrdinalIgnoreCase) ||
                 msg.Contains("mã hóa", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsEncryptedOfficeFile(string path, string extension)
    {
        if (extension is not (".docx" or ".docm" or ".dotx" or ".xlsx" or ".xlsm" or ".pptx" or ".pptm"))
        {
            return false;
        }

        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length < 512) return false;
            Span<byte> header = stackalloc byte[8];
            if (fs.Read(header) < 8) return false;
            // OLE2 Compound Document signature: D0 CF 11 E0 A1 B1 1A E1
            ReadOnlySpan<byte> ole2 = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
            if (!header.SequenceEqual(ole2)) return false;

            fs.Position = 0;
            var sampleLen = (int)Math.Min(fs.Length, 16384);
            var buf = new byte[sampleLen];
            _ = fs.Read(buf, 0, sampleLen);
            var utf16 = Encoding.Unicode.GetString(buf);
            return utf16.Contains("EncryptedPackage", StringComparison.Ordinal) ||
                   utf16.Contains("EncryptionInfo", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsEncryptedZipFile(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length < 30) return false;
            Span<byte> header = stackalloc byte[30];
            if (fs.Read(header) < 30) return false;
            // PK\x03\x04 local file header: general purpose bit flag at offset 6..7, bit 0 = encrypted
            if (header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04)
            {
                return (header[6] & 0x01) != 0;
            }
        }
        catch
        {
        }
        return false;
    }
}

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
