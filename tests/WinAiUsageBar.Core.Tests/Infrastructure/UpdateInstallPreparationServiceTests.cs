using System.IO.Compression;
using System.Diagnostics;
using System.Text.Json;
using WinAiUsageBar.Infrastructure.Storage;
using WinAiUsageBar.Infrastructure.Updates;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class UpdateInstallPreparationServiceTests
{
    [Fact]
    public async Task PrepareAsync_WritesApplyScriptForValidPackageAndInstallDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(Path.Combine(root, "appdata"));
        var installDirectory = Path.Combine(root, "install");
        Directory.CreateDirectory(installDirectory);
        await File.WriteAllTextAsync(Path.Combine(installDirectory, "WinAiUsageBar.App.exe"), "old exe");
        var packagePath = Path.Combine(root, "WinAIUsageBar-0.2.0-win-x64.zip");
        CreatePackage(packagePath, includeAppExe: true);
        var now = new DateTimeOffset(2026, 7, 8, 23, 0, 0, TimeSpan.Zero);
        var service = new UpdateInstallPreparationService(paths, () => now);

        try
        {
            var result = await service.PrepareAsync(
                new UpdateInstallPreparationRequest(
                    packagePath,
                    installDirectory,
                    ProcessIdToWait: 1234,
                    RestartAfterInstall: true),
                CancellationToken.None);

            Assert.Equal(UpdateInstallPreparationStatus.Prepared, result.Status);
            Assert.True(File.Exists(result.ScriptPath));
            Assert.Contains("apply-update.ps1", result.Command, StringComparison.Ordinal);
            Assert.Equal(Path.GetFullPath(packagePath), result.PackagePath);
            Assert.Equal(Path.GetFullPath(installDirectory), result.InstallDirectory);
            Assert.StartsWith(paths.UpdatesDirectory, result.ScriptPath, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith("staging", result.StagingDirectory, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith("backup", result.BackupDirectory, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(Path.Combine(Path.GetDirectoryName(result.ScriptPath!)!, "install-result.json"), result.ResultPath);

            var script = await File.ReadAllTextAsync(result.ScriptPath!);
            Assert.Contains("# WinAI Usage Bar generated update script", script, StringComparison.Ordinal);
            Assert.Contains("$WinAiUsageBarGeneratedUpdateScriptVersion = 1", script, StringComparison.Ordinal);
            Assert.Contains("$ResultPath = ", script, StringComparison.Ordinal);
            Assert.Contains("function Write-InstallResult", script, StringComparison.Ordinal);
            Assert.Contains("status = $Status", script, StringComparison.Ordinal);
            Assert.Contains("completedAtUtc = (Get-Date).ToUniversalTime().ToString('o')", script, StringComparison.Ordinal);
            Assert.Contains("packageFileName = [System.IO.Path]::GetFileName($PackagePath)", script, StringComparison.Ordinal);
            Assert.Contains("installDirectory = $InstallDirectory", script, StringComparison.Ordinal);
            Assert.Contains("validationStatus = $ValidationStatus", script, StringComparison.Ordinal);
            Assert.Contains("validationExitCode", script, StringComparison.Ordinal);
            Assert.Contains("$ValidationOutputPath = Join-Path $ResultDirectory 'validation.out.txt'", script, StringComparison.Ordinal);
            Assert.Contains("$ValidationErrorPath = Join-Path $ResultDirectory 'validation.err.txt'", script, StringComparison.Ordinal);
            Assert.Contains("validationOutputPath = $ValidationOutputPath", script, StringComparison.Ordinal);
            Assert.Contains("validationErrorPath = $ValidationErrorPath", script, StringComparison.Ordinal);
            Assert.Contains("Wait-Process -Id $ProcessIdToWait", script, StringComparison.Ordinal);
            Assert.Contains("$ProcessIdToWait = 1234", script, StringComparison.Ordinal);
            Assert.Contains("$RestartAfterInstall = $true", script, StringComparison.Ordinal);
            Assert.Contains("Expand-Archive -LiteralPath $PackagePath", script, StringComparison.Ordinal);
            Assert.Contains("function Restore-Backup", script, StringComparison.Ordinal);
            Assert.Contains("function Invoke-PostInstallValidation", script, StringComparison.Ordinal);
            Assert.Contains("$StartInfo.Arguments = '--smoke-test'", script, StringComparison.Ordinal);
            Assert.Contains("$StartInfo.RedirectStandardOutput = $true", script, StringComparison.Ordinal);
            Assert.Contains("$StartInfo.RedirectStandardError = $true", script, StringComparison.Ordinal);
            Assert.Contains("function Write-ValidationLogFiles", script, StringComparison.Ordinal);
            Assert.Contains("[System.IO.File]::WriteAllText($ValidationOutputPath", script, StringComparison.Ordinal);
            Assert.Contains("[System.IO.File]::WriteAllText($ValidationErrorPath", script, StringComparison.Ordinal);
            Assert.Contains("Post-install smoke test failed", script, StringComparison.Ordinal);
            Assert.Contains("Copy-Item -LiteralPath $_.FullName -Destination $BackupDirectory -Recurse -Force", script, StringComparison.Ordinal);
            Assert.Contains("Remove-Item -LiteralPath $_.FullName -Recurse -Force", script, StringComparison.Ordinal);
            Assert.Contains("Copy-Item -LiteralPath $_.FullName", script, StringComparison.Ordinal);
            Assert.Contains("Restore-Backup", script, StringComparison.Ordinal);
            Assert.Contains("Write-InstallResult -Status 'Succeeded'", script, StringComparison.Ordinal);
            Assert.Contains("Write-InstallResult -Status 'Failed'", script, StringComparison.Ordinal);
            Assert.Contains("Start-Process -FilePath $RestartExe", script, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PreparedScript_WritesSucceededResultFileWhenApplied()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(Path.Combine(root, "appdata"));
        var installDirectory = Path.Combine(root, "install");
        Directory.CreateDirectory(installDirectory);
        await File.WriteAllTextAsync(Path.Combine(installDirectory, "WinAiUsageBar.App.exe"), "old exe");
        var packagePath = Path.Combine(root, "WinAIUsageBar-0.2.0-win-x64.zip");
        CreatePackageFromBuiltAppOutput(packagePath);
        var service = new UpdateInstallPreparationService(paths);

        try
        {
            var result = await service.PrepareAsync(
                new UpdateInstallPreparationRequest(
                    packagePath,
                    installDirectory,
                    ProcessIdToWait: 0,
                    RestartAfterInstall: false),
                CancellationToken.None);

            var scriptResult = await RunPowerShellScriptAsync(result.ScriptPath!);

            Assert.True(
                scriptResult.ExitCode == 0,
                CreateScriptFailureMessage(scriptResult, result.ResultPath));
            Assert.True(File.Exists(result.ResultPath));
            using var resultDocument = JsonDocument.Parse(await File.ReadAllTextAsync(result.ResultPath!));
            var rootElement = resultDocument.RootElement;
            Assert.Equal("Succeeded", rootElement.GetProperty("status").GetString());
            Assert.Equal("Update installed successfully. Post-install validation passed.", rootElement.GetProperty("message").GetString());
            Assert.Equal(Path.GetFileName(packagePath), rootElement.GetProperty("packageFileName").GetString());
            Assert.Equal(Path.GetFullPath(installDirectory), rootElement.GetProperty("installDirectory").GetString());
            Assert.Equal("Passed", rootElement.GetProperty("validationStatus").GetString());
            Assert.Equal(0, rootElement.GetProperty("validationExitCode").GetInt32());
            var validationOutputPath = rootElement.GetProperty("validationOutputPath").GetString();
            var validationErrorPath = rootElement.GetProperty("validationErrorPath").GetString();
            Assert.EndsWith("validation.out.txt", validationOutputPath, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith("validation.err.txt", validationErrorPath, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(validationOutputPath));
            Assert.True(File.Exists(validationErrorPath));
            Assert.True(rootElement.GetProperty("validationOutputBytes").GetInt64() >= 0);
            Assert.True(rootElement.GetProperty("validationErrorBytes").GetInt64() >= 0);
            Assert.False(string.IsNullOrWhiteSpace(rootElement.GetProperty("completedAtUtc").GetString()));
            Assert.True(File.Exists(Path.Combine(installDirectory, "WinAiUsageBar.App.exe")));
            Assert.True(File.Exists(Path.Combine(installDirectory, "WinAiUsageBar.App.dll")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PreparedScript_RestoresBackupWhenPostInstallValidationFails()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(Path.Combine(root, "appdata"));
        var installDirectory = Path.Combine(root, "install");
        Directory.CreateDirectory(installDirectory);
        await File.WriteAllTextAsync(Path.Combine(installDirectory, "WinAiUsageBar.App.exe"), "old exe");
        var packagePath = Path.Combine(root, "WinAIUsageBar-0.2.0-win-x64.zip");
        CreatePackage(packagePath, includeAppExe: true);
        var service = new UpdateInstallPreparationService(paths);

        try
        {
            var result = await service.PrepareAsync(
                new UpdateInstallPreparationRequest(
                    packagePath,
                    installDirectory,
                    ProcessIdToWait: 0,
                    RestartAfterInstall: false),
                CancellationToken.None);

            var scriptResult = await RunPowerShellScriptAsync(result.ScriptPath!);

            Assert.NotEqual(0, scriptResult.ExitCode);
            Assert.True(File.Exists(result.ResultPath));
            using var resultDocument = JsonDocument.Parse(await File.ReadAllTextAsync(result.ResultPath!));
            var rootElement = resultDocument.RootElement;
            Assert.Equal("Failed", rootElement.GetProperty("status").GetString());
            Assert.Contains("Post-install smoke test", rootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
            Assert.Equal("FailedToStart", rootElement.GetProperty("validationStatus").GetString());
            Assert.False(rootElement.TryGetProperty("validationExitCode", out _));
            Assert.Equal("old exe", await File.ReadAllTextAsync(Path.Combine(installDirectory, "WinAiUsageBar.App.exe")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PreparedScript_WritesFailedResultFileWhenApplyFails()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(Path.Combine(root, "appdata"));
        var installDirectory = Path.Combine(root, "install");
        Directory.CreateDirectory(installDirectory);
        await File.WriteAllTextAsync(Path.Combine(installDirectory, "WinAiUsageBar.App.exe"), "old exe");
        var packagePath = Path.Combine(root, "WinAIUsageBar-0.2.0-win-x64.zip");
        CreatePackage(packagePath, includeAppExe: true);
        var service = new UpdateInstallPreparationService(paths);

        try
        {
            var result = await service.PrepareAsync(
                new UpdateInstallPreparationRequest(
                    packagePath,
                    installDirectory,
                    ProcessIdToWait: 0,
                    RestartAfterInstall: false),
                CancellationToken.None);
            File.Delete(packagePath);

            var scriptResult = await RunPowerShellScriptAsync(result.ScriptPath!);

            Assert.NotEqual(0, scriptResult.ExitCode);
            Assert.True(File.Exists(result.ResultPath));
            using var resultDocument = JsonDocument.Parse(await File.ReadAllTextAsync(result.ResultPath!));
            var rootElement = resultDocument.RootElement;
            Assert.Equal("Failed", rootElement.GetProperty("status").GetString());
            Assert.Contains("Update package was not found", rootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
            Assert.Equal(Path.GetFileName(packagePath), rootElement.GetProperty("packageFileName").GetString());
            Assert.Equal(Path.GetFullPath(installDirectory), rootElement.GetProperty("installDirectory").GetString());
            Assert.Equal("NotRun", rootElement.GetProperty("validationStatus").GetString());
            Assert.Equal("old exe", await File.ReadAllTextAsync(Path.Combine(installDirectory, "WinAiUsageBar.App.exe")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PrepareAsync_WritesApplyScriptWithUtf8BomForWindowsPowerShellUnicodePaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", $"unicode-{Guid.NewGuid():N}");
        var paths = new AppDataPaths(Path.Combine(root, "appdata"));
        var installDirectory = Path.Combine(root, "install-日本語");
        Directory.CreateDirectory(installDirectory);
        await File.WriteAllTextAsync(Path.Combine(installDirectory, "WinAiUsageBar.App.exe"), "old exe");
        var packagePath = Path.Combine(root, "WinAIUsageBar-0.2.0-win-x64.zip");
        CreatePackage(packagePath, includeAppExe: true);
        var service = new UpdateInstallPreparationService(paths);

        try
        {
            var result = await service.PrepareAsync(
                new UpdateInstallPreparationRequest(
                    packagePath,
                    installDirectory,
                    ProcessIdToWait: 0,
                    RestartAfterInstall: false),
                CancellationToken.None);

            Assert.Equal(UpdateInstallPreparationStatus.Prepared, result.Status);
            var bytes = await File.ReadAllBytesAsync(result.ScriptPath!);
            Assert.True(bytes.Length > 3);
            Assert.Equal(0xEF, bytes[0]);
            Assert.Equal(0xBB, bytes[1]);
            Assert.Equal(0xBF, bytes[2]);

            var script = await File.ReadAllTextAsync(result.ScriptPath!);
            Assert.Contains("install-日本語", script, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PrepareAsync_RejectsPackageWithoutAppExecutable()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(Path.Combine(root, "appdata"));
        var installDirectory = Path.Combine(root, "install");
        Directory.CreateDirectory(installDirectory);
        await File.WriteAllTextAsync(Path.Combine(installDirectory, "WinAiUsageBar.App.exe"), "old exe");
        var packagePath = Path.Combine(root, "WinAIUsageBar-0.2.0-win-x64.zip");
        CreatePackage(packagePath, includeAppExe: false);
        var service = new UpdateInstallPreparationService(paths);

        try
        {
            var result = await service.PrepareAsync(
                new UpdateInstallPreparationRequest(
                    packagePath,
                    installDirectory,
                    ProcessIdToWait: 0,
                    RestartAfterInstall: false),
                CancellationToken.None);

            Assert.Equal(UpdateInstallPreparationStatus.InvalidPackage, result.Status);
            Assert.Null(result.ScriptPath);
            Assert.False(Directory.Exists(paths.UpdatesDirectory));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("/absolute.txt")]
    [InlineData("C:/absolute.txt")]
    [InlineData("folder/../outside.txt")]
    public async Task PrepareAsync_RejectsPackageWithUnsafeArchiveEntry(string entryName)
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(Path.Combine(root, "appdata"));
        var installDirectory = Path.Combine(root, "install");
        Directory.CreateDirectory(installDirectory);
        await File.WriteAllTextAsync(Path.Combine(installDirectory, "WinAiUsageBar.App.exe"), "old exe");
        var packagePath = Path.Combine(root, "WinAIUsageBar-0.2.0-win-x64.zip");
        CreatePackage(packagePath, includeAppExe: true, entryName);
        var service = new UpdateInstallPreparationService(paths);

        try
        {
            var result = await service.PrepareAsync(
                new UpdateInstallPreparationRequest(
                    packagePath,
                    installDirectory,
                    ProcessIdToWait: 0,
                    RestartAfterInstall: false),
                CancellationToken.None);

            Assert.Equal(UpdateInstallPreparationStatus.InvalidPackage, result.Status);
            Assert.Contains("unsafe archive entry", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Null(result.ScriptPath);
            Assert.False(Directory.Exists(paths.UpdatesDirectory));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PrepareAsync_RejectsInstallDirectoryWithoutAppExecutable()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(Path.Combine(root, "appdata"));
        var installDirectory = Path.Combine(root, "install");
        Directory.CreateDirectory(installDirectory);
        var packagePath = Path.Combine(root, "WinAIUsageBar-0.2.0-win-x64.zip");
        CreatePackage(packagePath, includeAppExe: true);
        var service = new UpdateInstallPreparationService(paths);

        try
        {
            var result = await service.PrepareAsync(
                new UpdateInstallPreparationRequest(
                    packagePath,
                    installDirectory,
                    ProcessIdToWait: 0,
                    RestartAfterInstall: false),
                CancellationToken.None);

            Assert.Equal(UpdateInstallPreparationStatus.InvalidInstallDirectory, result.Status);
            Assert.Null(result.ScriptPath);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static void CreatePackageFromBuiltAppOutput(string packagePath)
    {
        var sourceDirectory = AppContext.BaseDirectory;
        var appExe = Path.Combine(sourceDirectory, "WinAiUsageBar.App.exe");
        if (!File.Exists(appExe))
        {
            throw new InvalidOperationException($"Built app executable was not found for update script tests: {appExe}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(packagePath)!);
        using var stream = File.Open(packagePath, FileMode.CreateNew);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file).Replace('\\', '/');
            archive.CreateEntryFromFile(file, relativePath);
        }
    }

    private static void CreatePackage(
        string packagePath,
        bool includeAppExe,
        params string[] extraEntryNames)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(packagePath)!);
        using var stream = File.Open(packagePath, FileMode.CreateNew);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        if (includeAppExe)
        {
            var exe = archive.CreateEntry("WinAiUsageBar.App.exe");
            using var writer = new StreamWriter(exe.Open());
            writer.Write("new exe");
        }

        var readme = archive.CreateEntry("README.txt");
        using (var readmeWriter = new StreamWriter(readme.Open()))
        {
            readmeWriter.Write("package");
        }

        foreach (var entryName in extraEntryNames)
        {
            var entry = archive.CreateEntry(entryName);
            using var writer = new StreamWriter(entry.Open());
            writer.Write("unsafe");
        }
    }

    private static async Task<ScriptRunResult> RunPowerShellScriptAsync(string scriptPath)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            ArgumentList =
            {
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                scriptPath
            }
        });
        Assert.NotNull(process);
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ScriptRunResult(
            process.ExitCode,
            await outputTask,
            await errorTask);
    }

    private static string CreateScriptFailureMessage(ScriptRunResult result, string? resultPath)
    {
        var installResult = resultPath is not null && File.Exists(resultPath)
            ? File.ReadAllText(resultPath)
            : "(missing)";
        return $"""
            Expected generated update script to exit with 0.
            Exit code: {result.ExitCode}
            Stdout:
            {result.Output}
            Stderr:
            {result.Error}
            install-result.json:
            {installResult}
            """;
    }

    private sealed record ScriptRunResult(int ExitCode, string Output, string Error);
}
