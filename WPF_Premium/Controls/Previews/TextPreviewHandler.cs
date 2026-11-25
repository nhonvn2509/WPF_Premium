using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;

namespace WPF_Premium.Controls.Previews;

internal sealed class TextPreviewHandler : IFilePreviewHandler
{
    private const int MaxTextPreviewCharacters = 1_000_000;
    private const int MaxArchivePreviewEntries = 10_000;
    private const long MaxJsonPreviewBytes = 8 * 1024 * 1024;

    public bool CanHandle(PreviewContext context) => true;

    public IPreviewSession CreateSession(PreviewContext context, IPreviewHost host)
    {
        host.SetToolbarMode(PreviewToolbarMode.Text);
        var viewer = new NotepadTextViewer();
        AutomationProperties.SetAutomationId(viewer, "PreviewTextContent");
        var session = new TextPreviewSession(context.FilePath, context.Extension, viewer);
        session.Refresh(includeReadableStrings: false, includeHexAscii: false);
        return session;
    }

    internal sealed class TextPreviewSession(string filePath, string extension, NotepadTextViewer viewer) : IPreviewSession
    {
        public FrameworkElement View => viewer;

        public void Refresh(bool includeReadableStrings, bool includeHexAscii)
        {
            var content = BuildContent(filePath, extension, includeReadableStrings, includeHexAscii);
            var encodingLabel = includeHexAscii ? "Hex / ASCII" : includeReadableStrings ? "Readable Strings" : null;
            viewer.SetContent(content, filePath, includeHexAscii || includeReadableStrings ? ".txt" : extension, encodingLabel);
        }

        public void Dispose() { }
    }

    private static string BuildContent(string path, string ext, bool includeReadableStrings, bool includeHexAscii)
    {
        if (includeReadableStrings || includeHexAscii)
        {
            return ReadBinaryPreview(path, ext, includeReadableStrings, includeHexAscii);
        }

        return ext switch
        {
            ".epub" => "Không đọc được EPUB",
            ".azw3" or ".fb2" => $"Không đọc được {ext.TrimStart('.').ToUpperInvariant()}",
            ".ttf" => $"Tệp phông chữ: {Path.GetFileName(path)}",
            ".lrf" => "Tệp sách LRF (Sony Broadband eBook). Định dạng nhị phân này chưa có bộ giải mã nội dung tích hợp; bạn vẫn có thể mở bằng ứng dụng mặc định.",
            ".djvu" or ".djv" => "Tài liệu DjVu nhiều trang. Bộ kết xuất DjVu chưa được tích hợp trên máy này; hãy mở bằng ứng dụng mặc định.",
            ".doc" or ".xls" or ".ppt" => ReadLegacyOffice(path, ext),
            ".eml" => ReadEml(path),
            ".json" => ReadJsonPreview(path),
            ".zip" => ReadZipArchive(path),
            _ => ReadNotepadTextPreview(path)
        };
    }

    private static string ReadZipArchive(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var content = new StringBuilder();
        var count = 0;
        var truncated = false;
        foreach (var entry in archive.Entries)
        {
            if (count++ >= MaxArchivePreviewEntries) { truncated = true; break; }
            if (content.Length > 0) content.AppendLine();
            content.Append(entry.FullName).Append("    ").Append(FormatSize(entry.Length));
        }
        return truncated ? content.AppendLine().Append("… [đã giới hạn 10.000 mục]").ToString() : content.ToString();
    }

    private static string ReadJsonPreview(string path)
    {
        if (new FileInfo(path).Length > MaxJsonPreviewBytes)
            return "Tệp JSON quá lớn để xem trước (giới hạn 8 MB). Hãy mở bằng ứng dụng mặc định.";
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return LimitPreview(JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }), MaxTextPreviewCharacters, "JSON");
    }

    private static string ReadNotepadTextPreview(string path)
    {
        string text;
        try
        {
            text = ReadTextPreview(path, DetectEncoding(path));
        }
        catch (DecoderFallbackException)
        {
            text = ReadTextPreview(path, Encoding.Latin1);
        }
        return text.Contains('\0') ? text.Replace('\0', ' ') : text;
    }

    private static string ReadBinaryPreview(string path, string ext, bool includeReadableStrings, bool includeHexAscii)
    {
        const int maxDumpBytes = 32 * 1024;
        var fileInfo = new FileInfo(path);
        using var stream = File.OpenRead(path);
        var toRead = (int)Math.Min(maxDumpBytes, fileInfo.Length);
        var bytes = new byte[toRead];
        var read = stream.Read(bytes, 0, toRead);

        var sb = new StringBuilder();
        var description = ext switch
        {
            ".gpsidt" => "Gói dữ liệu định danh / tọa độ VSAT (.GPSIDT)",
            ".a_voc" => "Dữ liệu thoại / âm thanh thô (.A_VOC)",
            ".unk" => "Tệp dữ liệu nhị phân chưa xác định (.UNK)",
            ".x3" => "Tệp dữ liệu phiên (.X3)",
            ".777" => "Tệp dữ liệu phiên (.777)",
            _ => $"Tệp dữ liệu ({ext.ToUpperInvariant()})"
        };

        sb.AppendLine($"Định dạng: {description}");
        sb.AppendLine($"Kích thước: {FormatSize(fileInfo.Length)} ({fileInfo.Length:N0} bytes)");

        if (includeReadableStrings)
        {
            var strings = ExtractPrintableStrings(bytes.AsSpan(0, read));
            sb.AppendLine();
            if (strings.Count > 0)
            {
                sb.AppendLine("Chuỗi ký tự đọc được:");
                foreach (var s in strings.Take(40)) sb.AppendLine($"  • {s}");
            }
            else
            {
                sb.AppendLine("Chuỗi ký tự đọc được: (không có)");
            }
        }

        if (includeHexAscii)
        {
            sb.AppendLine();
            sb.AppendLine("Nội dung Hex / ASCII:");
            for (var offset = 0; offset < read; offset += 16)
            {
                sb.Append($"{offset:X8}  ");
                for (var i = 0; i < 16; i++)
                {
                    sb.Append(offset + i < read ? $"{bytes[offset + i]:X2} " : "   ");
                    if (i == 7) sb.Append(' ');
                }
                sb.Append(" |");
                for (var i = 0; i < 16 && offset + i < read; i++)
                {
                    var b = bytes[offset + i];
                    sb.Append(b is >= 32 and <= 126 ? (char)b : '.');
                }
                sb.AppendLine("|");
            }
            if (fileInfo.Length > maxDumpBytes)
                sb.AppendLine().Append($"… [đã giới hạn hiển thị {maxDumpBytes:N0} bytes đầu]");
        }

        return sb.ToString();
    }

    private static List<string> ExtractPrintableStrings(ReadOnlySpan<byte> bytes)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        foreach (var b in bytes)
        {
            if (b is >= 32 and <= 126) current.Append((char)b);
            else
            {
                if (current.Length >= 4) result.Add(current.ToString());
                current.Clear();
            }
        }
        if (current.Length >= 4) result.Add(current.ToString());
        return result;
    }

    private static string ReadTextPreview(string path, Encoding encoding)
    {
        using var reader = new StreamReader(path, encoding, detectEncodingFromByteOrderMarks: true);
        var buffer = new char[8192];
        var content = new StringBuilder();
        while (content.Length < MaxTextPreviewCharacters)
        {
            var read = reader.ReadBlock(buffer, 0, Math.Min(buffer.Length, MaxTextPreviewCharacters - content.Length));
            if (read == 0) break;
            content.Append(buffer, 0, read);
        }
        return content.Length == MaxTextPreviewCharacters && reader.Read() != -1
            ? content.Append($"{Environment.NewLine}{Environment.NewLine}… [đã giới hạn phần xem trước văn bản ở {MaxTextPreviewCharacters:N0} ký tự]").ToString()
            : content.ToString();
    }

    private static string LimitPreview(string content, int limit, string kind) =>
        content.Length <= limit ? content : content[..limit] + $"{Environment.NewLine}{Environment.NewLine}… [đã giới hạn phần xem trước {kind} ở {limit:N0} ký tự]";

    private static string ReadLegacyOffice(string path, string extension)
    {
        try
        {
            var type = Type.GetType("DotnetPoi.HWPF.UserModel.HWPFDocument, DotnetPoi.Legacy", false);
            if (extension == ".doc" && type is not null)
            {
                using var stream = File.OpenRead(path);
                dynamic doc = Activator.CreateInstance(type, stream)!;
                return (string)doc.getText();
            }
        }
        catch (Exception ex)
        {
            return $"Không thể đọc tài liệu Office cũ: {ex.GetBaseException().Message}";
        }
        return "Không thể trích xuất văn bản từ định dạng Office cũ này. Hãy mở bằng ứng dụng mặc định.";
    }

    private static string ReadEml(string path)
    {
        var raw = File.ReadAllText(path, Encoding.UTF8);
        var split = raw.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        if (split < 0) split = raw.IndexOf("\n\n", StringComparison.Ordinal);
        return split < 0 ? raw : raw[..split] + Environment.NewLine + Environment.NewLine + raw[(split + (raw.AsSpan(split).StartsWith("\r\n\r\n") ? 4 : 2))..];
    }

    private static Encoding DetectEncoding(string path)
    {
        using var f = File.OpenRead(path);
        Span<byte> b = stackalloc byte[4];
        var n = f.Read(b);
        if (n >= 2 && b[0] == 0xff && b[1] == 0xfe) return Encoding.Unicode;
        if (n >= 2 && b[0] == 0xfe && b[1] == 0xff) return Encoding.BigEndianUnicode;
        if (n >= 3 && b[0] == 0xef && b[1] == 0xbb && b[2] == 0xbf) return Encoding.UTF8;
        return new UTF8Encoding(false, true);
    }

    private static string FormatSize(long n) =>
        n < 1024 ? $"{n} B" : n < 1048576 ? $"{n / 1024d:0.0} KB" : $"{n / 1048576d:0.0} MB";
}
