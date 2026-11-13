using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using DotnetPoi.HSSF.UserModel;
using FlaApplication = FlaUI.Core.Application;

namespace WPF_Premium.UIAutomationTests;

[TestClass]
[DoNotParallelize]
public sealed class FilePreviewUiTests
{
    private static readonly PreviewScenario[] PreviewScenarios =
    [
        new(".png", PreviewKind.Image),
        new(".jpg", PreviewKind.Image),
        new(".jpeg", PreviewKind.Image),
        new(".bmp", PreviewKind.Image),
        new(".gif", PreviewKind.Image),
        new(".tif", PreviewKind.Image),
        new(".tiff", PreviewKind.Image),
        new(".ico", PreviewKind.Image),
        new(".webp", PreviewKind.Image),
        new(".svg", PreviewKind.Image),
        new(".psd", PreviewKind.Image),
        new(".jp2", PreviewKind.Image),
        new(".cr2", PreviewKind.Image),
        new(".heif", PreviewKind.Image),
        new(".mp3", PreviewKind.Audio),
        new(".wav", PreviewKind.Audio),
        new(".wma", PreviewKind.Audio),
        new(".m4a", PreviewKind.Audio),
        new(".aac", PreviewKind.Audio),
        new(".mid", PreviewKind.Audio),
        new(".midi", PreviewKind.Audio),
        new(".flac", PreviewKind.Audio),
        new(".ogg", PreviewKind.Audio),
        new(".opus", PreviewKind.Audio),
        new(".ac3", PreviewKind.Audio),
        new(".sph", PreviewKind.Audio),
        new(".8svx", PreviewKind.Audio),
        new(".wv", PreviewKind.Audio),
        new(".mp4", PreviewKind.Video),
        new(".wmv", PreviewKind.Video),
        new(".avi", PreviewKind.Video),
        new(".mpeg", PreviewKind.Video),
        new(".mpg", PreviewKind.Video),
        new(".mov", PreviewKind.Video),
        new(".mkv", PreviewKind.Video),
        new(".webm", PreviewKind.Video),
        new(".m4v", PreviewKind.Video),
        new(".rm", PreviewKind.Video),
        new(".swf", PreviewKind.Video),
        new(".mxf", PreviewKind.Video),
        new(".txt", PreviewKind.Text),
        new(".csv", PreviewKind.Text),
        new(".tsv", PreviewKind.Text),
        new(".xml", PreviewKind.Text),
        new(".md", PreviewKind.Text),
        new(".log", PreviewKind.Text),
        new(".json", PreviewKind.Text),
        new(".eml", PreviewKind.Text),
        new(".pdf", PreviewKind.Document),
        new(".docx", PreviewKind.Document),
        new(".docm", PreviewKind.Document),
        new(".xlsx", PreviewKind.Document),
        new(".xlsm", PreviewKind.Document),
        new(".pptx", PreviewKind.Document),
        new(".odt", PreviewKind.Document),
        new(".ods", PreviewKind.Document),
        new(".odp", PreviewKind.Document),
        new(".html", PreviewKind.Text),
        new(".rtf", PreviewKind.Text),
        new(".yaml", PreviewKind.Text),
        new(".doc", PreviewKind.Text),
        new(".xls", PreviewKind.Document),
        new(".ppt", PreviewKind.Text),
        new(".zip", PreviewKind.Text),
        new(".azw3", PreviewKind.Text),
        new(".epub", PreviewKind.Text),
        new(".fb2", PreviewKind.Text),
        new(".ps1", PreviewKind.Text),
        new(".htaccess", PreviewKind.Text),
        new(".ttf", PreviewKind.Text)
    ];

    public static IEnumerable<object[]> PreviewCases
    {
        get
        {
            var testDirectory = Environment.GetEnvironmentVariable("WPF_PREVIEW_TEST_DIRECTORY");
            if (!string.IsNullOrWhiteSpace(testDirectory) && Directory.Exists(testDirectory))
            {
                var files = Directory.EnumerateFiles(testDirectory);
                var selectedFiles = Environment.GetEnvironmentVariable("WPF_PREVIEW_TEST_FILES")?
                    .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (selectedFiles is { Length: > 0 }) files = files.Where(path => selectedFiles.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase));
                return files
                    .Select(path => new PreviewScenario(Path.GetExtension(path).ToLowerInvariant(), GetPreviewKind(Path.GetExtension(path)), path))
                    .Select(scenario => new object[] { scenario });
            }
            var requestedExtensions = Environment.GetEnvironmentVariable("WPF_PREVIEW_TEST_EXTENSIONS")?
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var scenarios = requestedExtensions is { Length: > 0 }
                ? PreviewScenarios.Where(scenario => requestedExtensions.Contains(scenario.Extension, StringComparer.OrdinalIgnoreCase))
                : PreviewScenarios;
            return scenarios.Select(scenario => new object[] { scenario });
        }
    }

    [TestMethod]
    [DynamicData(nameof(PreviewCases))]
    [TestCategory("UI")]
    public void Opens_the_expected_preview_and_file_actions_for_each_supported_extension(PreviewScenario scenario)
    {
        var fixtureDirectory = Path.Combine(Path.GetTempPath(), "WpfPremiumPreviewTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixtureDirectory);
        var fixturePath = scenario.FilePath ?? Path.Combine(fixtureDirectory, $"preview{scenario.Extension}");
        if (scenario.FilePath is null) WriteFixture(fixturePath, scenario.Extension);

        using var process = StartDemo(fixturePath);
        try
        {
            using var application = FlaApplication.Attach(process.Id);
            using var automation = new UIA3Automation();
            var window = WaitForMainWindow(application, automation);
            WaitForElement(window, "FileNameText");
            var elementsByAutomationId = new Dictionary<string, AutomationElement>(StringComparer.Ordinal);
            foreach (var element in window.FindAllDescendants())
            {
                try
                {
                    if (!string.IsNullOrEmpty(element.AutomationId))
                    {
                        elementsByAutomationId.TryAdd(element.AutomationId, element);
                    }
                }
                catch (FlaUI.Core.Exceptions.PropertyNotSupportedException)
                {
                    // Some WPF automation peers do not expose every optional UIA property.
                }
            }

            Assert.IsTrue(elementsByAutomationId.ContainsKey("FileNameText"), "The selected file name should be present.");
            Assert.IsTrue(elementsByAutomationId.ContainsKey("OpenWithDefaultAppButton"), "Every preview must offer opening the file in its default application.");
            Assert.IsTrue(elementsByAutomationId.ContainsKey("OpenContainingFolderButton"), "Every preview must offer opening its containing folder.");
            var expectedFileName = scenario.FilePath is null ? $"preview{scenario.Extension}" : Path.GetFileName(scenario.FilePath);
            Assert.AreEqual(expectedFileName, elementsByAutomationId["FileNameText"].Name);

            foreach (var automationId in scenario.RequiredAutomationIds)
            {
                Assert.IsTrue(
                    elementsByAutomationId.ContainsKey(automationId),
                    $"Expected UI element '{automationId}' for {scenario.Extension} preview.");
            }

            if (scenario.Kind == PreviewKind.Image)
            {
                Assert.IsTrue(elementsByAutomationId.ContainsKey("PreviewZoomText"));
                WaitForImageDecode(window);
            }
            else if (scenario.Kind == PreviewKind.Audio)
            {
                Assert.AreEqual("Dạng sóng trực quan", elementsByAutomationId["PreviewZoomText"].Name);
                if (scenario.Extension == ".wav" || scenario.FilePath is not null)
                {
                    WaitForEnabled(window, "AudioSeekSlider");
                    var initialTime = elementsByAutomationId["AudioPlaybackTimeText"].Name;
                    elementsByAutomationId["AudioPlayButton"].Patterns.Invoke.Pattern?.Invoke();
                    WaitForPlaybackProgress(window, initialTime);
                }
                Assert.IsTrue(elementsByAutomationId.ContainsKey("AudioPlaybackTimeText"));
            }
            else if (scenario.Kind == PreviewKind.Video)
            {
                Assert.AreEqual("Video", elementsByAutomationId["PreviewZoomText"].Name);
                WaitForVideoReady(window);
            }
            else if (scenario.Kind == PreviewKind.Document)
            {
                Assert.IsTrue(
                    elementsByAutomationId.ContainsKey("PreviewDocumentBrowser"),
                    elementsByAutomationId.TryGetValue("PreviewLoadError", out var documentError)
                        ? $"Document preview failed: {documentError.Name}"
                        : $"{scenario.Extension} phải dùng trình xem tài liệu trực quan.");
            }
            else
            {
                var expectedSnippet = scenario.ExpectedTextSnippet;
                var previewText = elementsByAutomationId["PreviewTextContent"].Patterns.Value.PatternOrDefault?.Value;
                Assert.IsNotNull(previewText, "The text preview should expose its contents through UI Automation.");
                if (expectedSnippet is not null) StringAssert.Contains(previewText, expectedSnippet);
            }
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(TimeSpan.FromSeconds(5));
            }

            Directory.Delete(fixtureDirectory, recursive: true);
        }
    }

    private static Process StartDemo(string previewPath)
    {
        var configuration = Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))?.Name
            ?? "Debug";
        var repositoryRoot = FindRepositoryRoot();
        var executablePath = Path.Combine(
            repositoryRoot,
            "WPF_Premium_Demo",
            "bin",
            configuration,
            "net10.0-windows10.0.19041",
            "WPF_Premium_Demo.exe");

        Assert.IsTrue(File.Exists(executablePath), $"Build the demo before running UI tests. Expected: {executablePath}");
        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(executablePath)!
        };
        startInfo.ArgumentList.Add("--preview");
        startInfo.ArgumentList.Add(previewPath);

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the WPF Premium demo process.");
    }

    private static PreviewKind GetPreviewKind(string extension)
    {
        var normalized = extension.ToLowerInvariant();
        if (new[] { ".bmp", ".cr2", ".cur", ".dds", ".dng", ".erf", ".exr", ".fts", ".gif", ".hdr", ".heic", ".heif", ".ico", ".jfif", ".jp2", ".jpe", ".jpeg", ".jpg", ".jps", ".mng", ".nef", ".nrw", ".orf", ".pam", ".pbm", ".pcd", ".pcx", ".pef", ".pes", ".pfm", ".pgm", ".picon", ".pict", ".png", ".pnm", ".ppm", ".psd", ".raf", ".ras", ".rw2", ".sfw", ".sgi", ".svg", ".tga", ".tif", ".tiff", ".wbmp", ".webp", ".wpg", ".x3f", ".xbm", ".xcf", ".xpm", ".xwd", ".wdp", ".jxr" }.Contains(normalized)) return PreviewKind.Image;
        if (new[] { ".8svx", ".aac", ".ac3", ".aif", ".aiff", ".aifc", ".amb", ".au", ".avr", ".caf", ".cdda", ".cvs", ".cvsd", ".cvu", ".dts", ".dvms", ".fap", ".flac", ".fssd", ".gsrt", ".hcom", ".htk", ".ima", ".ircam", ".m4a", ".m4r", ".maud", ".mid", ".midi", ".mp2", ".mp3", ".nist", ".oga", ".ogg", ".opus", ".paf", ".prc", ".pvf", ".ra", ".sd2", ".sln", ".smp", ".snd", ".sndr", ".sndt", ".sou", ".sph", ".spx", ".tta", ".txw", ".vms", ".voc", ".vox", ".w64", ".wav", ".wma", ".wv", ".wve", ".amr", ".3ga", ".mka" }.Contains(normalized)) return PreviewKind.Audio;
        if (new[] { ".3gp", ".asf", ".avi", ".f4v", ".flv", ".hevc", ".m2ts", ".m2v", ".m4v", ".mjpeg", ".mkv", ".mov", ".mp4", ".mpeg", ".mpg", ".mts", ".mxf", ".ogv", ".rm", ".swf", ".ts", ".vob", ".webm", ".wmv", ".wtv" }.Contains(normalized)) return PreviewKind.Video;
        return PreviewKind.Text;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WPF_Premium.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate WPF_Premium.slnx from the test output directory.");
    }

    private static Window WaitForMainWindow(FlaApplication application, UIA3Automation automation)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(20))
        {
            try
            {
                var window = application.GetMainWindow(automation, TimeSpan.FromSeconds(1));
                if (window is not null)
                {
                    return window;
                }
            }
            catch (TimeoutException)
            {
                // The WPF window has not been created yet.
            }

            Thread.Sleep(150);
        }

        Assert.Fail("The WPF Premium demo did not expose its preview window within 20 seconds.");
        throw new AssertFailedException("Unreachable after Assert.Fail.");
    }

    private static AutomationElement WaitForElement(AutomationElement root, string automationId)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(15))
        {
            var element = root.FindFirstDescendant(condition => condition.ByAutomationId(automationId));
            if (element is not null)
            {
                return element;
            }

            Thread.Sleep(150);
        }

        Assert.Fail($"UI Automation element '{automationId}' was not found within 15 seconds.");
        throw new AssertFailedException("Unreachable after Assert.Fail.");
    }

    private static void WaitForEnabled(AutomationElement root, string automationId)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(10))
        {
            var element = root.FindFirstDescendant(condition => condition.ByAutomationId(automationId));
            if (element?.IsEnabled == true)
            {
                return;
            }

            Thread.Sleep(100);
        }

        Assert.Fail($"UI Automation element '{automationId}' did not become enabled within 10 seconds.");
    }

    private static void WaitForImageDecode(AutomationElement root)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(45))
        {
            var status = root.FindFirstDescendant(condition => condition.ByAutomationId("PreviewImageStatus"));
            if (status is null || status.IsOffscreen) return;
            if (!status.Name.Contains("Đang giải mã", StringComparison.Ordinal)) return;
            Thread.Sleep(150);
        }

        Assert.Fail("The image preview did not finish decoding within 45 seconds.");
    }

    private static void WaitForPlaybackProgress(AutomationElement root, string initialTime)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(8))
        {
            var time = root.FindFirstDescendant(condition => condition.ByAutomationId("AudioPlaybackTimeText"));
            if (time is not null && time.Name != initialTime && time.Name != "00:00") return;
            Thread.Sleep(200);
        }

        Assert.Fail("Audio playback did not advance the timeline after pressing Play.");
    }

    private static void WaitForVideoReady(AutomationElement root)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(20))
        {
            var status = root.FindFirstDescendant(condition => condition.ByAutomationId("VideoPreviewStatus"));
            if (status is null || status.IsOffscreen || !status.Name.Contains("Äang táº£i", StringComparison.Ordinal)) return;
            Thread.Sleep(200);
        }

        Assert.Fail("The video preview remained in its loading state for 20 seconds.");
    }

    private static void WriteFixture(string path, string extension)
    {
        switch (extension)
        {
            case ".png":
            case ".jpg":
            case ".jpeg":
            case ".bmp":
            case ".gif":
            case ".tif":
            case ".tiff":
            case ".ico":
            case ".webp":
            case ".svg":
            case ".psd":
            case ".jp2":
            case ".cr2":
            case ".heif":
                File.WriteAllBytes(path, Convert.FromBase64String(
                    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a9k8AAAAASUVORK5CYII="));
                break;
            case ".docx":
            case ".docm":
                WriteZip(path, ("word/document.xml", "<w:document xmlns:w=\"urn:w\"><w:body><w:p><w:r><w:t>WPF preview sample</w:t></w:r></w:p></w:body></w:document>"));
                break;
            case ".xlsx":
            case ".xlsm":
                WriteZip(
                    path,
                    ("xl/workbook.xml", "<workbook xmlns:r=\"urn:r\"><sheets><sheet name=\"Sample data\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>"),
                    ("xl/_rels/workbook.xml.rels", "<Relationships><Relationship Id=\"rId1\" Target=\"worksheets/sheet1.xml\"/></Relationships>"),
                    ("xl/sharedStrings.xml", "<sst><si><t>WPF preview sample</t></si></sst>"),
                    ("xl/worksheets/sheet1.xml", "<worksheet><sheetData><row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c></row></sheetData></worksheet>"));
                break;
            case ".pptx":
                WriteZip(
                    path,
                    ("ppt/presentation.xml", "<p:presentation xmlns:p=\"urn:p\"><p:sldSz cx=\"12192000\" cy=\"6858000\"/></p:presentation>"),
                    ("ppt/slides/slide1.xml", "<p:sld xmlns:p=\"urn:p\" xmlns:a=\"urn:a\"><p:sp><p:txBody><a:p><a:r><a:t>WPF preview sample</a:t></a:r></a:p></p:txBody></p:sp></p:sld>"));
                break;
            case ".odt":
                WriteZip(path, ("content.xml", "<office:document-content xmlns:office=\"urn:office\" xmlns:text=\"urn:text\"><office:body><office:text><text:p>WPF preview sample</text:p></office:text></office:body></office:document-content>"));
                break;
            case ".ods":
                WriteZip(path, ("content.xml", "<office:document-content xmlns:office=\"urn:office\" xmlns:table=\"urn:table\" xmlns:text=\"urn:text\"><office:body><office:spreadsheet><table:table table:name=\"Sheet1\"><table:table-row><table:table-cell><text:p>WPF preview sample</text:p></table:table-cell></table:table-row></table:table></office:spreadsheet></office:body></office:document-content>"));
                break;
            case ".odp":
                WriteZip(path, ("content.xml", "<office:document-content xmlns:office=\"urn:office\" xmlns:presentation=\"urn:presentation\" xmlns:draw=\"urn:draw\" xmlns:text=\"urn:text\"><office:body><presentation:presentation><draw:page><draw:frame><draw:text-box><text:p>WPF preview sample</text:p></draw:text-box></draw:frame></draw:page></presentation:presentation></office:body></office:document-content>"));
                break;
            case ".zip":
                WriteZip(path, ("sample.txt", "WPF preview sample"));
                break;
            case ".pdf":
                WritePdf(path);
                break;
            case ".xls":
                using (var workbook = new HSSFWorkbook())
                {
                    var sheet = workbook.createSheet("Sheet1");
                    sheet.createRow(0).createCell(0).setCellValue("WPF preview sample");
                    using var stream = File.Create(path);
                    workbook.write(stream);
                }
                break;
            case ".eml":
                File.WriteAllText(path, "Subject: WPF preview sample\r\nFrom: test@example.com\r\n\r\nPreview body", Encoding.UTF8);
                break;
            case ".json":
                File.WriteAllText(path, "{\"message\":\"WPF preview sample\"}", Encoding.UTF8);
                break;
            case ".html":
                File.WriteAllText(path, "<html><body><h1>WPF preview sample</h1></body></html>", Encoding.UTF8);
                break;
            case ".rtf":
                File.WriteAllText(path, @"{\rtf1\ansi WPF preview sample}", Encoding.ASCII);
                break;
            case ".wav":
                WriteWave(path);
                break;
            case ".txt":
                File.WriteAllText(path, "WPF preview sample", Encoding.UTF8);
                break;
            default:
                File.WriteAllText(path, "WPF preview sample", Encoding.UTF8);
                break;
        }
    }

    private static void WriteZip(string path, params (string EntryName, string Content)[] entries)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (entryName, content) in entries)
        {
            using var writer = new StreamWriter(archive.CreateEntry(entryName).Open(), Encoding.UTF8);
            writer.Write(content);
        }
    }

    private static void WritePdf(string path)
    {
        var content = "BT /F1 16 Tf 20 90 Td (WPF preview sample) Tj ET";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 144] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream"
        };
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.NewLine = "\n";
        writer.WriteLine("%PDF-1.4");
        var offsets = new List<long> { 0 };
        foreach (var (body, index) in objects.Select((body, index) => (body, index)))
        {
            writer.Flush();
            offsets.Add(stream.Position);
            writer.WriteLine($"{index + 1} 0 obj");
            writer.WriteLine(body);
            writer.WriteLine("endobj");
        }
        writer.Flush();
        var xrefOffset = stream.Position;
        writer.WriteLine($"xref\n0 {offsets.Count}\n0000000000 65535 f ");
        foreach (var offset in offsets.Skip(1)) writer.WriteLine($"{offset:0000000000} 00000 n ");
        writer.WriteLine($"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");
        writer.Flush();
        File.WriteAllBytes(path, stream.ToArray());
    }

    private static void WriteWave(string path)
    {
        const int sampleRate = 8000;
        const short bitsPerSample = 16;
        const short channels = 1;
        const int sampleCount = sampleRate / 4;
        var dataLength = sampleCount * channels * (bitsPerSample / 8);

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataLength);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * (bitsPerSample / 8));
        writer.Write((short)(channels * (bitsPerSample / 8)));
        writer.Write(bitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataLength);
        for (var sample = 0; sample < sampleCount; sample++)
        {
            writer.Write((short)(Math.Sin(2 * Math.PI * 440 * sample / sampleRate) * short.MaxValue * .25));
        }
    }

    public sealed record PreviewScenario(string Extension, PreviewKind Kind, string? FilePath = null)
    {
        public string? ExpectedTextSnippet => FilePath is not null ? null : Extension switch
        {
            ".eml" => "Subject: WPF preview sample",
            ".pdf" => null,
            ".doc" => "Không thể đọc tài liệu Office cũ",
            ".ppt" => "Không thể trích xuất văn bản từ định dạng Office cũ này",
            ".azw3" => "Định dạng sách điện tử này chưa có bộ giải mã tích hợp",
            ".epub" => "Không đọc được EPUB",
            ".fb2" => "Không đọc được FB2",
            ".ttf" => "Tệp phông chữ: preview.ttf",
            ".zip" => "sample.txt",
            _ => "WPF preview sample"
        };

        public IReadOnlyList<string> RequiredAutomationIds => Kind switch
        {
            PreviewKind.Image => ["PreviewImage", "ImageZoomInButton", "ImageZoomOutButton", "ImageZoomResetButton", "ImageMagnifierToggle"],
            PreviewKind.Audio => ["AudioWaveform", "AudioPlayButton", "AudioStopButton", "AudioSeekSlider", "AudioPlaybackTimeText"],
            PreviewKind.Video => ["VideoPlayButton", "VideoSeekSlider", "VideoVolumeSlider"],
            PreviewKind.Document => ["PreviewDocumentBrowser"],
            _ => ["PreviewTextContent", "PreviewSearchBox", "PreviewFindNextButton", "PreviewWordWrapToggle", "PreviewCopyButton"]
        };
    }

    public enum PreviewKind
    {
        Image,
        Audio,
        Video,
        Document,
        Text
    }
}
