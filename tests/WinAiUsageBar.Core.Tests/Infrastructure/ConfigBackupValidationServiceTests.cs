using System.Text.Json;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class ConfigBackupValidationServiceTests
{
    [Fact]
    public async Task ValidateAsync_MigratesAndSummarizesValidBackup()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var backupPath = Path.Combine(root, "config-backup.json");
        Directory.CreateDirectory(root);
        var config = new AppConfig
        {
            Version = 0,
            Providers =
            [
                new ProviderConfig
                {
                    ProviderId = ProviderId.Codex,
                    IsEnabled = true,
                    SourceKind = DataSourceKind.Mock
                }
            ]
        };
        await WriteConfigAsync(backupPath, config);
        var service = new ConfigBackupValidationService();

        try
        {
            var result = await service.ValidateAsync(backupPath, CancellationToken.None);

            Assert.True(result.IsValid);
            Assert.Equal(Path.GetFullPath(backupPath), result.Path);
            Assert.Equal(AppConfigMigrations.CurrentVersion, result.ConfigVersion);
            Assert.Equal(ProviderDescriptors.All.Count, result.ProviderCount);
            Assert.True(result.EnabledProviderCount >= 1);
            Assert.Equal(ProviderDescriptors.All.Count - 1, result.DefaultedProviderCount);
            Assert.Empty(result.Errors);
            Assert.Contains(result.Warnings, warning => warning.Contains("missing provider config", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ValidateAsync_ReturnsInvalidWhenFileIsMissing()
    {
        var service = new ConfigBackupValidationService();

        var result = await service.ValidateAsync(@"C:\does-not-exist\config-backup.json", CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains("not found", string.Join(" ", result.Errors), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsInvalidForMalformedJson()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var backupPath = Path.Combine(root, "config-backup.json");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(backupPath, "{ nope");
        var service = new ConfigBackupValidationService();

        try
        {
            var result = await service.ValidateAsync(backupPath, CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.Contains("could not be parsed", string.Join(" ", result.Errors), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task WriteConfigAsync(string path, AppConfig config)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(
            stream,
            config,
            JsonInfrastructureOptions.CreateIndented());
    }
}
