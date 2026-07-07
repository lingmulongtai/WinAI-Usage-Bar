namespace WinAiUsageBar.Infrastructure.Security;

public interface ISecretStore
{
    Task SetSecretAsync(string name, string value, CancellationToken cancellationToken);

    Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken);

    Task DeleteSecretAsync(string name, CancellationToken cancellationToken);
}
