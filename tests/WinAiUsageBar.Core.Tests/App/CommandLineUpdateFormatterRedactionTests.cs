using WinAiUsageBar.App.Services;
using WinAiUsageBar.Infrastructure.Updates;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class CommandLineUpdateFormatterRedactionTests
{
    [Fact]
    public void Formatters_RedactSensitiveUpdateOutput()
    {
        var updateCheck = new ReleaseUpdateCheckResult(
            UpdateCheckStatus.UpdateAvailable,
            CurrentVersion: "0.1.0 token=current-secret",
            LatestVersion: "0.2.0 cookie=latest-secret",
            "Authorization: Bearer update-secret",
            IsUpdateAvailable: true,
            new Uri("https://example.test/releases/v0.2.0?access_token=release-secret"),
            new UpdatePackageAsset(
                "WinAIUsageBar.zip token=asset-secret",
                new Uri("https://example.test/package.zip?token=package-secret"),
                2048),
            new UpdatePackageAsset(
                "WinAIUsageBar.zip.sha256 secret=checksum-secret",
                new Uri("https://example.test/package.zip.sha256?cookie=checksum-cookie"),
                128),
            new UpdatePackageAsset(
                "WinAIUsageBar-setup.exe token=installer-secret",
                new Uri("https://example.test/setup.exe?token=installer-secret"),
                4096),
            new UpdatePackageAsset(
                "WinAIUsageBar-setup.exe.sha256 secret=installer-checksum-secret",
                new Uri("https://example.test/setup.exe.sha256?cookie=installer-checksum-cookie"),
                128));
        var download = new UpdateDownloadResult(
            UpdateDownloadStatus.Downloaded,
            "Downloaded with api_key=download-secret",
            @"C:\Updates\token=package-secret\package.zip",
            @"C:\Updates\secret=checksum-secret\package.zip.sha256",
            "token=sample-github-token-value",
            "api_key=sample-openai-key-value");
        var preparation = new UpdateInstallPreparationResult(
            UpdateInstallPreparationStatus.Prepared,
            "Prepared with access_token=prepare-secret",
            @"C:\Updates\install-1\secret=script-secret\apply-update.ps1",
            @"powershell -File C:\Updates\apply-update.ps1 -token command-secret",
            @"C:\Updates\token=package-secret\package.zip",
            @"C:\App\cookie=install-cookie",
            @"C:\Updates\staging\secret=staging-secret",
            @"C:\Updates\backup\secret=backup-secret")
        {
            ResultPath = @"C:\Updates\install-1\token=result-secret\install-result.json"
        };
        var launch = new UpdateInstallLaunchResult(
            UpdateInstallLaunchStatus.Launched,
            "Launched with Authorization: Bearer launch-secret",
            @"C:\Updates\install-1\secret=script-secret\apply-update.ps1",
            @"powershell -File C:\Updates\apply-update.ps1 -token command-secret",
            4321);
        var latestInstall = new LatestUpdateInstallResult(
            LatestUpdateInstallStatus.Launched,
            "Latest install with patSecretName=latest-secret",
            updateCheck,
            download,
            preparation,
            launch);
        var startup = new StartupUpdateResult(
            StartupUpdateStatus.InstallLaunched,
            "Startup update with refresh_token=startup-secret",
            "0.2.0 token=latest-secret",
            @"C:\Updates\token=package-secret\package.zip",
            @"C:\Updates\install-1\secret=script-secret\apply-update.ps1")
        {
            InstallResultPath = @"C:\Updates\install-1\secret=result-secret\install-result.json"
        };

        var outputs = new[]
        {
            CommandLineUpdateCheckFormatter.Format(updateCheck),
            CommandLineUpdateDownloadFormatter.Format(updateCheck, download),
            CommandLineUpdateInstallPreparationFormatter.Format(preparation),
            CommandLineUpdateInstallLaunchFormatter.Format(launch),
            CommandLineLatestUpdateInstallFormatter.Format(latestInstall),
            CommandLineStartupUpdateFormatter.Format(startup, "0.1.0 token=current-secret")
        };

        foreach (var output in outputs)
        {
            Assert.Contains("[REDACTED]", output, StringComparison.Ordinal);
            Assert.DoesNotContain("secret", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("cookie", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("authorization", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("api_key", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("sample-github-token-value", output, StringComparison.Ordinal);
            Assert.DoesNotContain("sample-openai-key-value", output, StringComparison.Ordinal);
        }

        Assert.Contains("Status: UpdateAvailable", outputs[0], StringComparison.Ordinal);
        Assert.Contains("Download status: Downloaded", outputs[1], StringComparison.Ordinal);
        Assert.Contains("Status: Prepared", outputs[2], StringComparison.Ordinal);
        Assert.Contains("Status: Launched", outputs[3], StringComparison.Ordinal);
        Assert.Contains("Latest update install", outputs[4], StringComparison.Ordinal);
        Assert.Contains("Status: InstallLaunched", outputs[5], StringComparison.Ordinal);
    }

    [Fact]
    public void Formatters_KeepOrdinaryUpdateOutputReadable()
    {
        var updateCheck = new ReleaseUpdateCheckResult(
            UpdateCheckStatus.UpdateAvailable,
            CurrentVersion: "0.1.0",
            LatestVersion: "0.2.0",
            "A newer GitHub release is available.",
            IsUpdateAvailable: true,
            new Uri("https://example.test/releases/v0.2.0"),
            new UpdatePackageAsset(
                "WinAIUsageBar-0.2.0-win-x64.zip",
                new Uri("https://example.test/package.zip"),
                2048),
            new UpdatePackageAsset(
                "WinAIUsageBar-0.2.0-win-x64.zip.sha256",
                new Uri("https://example.test/package.zip.sha256"),
                128),
            new UpdatePackageAsset(
                "WinAIUsageBar-0.2.0-setup.exe",
                new Uri("https://example.test/setup.exe"),
                4096),
            new UpdatePackageAsset(
                "WinAIUsageBar-0.2.0-setup.exe.sha256",
                new Uri("https://example.test/setup.exe.sha256"),
                128));

        var output = CommandLineUpdateCheckFormatter.Format(updateCheck);

        Assert.Contains("Update check", output, StringComparison.Ordinal);
        Assert.Contains("Status: UpdateAvailable", output, StringComparison.Ordinal);
        Assert.Contains("Current version: 0.1.0", output, StringComparison.Ordinal);
        Assert.Contains("Latest version: 0.2.0", output, StringComparison.Ordinal);
        Assert.Contains("A newer GitHub release is available.", output, StringComparison.Ordinal);
        Assert.Contains("WinAIUsageBar-0.2.0-win-x64.zip", output, StringComparison.Ordinal);
        Assert.Contains("Installer: WinAIUsageBar-0.2.0-setup.exe", output, StringComparison.Ordinal);
        Assert.Contains("Installer checksum: WinAIUsageBar-0.2.0-setup.exe.sha256", output, StringComparison.Ordinal);
        Assert.DoesNotContain("[REDACTED]", output, StringComparison.Ordinal);
    }
}
