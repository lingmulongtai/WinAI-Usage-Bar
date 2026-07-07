using WinAiUsageBar.Core.Abstractions;

namespace WinAiUsageBar.Infrastructure.Security;

public sealed class SecretStoreResolver(ISecretStore secretStore) : ISecretResolver
{
    public Task<string?> ResolveSecretAsync(string name, CancellationToken cancellationToken)
    {
        return secretStore.GetSecretAsync(name, cancellationToken);
    }
}
