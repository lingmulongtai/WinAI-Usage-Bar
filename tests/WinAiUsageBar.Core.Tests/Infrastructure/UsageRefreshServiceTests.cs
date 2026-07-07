using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;
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
        var refreshService = new UsageRefreshService(
            new InMemoryConfigStore(config),
            snapshotStore,
            new FakeProviderSource(
                [ProviderDescriptors.Get(ProviderId.Codex)],
                descriptor => new SuccessfulProviderAdapter(descriptor)),
            TestPaths(),
            new RecordingNotificationService(),
            _ => TimeSpan.FromMilliseconds(10));

        await refreshService.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => snapshotStore.SaveCalls >= 2);
        await refreshService.DisposeAsync();

        Assert.True(snapshotStore.SaveCalls >= 2);
        Assert.NotEmpty(snapshotStore.Saved);
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

        public Task AppendHistoryAsync(IEnumerable<UsageSnapshot> nextSnapshots, CancellationToken cancellationToken)
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

    private sealed class RecordingNotificationService : IAppNotificationService
    {
        public Task NotifyAsync(UsageSnapshot snapshot, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
