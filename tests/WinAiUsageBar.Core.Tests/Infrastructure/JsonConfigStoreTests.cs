using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class JsonConfigStoreTests
{
    [Fact]
    public async Task JsonAppConfigStore_LoadSave_RoundTripsProviderSettings()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var store = new JsonAppConfigStore(paths);

        var config = await store.LoadAsync(CancellationToken.None);
        var codex = config.Providers.Single(provider => provider.ProviderId == ProviderId.Codex);
        var gemini = config.Providers.Single(provider => provider.ProviderId == ProviderId.Gemini);
        codex.IsEnabled = true;
        codex.SourceKind = DataSourceKind.Manual;
        codex.Manual.RemainingPercent = 52;
        gemini.SourceKind = DataSourceKind.OfficialApi;
        gemini.ApiKey.SecretName = "gemini-api-key";
        config.Startup.LaunchOnLogin = true;

        await store.SaveAsync(config, CancellationToken.None);
        var reloaded = await store.LoadAsync(CancellationToken.None);
        var reloadedCodex = reloaded.Providers.Single(provider => provider.ProviderId == ProviderId.Codex);
        var reloadedGemini = reloaded.Providers.Single(provider => provider.ProviderId == ProviderId.Gemini);

        Assert.True(reloadedCodex.IsEnabled);
        Assert.Equal(DataSourceKind.Manual, reloadedCodex.SourceKind);
        Assert.Equal(52, reloadedCodex.Manual.RemainingPercent);
        Assert.Equal(DataSourceKind.OfficialApi, reloadedGemini.SourceKind);
        Assert.Equal("gemini-api-key", reloadedGemini.ApiKey.SecretName);
        Assert.True(reloaded.Startup.LaunchOnLogin);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task JsonAppConfigStore_LoadAsync_DoesNotRewriteAlreadyNormalizedConfig()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        var store = new JsonAppConfigStore(paths);
        var config = AppConfig.CreateDefault();
        config.Startup.LaunchOnLogin = true;

        try
        {
            await store.SaveAsync(config, CancellationToken.None);
            var expectedLastWriteTime = new DateTime(2026, 7, 8, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(paths.ConfigPath, expectedLastWriteTime);

            var loaded = await store.LoadAsync(CancellationToken.None);

            Assert.True(loaded.Startup.LaunchOnLogin);
            Assert.Equal(expectedLastWriteTime, File.GetLastWriteTimeUtc(paths.ConfigPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task JsonAppConfigStore_LoadAsync_MigratesMissingSectionsAndPreservesUserValues()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(paths.ConfigPath, """
        {
          "version": 0,
          "providers": [
            {
              "providerId": "Codex",
              "isEnabled": true,
              "sourceKind": "Manual",
              "cli": null,
              "manual": {
                "remainingPercent": 42,
                "notes": "keep me"
              }
            }
          ],
          "widget": {
            "left": 123,
            "top": 456,
            "width": 200,
            "height": 100,
            "providerIds": ["Gemini", "Gemini"]
          }
        }
        """);

        try
        {
            var store = new JsonAppConfigStore(paths);
            var config = await store.LoadAsync(CancellationToken.None);
            var codex = config.Providers.Single(provider => provider.ProviderId == ProviderId.Codex);

            Assert.Equal(AppConfigMigrations.CurrentVersion, config.Version);
            Assert.Equal(ProviderDescriptors.All.Count, config.Providers.Count);
            Assert.True(codex.IsEnabled);
            Assert.Equal(DataSourceKind.Manual, codex.SourceKind);
            Assert.Equal(42, codex.Manual.RemainingPercent);
            Assert.Equal("keep me", codex.Manual.Notes);
            Assert.NotNull(codex.Cli);
            Assert.NotNull(config.Refresh);
            Assert.NotNull(config.Appearance);
            Assert.NotNull(config.Notifications);
            Assert.NotNull(config.Startup);
            Assert.False(config.Startup.LaunchOnLogin);
            Assert.NotNull(config.HistoryRetention);
            Assert.Equal(30, config.HistoryRetention.MaxDays);
            Assert.NotNull(config.Onboarding);
            Assert.False(config.Onboarding.HasCompletedFirstRun);
            Assert.Null(config.Onboarding.CompletedAt);
            Assert.NotNull(config.Updates);
            Assert.True(config.Updates.CheckOnStartup);
            Assert.Equal(24, config.Updates.MinimumCheckIntervalHours);
            Assert.False(config.Updates.DownloadAutomatically);
            Assert.False(config.Updates.InstallAutomatically);
            Assert.Null(config.Updates.LastStatus);
            Assert.Equal(123, config.Widget.Left);
            Assert.Equal(456, config.Widget.Top);
            Assert.Equal(280, config.Widget.Width);
            Assert.Equal(160, config.Widget.Height);
            Assert.Equal([ProviderId.Gemini], config.Widget.ProviderIds);

            var persisted = await File.ReadAllTextAsync(paths.ConfigPath);
            Assert.Contains("\"historyRetention\"", persisted, StringComparison.Ordinal);
            Assert.Contains("\"onboarding\"", persisted, StringComparison.Ordinal);
            Assert.Contains("\"updates\"", persisted, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task JsonAppConfigStore_LoadAsync_RemovesInvalidProvidersAndNormalizesUnsupportedSources()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(paths.ConfigPath, """
        {
          "version": 1,
          "providers": [
            {
              "providerId": 999,
              "isEnabled": true,
              "sourceKind": "Manual"
            },
            {
              "providerId": "Codex",
              "isEnabled": true,
              "sourceKind": 999
            }
          ],
          "widget": {
            "providerIds": [999, "Codex", "Codex"]
          }
        }
        """);

        try
        {
            var store = new JsonAppConfigStore(paths);
            var config = await store.LoadAsync(CancellationToken.None);
            var codex = config.Providers.Single(provider => provider.ProviderId == ProviderId.Codex);

            Assert.Equal(ProviderDescriptors.All.Count, config.Providers.Count);
            Assert.DoesNotContain(config.Providers, provider => !Enum.IsDefined(provider.ProviderId));
            Assert.Equal(DataSourceKind.Manual, codex.SourceKind);
            Assert.Equal([ProviderId.Codex], config.Widget.ProviderIds);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task JsonAppConfigStore_LoadAsync_BacksUpCorruptConfigAndCreatesDefault()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(paths.ConfigPath, "{ this is not json");

        try
        {
            var store = new JsonAppConfigStore(paths);
            var config = await store.LoadAsync(CancellationToken.None);
            var backups = Directory.GetFiles(root, "config.invalid.*.json");

            Assert.Equal(ProviderDescriptors.All.Count, config.Providers.Count);
            Assert.True(File.Exists(paths.ConfigPath));
            Assert.Single(backups);
            Assert.Contains("this is not json", await File.ReadAllTextAsync(backups.Single()));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task JsonAppConfigStore_SaveAsync_IgnoresLockedLegacyTempFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        Directory.CreateDirectory(root);
        var legacyTempPath = $"{paths.ConfigPath}.tmp";
        await File.WriteAllTextAsync(legacyTempPath, "locked legacy temp");

        var lockStream = new FileStream(
            legacyTempPath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);

        try
        {
            var store = new JsonAppConfigStore(paths);
            var config = AppConfigMigrations.Migrate(null);
            config.Startup.LaunchOnLogin = true;

            await store.SaveAsync(config, CancellationToken.None);
            var reloaded = await store.LoadAsync(CancellationToken.None);

            Assert.True(File.Exists(paths.ConfigPath));
            Assert.True(reloaded.Startup.LaunchOnLogin);
            Assert.True(File.Exists(legacyTempPath));
        }
        finally
        {
            await lockStream.DisposeAsync();
            Directory.Delete(root, recursive: true);
        }
    }
}
