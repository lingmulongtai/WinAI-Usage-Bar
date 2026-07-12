using System.IO.Compression;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class ReleasePackagingScriptTests
{
    [Fact]
    public async Task PackageScript_CreatesZipAndChecksumWithWindowsPowerShell()
    {
        var root = FindRepositoryRoot();
        var workRoot = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var publishPath = Path.Combine(workRoot, "publish");
        var outputPath = Path.Combine(workRoot, "packages");
        var packageName = $"PackageSmoke-{Guid.NewGuid():N}";
        Directory.CreateDirectory(publishPath);
        Directory.CreateDirectory(Path.Combine(publishPath, "nested"));
        await File.WriteAllTextAsync(Path.Combine(publishPath, "WinAiUsageBar.App.exe"), "stub");
        await File.WriteAllTextAsync(Path.Combine(publishPath, "nested", "data.txt"), "payload");

        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(Path.Combine(root, "scripts", "package.ps1"));
            startInfo.ArgumentList.Add("-PublishPath");
            startInfo.ArgumentList.Add(publishPath);
            startInfo.ArgumentList.Add("-OutputDirectory");
            startInfo.ArgumentList.Add(outputPath);
            startInfo.ArgumentList.Add("-Runtime");
            startInfo.ArgumentList.Add("win-x64");
            startInfo.ArgumentList.Add("-PackageName");
            startInfo.ArgumentList.Add(packageName);

            using var process = System.Diagnostics.Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start PowerShell.");
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            Assert.True(process.ExitCode == 0, $"stdout: {output}{Environment.NewLine}stderr: {error}");

            var zipPath = Path.Combine(outputPath, $"{packageName}.zip");
            var checksumPath = $"{zipPath}.sha256";
            Assert.True(File.Exists(zipPath), $"Package was not created: {zipPath}");
            Assert.True(File.Exists(checksumPath), $"Checksum was not created: {checksumPath}");
            Assert.Contains($"{packageName}.zip", await File.ReadAllTextAsync(checksumPath), StringComparison.Ordinal);

            using var archive = ZipFile.OpenRead(zipPath);
            Assert.Contains(archive.Entries, entry => entry.FullName == "WinAiUsageBar.App.exe");
            Assert.Contains(archive.Entries, entry => entry.FullName == "nested/data.txt");
        }
        finally
        {
            if (Directory.Exists(workRoot))
            {
                Directory.Delete(workRoot, recursive: true);
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WinAIUsageBar.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root with WinAIUsageBar.sln was not found.");
    }
}
