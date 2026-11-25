using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace WPF_Premium.Controls;

internal static class LibreOfficePreviewConverter
{
    private static readonly Lazy<string?> CachedExecutablePath = new(DiscoverExecutablePath);
    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WPF_Premium",
        "PreviewCache");

    public static bool IsAvailable => !string.IsNullOrEmpty(CachedExecutablePath.Value);

    public static async Task<string?> ConvertToPdfAsync(string sourcePath, CancellationToken cancellationToken)
    {
        var sofficePath = CachedExecutablePath.Value;
        if (string.IsNullOrEmpty(sofficePath) || !File.Exists(sourcePath))
        {
            return null;
        }

        var fullSourcePath = Path.GetFullPath(sourcePath);
        var info = new FileInfo(fullSourcePath);
        var cacheKey = ComputeCacheKey(fullSourcePath, info.LastWriteTimeUtc.Ticks, info.Length);
        var cachedPdfPath = Path.Combine(CacheDirectory, $"{cacheKey}.pdf");
        if (File.Exists(cachedPdfPath) && new FileInfo(cachedPdfPath).Length > 0)
        {
            return cachedPdfPath;
        }

        Directory.CreateDirectory(CacheDirectory);
        var workDir = Path.Combine(CacheDirectory, $"work-{Guid.NewGuid():N}");
        var profileDir = Path.Combine(workDir, "profile");
        Directory.CreateDirectory(workDir);
        Directory.CreateDirectory(profileDir);

        try
        {
            var profileUri = new Uri(profileDir).AbsoluteUri;
            var args = $"--headless --norestore --invisible --nodefault --nolockcheck --nofirststartwizard \"-env:UserInstallation={profileUri}\" --convert-to pdf --outdir \"{workDir}\" \"{fullSourcePath}\"";

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = sofficePath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            if (!process.Start())
            {
                return null;
            }

            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                }
            });

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(45));
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);

            var expectedPdf = Path.Combine(workDir, Path.GetFileNameWithoutExtension(fullSourcePath) + ".pdf");
            if (!File.Exists(expectedPdf) || new FileInfo(expectedPdf).Length == 0)
            {
                expectedPdf = Directory.EnumerateFiles(workDir, "*.pdf").FirstOrDefault() ?? string.Empty;
            }

            if (string.IsNullOrEmpty(expectedPdf) || !File.Exists(expectedPdf))
            {
                return null;
            }

            File.Move(expectedPdf, cachedPdfPath, overwrite: true);
            return cachedPdfPath;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
        finally
        {
            try
            {
                if (Directory.Exists(workDir))
                {
                    Directory.Delete(workDir, recursive: true);
                }
            }
            catch
            {
            }
        }
    }

    private static string ComputeCacheKey(string fullPath, long lastWriteTicks, long length)
    {
        var input = $"{fullPath}|{lastWriteTicks}|{length}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash);
    }

    private static string? DiscoverExecutablePath()
    {
        foreach (var envVar in new[] { "VSAT_LIBREOFFICE_PATH", "LIBREOFFICE_PATH" })
        {
            var envValue = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrWhiteSpace(envValue) && File.Exists(envValue))
            {
                return envValue;
            }
        }

        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "LibreOffice", "program", "soffice.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LibreOffice", "program", "soffice.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "LibreOffice", "program", "soffice.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "LibreOffice", "program", "soffice.exe")
        };

        foreach (var registryPath in new[]
        {
            @"SOFTWARE\LibreOffice\UNO\InstallPath",
            @"SOFTWARE\WOW6432Node\LibreOffice\UNO\InstallPath"
        })
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(registryPath);
                if (key?.GetValue(null) is string installDir && !string.IsNullOrWhiteSpace(installDir))
                {
                    candidates.Add(Path.Combine(installDir, "soffice.exe"));
                }
            }
            catch
            {
            }
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathEnv))
        {
            foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                candidates.Add(Path.Combine(dir, "soffice.exe"));
            }
        }

        return candidates.FirstOrDefault(File.Exists);
    }
}
