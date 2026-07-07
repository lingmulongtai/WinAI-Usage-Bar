using System.Text.Json;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Providers;

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
            var defaultConfig = AppConfig.CreateDefault();
            await SaveAsync(defaultConfig, cancellationToken).ConfigureAwait(false);
            return defaultConfig;
        }

        await using var stream = File.OpenRead(paths.ConfigPath);
        var config = await JsonSerializer.DeserializeAsync<AppConfig>(stream, options, cancellationToken).ConfigureAwait(false)
            ?? AppConfig.CreateDefault();

        foreach (var descriptor in ProviderDescriptors.All)
        {
            config.GetOrCreateProvider(descriptor);
        }

        return config;
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
}
