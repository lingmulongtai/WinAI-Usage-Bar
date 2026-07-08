using System.IO.Compression;
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

            var script = await File.ReadAllTextAsync(result.ScriptPath!);
            Assert.Contains("Wait-Process -Id $ProcessIdToWait", script, StringComparison.Ordinal);
            Assert.Contains("$ProcessIdToWait = 1234", script, StringComparison.Ordinal);
            Assert.Contains("$RestartAfterInstall = $true", script, StringComparison.Ordinal);
            Assert.Contains("Expand-Archive -LiteralPath $PackagePath", script, StringComparison.Ordinal);
            Assert.Contains("function Restore-Backup", script, StringComparison.Ordinal);
            Assert.Contains("Copy-Item -LiteralPath $_.FullName -Destination $BackupDirectory -Recurse -Force", script, StringComparison.Ordinal);
            Assert.Contains("Remove-Item -LiteralPath $_.FullName -Recurse -Force", script, StringComparison.Ordinal);
            Assert.Contains("Copy-Item -LiteralPath $_.FullName", script, StringComparison.Ordinal);
            Assert.Contains("Restore-Backup", script, StringComparison.Ordinal);
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
}
