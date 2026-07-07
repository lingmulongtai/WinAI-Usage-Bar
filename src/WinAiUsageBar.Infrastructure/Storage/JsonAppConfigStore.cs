using System.Text.Json;
using WinAiUsageBar.Core.Configuration;

namespace WinAiUsageBar.Infrastructure.Storage;

public interface IAppConfigStore
{
    Task<AppConfig> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(AppConfig config, CancellationToken cancellationToken);
}

public sealed class JsonAppConfigStore(AppDataPaths paths) : IAppConfigStore
{
    private readonly JsonSerializerOptions options = JsonInfrastructureOptions.CreateIndented();

    public async Task<AppConfig> LoadAsync(CancellationToken cancellationToken)
    {
        paths.EnsureCreated();

        if (!File.Exists(paths.ConfigPath))
        {
            var defaultConfig = AppConfigMigrations.Migrate(null);
            await SaveAsync(defaultConfig, cancellationToken).ConfigureAwait(false);
            return defaultConfig;
        }

        try
        {
            AppConfig? config;
            await using (var stream = File.OpenRead(paths.ConfigPath))
            {
                config = await JsonSerializer.DeserializeAsync<AppConfig>(stream, options, cancellationToken).ConfigureAwait(false);
            }

            var migrated = AppConfigMigrations.Migrate(config);
            await SaveAsync(migrated, cancellationToken).ConfigureAwait(false);
            return migrated;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            BackupInvalidConfig();
            var defaultConfig = AppConfigMigrations.Migrate(null);
            await SaveAsync(defaultConfig, cancellationToken).ConfigureAwait(false);
            return defaultConfig;
        }
    }

    public async Task SaveAsync(AppConfig config, CancellationToken cancellationToken)
    {
        paths.EnsureCreated();
        var tempPath = $"{paths.ConfigPath}.tmp";

        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, config, options, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, paths.ConfigPath, overwrite: true);
    }

    private void BackupInvalidConfig()
    {
        if (!File.Exists(paths.ConfigPath))
        {
            return;
        }

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff");
        var backupPath = Path.Combine(
            paths.RootDirectory,
            $"config.invalid.{timestamp}.json");

        File.Move(paths.ConfigPath, backupPath, overwrite: false);
    }
}
