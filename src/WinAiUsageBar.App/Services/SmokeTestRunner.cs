using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Providers;
using WinAiUsageBar.Infrastructure.Security;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.App.Services;

public static class SmokeTestRunner
{
    public static async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "WinAiUsageBarSmokeTest",
            Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);

        try
        {
            var configStore = new JsonAppConfigStore(paths);
            var config = await configStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (config.Providers.Count != ProviderDescriptors.All.Count)
            {
                throw new InvalidOperationException("Default config did not include every provider.");
            }

            config.Refresh.Interval = RefreshIntervalKind.Manual;
            await configStore.SaveAsync(config, cancellationToken).ConfigureAwait(false);
            var reloaded = await configStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (reloaded.Refresh.Interval != RefreshIntervalKind.Manual)
            {
                throw new InvalidOperationException("Config round-trip failed.");
            }

            var secretStore = new DpapiSecretStore(paths);
            await secretStore.SetSecretAsync("smoke-test-secret", "secret-value", cancellationToken).ConfigureAwait(false);
            if (!await secretStore.HasSecretAsync("smoke-test-secret", cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("Secret existence check failed.");
            }

            var secret = await secretStore.GetSecretAsync("smoke-test-secret", cancellationToken).ConfigureAwait(false);
            if (secret != "secret-value")
            {
                throw new InvalidOperationException("Secret round-trip failed.");
            }

            await secretStore.DeleteSecretAsync("smoke-test-secret", cancellationToken).ConfigureAwait(false);

            var registry = new ProviderRegistry();
            if (registry.GetDescriptors().Count != ProviderDescriptors.All.Count)
            {
                throw new InvalidOperationException("Provider registry descriptor count mismatch.");
            }

            return 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine($"Smoke test failed: {ex.Message}");
            return 1;
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
