using System.Security.Cryptography;
using System.Text;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Infrastructure.Security;

public sealed class DpapiSecretStore(AppDataPaths paths) : ISecretStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("WinAiUsageBar:v1");

    public async Task SetSecretAsync(string name, string value, CancellationToken cancellationToken)
    {
        paths.EnsureCreated();
        var bytes = Encoding.UTF8.GetBytes(value);
        var protectedBytes = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(GetPath(name), protectedBytes, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken)
    {
        paths.EnsureCreated();
        var path = GetPath(name);
        if (!File.Exists(path))
        {
            return null;
        }

        var protectedBytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        var bytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }

    public Task<bool> HasSecretAsync(string name, CancellationToken cancellationToken)
    {
        paths.EnsureCreated();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(GetPath(name)));
    }

    public Task DeleteSecretAsync(string name, CancellationToken cancellationToken)
    {
        paths.EnsureCreated();
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetPath(name);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string GetPath(string name)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(name));
        var fileName = Convert.ToHexString(hash).ToLowerInvariant();
        return Path.Combine(paths.SecretsDirectory, $"{fileName}.secret");
    }
}
