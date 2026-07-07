using System.Text.Json;
using WinAiUsageBar.Core.Configuration;

namespace WinAiUsageBar.Infrastructure.Storage;

public interface IConfigBackupRestoreService
{
    Task<ConfigBackupRestoreResult> RestoreAsync(string path, CancellationToken cancellationToken);
}

public sealed record ConfigBackupRestoreResult(
    string Path,
    bool Restored,
    string? RollbackBackupPath,
    int? ConfigVersion,
    int? ProviderCount,
    int? EnabledProviderCount,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);

public sealed class ConfigBackupRestoreService(
    AppDataPaths paths,
    IAppConfigStore configStore,
    IConfigBackupValidationService? validationService = null,
    Func<DateTimeOffset>? nowProvider = null) : IConfigBackupRestoreService
{
    private readonly IConfigBackupValidationService validationService = validationService ?? new ConfigBackupValidationService();
    private readonly JsonSerializerOptions options = JsonInfrastructureOptions.CreateIndented();
    private readonly Func<DateTimeOffset> nowProvider = nowProvider ?? (() => DateTimeOffset.Now);

    public async Task<ConfigBackupRestoreResult> RestoreAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var validation = await validationService.ValidateAsync(path, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return new ConfigBackupRestoreResult(
                validation.Path,
                Restored: false,
                RollbackBackupPath: null,
                ConfigVersion: null,
                ProviderCount: null,
                EnabledProviderCount: null,
                validation.Errors,
                validation.Warnings);
        }

        var migrated = await LoadMigratedConfigAsync(validation.Path, cancellationToken).ConfigureAwait(false);
        var rollbackPath = await CreateRollbackBackupAsync(cancellationToken).ConfigureAwait(false);
        await configStore.SaveAsync(migrated, cancellationToken).ConfigureAwait(false);

        return new ConfigBackupRestoreResult(
            validation.Path,
            Restored: true,
            rollbackPath,
            migrated.Version,
            migrated.Providers.Count,
            migrated.Providers.Count(provider => provider.IsEnabled),
            Errors: [],
            validation.Warnings);
    }

    private async Task<AppConfig> LoadMigratedConfigAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var parsed = await JsonSerializer.DeserializeAsync<AppConfig>(stream, options, cancellationToken).ConfigureAwait(false);
        return AppConfigMigrations.Migrate(parsed);
    }

    private async Task<string> CreateRollbackBackupAsync(CancellationToken cancellationToken)
    {
        paths.EnsureCreated();
        var current = await configStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var createdAt = nowProvider();
        var rollbackPath = Path.Combine(
            paths.ConfigBackupsDirectory,
            $"config-backup-before-restore-{createdAt:yyyyMMdd-HHmmss}.json");
        var tempPath = $"{rollbackPath}.tmp";

        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, current, options, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, rollbackPath, overwrite: true);
        return rollbackPath;
    }
}
