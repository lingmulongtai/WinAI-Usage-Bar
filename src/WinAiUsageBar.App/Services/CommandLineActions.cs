using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;
using WinAiUsageBar.Infrastructure.Diagnostics;
using WinAiUsageBar.Infrastructure.Notifications;
using WinAiUsageBar.Infrastructure.Process;
using WinAiUsageBar.Infrastructure.Storage;
using WinAiUsageBar.Infrastructure.Updates;

namespace WinAiUsageBar.App.Services;

public static class CommandLineActions
{
    private static readonly CliCommandCheck[] HealthReportCliChecks =
    [
        new("codex", "--version"),
        new("claude", "--version"),
        new("gh", "--version"),
        new("git", "--version")
    ];

    public static async Task<string> ExportDiagnosticsAsync(CancellationToken cancellationToken)
    {
        var paths = AppDataPaths.CreateDefault();
        var service = new DiagnosticsExportService(paths);
        var result = await service.ExportAsync(cancellationToken).ConfigureAwait(false);
        return result.Path;
    }

    public static async Task<CommandLineActionResult> ExportConfigBackupAsync(
        CancellationToken cancellationToken)
    {
        return await ExportConfigBackupAsync(
            cancellationToken,
            AppDataPaths.CreateDefault()).ConfigureAwait(false);
    }

    public static async Task<CommandLineActionResult> ExportConfigBackupAsync(
        CancellationToken cancellationToken,
        AppDataPaths paths)
    {
        var service = new DataMaintenanceService(paths, new JsonAppConfigStore(paths));
        var result = await service.ExportConfigBackupAsync(cancellationToken).ConfigureAwait(false);
        return new CommandLineActionResult(
            CommandLineConfigBackupExportFormatter.Format(result),
            0);
    }

    public static async Task<string> CreateHealthReportAsync(CancellationToken cancellationToken)
    {
        var paths = AppDataPaths.CreateDefault();
        return await CreateHealthReportAsync(cancellationToken, paths).ConfigureAwait(false);
    }

    public static async Task<string> CreateHealthReportAsync(
        CancellationToken cancellationToken,
        AppDataPaths paths,
        ICliEnvironmentService? cliEnvironmentService = null)
    {
        var configStore = new JsonAppConfigStore(paths);
        var snapshotStore = new JsonSnapshotStore(paths);
        var diagnosticsService = new DiagnosticsSummaryService(paths, configStore, snapshotStore);
        var historyService = new HistorySummaryService(paths);
        var storagePressureService = new StoragePressureGuidanceService();
        var recoveryGuidanceService = new RecoveryGuidanceService();
        cliEnvironmentService ??= new CliEnvironmentService();

        var diagnostics = await diagnosticsService.GetSummaryAsync(cancellationToken).ConfigureAwait(false);
        var history = await historyService.GetSummaryAsync(cancellationToken).ConfigureAwait(false);
        var storagePressure = storagePressureService.CreateGuidance(diagnostics);
        var recoveryGuidance = recoveryGuidanceService.CreateGuidance(diagnostics);
        var cliEnvironment = await cliEnvironmentService.GetReportAsync(HealthReportCliChecks, cancellationToken)
            .ConfigureAwait(false);

        return CommandLineHealthReportFormatter.Format(
            AppInfoProvider.Get(),
            diagnostics,
            history,
            DateTimeOffset.Now,
            cliEnvironment,
            storagePressure,
            recoveryGuidance);
    }

    public static string CreateProviderCatalog()
    {
        return CommandLineProviderCatalogFormatter.Format(ProviderDescriptors.All);
    }

    public static async Task<CommandLineActionResult> SetProviderCliOverrideAsync(
        CommandLineSetProviderCliOverrideOptions options,
        CancellationToken cancellationToken)
    {
        return await SetProviderCliOverrideAsync(
            options,
            cancellationToken,
            AppDataPaths.CreateDefault()).ConfigureAwait(false);
    }

    public static async Task<CommandLineActionResult> SetProviderCliOverrideAsync(
        CommandLineSetProviderCliOverrideOptions options,
        CancellationToken cancellationToken,
        AppDataPaths paths)
    {
        var validation = ValidateProviderCliOverrideOptions(options);
        if (!validation.IsValid || validation.Descriptor is null || validation.NormalizedOverride is null)
        {
            return new CommandLineActionResult(validation.ErrorMessage, 2);
        }

        var configStore = new JsonAppConfigStore(paths);
        var config = await configStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var provider = config.GetOrCreateProvider(validation.Descriptor);
        provider.Cli.CommandPathOverride = validation.NormalizedOverride;
        await configStore.SaveAsync(config, cancellationToken).ConfigureAwait(false);

        return new CommandLineActionResult(
            $"""
            Provider CLI override
            Provider: {validation.Descriptor.DisplayName}
            Status: saved
            Command override: configured (value not shown)
            """,
            0);
    }

    public static async Task<CommandLineActionResult> CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        return await CheckForUpdatesAsync(
            new CommandLineUpdateVersionOptions(CurrentVersionOverride: null),
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task<CommandLineActionResult> CheckForUpdatesAsync(
        CommandLineUpdateVersionOptions options,
        CancellationToken cancellationToken)
    {
        var service = new ReleaseUpdateCheckService(new GitHubLatestReleaseClient());
        return await CheckForUpdatesAsync(
            AppInfoProvider.Get(),
            service,
            cancellationToken,
            options.CurrentVersionOverride).ConfigureAwait(false);
    }

    public static async Task<CommandLineActionResult> CheckForUpdatesAsync(
        AppInfo appInfo,
        IReleaseUpdateCheckService service,
        CancellationToken cancellationToken,
        string? currentVersionOverride = null)
    {
        var result = await service.CheckAsync(
            ResolveCurrentVersion(appInfo, currentVersionOverride),
            cancellationToken).ConfigureAwait(false);
        return new CommandLineActionResult(
            CommandLineUpdateCheckFormatter.Format(result),
            result.Status == UpdateCheckStatus.Error ? 1 : 0);
    }

    public static async Task<CommandLineActionResult> DownloadUpdateAsync(CancellationToken cancellationToken)
    {
        return await DownloadUpdateAsync(
            new CommandLineUpdateVersionOptions(CurrentVersionOverride: null),
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task<CommandLineActionResult> DownloadUpdateAsync(
        CommandLineUpdateVersionOptions options,
        CancellationToken cancellationToken)
    {
        var paths = AppDataPaths.CreateDefault();
        var updateCheck = new ReleaseUpdateCheckService(new GitHubLatestReleaseClient());
        var downloader = new UpdatePackageDownloader();
        return await DownloadUpdateAsync(
            AppInfoProvider.Get(),
            updateCheck,
            downloader,
            paths,
            cancellationToken,
            options.CurrentVersionOverride).ConfigureAwait(false);
    }

    public static async Task<CommandLineActionResult> DownloadUpdateAsync(
        AppInfo appInfo,
        IReleaseUpdateCheckService updateCheck,
        IUpdatePackageDownloader downloader,
        AppDataPaths paths,
        CancellationToken cancellationToken,
        string? currentVersionOverride = null)
    {
        var update = await updateCheck.CheckAsync(
            ResolveCurrentVersion(appInfo, currentVersionOverride),
            cancellationToken).ConfigureAwait(false);
        if (!update.IsUpdateAvailable || update.Package is null || update.Checksum is null)
        {
            var exitCode = update.Status == UpdateCheckStatus.Error ? 1 : 0;
            return new CommandLineActionResult(
                CommandLineUpdateDownloadFormatter.Format(update, download: null),
                exitCode);
        }

        var download = await downloader.DownloadAndVerifyAsync(
            update.Package,
            update.Checksum,
            paths.UpdatesDirectory,
            cancellationToken).ConfigureAwait(false);
        var isFailure = download.Status is UpdateDownloadStatus.Error
            or UpdateDownloadStatus.InvalidAsset
            or UpdateDownloadStatus.ChecksumMismatch;

        return new CommandLineActionResult(
            CommandLineUpdateDownloadFormatter.Format(update, download),
            isFailure ? 1 : 0);
    }

    public static async Task<CommandLineActionResult> PrepareUpdateInstallAsync(
        CommandLinePrepareUpdateInstallOptions options,
        CancellationToken cancellationToken)
    {
        var paths = AppDataPaths.CreateDefault();
        var service = new UpdateInstallPreparationService(paths);
        return await PrepareUpdateInstallAsync(
            options,
            service,
            defaultInstallDirectory: AppContext.BaseDirectory,
            processIdToWait: Environment.ProcessId,
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task<CommandLineActionResult> PrepareUpdateInstallAsync(
        CommandLinePrepareUpdateInstallOptions options,
        IUpdateInstallPreparationService service,
        string defaultInstallDirectory,
        int processIdToWait,
        CancellationToken cancellationToken)
    {
        var installDirectory = string.IsNullOrWhiteSpace(options.InstallDirectory)
            ? defaultInstallDirectory
            : options.InstallDirectory;
        var result = await service.PrepareAsync(
            new UpdateInstallPreparationRequest(
                options.PackagePath,
                installDirectory,
                processIdToWait,
                options.RestartAfterInstall),
            cancellationToken).ConfigureAwait(false);
        var isFailure = result.Status is not UpdateInstallPreparationStatus.Prepared;
        return new CommandLineActionResult(
            CommandLineUpdateInstallPreparationFormatter.Format(result),
            isFailure ? 1 : 0);
    }

    public static async Task<CommandLineActionResult> LaunchPreparedUpdateAsync(
        CommandLineLaunchPreparedUpdateOptions options,
        CancellationToken cancellationToken)
    {
        var paths = AppDataPaths.CreateDefault();
        var service = new UpdateInstallLaunchService(paths);
        return await LaunchPreparedUpdateAsync(options, service, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<CommandLineActionResult> LaunchPreparedUpdateAsync(
        CommandLineLaunchPreparedUpdateOptions options,
        IUpdateInstallLaunchService service,
        CancellationToken cancellationToken)
    {
        var result = await service.LaunchAsync(
            new UpdateInstallLaunchRequest(options.ScriptPath),
            cancellationToken).ConfigureAwait(false);
        var isFailure = result.Status is not UpdateInstallLaunchStatus.Launched;
        return new CommandLineActionResult(
            CommandLineUpdateInstallLaunchFormatter.Format(result),
            isFailure ? 1 : 0);
    }

    public static async Task<CommandLineActionResult> InstallLatestUpdateAsync(
        CommandLineInstallLatestUpdateOptions options,
        CancellationToken cancellationToken)
    {
        var paths = AppDataPaths.CreateDefault();
        var updateCheck = new ReleaseUpdateCheckService(new GitHubLatestReleaseClient());
        var downloader = new UpdatePackageDownloader();
        var preparation = new UpdateInstallPreparationService(paths);
        var launcher = new UpdateInstallLaunchService(paths);
        var service = new LatestUpdateInstallService(
            updateCheck,
            downloader,
            preparation,
            launcher,
            paths);
        return await InstallLatestUpdateAsync(
            options,
            service,
            AppInfoProvider.Get(),
            installDirectory: AppContext.BaseDirectory,
            processIdToWait: Environment.ProcessId,
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task<CommandLineActionResult> InstallLatestUpdateAsync(
        CommandLineInstallLatestUpdateOptions options,
        ILatestUpdateInstallService service,
        AppInfo appInfo,
        string installDirectory,
        int processIdToWait,
        CancellationToken cancellationToken)
    {
        var result = await service.InstallLatestAsync(
            new LatestUpdateInstallRequest(
                ResolveCurrentVersion(appInfo, options.CurrentVersionOverride),
                installDirectory,
                processIdToWait,
                options.RestartAfterInstall),
            cancellationToken).ConfigureAwait(false);
        var isFailure = result.Status is not LatestUpdateInstallStatus.SkippedNoUpdate
            and not LatestUpdateInstallStatus.Launched;
        return new CommandLineActionResult(
            CommandLineLatestUpdateInstallFormatter.Format(result),
            isFailure ? 1 : 0);
    }

    public static async Task<CommandLineActionResult> RunStartupUpdateAsync(CancellationToken cancellationToken)
    {
        var paths = AppDataPaths.CreateDefault();
        var configStore = new JsonAppConfigStore(paths);
        var updateCheck = new ReleaseUpdateCheckService(new GitHubLatestReleaseClient());
        var downloader = new UpdatePackageDownloader();
        var preparation = new UpdateInstallPreparationService(paths);
        var launcher = new UpdateInstallLaunchService(paths);
        var service = new StartupUpdateService(
            configStore,
            updateCheck,
            downloader,
            preparation,
            launcher,
            paths);

        return await RunStartupUpdateAsync(
            service,
            AppInfoProvider.Get(),
            installDirectory: AppContext.BaseDirectory,
            processIdToWait: Environment.ProcessId,
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task<CommandLineActionResult> RunStartupUpdateAsync(
        IStartupUpdateService service,
        AppInfo appInfo,
        string installDirectory,
        int processIdToWait,
        CancellationToken cancellationToken)
    {
        var currentVersion = appInfo.InformationalVersion;
        var result = await service.RunAsync(
            new StartupUpdateRequest(
                currentVersion,
                installDirectory,
                processIdToWait),
            cancellationToken).ConfigureAwait(false);
        return new CommandLineActionResult(
            CommandLineStartupUpdateFormatter.Format(result, currentVersion),
            0);
    }

    private static string ResolveCurrentVersion(AppInfo appInfo, string? currentVersionOverride)
    {
        return string.IsNullOrWhiteSpace(currentVersionOverride)
            ? appInfo.InformationalVersion
            : currentVersionOverride.Trim();
    }

    public static async Task<CommandLineActionResult> PruneSupportArtifactsAsync(
        CommandLinePruneSupportArtifactsOptions options,
        CancellationToken cancellationToken)
    {
        return await PruneSupportArtifactsAsync(
            options,
            cancellationToken,
            AppDataPaths.CreateDefault()).ConfigureAwait(false);
    }

    public static async Task<CommandLineActionResult> PruneSupportArtifactsAsync(
        CommandLinePruneSupportArtifactsOptions options,
        CancellationToken cancellationToken,
        AppDataPaths paths)
    {
        var service = new DataMaintenanceService(paths, new JsonAppConfigStore(paths));
        var backups = await service.PruneConfigBackupsAsync(options.KeepNewest, cancellationToken)
            .ConfigureAwait(false);
        var diagnosticsExports = await service.PruneDiagnosticsExportsAsync(options.KeepNewest, cancellationToken)
            .ConfigureAwait(false);

        return new CommandLineActionResult(
            CommandLineSupportArtifactPruneFormatter.Format(backups, diagnosticsExports),
            0);
    }

    public static async Task<CommandLineActionResult> RefreshOnceAsync(
        CommandLineRefreshOnceOptions options,
        CancellationToken cancellationToken)
    {
        return await RefreshOnceAsync(options, cancellationToken, AppDataPaths.CreateDefault()).ConfigureAwait(false);
    }

    public static async Task<CommandLineActionResult> RefreshOnceAsync(
        CommandLineRefreshOnceOptions options,
        CancellationToken cancellationToken,
        AppDataPaths paths)
    {
        var baseConfigStore = new JsonAppConfigStore(paths);
        var config = await baseConfigStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var overrideResult = ApplyRefreshOnceOverrides(config, options);
        if (!overrideResult.IsValid)
        {
            return new CommandLineActionResult(overrideResult.ErrorMessage, 2);
        }

        var services = AppCompositionRoot.CreateServices(
            paths,
            new NoOpAppNotificationService(),
            new OneShotConfigStore(config));
        var refreshService = services.RefreshService;

        try
        {
            await refreshService.InitializeAsync(cancellationToken).ConfigureAwait(false);
            await refreshService.RefreshNowAsync(cancellationToken).ConfigureAwait(false);
            var output = CommandLineRefreshReportFormatter.Format(
                AppInfoProvider.Get(),
                refreshService.CurrentSnapshots,
                DateTimeOffset.Now);
            return new CommandLineActionResult(output, 0);
        }
        finally
        {
            await refreshService.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static RefreshOnceOverrideResult ApplyRefreshOnceOverrides(
        AppConfig config,
        CommandLineRefreshOnceOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ProviderId))
        {
            return RefreshOnceOverrideResult.Valid();
        }

        if (!Enum.TryParse<ProviderId>(options.ProviderId, ignoreCase: true, out var providerId))
        {
            return RefreshOnceOverrideResult.Invalid(
                $"Unknown provider '{options.ProviderId}'. Use --provider-catalog to list supported providers.");
        }

        var descriptor = ProviderDescriptors.All.FirstOrDefault(item => item.Id == providerId);
        if (descriptor is null)
        {
            return RefreshOnceOverrideResult.Invalid(
                $"Unknown provider '{options.ProviderId}'. Use --provider-catalog to list supported providers.");
        }

        DataSourceKind? sourceKind = null;
        if (!string.IsNullOrWhiteSpace(options.SourceKind))
        {
            if (!Enum.TryParse<DataSourceKind>(options.SourceKind, ignoreCase: true, out var parsedSourceKind))
            {
                return RefreshOnceOverrideResult.Invalid(
                    $"Unknown source '{options.SourceKind}'. Use --provider-catalog to list supported sources.");
            }

            if (!descriptor.SupportedSources.Contains(parsedSourceKind))
            {
                return RefreshOnceOverrideResult.Invalid(
                    $"{descriptor.DisplayName} does not support source {parsedSourceKind}. Supported sources: {string.Join(", ", descriptor.SupportedSources)}.");
            }

            sourceKind = parsedSourceKind;
        }

        foreach (var provider in config.Providers)
        {
            provider.IsEnabled = false;
        }

        var selectedProvider = config.GetOrCreateProvider(descriptor);
        selectedProvider.IsEnabled = true;
        if (sourceKind is not null)
        {
            selectedProvider.SourceKind = sourceKind.Value;
        }

        return RefreshOnceOverrideResult.Valid();
    }

    private static ProviderCliOverrideValidationResult ValidateProviderCliOverrideOptions(
        CommandLineSetProviderCliOverrideOptions options)
    {
        if (!Enum.TryParse<ProviderId>(options.ProviderId, ignoreCase: true, out var providerId))
        {
            return ProviderCliOverrideValidationResult.Invalid(
                $"Unknown provider '{options.ProviderId}'. Use --provider-catalog to list supported providers.");
        }

        var descriptor = ProviderDescriptors.All.FirstOrDefault(item => item.Id == providerId);
        if (descriptor is null)
        {
            return ProviderCliOverrideValidationResult.Invalid(
                $"Unknown provider '{options.ProviderId}'. Use --provider-catalog to list supported providers.");
        }

        if (!descriptor.SupportedSources.Any(source => source is DataSourceKind.Cli or DataSourceKind.LocalAppServer))
        {
            return ProviderCliOverrideValidationResult.Invalid(
                $"{descriptor.DisplayName} does not support CLI command overrides.");
        }

        if (CliCommandSettings.HasInvalidCommandPathOverrideQuotes(options.CommandPathOverride))
        {
            return ProviderCliOverrideValidationResult.Invalid(
                "CLI command override quotes must wrap a single path or command.");
        }

        var normalizedOverride = CliCommandSettings.NormalizeCommandPathOverride(options.CommandPathOverride);
        if (normalizedOverride is null)
        {
            return ProviderCliOverrideValidationResult.Invalid(
                "CLI command override requires --command <path-or-command>.");
        }

        if (normalizedOverride.Length > 512)
        {
            return ProviderCliOverrideValidationResult.Invalid(
                "CLI command override must be 512 characters or fewer.");
        }

        if (normalizedOverride.Contains('\r') || normalizedOverride.Contains('\n'))
        {
            return ProviderCliOverrideValidationResult.Invalid(
                "CLI command override must be a single path or command.");
        }

        if (CliCommandSettings.LooksSensitiveCommandPathOverride(normalizedOverride))
        {
            return ProviderCliOverrideValidationResult.Invalid(
                "CLI command override must not contain tokens, cookies, or auth values.");
        }

        return ProviderCliOverrideValidationResult.Valid(descriptor, normalizedOverride);
    }

    public static async Task<CommandLineActionResult> ValidateConfigBackupAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var service = new ConfigBackupValidationService();
        var result = await service.ValidateAsync(path, cancellationToken).ConfigureAwait(false);
        return new CommandLineActionResult(
            CommandLineConfigBackupValidationFormatter.Format(result),
            result.IsValid ? 0 : 1);
    }

    public static async Task<CommandLineActionResult> RestoreConfigBackupAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var paths = AppDataPaths.CreateDefault();
        var configStore = new JsonAppConfigStore(paths);
        var service = new ConfigBackupRestoreService(paths, configStore);
        var result = await service.RestoreAsync(path, cancellationToken).ConfigureAwait(false);
        return new CommandLineActionResult(
            CommandLineConfigBackupRestoreFormatter.Format(result),
            result.Restored ? 0 : 1);
    }
}

public sealed record CommandLineActionResult(
    string Output,
    int ExitCode);

internal sealed record RefreshOnceOverrideResult(
    bool IsValid,
    string ErrorMessage)
{
    public static RefreshOnceOverrideResult Valid()
    {
        return new RefreshOnceOverrideResult(true, string.Empty);
    }

    public static RefreshOnceOverrideResult Invalid(string errorMessage)
    {
        return new RefreshOnceOverrideResult(false, errorMessage);
    }
}

internal sealed record ProviderCliOverrideValidationResult(
    bool IsValid,
    ProviderDescriptor? Descriptor,
    string? NormalizedOverride,
    string ErrorMessage)
{
    public static ProviderCliOverrideValidationResult Valid(
        ProviderDescriptor descriptor,
        string normalizedOverride)
    {
        return new ProviderCliOverrideValidationResult(true, descriptor, normalizedOverride, string.Empty);
    }

    public static ProviderCliOverrideValidationResult Invalid(string errorMessage)
    {
        return new ProviderCliOverrideValidationResult(false, null, null, errorMessage);
    }
}

internal sealed class OneShotConfigStore(AppConfig config) : IAppConfigStore
{
    public Task<AppConfig> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(config);
    }

    public Task SaveAsync(AppConfig nextConfig, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
