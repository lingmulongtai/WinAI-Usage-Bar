using System.Text.Json;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Providers;

namespace WinAiUsageBar.Infrastructure.Storage;

public interface IConfigBackupValidationService
{
    Task<ConfigBackupValidationResult> ValidateAsync(string path, CancellationToken cancellationToken);
}

public sealed record ConfigBackupValidationResult(
    string Path,
    bool IsValid,
    int? ConfigVersion,
    int? ProviderCount,
    int? EnabledProviderCount,
    int? DefaultedProviderCount,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);

public sealed class ConfigBackupValidationService : IConfigBackupValidationService
{
    private readonly JsonSerializerOptions options = JsonInfrastructureOptions.CreateIndented();

    public async Task<ConfigBackupValidationResult> ValidateAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Failure(path, "Backup path is required.");
        }

        var fullPath = Path.GetFullPath(path.Trim());
        if (!File.Exists(fullPath))
        {
            return Failure(fullPath, "Backup file was not found.");
        }

        AppConfig? parsed;
        try
        {
            await using var stream = File.OpenRead(fullPath);
            parsed = await JsonSerializer.DeserializeAsync<AppConfig>(stream, options, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            return Failure(fullPath, "Backup file could not be parsed as WinAI Usage Bar config JSON.");
        }

        var originalProviderCount = parsed?.Providers?.Count ?? 0;
        var migrated = AppConfigMigrations.Migrate(parsed);
        var warnings = new List<string>();
        var defaultedProviderCount = Math.Max(0, migrated.Providers.Count - originalProviderCount);

        if (defaultedProviderCount > 0)
        {
            warnings.Add($"{defaultedProviderCount} missing provider config(s) will be defaulted by migration.");
        }

        var unsupportedSourceCount = migrated.Providers.Count(provider =>
        {
            var descriptor = ProviderDescriptors.Get(provider.ProviderId);
            return !descriptor.SupportedSources.Contains(provider.SourceKind);
        });

        if (unsupportedSourceCount > 0)
        {
            warnings.Add($"{unsupportedSourceCount} provider source setting(s) are unsupported after migration.");
        }

        return new ConfigBackupValidationResult(
            fullPath,
            IsValid: true,
            migrated.Version,
            migrated.Providers.Count,
            migrated.Providers.Count(provider => provider.IsEnabled),
            defaultedProviderCount,
            Errors: [],
            Warnings: warnings);
    }

    private static ConfigBackupValidationResult Failure(string path, string error)
    {
        return new ConfigBackupValidationResult(
            string.IsNullOrWhiteSpace(path) ? string.Empty : path,
            IsValid: false,
            ConfigVersion: null,
            ProviderCount: null,
            EnabledProviderCount: null,
            DefaultedProviderCount: null,
            Errors: [error],
            Warnings: []);
    }
}
