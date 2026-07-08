using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using WinAiUsageBar.Infrastructure.Security;

namespace WinAiUsageBar.Infrastructure.Process;

public interface ICliEnvironmentService
{
    Task<CliEnvironmentReport> GetReportAsync(
        IReadOnlyList<CliCommandCheck> commands,
        CancellationToken cancellationToken);
}

public sealed record CliCommandCheck(
    string CommandName,
    string StartupArgument,
    string? CommandOverride = null);

public sealed record CliEnvironmentReport(
    IReadOnlyList<CliCommandStatus> Commands);

public sealed record CliCommandStatus(
    string CommandName,
    bool IsFound,
    IReadOnlyList<string> Paths,
    bool? CanStart,
    int? ExitCode,
    bool TimedOut,
    string StatusMessage,
    string? LaunchTarget = null,
    bool UsesCommandProcessor = false,
    bool UsesConfiguredOverride = false);

public sealed record CliCommandStartupResult(
    bool CanStart,
    int? ExitCode,
    string StatusMessage);

public sealed class CliEnvironmentService(
    Func<string, CancellationToken, Task<IReadOnlyList<string>>>? pathResolver = null,
    Func<CliCommandCheck, CancellationToken, Task<CliCommandStartupResult>>? startupRunner = null,
    TimeSpan? startupTimeout = null) : ICliEnvironmentService
{
    private readonly Func<string, CancellationToken, Task<IReadOnlyList<string>>> pathResolver =
        pathResolver ?? ResolvePathsAsync;
    private readonly Func<CliCommandCheck, CancellationToken, Task<CliCommandStartupResult>>? startupRunnerOverride =
        startupRunner;
    private readonly TimeSpan startupTimeout = startupTimeout ?? TimeSpan.FromSeconds(3);

    public async Task<CliEnvironmentReport> GetReportAsync(
        IReadOnlyList<CliCommandCheck> commands,
        CancellationToken cancellationToken)
    {
        var statuses = new List<CliCommandStatus>();
        foreach (var command in commands)
        {
            statuses.Add(await GetStatusAsync(command, cancellationToken).ConfigureAwait(false));
        }

        return new CliEnvironmentReport(statuses);
    }

    private async Task<CliCommandStatus> GetStatusAsync(
        CliCommandCheck command,
        CancellationToken cancellationToken)
    {
        var configuredOverride = string.IsNullOrWhiteSpace(command.CommandOverride)
            ? null
            : command.CommandOverride.Trim();
        var usesConfiguredOverride = configuredOverride is not null;
        IReadOnlyList<string> paths = usesConfiguredOverride
            ? [configuredOverride!]
            : await pathResolver(command.CommandName, cancellationToken).ConfigureAwait(false);
        if (paths.Count == 0)
        {
            return new CliCommandStatus(
                command.CommandName,
                IsFound: false,
                Paths: [],
                CanStart: null,
                ExitCode: null,
                TimedOut: false,
                StatusMessage: "Not found on PATH.");
        }

        var launchPlan = CliCommandLaunchPlanner.Create(command.CommandName, paths);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(startupTimeout);

        try
        {
            var result = startupRunnerOverride is null
                ? await RunStartupCheckAsync(command, launchPlan, timeout.Token).ConfigureAwait(false)
                : await startupRunnerOverride(command, timeout.Token).ConfigureAwait(false);
            return new CliCommandStatus(
                command.CommandName,
                IsFound: true,
                paths.Select(DiagnosticRedactor.Redact).ToList(),
                result.CanStart,
                result.ExitCode,
                TimedOut: false,
                FirstLine(DiagnosticRedactor.Redact(result.StatusMessage)),
                DiagnosticRedactor.Redact(launchPlan.TargetPath),
                launchPlan.UsesCommandProcessor,
                usesConfiguredOverride);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new CliCommandStatus(
                command.CommandName,
                IsFound: true,
                paths.Select(DiagnosticRedactor.Redact).ToList(),
                CanStart: false,
                ExitCode: null,
                TimedOut: true,
                StatusMessage: "Startup check timed out.",
                DiagnosticRedactor.Redact(launchPlan.TargetPath),
                launchPlan.UsesCommandProcessor,
                usesConfiguredOverride);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return new CliCommandStatus(
                command.CommandName,
                IsFound: true,
                paths.Select(DiagnosticRedactor.Redact).ToList(),
                CanStart: false,
                ExitCode: null,
                TimedOut: false,
                DiagnosticRedactor.Redact(ex.Message),
                DiagnosticRedactor.Redact(launchPlan.TargetPath),
                launchPlan.UsesCommandProcessor,
                usesConfiguredOverride);
        }
    }

    private static async Task<IReadOnlyList<string>> ResolvePathsAsync(
        string commandName,
        CancellationToken cancellationToken)
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "where.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.StartInfo.ArgumentList.Add(commandName);

        try
        {
            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var output = await outputTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                return [];
            }

            return output
                .Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static async Task<CliCommandStartupResult> RunStartupCheckAsync(
        CliCommandCheck command,
        CliCommandLaunchPlan launchPlan,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> arguments = string.IsNullOrWhiteSpace(command.StartupArgument)
            ? Array.Empty<string>()
            : [command.StartupArgument];
        var startInfo = launchPlan.CreateStartInfo(arguments);
        using var process = new System.Diagnostics.Process { StartInfo = startInfo };

        try
        {
            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var output = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);
            var text = string.IsNullOrWhiteSpace(output) ? error : output;
            var status = string.IsNullOrWhiteSpace(text)
                ? $"Exited with code {process.ExitCode}."
                : FirstLine(text);

            return new CliCommandStartupResult(
                process.ExitCode == 0,
                process.ExitCode,
                status);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
    }

    private static string FirstLine(string text)
    {
        return text
            .Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? "No output.";
    }

    private static void TryKill(System.Diagnostics.Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Startup probing is best-effort.
        }
    }
}
