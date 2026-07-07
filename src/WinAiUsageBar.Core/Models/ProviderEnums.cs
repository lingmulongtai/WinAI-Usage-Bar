namespace WinAiUsageBar.Core.Models;

public enum ProviderId
{
    ChatGPT,
    Codex,
    Gemini,
    Claude,
    ClaudeCode,
    OpenCodeZen,
    GitHubCopilot
}

public enum ProviderHealth
{
    Unknown,
    Ok,
    Warning,
    Error,
    AuthRequired,
    Unsupported
}

public enum DataSourceKind
{
    Manual,
    Cli,
    LocalFile,
    LocalAppServer,
    OfficialApi,
    Mock
}
