namespace WinAiUsageBar.Core.Abstractions;

public interface ICommandProbe
{
    Task<bool> ExistsAsync(string commandName, CancellationToken cancellationToken);
}

public interface ICodexAppServerClient
{
    Task<CodexAppServerData> FetchAccountUsageAsync(CancellationToken cancellationToken);
}

public sealed record CodexAppServerData(
    string? AccountJson,
    string? RateLimitsJson,
    string? UsageJson,
    IReadOnlyList<string> Diagnostics);
