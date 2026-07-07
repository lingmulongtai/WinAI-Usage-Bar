using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;
using WinAiUsageBar.Infrastructure.Diagnostics;
using WinAiUsageBar.Infrastructure.Notifications;
using WinAiUsageBar.Infrastructure.Security;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Infrastructure.Scheduling;

public interface IUsageRefreshService : IAsyncDisposable
{
    event EventHandler<IReadOnlyList<UsageSnapshot>>? SnapshotsChanged;

    IReadOnlyList<UsageSnapshot> CurrentSnapshots { get; }

    Task InitializeAsync(CancellationToken cancellationToken);

    Task StartAsync(CancellationToken cancellationToken);

    Task RestartAsync(CancellationToken cancellationToken);

    Task RefreshNowAsync(CancellationToken cancellationToken);
}

public sealed class UsageRefreshService(
    IAppConfigStore configStore,
    ISnapshotStore snapshotStore,
    IProviderAdapterSource providerRegistry,
    AppDataPaths paths,
    IAppNotificationService notificationService,
    Func<RefreshIntervalKind, TimeSpan?>? intervalMapper = null,
    IAppDiagnosticsLog? diagnosticsLog = null) : IUsageRefreshService
{
    private readonly SemaphoreSlim refreshLock = new(1, 1);
    private readonly SemaphoreSlim scheduleLock = new(1, 1);
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

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return RestartAsync(cancellationToken);
    }

    public async Task RestartAsync(CancellationToken cancellationToken)
    {
        await scheduleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopLoopAsync().ConfigureAwait(false);
            await StartLoopFromConfigAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            scheduleLock.Release();
        }
    }

    private async Task StartLoopFromConfigAsync(CancellationToken cancellationToken)
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

                await LogProviderDiagnosticsAsync(descriptor, result.Diagnostics).ConfigureAwait(false);

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
            await snapshotStore.AppendHistoryAsync(snapshots, config.HistoryRetention, cancellationToken).ConfigureAwait(false);
            SnapshotsChanged?.Invoke(this, CurrentSnapshots);
        }
        finally
        {
            refreshLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopLoopAsync().ConfigureAwait(false);

        await notificationService.DisposeAsync().ConfigureAwait(false);
        refreshLock.Dispose();
        scheduleLock.Dispose();
    }

    private async Task StopLoopAsync()
    {
        var cancellation = loopCancellation;
        var task = loopTask;
        loopCancellation = null;
        loopTask = null;

        if (cancellation is not null)
        {
            await cancellation.CancelAsync().ConfigureAwait(false);
            cancellation.Dispose();
        }

        if (task is not null)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }
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
            catch (Exception ex)
            {
                await LogLoopFailureAsync(ex).ConfigureAwait(false);
                // A refresh-level failure should not permanently stop future timer ticks.
            }
        }
    }

    private async Task LogProviderDiagnosticsAsync(
        ProviderDescriptor descriptor,
        IReadOnlyList<string> diagnostics)
    {
        if (diagnosticsLog is null || diagnostics.Count == 0)
        {
            return;
        }

        try
        {
            foreach (var diagnostic in diagnostics.Where(diagnostic => !string.IsNullOrWhiteSpace(diagnostic)).Take(10))
            {
                var safeDiagnostic = DiagnosticRedactor.Redact(diagnostic);
                await diagnosticsLog.InfoAsync(
                    $"{descriptor.DisplayName} provider diagnostic: {safeDiagnostic}",
                    CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch
        {
            // Provider diagnostics are helpful but must never fail refresh.
        }
    }

    private async Task LogLoopFailureAsync(Exception exception)
    {
        if (diagnosticsLog is null)
        {
            return;
        }

        try
        {
            await diagnosticsLog.ErrorAsync(
                "Periodic refresh failed; future ticks will continue.",
                exception,
                CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Diagnostics must never stop the refresh loop.
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
