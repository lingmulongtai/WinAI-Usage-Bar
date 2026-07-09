using WinAiUsageBar.Core.Abstractions;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;
using WinAiUsageBar.Infrastructure.Diagnostics;
using WinAiUsageBar.Infrastructure.Notifications;
using WinAiUsageBar.Infrastructure.Scheduling;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class UsageRefreshServiceTests
{
    [Fact]
    public async Task RefreshNowAsync_ConvertsProviderExceptionToErrorSnapshotAndContinues()
    {
        var config = DisabledDefaultConfig();
        var codex = Enable(config, ProviderId.Codex);
        var chatGpt = Enable(config, ProviderId.ChatGPT);
        var paths = TestPaths();
        var snapshotStore = new InMemorySnapshotStore();
        var refreshService = new UsageRefreshService(
            new InMemoryConfigStore(config),
            snapshotStore,
            new FakeProviderSource(
                [ProviderDescriptors.Get(ProviderId.Codex), ProviderDescriptors.Get(ProviderId.ChatGPT)],
                descriptor => descriptor.Id == ProviderId.Codex
                    ? new ThrowingProviderAdapter(descriptor, "access_token=super-secret")
                    : new SuccessfulProviderAdapter(descriptor)),
            paths,
            new RecordingNotificationService());

        await refreshService.RefreshNowAsync(CancellationToken.None);

        var codexSnapshot = refreshService.CurrentSnapshots.Single(snapshot => snapshot.ProviderId == codex.ProviderId);
        var chatGptSnapshot = refreshService.CurrentSnapshots.Single(snapshot => snapshot.ProviderId == chatGpt.ProviderId);

        Assert.Equal(ProviderHealth.Error, codexSnapshot.Health);
        Assert.Equal("Codex refresh failed.", codexSnapshot.ErrorMessage);
        Assert.DoesNotContain("super-secret", snapshotStore.Saved.Single(snapshot => snapshot.ProviderId == ProviderId.Codex).StatusMessage);
        Assert.Equal(ProviderHealth.Ok, chatGptSnapshot.Health);
        Assert.Equal(2, snapshotStore.Saved.Count);
        Assert.Equal(2, snapshotStore.History.Count);
    }

    [Fact]
    public async Task RefreshNowAsync_PreservesCachedUsageWhenProviderFails()
    {
        var config = DisabledDefaultConfig();
        Enable(config, ProviderId.Codex);
        var cached = new UsageSnapshot(
            ProviderId.Codex,
            "Codex",
            ProviderHealth.Ok,
            Identity: null,
            new UsageWindow("Cached", 40, 60, null, "cached reset", "%", 40, 100),
            SecondaryWindow: null,
            Credits: null,
            DataSourceKind.Manual,
            DateTimeOffset.Now.AddMinutes(-10),
            "Cached data",
            ErrorMessage: null);

        var snapshotStore = new InMemorySnapshotStore([cached]);
        var refreshService = new UsageRefreshService(
            new InMemoryConfigStore(config),
            snapshotStore,
            new FakeProviderSource(
                [ProviderDescriptors.Get(ProviderId.Codex)],
                descriptor => new ThrowingProviderAdapter(descriptor, "boom")),
            TestPaths(),
            new RecordingNotificationService());

        await refreshService.InitializeAsync(CancellationToken.None);
        await refreshService.RefreshNowAsync(CancellationToken.None);

        var snapshot = refreshService.CurrentSnapshots.Single();

        Assert.Equal(ProviderHealth.Error, snapshot.Health);
        Assert.Equal(60, snapshot.PrimaryWindow?.RemainingPercent);
        Assert.Equal("Codex refresh failed.", snapshot.ErrorMessage);
        Assert.Equal("cached reset", snapshot.PrimaryWindow?.ResetDescription);
    }

    [Fact]
    public async Task StartAsync_RefreshLoopContinuesAfterRefreshLevelFailure()
    {
        var config = DisabledDefaultConfig();
        Enable(config, ProviderId.Codex);
        config.Refresh.Interval = RefreshIntervalKind.OneMinute;
        var snapshotStore = new InMemorySnapshotStore { ThrowOnNextSave = true };
        var diagnostics = new RecordingDiagnosticsLog();
        var refreshService = new UsageRefreshService(
            new InMemoryConfigStore(config),
            snapshotStore,
            new FakeProviderSource(
                [ProviderDescriptors.Get(ProviderId.Codex)],
                descriptor => new SuccessfulProviderAdapter(descriptor)),
            TestPaths(),
            new RecordingNotificationService(),
            _ => TimeSpan.FromMilliseconds(10),
            diagnostics);

        await refreshService.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => snapshotStore.SaveCalls >= 2);
        await refreshService.DisposeAsync();

        Assert.True(snapshotStore.SaveCalls >= 2);
        Assert.NotEmpty(snapshotStore.Saved);
        Assert.Contains(diagnostics.Errors, error => error.Message.Contains("Periodic refresh failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RestartAsync_ReloadsIntervalFromConfig()
    {
        var config = DisabledDefaultConfig();
        Enable(config, ProviderId.Codex);
        config.Refresh.Interval = RefreshIntervalKind.Manual;
        var snapshotStore = new InMemorySnapshotStore();
        var refreshService = new UsageRefreshService(
            new InMemoryConfigStore(config),
            snapshotStore,
            new FakeProviderSource(
                [ProviderDescriptors.Get(ProviderId.Codex)],
                descriptor => new SuccessfulProviderAdapter(descriptor)),
            TestPaths(),
            new RecordingNotificationService(),
            interval => interval == RefreshIntervalKind.Manual
                ? null
                : TimeSpan.FromMilliseconds(10));

        await refreshService.StartAsync(CancellationToken.None);
        await Task.Delay(40);
        Assert.Equal(0, snapshotStore.SaveCalls);

        config.Refresh.Interval = RefreshIntervalKind.OneMinute;
        await refreshService.RestartAsync(CancellationToken.None);
        await WaitUntilAsync(() => snapshotStore.SaveCalls >= 1);

        config.Refresh.Interval = RefreshIntervalKind.Manual;
        await refreshService.RestartAsync(CancellationToken.None);
        var saveCallsAfterStop = snapshotStore.SaveCalls;
        await Task.Delay(40);
        await refreshService.DisposeAsync();

        Assert.Equal(saveCallsAfterStop, snapshotStore.SaveCalls);
    }

    [Fact]
    public async Task RefreshNowAsync_DoesNotNotifyWhenNotificationsAreDisabled()
    {
        var config = DisabledDefaultConfig();
        Enable(config, ProviderId.Codex);
        config.Notifications.IsEnabled = false;
        var notifications = new RecordingNotificationService();
        var refreshService = new UsageRefreshService(
            new InMemoryConfigStore(config),
            new InMemorySnapshotStore(),
            new FakeProviderSource(
                [ProviderDescriptors.Get(ProviderId.Codex)],
                descriptor => new LowQuotaProviderAdapter(descriptor)),
            TestPaths(),
            notifications);

        await refreshService.RefreshNowAsync(CancellationToken.None);

        Assert.Empty(notifications.Snapshots);
    }

    [Fact]
    public async Task RefreshNowAsync_SuppressesDuplicateNotificationsForSameReason()
    {
        var config = DisabledDefaultConfig();
        Enable(config, ProviderId.Codex);
        var notifications = new RecordingNotificationService();
        var adapter = new MutableQuotaProviderAdapter(ProviderDescriptors.Get(ProviderId.Codex), 15);
        var refreshService = new UsageRefreshService(
            new InMemoryConfigStore(config),
            new InMemorySnapshotStore(),
            new FakeProviderSource([adapter.Descriptor], _ => adapter),
            TestPaths(),
            notifications);

        await refreshService.RefreshNowAsync(CancellationToken.None);
        await refreshService.RefreshNowAsync(CancellationToken.None);

        Assert.Single(notifications.Snapshots);
    }

    [Fact]
    public async Task RefreshNowAsync_NotifiesAgainWhenNotificationReasonChanges()
    {
        var config = DisabledDefaultConfig();
        Enable(config, ProviderId.Codex);
        var notifications = new RecordingNotificationService();
        var adapter = new MutableQuotaProviderAdapter(ProviderDescriptors.Get(ProviderId.Codex), 15);
        var refreshService = new UsageRefreshService(
            new InMemoryConfigStore(config),
            new InMemorySnapshotStore(),
            new FakeProviderSource([adapter.Descriptor], _ => adapter),
            TestPaths(),
            notifications);

        await refreshService.RefreshNowAsync(CancellationToken.None);
        adapter.RemainingPercent = 8;
        await refreshService.RefreshNowAsync(CancellationToken.None);

        Assert.Equal(2, notifications.Snapshots.Count);
        Assert.Equal(15, notifications.Snapshots[0].PrimaryWindow?.RemainingPercent);
        Assert.Equal(8, notifications.Snapshots[1].PrimaryWindow?.RemainingPercent);
    }

    [Fact]
    public async Task RefreshNowAsync_ClearsNotificationStateAfterRecovery()
    {
        var config = DisabledDefaultConfig();
        Enable(config, ProviderId.Codex);
        var notifications = new RecordingNotificationService();
        var adapter = new MutableQuotaProviderAdapter(ProviderDescriptors.Get(ProviderId.Codex), 15);
        var refreshService = new UsageRefreshService(
            new InMemoryConfigStore(config),
            new InMemorySnapshotStore(),
            new FakeProviderSource([adapter.Descriptor], _ => adapter),
            TestPaths(),
            notifications);

        await refreshService.RefreshNowAsync(CancellationToken.None);
        adapter.RemainingPercent = 75;
        await refreshService.RefreshNowAsync(CancellationToken.None);
        adapter.RemainingPercent = 15;
        await refreshService.RefreshNowAsync(CancellationToken.None);

        Assert.Equal(2, notifications.Snapshots.Count);
        Assert.All(notifications.Snapshots, snapshot =>
            Assert.Equal(15, snapshot.PrimaryWindow?.RemainingPercent));
    }

    [Fact]
    public async Task RefreshNowAsync_ClearsNotificationStateWhenNotificationsAreDisabled()
    {
        var config = DisabledDefaultConfig();
        Enable(config, ProviderId.Codex);
        var notifications = new RecordingNotificationService();
        var adapter = new MutableQuotaProviderAdapter(ProviderDescriptors.Get(ProviderId.Codex), 15);
        var refreshService = new UsageRefreshService(
            new InMemoryConfigStore(config),
            new InMemorySnapshotStore(),
            new FakeProviderSource([adapter.Descriptor], _ => adapter),
            TestPaths(),
            notifications);

        await refreshService.RefreshNowAsync(CancellationToken.None);
        config.Notifications.IsEnabled = false;
        await refreshService.RefreshNowAsync(CancellationToken.None);
        config.Notifications.IsEnabled = true;
        await refreshService.RefreshNowAsync(CancellationToken.None);

        Assert.Equal(2, notifications.Snapshots.Count);
    }

    [Fact]
    public async Task RefreshNowAsync_ClearsNotificationStateWhenProviderIsDisabled()
    {
        var config = DisabledDefaultConfig();
        var codex = Enable(config, ProviderId.Codex);
        var notifications = new RecordingNotificationService();
        var adapter = new MutableQuotaProviderAdapter(ProviderDescriptors.Get(ProviderId.Codex), 15);
        var refreshService = new UsageRefreshService(
            new InMemoryConfigStore(config),
            new InMemorySnapshotStore(),
            new FakeProviderSource([adapter.Descriptor], _ => adapter),
            TestPaths(),
            notifications);

        await refreshService.RefreshNowAsync(CancellationToken.None);
        codex.IsEnabled = false;
        await refreshService.RefreshNowAsync(CancellationToken.None);
        codex.IsEnabled = true;
        await refreshService.RefreshNowAsync(CancellationToken.None);

        Assert.Equal(2, notifications.Snapshots.Count);
    }

    [Fact]
    public async Task RefreshNowAsync_LogsProviderDiagnosticsWithRedaction()
    {
        var config = DisabledDefaultConfig();
        Enable(config, ProviderId.Codex);
        var diagnostics = new RecordingDiagnosticsLog();
        var refreshService = new UsageRefreshService(
            new InMemoryConfigStore(config),
            new InMemorySnapshotStore(),
            new FakeProviderSource(
                [ProviderDescriptors.Get(ProviderId.Codex)],
                descriptor => new DiagnosticProviderAdapter(
                    descriptor,
                    [
                        "authorization: bearer sample-provider-secret",
                        "started local app-server"
                    ])),
            TestPaths(),
            new RecordingNotificationService(),
            diagnosticsLog: diagnostics);

        await refreshService.RefreshNowAsync(CancellationToken.None);

        Assert.Contains(diagnostics.InfoMessages, message => message.Contains("Codex provider diagnostic", StringComparison.Ordinal));
        Assert.Contains(diagnostics.InfoMessages, message => message.Contains("started local app-server", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics.InfoMessages, message => message.Contains("sample-provider-secret", StringComparison.Ordinal));
        Assert.Contains(diagnostics.InfoMessages, message => message.Contains("[REDACTED]", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RefreshNowAsync_GitHubCopilotOfficialApiMissingScopeCompletesWithRealSnapshotStore()
    {
        var config = DisabledDefaultConfig();
        var copilot = Enable(config, ProviderId.GitHubCopilot);
        copilot.SourceKind = DataSourceKind.OfficialApi;
        var paths = TestPaths();
        var refreshService = new UsageRefreshService(
            new InMemoryConfigStore(config),
            new JsonSnapshotStore(paths),
            new ProviderRegistry(
                secretResolver: new NullSecretResolver(),
                gitHubCopilotMetricsClient: new UnusedGitHubCopilotMetricsClient()),
            paths,
            new NoOpAppNotificationService(),
            diagnosticsLog: new FileAppDiagnosticsLog(paths));

        await refreshService.RefreshNowAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));

        var snapshot = Assert.Single(refreshService.CurrentSnapshots);
        Assert.Equal(ProviderId.GitHubCopilot, snapshot.ProviderId);
        Assert.Equal(ProviderHealth.AuthRequired, snapshot.Health);
        Assert.Contains("Manual mode", snapshot.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefreshNowAsync_SanitizesSnapshotTextBeforePublishingAndPersistence()
    {
        var config = DisabledDefaultConfig();
        Enable(config, ProviderId.Codex);
        var snapshotStore = new InMemorySnapshotStore();
        var notifications = new RecordingNotificationService();
        var secretValue = "sample-provider-secret";
        var refreshService = new UsageRefreshService(
            new InMemoryConfigStore(config),
            snapshotStore,
            new FakeProviderSource(
                [ProviderDescriptors.Get(ProviderId.Codex)],
                descriptor => new SensitiveSnapshotProviderAdapter(descriptor, secretValue)),
            TestPaths(),
            notifications);

        await refreshService.RefreshNowAsync(CancellationToken.None);

        var current = Assert.Single(refreshService.CurrentSnapshots);
        var saved = Assert.Single(snapshotStore.Saved);
        var history = Assert.Single(snapshotStore.History);
        var notification = Assert.Single(notifications.Snapshots);

        AssertSanitizedSnapshot(current, secretValue);
        AssertSanitizedSnapshot(saved, secretValue);
        AssertSanitizedSnapshot(history, secretValue);
        AssertSanitizedSnapshot(notification, secretValue);
    }

    [Fact]
    public async Task RefreshNowAsync_SanitizesCachedUsageTextWhenProviderFails()
    {
        var config = DisabledDefaultConfig();
        Enable(config, ProviderId.Codex);
        var secretValue = "sample-cached-secret";
        var cached = new UsageSnapshot(
            ProviderId.Codex,
            "Codex",
            ProviderHealth.Ok,
            new ProviderIdentity(
                Email: $"user access_token={secretValue}",
                AccountName: $"account token={secretValue}",
                PlanName: $"plan cookie={secretValue}",
                Organization: $"org api_key={secretValue}"),
            new UsageWindow(
                $"Cached token={secretValue}",
                40,
                60,
                null,
                $"cached reset cookie={secretValue}",
                $"requests secret_name={secretValue}",
                40,
                100),
            SecondaryWindow: null,
            Credits: new ProviderCredits(1m, $"USD token={secretValue}", null, null),
            DataSourceKind.Manual,
            DateTimeOffset.Now.AddMinutes(-10),
            $"cached status access_token={secretValue}",
            ErrorMessage: null);

        var snapshotStore = new InMemorySnapshotStore([cached]);
        var refreshService = new UsageRefreshService(
            new InMemoryConfigStore(config),
            snapshotStore,
            new FakeProviderSource(
                [ProviderDescriptors.Get(ProviderId.Codex)],
                descriptor => new ThrowingProviderAdapter(descriptor, "boom")),
            TestPaths(),
            new RecordingNotificationService());

        await refreshService.InitializeAsync(CancellationToken.None);
        await refreshService.RefreshNowAsync(CancellationToken.None);

        var current = Assert.Single(refreshService.CurrentSnapshots);
        var saved = Assert.Single(snapshotStore.Saved);
        var history = Assert.Single(snapshotStore.History);

        Assert.Equal(ProviderHealth.Error, current.Health);
        Assert.Equal(60, current.PrimaryWindow?.RemainingPercent);
        AssertSanitizedSnapshot(current, secretValue);
        AssertSanitizedSnapshot(saved, secretValue);
        AssertSanitizedSnapshot(history, secretValue);
    }

    [Fact]
    public async Task RefreshNowAsync_ContinuesWhenDiagnosticLoggingFails()
    {
        var config = DisabledDefaultConfig();
        Enable(config, ProviderId.Codex);
        var snapshotStore = new InMemorySnapshotStore();
        var refreshService = new UsageRefreshService(
            new InMemoryConfigStore(config),
            snapshotStore,
            new FakeProviderSource(
                [ProviderDescriptors.Get(ProviderId.Codex)],
                descriptor => new DiagnosticProviderAdapter(descriptor, ["diagnostic"])),
            TestPaths(),
            new RecordingNotificationService(),
            diagnosticsLog: new ThrowingInfoDiagnosticsLog());

        await refreshService.RefreshNowAsync(CancellationToken.None);

        Assert.Single(snapshotStore.Saved);
    }

    private static AppConfig DisabledDefaultConfig()
    {
        var config = AppConfig.CreateDefault();
        foreach (var provider in config.Providers)
        {
            provider.IsEnabled = false;
        }

        return config;
    }

    private static ProviderConfig Enable(AppConfig config, ProviderId providerId)
    {
        var provider = config.Providers.Single(item => item.ProviderId == providerId);
        provider.IsEnabled = true;
        provider.SourceKind = DataSourceKind.Manual;
        return provider;
    }

    private static AppDataPaths TestPaths()
    {
        return new AppDataPaths(Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N")));
    }

    private static void AssertSanitizedSnapshot(UsageSnapshot snapshot, string secretValue)
    {
        var text = SnapshotText(snapshot);
        Assert.DoesNotContain(secretValue, text, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", text, StringComparison.Ordinal);
    }

    private static string SnapshotText(UsageSnapshot snapshot)
    {
        return string.Join(
            Environment.NewLine,
            snapshot.DisplayName,
            snapshot.Identity?.Email,
            snapshot.Identity?.AccountName,
            snapshot.Identity?.PlanName,
            snapshot.Identity?.Organization,
            snapshot.PrimaryWindow?.Label,
            snapshot.PrimaryWindow?.ResetDescription,
            snapshot.PrimaryWindow?.Unit,
            snapshot.SecondaryWindow?.Label,
            snapshot.SecondaryWindow?.ResetDescription,
            snapshot.SecondaryWindow?.Unit,
            snapshot.Credits?.Currency,
            snapshot.StatusMessage,
            snapshot.ErrorMessage);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
        {
            await Task.Delay(20, cancellation.Token);
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

    private sealed class InMemorySnapshotStore(IEnumerable<UsageSnapshot>? initialSnapshots = null) : ISnapshotStore
    {
        private readonly List<UsageSnapshot> snapshots = initialSnapshots?.ToList() ?? [];

        public int SaveCalls { get; private set; }

        public bool ThrowOnNextSave { get; set; }

        public IReadOnlyList<UsageSnapshot> Saved => snapshots;

        public List<UsageSnapshot> History { get; } = [];

        public Task<IReadOnlyDictionary<ProviderId, UsageSnapshot>> LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyDictionary<ProviderId, UsageSnapshot>>(
                snapshots.ToDictionary(snapshot => snapshot.ProviderId));
        }

        public Task SaveAsync(IEnumerable<UsageSnapshot> nextSnapshots, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveCalls++;
            if (ThrowOnNextSave)
            {
                ThrowOnNextSave = false;
                throw new InvalidOperationException("simulated save failure");
            }

            snapshots.Clear();
            snapshots.AddRange(nextSnapshots);
            return Task.CompletedTask;
        }

        public Task AppendHistoryAsync(
            IEnumerable<UsageSnapshot> nextSnapshots,
            HistoryRetentionSettings retention,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            History.AddRange(nextSnapshots);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProviderSource(
        IReadOnlyList<ProviderDescriptor> descriptors,
        Func<ProviderDescriptor, IProviderAdapter> adapterFactory) : IProviderAdapterSource
    {
        public IReadOnlyList<ProviderDescriptor> GetDescriptors()
        {
            return descriptors;
        }

        public IProviderAdapter CreateAdapter(ProviderDescriptor descriptor, ProviderConfig config)
        {
            return adapterFactory(descriptor);
        }
    }

    private sealed class SuccessfulProviderAdapter(ProviderDescriptor descriptor) : IProviderAdapter
    {
        public ProviderDescriptor Descriptor { get; } = descriptor;

        public Task<ProviderFetchResult> FetchAsync(ProviderFetchContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = new UsageSnapshot(
                Descriptor.Id,
                Descriptor.DisplayName,
                ProviderHealth.Ok,
                Identity: null,
                new UsageWindow("Test", 25, 75, null, "test reset", "%", 25, 100),
                SecondaryWindow: null,
                Credits: null,
                DataSourceKind.Manual,
                context.Now,
                "OK",
                ErrorMessage: null);

            return Task.FromResult(ProviderFetchResult.FromSnapshot(snapshot));
        }
    }

    private sealed class ThrowingProviderAdapter(ProviderDescriptor descriptor, string message) : IProviderAdapter
    {
        public ProviderDescriptor Descriptor { get; } = descriptor;

        public Task<ProviderFetchResult> FetchAsync(ProviderFetchContext context, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class LowQuotaProviderAdapter(ProviderDescriptor descriptor) : IProviderAdapter
    {
        public ProviderDescriptor Descriptor { get; } = descriptor;

        public Task<ProviderFetchResult> FetchAsync(ProviderFetchContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = new UsageSnapshot(
                Descriptor.Id,
                Descriptor.DisplayName,
                ProviderHealth.Warning,
                Identity: null,
                new UsageWindow("Test", 85, 15, null, "test reset", "%", 85, 100),
                SecondaryWindow: null,
                Credits: null,
                DataSourceKind.Manual,
                context.Now,
                "Low",
                ErrorMessage: null);

            return Task.FromResult(ProviderFetchResult.FromSnapshot(snapshot));
        }
    }

    private sealed class MutableQuotaProviderAdapter(
        ProviderDescriptor descriptor,
        double remainingPercent) : IProviderAdapter
    {
        public ProviderDescriptor Descriptor { get; } = descriptor;

        public double RemainingPercent { get; set; } = remainingPercent;

        public Task<ProviderFetchResult> FetchAsync(ProviderFetchContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var health = RemainingPercent < 20
                ? ProviderHealth.Warning
                : ProviderHealth.Ok;
            var usedPercent = 100 - RemainingPercent;
            var snapshot = new UsageSnapshot(
                Descriptor.Id,
                Descriptor.DisplayName,
                health,
                Identity: null,
                new UsageWindow("Test", usedPercent, RemainingPercent, null, "test reset", "%", usedPercent, 100),
                SecondaryWindow: null,
                Credits: null,
                DataSourceKind.Manual,
                context.Now,
                health == ProviderHealth.Ok ? "OK" : "Low",
                ErrorMessage: null);

            return Task.FromResult(ProviderFetchResult.FromSnapshot(snapshot));
        }
    }

    private sealed class DiagnosticProviderAdapter(
        ProviderDescriptor descriptor,
        IReadOnlyList<string> diagnostics) : IProviderAdapter
    {
        public ProviderDescriptor Descriptor { get; } = descriptor;

        public Task<ProviderFetchResult> FetchAsync(ProviderFetchContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = new UsageSnapshot(
                Descriptor.Id,
                Descriptor.DisplayName,
                ProviderHealth.Ok,
                Identity: null,
                new UsageWindow("Test", 25, 75, null, "test reset", "%", 25, 100),
                SecondaryWindow: null,
                Credits: null,
                DataSourceKind.Manual,
                context.Now,
                "OK",
                ErrorMessage: null);

            return Task.FromResult(ProviderFetchResult.FromSnapshot(snapshot, diagnostics.ToArray()));
        }
    }

    private sealed class SensitiveSnapshotProviderAdapter(
        ProviderDescriptor descriptor,
        string secretValue) : IProviderAdapter
    {
        public ProviderDescriptor Descriptor { get; } = descriptor;

        public Task<ProviderFetchResult> FetchAsync(ProviderFetchContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = new UsageSnapshot(
                Descriptor.Id,
                Descriptor.DisplayName,
                ProviderHealth.Warning,
                new ProviderIdentity(
                    Email: $"person access_token={secretValue}",
                    AccountName: $"account token={secretValue}",
                    PlanName: $"plan cookie={secretValue}",
                    Organization: $"org api_key={secretValue}"),
                new UsageWindow(
                    $"Primary token={secretValue}",
                    85,
                    15,
                    null,
                    $"primary reset cookie={secretValue}",
                    $"requests secret_name={secretValue}",
                    85,
                    100),
                new UsageWindow(
                    $"Secondary access_token={secretValue}",
                    10,
                    90,
                    null,
                    $"secondary reset api_key={secretValue}",
                    $"tokens pat_secret_name={secretValue}",
                    10,
                    100),
                new ProviderCredits(1m, $"USD token={secretValue}", null, null),
                DataSourceKind.Manual,
                context.Now,
                $"status authorization: bearer {secretValue}",
                $"error cookie={secretValue}");

            return Task.FromResult(ProviderFetchResult.FromSnapshot(snapshot));
        }
    }

    private sealed class RecordingNotificationService : IAppNotificationService
    {
        public List<UsageSnapshot> Snapshots { get; } = [];

        public Task NotifyAsync(UsageSnapshot snapshot, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Snapshots.Add(snapshot);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingDiagnosticsLog : IAppDiagnosticsLog
    {
        public List<string> InfoMessages { get; } = [];

        public List<(string Message, Exception Exception)> Errors { get; } = [];

        public Task InfoAsync(string message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InfoMessages.Add(message);
            return Task.CompletedTask;
        }

        public Task ErrorAsync(string message, Exception exception, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Errors.Add((message, exception));
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingInfoDiagnosticsLog : IAppDiagnosticsLog
    {
        public Task InfoAsync(string message, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("simulated diagnostics failure");
        }

        public Task ErrorAsync(string message, Exception exception, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class NullSecretResolver : ISecretResolver
    {
        public Task<string?> ResolveSecretAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<string?>(null);
        }
    }

    private sealed class UnusedGitHubCopilotMetricsClient : IGitHubCopilotMetricsClient
    {
        public Task<GitHubCopilotMetricsFetchResult> FetchLatestReportAsync(
            GitHubCopilotMetricsRequest request,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Missing scope should not call the GitHub Copilot metrics client.");
        }
    }
}
