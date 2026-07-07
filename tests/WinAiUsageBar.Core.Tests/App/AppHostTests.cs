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
        var host = new AppHost(
            new ImmediateDispatcher(),
            new AppHostServices(
                paths,
                configStore,
                refreshService,
                new WidgetPlacementStore(configStore),
                tray,
                diagnostics));

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
        public event EventHandler? ShowRequested
        {
            add { }
            remove { }
        }

        public event EventHandler? ShowWidgetRequested
        {
            add { }
            remove { }
        }

        public event EventHandler? RefreshNowRequested
        {
            add { }
            remove { }
        }

        public event EventHandler? SettingsRequested
        {
            add { }
            remove { }
        }

        public event EventHandler? ExitRequested
        {
            add { }
            remove { }
        }

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
}
