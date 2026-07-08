using WinAiUsageBar.Core.Models;

namespace WinAiUsageBar.App.Services;

public static class ProviderRepairGuidanceService
{
    public static IReadOnlyList<string> BuildRepairLines(UsageSnapshot snapshot)
    {
        var lines = new List<string>();
        switch (snapshot.Health)
        {
            case ProviderHealth.Ok:
                return [];
            case ProviderHealth.Warning:
                lines.Add("Review the status message, then refresh again before changing provider settings.");
                break;
            case ProviderHealth.AuthRequired:
                lines.Add("Reconnect credentials from Privacy & Data, then confirm the provider stores only a secret-name reference.");
                break;
            case ProviderHealth.Unsupported:
                lines.Add("Switch to Manual mode if this source is not available on this machine yet.");
                break;
            case ProviderHealth.Error:
                lines.Add("Refresh again. If the error repeats, export diagnostics before changing provider settings.");
                break;
            case ProviderHealth.Unknown:
                lines.Add("Refresh now to load the first snapshot, then review provider settings if the state stays unknown.");
                break;
        }

        lines.Add(SourceRepairLine(snapshot.SourceKind));
        if (IsCodexWindowsStartupBlocked(snapshot))
        {
            lines.Add("For Codex WindowsApps or App Execution Alias startup failures, install a launchable Codex CLI outside WindowsApps, set a provider CLI command override to that path, or repair package permissions, then rerun the health report.");
        }

        if (snapshot.ProviderId == ProviderId.GitHubCopilot
            && snapshot.SourceKind == DataSourceKind.OfficialApi
            && snapshot.Health == ProviderHealth.AuthRequired)
        {
            lines.Add("For GitHub Copilot metrics, confirm organization or enterprise mode and a PAT secret reference in provider settings.");
        }

        return lines;
    }

    private static string SourceRepairLine(DataSourceKind sourceKind)
    {
        return sourceKind switch
        {
            DataSourceKind.Cli => "For CLI sources, confirm the command is installed, starts from PATH, and appears in the health report.",
            DataSourceKind.LocalAppServer => "For local app-server sources, confirm the local provider command can start and the account is signed in.",
            DataSourceKind.OfficialApi => "For API sources, confirm required scope settings and secret references without pasting secret values into config.",
            DataSourceKind.Manual => "For Manual mode, update the manual values in Providers and refresh.",
            DataSourceKind.Mock => "For Mock mode, switch to Manual or a real source before relying on the data.",
            DataSourceKind.LocalFile => "For local-file sources, confirm the configured file path exists and does not contain secrets.",
            _ => "Review provider settings and refresh."
        };
    }

    private static bool IsCodexWindowsStartupBlocked(UsageSnapshot snapshot)
    {
        if (snapshot.ProviderId is not ProviderId.Codex and not ProviderId.ChatGPT
            || snapshot.SourceKind is not DataSourceKind.LocalAppServer
            || snapshot.Health is not (ProviderHealth.Unsupported or ProviderHealth.Error))
        {
            return false;
        }

        var text = string.Join(
            ' ',
            snapshot.StatusMessage ?? string.Empty,
            snapshot.ErrorMessage ?? string.Empty);
        return text.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase)
            || text.Contains("App Execution Alias", StringComparison.OrdinalIgnoreCase)
            || text.Contains("app execution alias", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Access is denied", StringComparison.OrdinalIgnoreCase)
            || text.Contains("could not start", StringComparison.OrdinalIgnoreCase);
    }
}
