using System.Text.Json;
using WinAiUsageBar.Core.Configuration;

namespace WinAiUsageBar.Infrastructure.Storage;

public interface IConfigResetService
{
    Task<ConfigResetResult> ResetToDefaultsAsync(CancellationToken cancellationToken);
}

public sealed record ConfigResetResult(
    bool Reset,
    string RollbackBackupPath,
    int ConfigVersion,
    int ProviderCount,
    int EnabledProviderCount,
    IReadOnlyList<string> Warnings);

public sealed class ConfigResetService(
    AppDataPaths paths,
    IAppConfigStore configStore,
    Func<DateTimeOffset>? nowProvider = null) : IConfigResetService
{
    private readonly JsonSerializerOptions options = JsonInfrastructureOptions.CreateIndented();
    private readonly Func<DateTimeOffset> nowProvider = nowProvider ?? (() => DateTimeOffset.Now);

    public async Task<ConfigResetResult> ResetToDefaultsAsync(CancellationToken cancellationToken)
    {
        paths.EnsureCreated();
        var current = await configStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var rollbackPath = await CreateRollbackBackupAsync(current, cancellationToken).ConfigureAwait(false);
        var defaults = AppConfig.CreateDefault();
        await configStore.SaveAsync(defaults, cancellationToken).ConfigureAwait(false);

        return new ConfigResetResult(
            Reset: true,
            rollbackPath,
            defaults.Version,
            defaults.Providers.Count,
            defaults.Providers.Count(provider => provider.IsEnabled),
            Warnings: ["Saved secrets were not deleted. Reconnect secret references from Privacy & Data if needed."]);
    }

    private async Task<string> CreateRollbackBackupAsync(
        AppConfig current,
        CancellationToken cancellationToken)
    {
        var createdAt = nowProvider();
        var rollbackPath = Path.Combine(
            paths.ConfigBackupsDirectory,
            $"config-backup-before-reset-{createdAt:yyyyMMdd-HHmmss}.json");
        var tempPath = $"{rollbackPath}.tmp";

        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, current, options, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, rollbackPath, overwrite: true);
        return rollbackPath;
    }
}
