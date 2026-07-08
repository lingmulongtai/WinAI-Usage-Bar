using WinAiUsageBar.App.ViewModels;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;
using WinAiUsageBar.Infrastructure.Diagnostics;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class DiagnosticsSummaryServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_ReturnsStorageAndSnapshotMetadataWithoutSecretReferences()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var configStore = new JsonAppConfigStore(paths);
        var snapshotStore = new JsonSnapshotStore(paths);
        var service = new DiagnosticsSummaryService(paths, configStore, snapshotStore);
        var config = AppConfig.CreateDefault();
        config.Version = 7;
        config.Refresh.Interval = RefreshIntervalKind.TwoMinutes;
        config.Notifications.IsEnabled = false;
        config.HistoryRetention.MaxDays = 14;
        config.HistoryRetention.MaxBytes = 1_000_000;
        config.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.Gemini)).ApiKey.SecretName = "gemini-secret-ref";
        config.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.GitHubCopilot)).GitHubCopilot.PatSecretName = "copilot-pat-secret";

        var now = new DateTimeOffset(2026, 7, 8, 16, 30, 0, TimeSpan.Zero);
        var snapshots = new[]
        {
            Snapshot(ProviderId.Codex, "Codex", now.AddMinutes(-10)),
            Snapshot(ProviderId.ChatGPT, "ChatGPT", now)
        };

        try
        {
            await configStore.SaveAsync(config, CancellationToken.None);
            await snapshotStore.SaveAsync(snapshots, CancellationToken.None);
            await snapshotStore.AppendHistoryAsync(snapshots, config.HistoryRetention, CancellationToken.None);
            await File.WriteAllTextAsync(paths.DiagnosticsLogPath, "normal diagnostics line");
            var olderBackupPath = Path.Combine(paths.ConfigBackupsDirectory, "config-backup-20260708-150000.json");
            var latestBackupPath = Path.Combine(paths.ConfigBackupsDirectory, "config-backup-20260708-160000.json");
            await File.WriteAllTextAsync(olderBackupPath, "old backup");
            await File.WriteAllTextAsync(latestBackupPath, "latest backup");
            File.SetLastWriteTime(latestBackupPath, now.DateTime);
            File.SetLastWriteTime(olderBackupPath, now.AddMinutes(-30).DateTime);
            var olderExportPath = Path.Combine(paths.DiagnosticsExportsDirectory, "diagnostics-export-20260708-150000.txt");
            var latestExportPath = Path.Combine(paths.DiagnosticsExportsDirectory, "diagnostics-export-20260708-160000.txt");
            await File.WriteAllTextAsync(olderExportPath, "old export");
            await File.WriteAllTextAsync(latestExportPath, "latest export");
            File.SetLastWriteTime(latestExportPath, now.AddMinutes(1).DateTime);
            File.SetLastWriteTime(olderExportPath, now.AddMinutes(-29).DateTime);
            var olderCrashReportPath = Path.Combine(
                paths.CrashReportsDirectory,
                "crash-report-20260708-150000-11111111111111111111111111111111.json");
            var latestCrashReportPath = Path.Combine(
                paths.CrashReportsDirectory,
                "crash-report-20260708-160000-22222222222222222222222222222222.json");
            var ignoredCrashReportPath = Path.Combine(
                paths.CrashReportsDirectory,
                "crash-report-20260708-170000-not-a-guid.json");
            await File.WriteAllTextAsync(olderCrashReportPath, "old crash report");
            await File.WriteAllTextAsync(latestCrashReportPath, "latest crash report");
            await File.WriteAllTextAsync(ignoredCrashReportPath, "ignored");
            File.SetLastWriteTime(latestCrashReportPath, now.AddMinutes(2).DateTime);
            File.SetLastWriteTime(olderCrashReportPath, now.AddMinutes(-28).DateTime);
            File.SetLastWriteTime(ignoredCrashReportPath, now.AddMinutes(3).DateTime);

            var summary = await service.GetSummaryAsync(CancellationToken.None);
            var recentCrashReports = new[]
            {
                new CrashReportFile(
                    latestCrashReportPath,
                    now.AddMinutes(2),
                    "latest crash report".Length,
                    "startup",
                    typeof(InvalidOperationException).FullName!,
                    "0.1.4",
                    MetadataAvailable: true,
                    MetadataStatus: "Metadata parsed.")
            };
            var viewModel = new DiagnosticsSummaryViewModel(summary, recentCrashReports);
            var visibleText = string.Join(Environment.NewLine, viewModel.OverviewLines)
                + Environment.NewLine
                + string.Join(Environment.NewLine, viewModel.Files.Select(file => $"{file.Label} {file.Path} {file.StatusText}"))
                + Environment.NewLine
                + string.Join(Environment.NewLine, viewModel.RecentCrashReports.Select(report => $"{report.FileName} {report.SummaryText} {report.Path}"));

            Assert.Equal(paths.RootDirectory, summary.RootDirectory);
            Assert.Equal(AppConfigMigrations.CurrentVersion, summary.ConfigVersion);
            Assert.Equal(ProviderDescriptors.All.Count, summary.ConfiguredProviderCount);
            Assert.Equal(config.Providers.Count(provider => provider.IsEnabled), summary.EnabledProviderCount);
            Assert.Equal(RefreshIntervalKind.TwoMinutes, summary.RefreshInterval);
            Assert.False(summary.NotificationsEnabled);
            Assert.Equal(2, summary.CachedSnapshotCount);
            Assert.Equal(now, summary.LatestSnapshotUpdatedAt);
            Assert.Equal(paths.ConfigBackupsDirectory, summary.ConfigBackupsDirectory);
            Assert.Equal(2, summary.ConfigBackupCount);
            Assert.Equal(latestBackupPath, summary.LatestConfigBackupPath);
            Assert.NotNull(summary.LatestConfigBackupCreatedAt);
            Assert.Equal("old backup".Length + "latest backup".Length, summary.ConfigBackupTotalBytes);
            Assert.Equal(2, summary.DiagnosticsExportCount);
            Assert.Equal(latestExportPath, summary.LatestDiagnosticsExportPath);
            Assert.NotNull(summary.LatestDiagnosticsExportCreatedAt);
            Assert.Equal("old export".Length + "latest export".Length, summary.DiagnosticsExportTotalBytes);
            Assert.Equal(paths.CrashReportsDirectory, summary.CrashReportsDirectory);
            Assert.Equal(2, summary.CrashReportCount);
            Assert.Equal(latestCrashReportPath, summary.LatestCrashReportPath);
            Assert.NotNull(summary.LatestCrashReportCreatedAt);
            Assert.Equal("old crash report".Length + "latest crash report".Length, summary.CrashReportTotalBytes);
            Assert.True(summary.ConfigFile.Exists);
            Assert.True(summary.SnapshotsFile.Exists);
            Assert.True(summary.HistoryFile.Exists);
            Assert.True(summary.DiagnosticsLogFile.Exists);
            Assert.Contains($"Config v{AppConfigMigrations.CurrentVersion}", viewModel.ConfigText);
            Assert.Contains("2 cached snapshot", viewModel.SnapshotText);
            Assert.Contains("2 config backup", viewModel.ConfigBackupText);
            Assert.Contains("2 diagnostics export", viewModel.DiagnosticsExportText);
            Assert.Contains("2 crash report", viewModel.CrashReportText);
            Assert.Equal(latestCrashReportPath, viewModel.LatestCrashReportPath);
            var recentCrashReport = Assert.Single(viewModel.RecentCrashReports);
            Assert.Equal(Path.GetFileName(latestCrashReportPath), recentCrashReport.FileName);
            Assert.Contains("startup", recentCrashReport.SummaryText, StringComparison.Ordinal);
            Assert.Contains(typeof(InvalidOperationException).FullName!, recentCrashReport.SummaryText, StringComparison.Ordinal);
            Assert.Contains("0.1.4", recentCrashReport.SummaryText, StringComparison.Ordinal);
            Assert.Contains("Metadata parsed", recentCrashReport.SummaryText, StringComparison.Ordinal);
            Assert.Contains(paths.CrashReportsDirectory, visibleText, StringComparison.Ordinal);
            Assert.DoesNotContain(ignoredCrashReportPath, visibleText, StringComparison.Ordinal);
            Assert.DoesNotContain("gemini-secret-ref", visibleText, StringComparison.Ordinal);
            Assert.DoesNotContain("copilot-pat-secret", visibleText, StringComparison.Ordinal);
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
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1_572_864, "1.5 MB")]
    public void FormatBytes_UsesReadableUnits(long bytes, string expected)
    {
        Assert.Equal(expected, DiagnosticsSummaryViewModel.FormatBytes(bytes));
    }

    private static UsageSnapshot Snapshot(ProviderId providerId, string displayName, DateTimeOffset updatedAt)
    {
        return new UsageSnapshot(
            providerId,
            displayName,
            ProviderHealth.Ok,
            Identity: null,
            PrimaryWindow: new UsageWindow("Test", 25, 75, null, "later", "%", null, null),
            SecondaryWindow: null,
            Credits: null,
            DataSourceKind.Mock,
            updatedAt,
            StatusMessage: "ok",
            ErrorMessage: null);
    }
}
