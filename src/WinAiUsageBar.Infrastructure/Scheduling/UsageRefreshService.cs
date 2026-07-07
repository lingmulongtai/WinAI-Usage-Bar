using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;
using WinAiUsageBar.Infrastructure.Notifications;
using WinAiUsageBar.Infrastructure.Security;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Infrastructure.Scheduling;

public sealed class UsageRefreshService(
    IAppConfigStore configStore,
    ISnapshotStore snapshotStore,
    IProviderAdapterSource providerRegistry,
    AppDataPaths paths,
    IAppNotificationService notificationService,
    Func<RefreshIntervalKind, TimeSpan?>? intervalMapper = null) : IAsyncDisposable
{
    private readonly SemaphoreSlim refreshLock = new(1, 1);
    private readonly Func<RefreshIntervalKind, TimeSpan?> mapRefreshInterval = intervalMapper ?? RefreshIntervalMapper.ToTimeSpan;
    private CancellationTokenSource? loopCancellation;
    private Task? loopTask;
    private IReadOnlyDictionary<ProviderId, UsageSnapshot> currentSnapshots = new Dictionary<ProviderId, UsageSnapshot>();

    public event EventHandler<IReadOnlyList<UsageSnapshot>>? SnapshotsChanged;

    public IReadOnlyList<UsageSnapshot> CurrentSnapshots => currentSnapshots.Values
        .OrderBy(snapshot => snapshot.DisplayName)
        .ToList();

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        currentSnapshots = await snapshotStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        SnapshotsChanged?.Invoke(this, CurrentSnapshots);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var config = await configStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var interval = mapRefreshInterval(config.Refresh.Interval);
        if (interval is null)
        {
            return;
        }

        loopCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        loopTask = RunLoopAsync(interval.Value, loopCancellation.Token);
    }

    public async Task RefreshNowAsync(CancellationToken cancellationToken)
    {
        await refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var config = await configStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            var snapshots = new List<UsageSnapshot>();
            var previous = currentSnapshots;

            foreach (var descriptor in providerRegistry.GetDescriptors())
            {
                var providerConfig = config.GetOrCreateProvider(descriptor);
                if (!providerConfig.IsEnabled)
                {
                    continue;
                }

                using var providerTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                providerTimeout.CancelAfter(TimeSpan.FromSeconds(15));

                var context = new ProviderFetchContext(providerConfig, DateTimeOffset.Now, paths.RootDirectory);
                var result = await FetchProviderSafelyAsync(
                    descriptor,
                    providerConfig,
                    context,
                    providerTimeout.Token,
                    cancellationToken).ConfigureAwait(false);

                if (result.Snapshot is null)
                {
                    continue;
                }

                var snapshot = result.Success || !previous.TryGetValue(descriptor.Id, out var cached)
                    ? result.Snapshot
                    : cached with
                    {
                        Health = result.Snapshot.Health,
                        SourceKind = result.Snapshot.SourceKind,
                        UpdatedAt = result.Snapshot.UpdatedAt,
                        StatusMessage = result.Snapshot.StatusMessage,
                        ErrorMessage = result.Snapshot.ErrorMessage
                    };

                snapshots.Add(snapshot);

                if (ShouldNotify(config, snapshot))
                {
                    await notificationService.NotifyAsync(snapshot, cancellationToken).ConfigureAwait(false);
                }
            }

            currentSnapshots = snapshots.ToDictionary(snapshot => snapshot.ProviderId);
            await snapshotStore.SaveAsync(snapshots, cancellationToken).ConfigureAwait(false);
            await snapshotStore.AppendHistoryAsync(snapshots, cancellationToken).ConfigureAwait(false);
            SnapshotsChanged?.Invoke(this, CurrentSnapshots);
        }
        finally
        {
            refreshLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (loopCancellation is not null)
        {
            await loopCancellation.CancelAsync().ConfigureAwait(false);
            loopCancellation.Dispose();
        }

        if (loopTask is not null)
        {
            try
            {
                await loopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        refreshLock.Dispose();
    }

    private async Task RunLoopAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                await RefreshNowAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // A refresh-level failure should not permanently stop future timer ticks.
            }
        }
    }

    private async Task<ProviderFetchResult> FetchProviderSafelyAsync(
        ProviderDescriptor descriptor,
        ProviderConfig providerConfig,
        ProviderFetchContext context,
        CancellationToken providerCancellationToken,
        CancellationToken refreshCancellationToken)
    {
        try
        {
            var adapter = providerRegistry.CreateAdapter(descriptor, providerConfig);
            return await adapter.FetchAsync(context, providerCancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (refreshCancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (providerCancellationToken.IsCancellationRequested)
        {
            return ProviderFetchResult.Failure(
                descriptor,
                ProviderHealth.Error,
                providerConfig.SourceKind,
                context.Now,
                $"{descriptor.DisplayName} refresh timed out.",
                "Provider refresh timed out.");
        }
        catch (Exception ex)
        {
            var safeMessage = DiagnosticRedactor.Redact(ex.Message);
            return ProviderFetchResult.Failure(
                descriptor,
                ProviderHealth.Error,
                providerConfig.SourceKind,
                context.Now,
                $"{descriptor.DisplayName} refresh failed.",
                safeMessage);
        }
    }

    private static bool ShouldNotify(AppConfig config, UsageSnapshot snapshot)
    {
        if (!config.Notifications.IsEnabled)
        {
            return false;
        }

        return snapshot.Health == ProviderHealth.AuthRequired
            || snapshot.PrimaryWindow?.RemainingPercent < 20;
    }
}
