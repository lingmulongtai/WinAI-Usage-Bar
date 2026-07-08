using System.Diagnostics;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Infrastructure.Updates;

public interface IUpdateInstallLaunchService
{
    Task<UpdateInstallLaunchResult> LaunchAsync(
        UpdateInstallLaunchRequest request,
        CancellationToken cancellationToken);
}

public sealed record UpdateInstallLaunchRequest(
    string ScriptPath);

public sealed record UpdateInstallLaunchResult(
    UpdateInstallLaunchStatus Status,
    string Message,
    string? ScriptPath,
    string? Command,
    int? ProcessId);

public enum UpdateInstallLaunchStatus
{
    Launched,
    InvalidScript,
    Error
}

public sealed class UpdateInstallLaunchService(
    AppDataPaths paths,
    Func<ProcessStartInfo, int?>? launchProcess = null) : IUpdateInstallLaunchService
{
    private readonly Func<ProcessStartInfo, int?> launchProcess =
        launchProcess ?? StartProcess;

    public Task<UpdateInstallLaunchResult> LaunchAsync(
        UpdateInstallLaunchRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var validation = ValidateScriptPath(request.ScriptPath);
            if (!validation.IsValid || validation.ScriptPath is null)
            {
                return Task.FromResult(Failure(
                    UpdateInstallLaunchStatus.InvalidScript,
                    validation.ErrorMessage));
            }

            var startInfo = CreateStartInfo(validation.ScriptPath);
            var processId = launchProcess(startInfo);
            if (processId is null)
            {
                return Task.FromResult(Failure(
                    UpdateInstallLaunchStatus.Error,
                    "PowerShell did not return a launched process."));
            }

            var command = FormatCommand(validation.ScriptPath);
            return Task.FromResult(new UpdateInstallLaunchResult(
                UpdateInstallLaunchStatus.Launched,
                "Prepared update install script launched. The script may wait for WinAI Usage Bar to exit.",
                validation.ScriptPath,
                command,
                processId));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Task.FromResult(Failure(
                UpdateInstallLaunchStatus.Error,
                $"Prepared update launch failed: {ex.Message}"));
        }
    }

    private UpdateInstallLaunchValidation ValidateScriptPath(string scriptPath)
    {
        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            return UpdateInstallLaunchValidation.Invalid("Missing update script path.");
        }

        var fullScriptPath = Path.GetFullPath(scriptPath);
        if (!File.Exists(fullScriptPath))
        {
            return UpdateInstallLaunchValidation.Invalid("Update install script was not found.");
        }

        if (!string.Equals(Path.GetFileName(fullScriptPath), "apply-update.ps1", StringComparison.OrdinalIgnoreCase))
        {
            return UpdateInstallLaunchValidation.Invalid("Update install script must be named apply-update.ps1.");
        }

        var updatesRoot = EnsureTrailingSeparator(Path.GetFullPath(paths.UpdatesDirectory));
        if (!fullScriptPath.StartsWith(updatesRoot, StringComparison.OrdinalIgnoreCase))
        {
            return UpdateInstallLaunchValidation.Invalid("Update install script must live under the app-owned updates directory.");
        }

        return UpdateInstallLaunchValidation.Valid(fullScriptPath);
    }

    private static ProcessStartInfo CreateStartInfo(string scriptPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        return startInfo;
    }

    private static int? StartProcess(ProcessStartInfo startInfo)
    {
        using var process = System.Diagnostics.Process.Start(startInfo);
        return process?.Id;
    }

    private static string FormatCommand(string scriptPath)
    {
        return $"powershell -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"";
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : $"{path}{Path.DirectorySeparatorChar}";
    }

    private static UpdateInstallLaunchResult Failure(
        UpdateInstallLaunchStatus status,
        string message)
    {
        return new UpdateInstallLaunchResult(
            status,
            message,
            ScriptPath: null,
            Command: null,
            ProcessId: null);
    }

    private sealed record UpdateInstallLaunchValidation(
        bool IsValid,
        string? ScriptPath,
        string ErrorMessage)
    {
        public static UpdateInstallLaunchValidation Valid(string scriptPath)
        {
            return new UpdateInstallLaunchValidation(true, scriptPath, string.Empty);
        }

        public static UpdateInstallLaunchValidation Invalid(string errorMessage)
        {
            return new UpdateInstallLaunchValidation(false, null, errorMessage);
        }
    }
}
