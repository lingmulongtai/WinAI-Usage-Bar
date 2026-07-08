using WinAiUsageBar.App.Services;
using WinAiUsageBar.App.ViewModels;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;
using WinAiUsageBar.Infrastructure.Notifications;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class UiCompositionSmokeTests
{
    [Fact]
    public async Task HeadlessUiComposition_ConstructsPrimaryViewModels()
    {
        var paths = TestPaths();
        paths.EnsureCreated();
        var services = AppCompositionRoot.CreateServices(paths, new NoOpAppNotificationService());

        try
        {
            var config = await services.ConfigStore.LoadAsync(CancellationToken.None);
            var descriptors = ProviderDescriptors.All;
            var now = new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);
            var snapshots = CreateSnapshots(now);

            var shell = new ShellViewModel();
            shell.ApplySnapshots(snapshots);
            var providerDetails = new ProviderDetailsPageViewModel(snapshots, () => now);
            var providerSettings = new ProviderSettingsPageViewModel(config, descriptors);
            var widgetSettings = new WidgetSettingsPageViewModel(config.Widget, descriptors);
            var firstRun = new FirstRunSetupViewModel(config, descriptors, () => now);
            var refreshSettings = new RefreshSettingsPageViewModel(config);
            var diagnostics = new DiagnosticsSummaryViewModel(await services.DiagnosticsSummaryService.GetSummaryAsync(CancellationToken.None));
            var history = new HistorySummaryViewModel(await services.HistorySummaryService.GetSummaryAsync(CancellationToken.None));
            var secretEditor = new SecretEditorViewModel();

            Assert.Equal(snapshots.Count, shell.Providers.Count);
            Assert.Contains("provider(s) updated", shell.StatusText, StringComparison.Ordinal);
            Assert.Contains("left", shell.BuildTrayTooltip(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(snapshots.Count, providerDetails.Providers.Count);
            Assert.True(providerDetails.HasProviders);
            Assert.Equal(descriptors.Count, providerSettings.Editors.Count);
            Assert.True(providerSettings.TryApply().IsValid);
            Assert.Equal(descriptors.Count, widgetSettings.ProviderOptions.Count);
            Assert.True(widgetSettings.TryApply().IsValid);
            Assert.True(firstRun.IsVisible);
            Assert.Equal(descriptors.Count, firstRun.ProviderLines.Count);
            Assert.Equal(3, firstRun.ChecklistItems.Count);
            Assert.True(refreshSettings.TryApply().IsValid);
            Assert.Contains("Last checked:", refreshSettings.UpdateStatusText, StringComparison.Ordinal);
            Assert.Contains("Config v", diagnostics.ConfigText, StringComparison.Ordinal);
            Assert.NotEmpty(diagnostics.OverviewLines);
            Assert.Equal(0, history.TotalEntries);
            Assert.Contains("No retained history", history.SummaryText, StringComparison.Ordinal);
            Assert.False(secretEditor.ValidateSave().IsValid);
        }
        finally
        {
            await services.RefreshService.DisposeAsync();
            if (Directory.Exists(paths.RootDirectory))
            {
                Directory.Delete(paths.RootDirectory, recursive: true);
            }
        }
    }

    private static IReadOnlyList<UsageSnapshot> CreateSnapshots(DateTimeOffset now)
    {
        return
        [
            new UsageSnapshot(
                ProviderId.Codex,
                "Codex",
                ProviderHealth.Ok,
                new ProviderIdentity("person@example.test", "Personal", "Plus", null),
                new UsageWindow(
                    "Primary",
                    UsedPercent: 40,
                    RemainingPercent: 60,
                    ResetsAt: now.AddHours(3),
                    ResetDescription: "Resets soon",
                    Unit: "requests",
                    Used: 40,
                    Limit: 100),
                SecondaryWindow: null,
                new ProviderCredits(12.5m, "USD", 3.2m, 12345),
                DataSourceKind.Mock,
                now.AddMinutes(-2),
                "Ready",
                ErrorMessage: null),
            new UsageSnapshot(
                ProviderId.ClaudeCode,
                "Claude Code",
                ProviderHealth.AuthRequired,
                Identity: null,
                PrimaryWindow: null,
                SecondaryWindow: null,
                Credits: null,
                DataSourceKind.Manual,
                now.AddMinutes(-5),
                "Manual mode available",
                "Authentication required")
        ];
    }

    private static AppDataPaths TestPaths()
    {
        return new AppDataPaths(Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N")));
    }
}
