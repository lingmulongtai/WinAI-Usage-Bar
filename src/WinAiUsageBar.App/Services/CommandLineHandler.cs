namespace WinAiUsageBar.App.Services;

public sealed record CommandLineHandleResult(
    bool Handled,
    int ExitCode);

public static class CommandLineHandler
{
    public static async Task<CommandLineHandleResult> TryHandleAsync(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        Func<CancellationToken, Task<int>> smokeTest,
        Func<CancellationToken, Task<string>> diagnosticsExport,
        Func<CancellationToken, Task<string>> healthReport,
        Func<string> providerCatalog,
        Func<string, CancellationToken, Task<CommandLineActionResult>> validateConfigBackup,
        Func<string, CancellationToken, Task<CommandLineActionResult>> restoreConfigBackup,
        Func<AppInfo> appInfoProvider,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task<string>>? refreshOnce = null)
    {
        if (args.Count == 0)
        {
            return new CommandLineHandleResult(Handled: false, ExitCode: 0);
        }

        if (args.Count == 2
            && string.Equals(args[0].Trim(), "--validate-config-backup", StringComparison.OrdinalIgnoreCase))
        {
            var result = await validateConfigBackup(args[1], cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync(result.Output.AsMemory(), cancellationToken).ConfigureAwait(false);
            return new CommandLineHandleResult(Handled: true, result.ExitCode);
        }

        if (args.Count == 3
            && string.Equals(args[0].Trim(), "--restore-config-backup", StringComparison.OrdinalIgnoreCase)
            && string.Equals(args[2].Trim(), "--confirm", StringComparison.OrdinalIgnoreCase))
        {
            var result = await restoreConfigBackup(args[1], cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync(result.Output.AsMemory(), cancellationToken).ConfigureAwait(false);
            return new CommandLineHandleResult(Handled: true, result.ExitCode);
        }

        if (args.Count >= 1
            && string.Equals(args[0].Trim(), "--restore-config-backup", StringComparison.OrdinalIgnoreCase))
        {
            await error.WriteLineAsync(
                "Restoring a config backup requires: --restore-config-backup <path> --confirm".AsMemory(),
                cancellationToken).ConfigureAwait(false);
            await error.WriteLineAsync(CreateHelpText().AsMemory(), cancellationToken).ConfigureAwait(false);
            return new CommandLineHandleResult(Handled: true, ExitCode: 2);
        }

        if (args.Count != 1)
        {
            await WriteUnknownArgumentsAsync(args, error, cancellationToken).ConfigureAwait(false);
            return new CommandLineHandleResult(Handled: true, ExitCode: 2);
        }

        var command = args[0].Trim();
        if (IsHelp(command))
        {
            await output.WriteLineAsync(CreateHelpText().AsMemory(), cancellationToken).ConfigureAwait(false);
            return new CommandLineHandleResult(Handled: true, ExitCode: 0);
        }

        if (string.Equals(command, "--version", StringComparison.OrdinalIgnoreCase))
        {
            var info = appInfoProvider();
            await output.WriteLineAsync(
                $"{info.ProductName} {info.InformationalVersion}".AsMemory(),
                cancellationToken).ConfigureAwait(false);
            return new CommandLineHandleResult(Handled: true, ExitCode: 0);
        }

        if (string.Equals(command, "--smoke-test", StringComparison.OrdinalIgnoreCase))
        {
            var exitCode = await smokeTest(cancellationToken).ConfigureAwait(false);
            return new CommandLineHandleResult(Handled: true, exitCode);
        }

        if (string.Equals(command, "--export-diagnostics", StringComparison.OrdinalIgnoreCase))
        {
            var path = await diagnosticsExport(cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync(
                $"Diagnostics exported to {path}".AsMemory(),
                cancellationToken).ConfigureAwait(false);
            return new CommandLineHandleResult(Handled: true, ExitCode: 0);
        }

        if (string.Equals(command, "--health-report", StringComparison.OrdinalIgnoreCase))
        {
            var report = await healthReport(cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync(report.AsMemory(), cancellationToken).ConfigureAwait(false);
            return new CommandLineHandleResult(Handled: true, ExitCode: 0);
        }

        if (string.Equals(command, "--refresh-once", StringComparison.OrdinalIgnoreCase))
        {
            if (refreshOnce is null)
            {
                await error.WriteLineAsync(
                    "--refresh-once is unavailable in this host.".AsMemory(),
                    cancellationToken).ConfigureAwait(false);
                return new CommandLineHandleResult(Handled: true, ExitCode: 2);
            }

            var report = await refreshOnce(cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync(report.AsMemory(), cancellationToken).ConfigureAwait(false);
            return new CommandLineHandleResult(Handled: true, ExitCode: 0);
        }

        if (string.Equals(command, "--provider-catalog", StringComparison.OrdinalIgnoreCase))
        {
            await output.WriteLineAsync(providerCatalog().AsMemory(), cancellationToken).ConfigureAwait(false);
            return new CommandLineHandleResult(Handled: true, ExitCode: 0);
        }

        if (string.Equals(command, "--validate-config-backup", StringComparison.OrdinalIgnoreCase))
        {
            await error.WriteLineAsync(
                "Missing path for --validate-config-backup.".AsMemory(),
                cancellationToken).ConfigureAwait(false);
            await error.WriteLineAsync(CreateHelpText().AsMemory(), cancellationToken).ConfigureAwait(false);
            return new CommandLineHandleResult(Handled: true, ExitCode: 2);
        }

        await WriteUnknownArgumentsAsync(args, error, cancellationToken).ConfigureAwait(false);
        return new CommandLineHandleResult(Handled: true, ExitCode: 2);
    }

    public static string CreateHelpText()
    {
        return """
        WinAI Usage Bar

        Usage:
          WinAiUsageBar.App.exe [--help|--version|--smoke-test|--export-diagnostics|--health-report|--refresh-once|--provider-catalog]
          WinAiUsageBar.App.exe --validate-config-backup <path>
          WinAiUsageBar.App.exe --restore-config-backup <path> --confirm

        Options:
          --help                Show this help text without launching the app.
          --version             Print the app version without launching the app.
          --smoke-test          Run packaged-app startup checks without launching UI.
          --export-diagnostics  Write a redacted diagnostics export without launching UI.
          --health-report       Print local config, cache, and history health without launching UI.
          --refresh-once        Refresh enabled providers once and print a safe snapshot report without launching UI.
          --provider-catalog    Print supported provider descriptors without launching UI.
          --validate-config-backup <path>
                                Validate a config backup without applying it.
          --restore-config-backup <path> --confirm
                                Restore a config backup after creating a rollback backup.
        """;
    }

    private static bool IsHelp(string command)
    {
        return string.Equals(command, "--help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, "-h", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, "/?", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WriteUnknownArgumentsAsync(
        IReadOnlyList<string> args,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        await error.WriteLineAsync(
            $"Unknown command-line argument(s): {string.Join(" ", args)}".AsMemory(),
            cancellationToken).ConfigureAwait(false);
        await error.WriteLineAsync(CreateHelpText().AsMemory(), cancellationToken).ConfigureAwait(false);
    }
}
