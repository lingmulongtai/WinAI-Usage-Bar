using WinAiUsageBar.App.Services;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Infrastructure.Diagnostics;
using WinAiUsageBar.Infrastructure.Scheduling;
using WinAiUsageBar.Infrastructure.Storage;
using WinAiUsageBar.Infrastructure.Tray;
using WinAiUsageBar.Infrastructure.Windows;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class AppHostTests
{
    [Fact]
    public async Task InitializeAsync_UsesInjectedServicesWithoutRealTrayOrProviders()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var configStore = new InMemoryConfigStore(AppConfig.CreateDefault());
        var refreshService = new FakeRefreshService([Snapshot(ProviderId.Codex, "Codex", 75)]);
        var tray = new FakeTrayIconService();
        var diagnostics = new RecordingDiagnosticsLog();
        var windowActivator = new FakeWindowActivator();
        var host = new AppHost(
            new ImmediateDispatcher(),
            new AppHostServices(
                paths,
                configStore,
                refreshService,
                tray,
                diagnostics,
                new FakeDiagnosticsExportService(paths),
                new FakeStartupRegistrationService(),
                windowActivator,
                new FakeExitService()));

        await host.InitializeAsync(CancellationToken.None);
        refreshService.Emit([Snapshot(ProviderId.ChatGPT, "ChatGPT", 64)]);

        Assert.True(refreshService.InitializeCalled);
        Assert.Single(host.ViewModel.Providers);
        Assert.Equal(ProviderId.ChatGPT, host.ViewModel.Providers[0].ProviderId);
        Assert.Contains("ChatGPT", tray.Tooltip, StringComparison.Ordinal);
        Assert.Contains("WinAI Usage Bar initialized.", diagnostics.InfoMessages);

        await host.DisposeAsync();

        Assert.True(tray.Disposed);
        Assert.True(refreshService.Disposed);
        Assert.True(windowActivator.Disposed);
    }

    [Fact]
    public async Task TrayCommands_RouteToInjectedWindowAndExitServices()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var configStore = new InMemoryConfigStore(AppConfig.CreateDefault());
        var refreshService = new FakeRefreshService([]);
        var tray = new FakeTrayIconService();
        var windowActivator = new FakeWindowActivator();
        var exitService = new FakeExitService();
        var host = new AppHost(
            new ImmediateDispatcher(),
            new AppHostServices(
                paths,
                configStore,
                refreshService,
                tray,
                new RecordingDiagnosticsLog(),
                new FakeDiagnosticsExportService(paths),
                new FakeStartupRegistrationService(),
                windowActivator,
                exitService));

        await host.InitializeAsync(CancellationToken.None);

        tray.RaiseShow();
        tray.RaiseSettings();
        tray.RaiseShowWidget();
        tray.RaiseExit();

        Assert.Equal(1, windowActivator.ShowCompactCount);
        Assert.Equal(1, windowActivator.ShowSettingsCount);
        Assert.Equal(1, windowActivator.ShowWidgetCount);
        Assert.Equal(1, exitService.ExitCount);
    }

    [Fact]
    public async Task TrayRefreshCommand_RunsRefreshThroughInjectedService()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var configStore = new InMemoryConfigStore(AppConfig.CreateDefault());
        var refreshService = new FakeRefreshService([]);
        var tray = new FakeTrayIconService();
        var host = new AppHost(
            new ImmediateDispatcher(),
            new AppHostServices(
                paths,
                configStore,
                refreshService,
                tray,
                new RecordingDiagnosticsLog(),
                new FakeDiagnosticsExportService(paths),
                new FakeStartupRegistrationService(),
                new FakeWindowActivator(),
                new FakeExitService()));

        await host.InitializeAsync(CancellationToken.None);
        tray.RaiseRefreshNow();

        await refreshService.RefreshObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, refreshService.RefreshCount);
    }

    [Fact]
    public async Task StartAsync_AppliesStartupRegistrationWhenConfigRequestsIt()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var config = AppConfig.CreateDefault();
        config.Startup.LaunchOnLogin = true;
        var startup = new FakeStartupRegistrationService();
        var host = new AppHost(
            new ImmediateDispatcher(),
            new AppHostServices(
                paths,
                new InMemoryConfigStore(config),
                new FakeRefreshService([]),
                new FakeTrayIconService(),
                new RecordingDiagnosticsLog(),
                new FakeDiagnosticsExportService(paths),
                startup,
                new FakeWindowActivator(),
                new FakeExitService()));

        await host.StartAsync(CancellationToken.None);

        Assert.True(startup.LastSetEnabled);
    }

    private static UsageSnapshot Snapshot(ProviderId providerId, string displayName, double remainingPercent)
    {
        return new UsageSnapshot(
            providerId,
            displayName,
            ProviderHealth.Ok,
            Identity: null,
            new UsageWindow("Test", 100 - remainingPercent, remainingPercent, null, "reset later", "%", null, null),
            SecondaryWindow: null,
            Credits: null,
            DataSourceKind.Mock,
            DateTimeOffset.Now,
            "test snapshot",
            ErrorMessage: null);
    }

    private sealed class ImmediateDispatcher : IAppDispatcher
    {
        public bool TryEnqueue(Action action)
        {
            action();
            return true;
        }
    }

    private sealed class InMemoryConfigStore(AppConfig config) : IAppConfigStore
    {
        public Task<AppConfig> LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(config);
        }

        public Task SaveAsync(AppConfig nextConfig, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            config = nextConfig;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRefreshService(IReadOnlyList<UsageSnapshot> currentSnapshots) : IUsageRefreshService
    {
        public event EventHandler<IReadOnlyList<UsageSnapshot>>? SnapshotsChanged;

        public IReadOnlyList<UsageSnapshot> CurrentSnapshots { get; private set; } = currentSnapshots;

        public bool InitializeCalled { get; private set; }

        public bool Disposed { get; private set; }

        public int RefreshCount { get; private set; }

        public TaskCompletionSource RefreshObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InitializeCalled = true;
            return Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task RefreshNowAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RefreshCount++;
            RefreshObserved.TrySetResult();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }

        public void Emit(IReadOnlyList<UsageSnapshot> snapshots)
        {
            CurrentSnapshots = snapshots;
            SnapshotsChanged?.Invoke(this, snapshots);
        }
    }

    private sealed class FakeTrayIconService : ITrayIconService
    {
        public event EventHandler? ShowRequested;

        public event EventHandler? ShowWidgetRequested;

        public event EventHandler? RefreshNowRequested;

        public event EventHandler? SettingsRequested;

        public event EventHandler? ExitRequested;

        public string Tooltip { get; private set; } = string.Empty;

        public bool Disposed { get; private set; }

        public void UpdateTooltip(string tooltip)
        {
            Tooltip = tooltip;
        }

        public void Dispose()
        {
            Disposed = true;
        }

        public void RaiseShow()
        {
            ShowRequested?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseShowWidget()
        {
            ShowWidgetRequested?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseRefreshNow()
        {
            RefreshNowRequested?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseSettings()
        {
            SettingsRequested?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseExit()
        {
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class RecordingDiagnosticsLog : IAppDiagnosticsLog
    {
        public List<string> InfoMessages { get; } = [];

        public Task InfoAsync(string message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InfoMessages.Add(message);
            return Task.CompletedTask;
        }

        public Task ErrorAsync(string message, Exception exception, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDiagnosticsExportService(AppDataPaths paths) : IDiagnosticsExportService
    {
        public Task<DiagnosticsExportResult> ExportAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new DiagnosticsExportResult(
                Path.Combine(paths.RootDirectory, "diagnostics-export.txt"),
                DateTimeOffset.Now,
                ["test"]));
        }
    }

    private sealed class FakeStartupRegistrationService : IStartupRegistrationService
    {
        public bool? LastSetEnabled { get; private set; }

        public Task<StartupRegistrationStatus> GetStatusAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new StartupRegistrationStatus(
                IsSupported: true,
                IsEnabled: LastSetEnabled == true,
                Command: null,
                "fake startup status"));
        }

        public Task SetEnabledAsync(bool isEnabled, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastSetEnabled = isEnabled;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWindowActivator : IAppWindowActivator
    {
        public int ShowCompactCount { get; private set; }

        public int ShowSettingsCount { get; private set; }

        public int ShowWidgetCount { get; private set; }

        public bool Disposed { get; private set; }

        public void ShowCompactPanel(AppHost host)
        {
            ShowCompactCount++;
        }

        public void ShowSettings(AppHost host)
        {
            ShowSettingsCount++;
        }

        public void ShowWidget(AppHost host)
        {
            ShowWidgetCount++;
        }

        public void OnSettingsClosed()
        {
        }

        public void OnCompactClosed()
        {
        }

        public void OnWidgetClosed()
        {
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private sealed class FakeExitService : IApplicationExitService
    {
        public int ExitCount { get; private set; }

        public void Exit()
        {
            ExitCount++;
        }
    }
}
