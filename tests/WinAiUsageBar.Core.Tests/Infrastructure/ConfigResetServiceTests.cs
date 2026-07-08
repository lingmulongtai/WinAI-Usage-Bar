using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class ConfigResetServiceTests
{
    [Fact]
    public async Task ResetToDefaultsAsync_BacksUpCurrentConfigAndKeepsSecrets()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var configStore = new JsonAppConfigStore(paths);
        var current = AppConfig.CreateDefault();
        current.Appearance.Theme = "Dark";
        current.Startup.LaunchOnLogin = true;
        current.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.Gemini)).IsEnabled = true;
        var now = new DateTimeOffset(2026, 7, 8, 21, 0, 0, TimeSpan.Zero);
        var service = new ConfigResetService(paths, configStore, () => now);

        try
        {
            await configStore.SaveAsync(current, CancellationToken.None);
            await File.WriteAllTextAsync(Path.Combine(paths.SecretsDirectory, "gemini-secret"), "secret-value");

            var result = await service.ResetToDefaultsAsync(CancellationToken.None);
            var reset = await configStore.LoadAsync(CancellationToken.None);
            var rollbackContent = await File.ReadAllTextAsync(result.RollbackBackupPath);

            Assert.True(result.Reset);
            Assert.Equal("config-backup-before-reset-20260708-210000.json", Path.GetFileName(result.RollbackBackupPath));
            Assert.Equal("System", reset.Appearance.Theme);
            Assert.False(reset.Startup.LaunchOnLogin);
            Assert.False(reset.GetOrCreateProvider(ProviderDescriptors.Get(ProviderId.Gemini)).IsEnabled);
            Assert.Contains("\"theme\": \"Dark\"", rollbackContent, StringComparison.Ordinal);
            Assert.Contains("\"launchOnLogin\": true", rollbackContent, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(paths.SecretsDirectory, "gemini-secret")));
            Assert.Equal("secret-value", await File.ReadAllTextAsync(Path.Combine(paths.SecretsDirectory, "gemini-secret")));
            Assert.Contains(result.Warnings, warning => warning.Contains("Saved secrets were not deleted", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
