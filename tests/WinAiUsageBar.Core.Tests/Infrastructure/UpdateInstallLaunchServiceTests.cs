using System.Diagnostics;
using WinAiUsageBar.Infrastructure.Storage;
using WinAiUsageBar.Infrastructure.Updates;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class UpdateInstallLaunchServiceTests
{
    [Fact]
    public async Task LaunchAsync_StartsApplyScriptUnderUpdatesDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(Path.Combine(root, "appdata"));
        var scriptPath = Path.Combine(paths.UpdatesDirectory, "install-1", "apply-update.ps1");
        ProcessStartInfo? capturedStartInfo = null;
        var service = new UpdateInstallLaunchService(
            paths,
            startInfo =>
            {
                capturedStartInfo = startInfo;
                return 4321;
            });

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
            await File.WriteAllTextAsync(scriptPath, "$ErrorActionPreference = 'Stop'");

            var result = await service.LaunchAsync(
                new UpdateInstallLaunchRequest(scriptPath),
                CancellationToken.None);

            Assert.Equal(UpdateInstallLaunchStatus.Launched, result.Status);
            Assert.Equal(4321, result.ProcessId);
            Assert.Equal(Path.GetFullPath(scriptPath), result.ScriptPath);
            Assert.Contains("apply-update.ps1", result.Command, StringComparison.Ordinal);
            Assert.NotNull(capturedStartInfo);
            Assert.Equal("powershell.exe", capturedStartInfo.FileName);
            Assert.False(capturedStartInfo.UseShellExecute);
            Assert.True(capturedStartInfo.CreateNoWindow);
            Assert.Equal(
                ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Path.GetFullPath(scriptPath)],
                capturedStartInfo.ArgumentList.ToArray());
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
    public async Task LaunchAsync_RejectsExternalScript()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(Path.Combine(root, "appdata"));
        var externalScript = Path.Combine(root, "outside", "apply-update.ps1");
        var launchCount = 0;
        var service = new UpdateInstallLaunchService(
            paths,
            _ =>
            {
                launchCount++;
                return 4321;
            });

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(externalScript)!);
            await File.WriteAllTextAsync(externalScript, "Write-Output nope");

            var result = await service.LaunchAsync(
                new UpdateInstallLaunchRequest(externalScript),
                CancellationToken.None);

            Assert.Equal(UpdateInstallLaunchStatus.InvalidScript, result.Status);
            Assert.Equal(0, launchCount);
            Assert.Contains("app-owned updates", result.Message, StringComparison.OrdinalIgnoreCase);
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
    public async Task LaunchAsync_RejectsWrongFileName()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(Path.Combine(root, "appdata"));
        var scriptPath = Path.Combine(paths.UpdatesDirectory, "install-1", "not-update.ps1");
        var service = new UpdateInstallLaunchService(paths, _ => 4321);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
            await File.WriteAllTextAsync(scriptPath, "Write-Output nope");

            var result = await service.LaunchAsync(
                new UpdateInstallLaunchRequest(scriptPath),
                CancellationToken.None);

            Assert.Equal(UpdateInstallLaunchStatus.InvalidScript, result.Status);
            Assert.Contains("apply-update.ps1", result.Message, StringComparison.OrdinalIgnoreCase);
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
    public async Task LaunchAsync_ReturnsErrorWhenProcessLaunchFails()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(Path.Combine(root, "appdata"));
        var scriptPath = Path.Combine(paths.UpdatesDirectory, "install-1", "apply-update.ps1");
        var service = new UpdateInstallLaunchService(
            paths,
            _ => throw new InvalidOperationException("blocked"));

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
            await File.WriteAllTextAsync(scriptPath, "$ErrorActionPreference = 'Stop'");

            var result = await service.LaunchAsync(
                new UpdateInstallLaunchRequest(scriptPath),
                CancellationToken.None);

            Assert.Equal(UpdateInstallLaunchStatus.Error, result.Status);
            Assert.Contains("blocked", result.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
