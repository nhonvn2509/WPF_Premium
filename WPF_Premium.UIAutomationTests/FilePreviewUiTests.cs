using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
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
        new(".mp3", PreviewKind.Audio),
        new(".wav", PreviewKind.Audio),
        new(".wma", PreviewKind.Audio),
        new(".m4a", PreviewKind.Audio),
        new(".aac", PreviewKind.Audio),
        new(".mid", PreviewKind.Audio),
        new(".midi", PreviewKind.Audio),
        new(".mp4", PreviewKind.Video),
        new(".wmv", PreviewKind.Video),
        new(".avi", PreviewKind.Video),
        new(".mpeg", PreviewKind.Video),
        new(".mpg", PreviewKind.Video),
        new(".mov", PreviewKind.Video),
        new(".txt", PreviewKind.Text),
        new(".csv", PreviewKind.Text),
        new(".xml", PreviewKind.Text),
        new(".md", PreviewKind.Text),
        new(".log", PreviewKind.Text),
        new(".json", PreviewKind.Text),
        new(".eml", PreviewKind.Text),
        new(".pdf", PreviewKind.Text),
        new(".docx", PreviewKind.Text),
        new(".xlsx", PreviewKind.Text),
        new(".xlsm", PreviewKind.Text),
        new(".doc", PreviewKind.Text),
        new(".xls", PreviewKind.Text),
        new(".ppt", PreviewKind.Text),
        new(".pptx", PreviewKind.Text),
        new(".zip", PreviewKind.Text)
    ];

    public static IEnumerable<object[]> PreviewCases
    {
        get
        {
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
        var fixturePath = Path.Combine(fixtureDirectory, $"preview{scenario.Extension}");
        WriteFixture(fixturePath, scenario.Extension);

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
            Assert.AreEqual($"preview{scenario.Extension}", elementsByAutomationId["FileNameText"].Name);

            foreach (var automationId in scenario.RequiredAutomationIds)
            {
                Assert.IsTrue(
                    elementsByAutomationId.ContainsKey(automationId),
                    $"Expected UI element '{automationId}' for {scenario.Extension} preview.");
            }

            if (scenario.Kind == PreviewKind.Image)
            {
                Assert.IsTrue(elementsByAutomationId.ContainsKey("PreviewZoomText"));
            }
            else if (scenario.Kind == PreviewKind.Audio)
            {
                Assert.AreEqual("Dạng sóng trực quan", elementsByAutomationId["PreviewZoomText"].Name);
                if (scenario.Extension == ".wav")
                {
                    WaitForEnabled(window, "AudioSeekSlider");
                }
                Assert.IsTrue(elementsByAutomationId.ContainsKey("AudioPlaybackTimeText"));
            }
            else if (scenario.Kind == PreviewKind.Video)
            {
                Assert.AreEqual("Video", elementsByAutomationId["PreviewZoomText"].Name);
            }
            else
            {
                var expectedSnippet = scenario.ExpectedTextSnippet;
                var previewText = elementsByAutomationId["PreviewTextContent"].Patterns.Value.PatternOrDefault?.Value;
                Assert.IsNotNull(previewText, "The text preview should expose its contents through UI Automation.");
                StringAssert.Contains(previewText, expectedSnippet);
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
                File.WriteAllBytes(path, Convert.FromBase64String(
                    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a9k8AAAAASUVORK5CYII="));
                break;
            case ".docx":
                WriteZip(path, ("word/document.xml", "<w:document xmlns:w=\"urn:w\"><w:body><w:p><w:r><w:t>WPF preview sample</w:t></w:r></w:p></w:body></w:document>"));
                break;
            case ".xlsx":
            case ".xlsm":
                WriteZip(
                    path,
                    ("xl/sharedStrings.xml", "<sst><si><t>WPF preview sample</t></si></sst>"),
                    ("xl/worksheets/sheet1.xml", "<worksheet><sheetData><row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c></row></sheetData></worksheet>"));
                break;
            case ".zip":
                WriteZip(path, ("sample.txt", "WPF preview sample"));
                break;
            case ".pdf":
                File.WriteAllText(path, "%PDF-1.4\nBT (WPF preview sample) Tj ET\n%%EOF", Encoding.Latin1);
                break;
            case ".eml":
                File.WriteAllText(path, "Subject: WPF preview sample\r\nFrom: test@example.com\r\n\r\nPreview body", Encoding.UTF8);
                break;
            case ".json":
                File.WriteAllText(path, "{\"message\":\"WPF preview sample\"}", Encoding.UTF8);
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

    public sealed record PreviewScenario(string Extension, PreviewKind Kind)
    {
        public string ExpectedTextSnippet => Extension switch
        {
            ".eml" => "Subject: WPF preview sample",
            ".pdf" => "PDF cần bộ đọc PDF",
            ".doc" or ".xls" or ".ppt" or ".pptx" => "Định dạng Office này cần",
            ".zip" => "sample.txt",
            _ => "WPF preview sample"
        };

        public IReadOnlyList<string> RequiredAutomationIds => Kind switch
        {
            PreviewKind.Image => ["PreviewImage", "ImageZoomInButton", "ImageZoomOutButton", "ImageZoomResetButton", "ImageMagnifierToggle"],
            PreviewKind.Audio => ["AudioWaveform", "AudioPlayButton", "AudioStopButton", "AudioSeekSlider", "AudioPlaybackTimeText"],
            PreviewKind.Video => ["VideoPlayButton", "VideoSeekSlider", "VideoVolumeSlider"],
            _ => ["PreviewTextContent", "PreviewSearchBox", "PreviewFindNextButton", "PreviewWordWrapToggle", "PreviewCopyButton"]
        };
    }

    public enum PreviewKind
    {
        Image,
        Audio,
        Video,
        Text
    }
}
