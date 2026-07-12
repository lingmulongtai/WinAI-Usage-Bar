namespace WinAiUsageBar.Core.Tests.App;

public sealed class SameInstallUpdateReportScriptTests
{
    [Fact]
    public async Task ReportScript_CreatesReportWithWindowsPowerShell()
    {
        var root = FindRepositoryRoot();
        var outputDirectory = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));

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
            startInfo.ArgumentList.Add(Path.Combine(root, "scripts", "new-same-install-update-report.ps1"));
            startInfo.ArgumentList.Add("-OutputDirectory");
            startInfo.ArgumentList.Add(outputDirectory);
            startInfo.ArgumentList.Add("-Commit");
            startInfo.ArgumentList.Add("testcommit");
            startInfo.ArgumentList.Add("-AppVersion");
            startInfo.ArgumentList.Add("0.0.0");
            startInfo.ArgumentList.Add("-SourceVersion");
            startInfo.ArgumentList.Add("0.1.6");
            startInfo.ArgumentList.Add("-TargetVersion");
            startInfo.ArgumentList.Add("0.1.7");
            startInfo.ArgumentList.Add("-InstallPath");
            startInfo.ArgumentList.Add(Path.Combine(outputDirectory, "install"));
            startInfo.ArgumentList.Add("-NormalAppDataPath");
            startInfo.ArgumentList.Add(Path.Combine(outputDirectory, "appdata"));

            using var process = System.Diagnostics.Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start PowerShell.");
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            Assert.True(process.ExitCode == 0, $"stdout: {output}{Environment.NewLine}stderr: {error}");

            var reportPath = Assert.Single(Directory.GetFiles(outputDirectory, "same-install-update-*.md"));
            var report = await File.ReadAllTextAsync(reportPath);
            Assert.Contains("Checklist source: docs\\same-install-update-dogfooding.md", report, StringComparison.Ordinal);
            Assert.Contains("| Source installed version | 0.1.6 |", report, StringComparison.Ordinal);
            Assert.Contains("| Target release version | 0.1.7 |", report, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
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
