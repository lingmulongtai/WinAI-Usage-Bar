namespace WinAiUsageBar.App.Services;

public sealed record CommandLineHandleResult(
    bool Handled,
    int ExitCode);

public sealed record CommandLineRefreshOnceOptions(
    string? ProviderId,
    string? SourceKind);

public sealed record CommandLineSetProviderCliOverrideOptions(
    string ProviderId,
    string CommandPathOverride);

public sealed record CommandLineClearProviderCliOverrideOptions(
    string ProviderId);

public sealed record CommandLinePruneSupportArtifactsOptions(
    int KeepNewest);

public sealed record CommandLineUpdateVersionOptions(
    string? CurrentVersionOverride);

public sealed record CommandLinePrepareUpdateInstallOptions(
    string PackagePath,
    string? InstallDirectory,
    bool RestartAfterInstall);

public sealed record CommandLineLaunchPreparedUpdateOptions(
    string ScriptPath);

public sealed record CommandLineInstallLatestUpdateOptions(
    bool RestartAfterInstall,
    string? CurrentVersionOverride = null);

public static class CommandLineHandler
{
    private const int DefaultPruneKeepNewest = 5;

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
        Func<CommandLineRefreshOnceOptions, CancellationToken, Task<CommandLineActionResult>>? refreshOnce = null,
        Func<CommandLineSetProviderCliOverrideOptions, CancellationToken, Task<CommandLineActionResult>>? setProviderCliOverride = null,
        Func<CommandLineClearProviderCliOverrideOptions, CancellationToken, Task<CommandLineActionResult>>? clearProviderCliOverride = null,
        Func<CommandLinePruneSupportArtifactsOptions, CancellationToken, Task<CommandLineActionResult>>? pruneSupportArtifacts = null,
        Func<CommandLineUpdateVersionOptions, CancellationToken, Task<CommandLineActionResult>>? checkForUpdates = null,
        Func<CommandLineUpdateVersionOptions, CancellationToken, Task<CommandLineActionResult>>? downloadUpdate = null,
        Func<CommandLinePrepareUpdateInstallOptions, CancellationToken, Task<CommandLineActionResult>>? prepareUpdateInstall = null,
        Func<CommandLineLaunchPreparedUpdateOptions, CancellationToken, Task<CommandLineActionResult>>? launchPreparedUpdate = null,
        Func<CommandLineInstallLatestUpdateOptions, CancellationToken, Task<CommandLineActionResult>>? installLatestUpdate = null,
        Func<CancellationToken, Task<CommandLineActionResult>>? exportConfigBackup = null,
        Func<CancellationToken, Task<CommandLineActionResult>>? runStartupUpdate = null)
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

        if (args.Count >= 1
            && string.Equals(args[0].Trim(), "--refresh-once", StringComparison.OrdinalIgnoreCase))
        {
            if (refreshOnce is null)
            {
                await error.WriteLineAsync(
                    "--refresh-once is unavailable in this host.".AsMemory(),
                    cancellationToken).ConfigureAwait(false);
                return new CommandLineHandleResult(Handled: true, ExitCode: 2);
            }

            var parseResult = ParseRefreshOnceOptions(args);
            if (!parseResult.IsValid || parseResult.Options is null)
            {
                await error.WriteLineAsync(parseResult.ErrorMessage.AsMemory(), cancellationToken).ConfigureAwait(false);
                await error.WriteLineAsync(CreateHelpText().AsMemory(), cancellationToken).ConfigureAwait(false);
                return new CommandLineHandleResult(Handled: true, ExitCode: 2);
            }

            var result = await refreshOnce(parseResult.Options, cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync(result.Output.AsMemory(), cancellationToken).ConfigureAwait(false);
            return new CommandLineHandleResult(Handled: true, result.ExitCode);
        }

        if (args.Count >= 1
            && string.Equals(args[0].Trim(), "--set-provider-cli-override", StringComparison.OrdinalIgnoreCase))
        {
            if (setProviderCliOverride is null)
            {
                await error.WriteLineAsync(
                    "--set-provider-cli-override is unavailable in this host.".AsMemory(),
                    cancellationToken).ConfigureAwait(false);
                return new CommandLineHandleResult(Handled: true, ExitCode: 2);
            }

            var parseResult = ParseSetProviderCliOverrideOptions(args);
            if (!parseResult.IsValid || parseResult.Options is null)
            {
                await error.WriteLineAsync(parseResult.ErrorMessage.AsMemory(), cancellationToken).ConfigureAwait(false);
                await error.WriteLineAsync(CreateHelpText().AsMemory(), cancellationToken).ConfigureAwait(false);
                return new CommandLineHandleResult(Handled: true, ExitCode: 2);
            }

            var result = await setProviderCliOverride(parseResult.Options, cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync(result.Output.AsMemory(), cancellationToken).ConfigureAwait(false);
            return new CommandLineHandleResult(Handled: true, result.ExitCode);
        }

        if (args.Count >= 1
            && string.Equals(args[0].Trim(), "--clear-provider-cli-override", StringComparison.OrdinalIgnoreCase))
        {
            if (clearProviderCliOverride is null)
            {
                await error.WriteLineAsync(
                    "--clear-provider-cli-override is unavailable in this host.".AsMemory(),
                    cancellationToken).ConfigureAwait(false);
                return new CommandLineHandleResult(Handled: true, ExitCode: 2);
            }

            var parseResult = ParseClearProviderCliOverrideOptions(args);
            if (!parseResult.IsValid || parseResult.Options is null)
            {
                await error.WriteLineAsync(parseResult.ErrorMessage.AsMemory(), cancellationToken).ConfigureAwait(false);
                await error.WriteLineAsync(CreateHelpText().AsMemory(), cancellationToken).ConfigureAwait(false);
                return new CommandLineHandleResult(Handled: true, ExitCode: 2);
            }

            var result = await clearProviderCliOverride(parseResult.Options, cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync(result.Output.AsMemory(), cancellationToken).ConfigureAwait(false);
            return new CommandLineHandleResult(Handled: true, result.ExitCode);
        }

        if (args.Count >= 1
            && string.Equals(args[0].Trim(), "--prune-support-artifacts", StringComparison.OrdinalIgnoreCase))
        {
            if (pruneSupportArtifacts is null)
            {
                await error.WriteLineAsync(
                    "--prune-support-artifacts is unavailable in this host.".AsMemory(),
                    cancellationToken).ConfigureAwait(false);
                return new CommandLineHandleResult(Handled: true, ExitCode: 2);
            }

            var parseResult = ParsePruneSupportArtifactsOptions(args);
            if (!parseResult.IsValid || parseResult.Options is null)
            {
                await error.WriteLineAsync(parseResult.ErrorMessage.AsMemory(), cancellationToken).ConfigureAwait(false);
                await error.WriteLineAsync(CreateHelpText().AsMemory(), cancellationToken).ConfigureAwait(false);
                return new CommandLineHandleResult(Handled: true, ExitCode: 2);
            }

            var result = await pruneSupportArtifacts(parseResult.Options, cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync(result.Output.AsMemory(), cancellationToken).ConfigureAwait(false);
            return new CommandLineHandleResult(Handled: true, result.ExitCode);
        }

        if (args.Count >= 1
            && string.Equals(args[0].Trim(), "--check-for-updates", StringComparison.OrdinalIgnoreCase))
        {
            if (checkForUpdates is null)
            {
                await error.WriteLineAsync(
                    "--check-for-updates is unavailable in this host.".AsMemory(),
                    cancellationToken).ConfigureAwait(false);
                return new CommandLineHandleResult(Handled: true, ExitCode: 2);
            }

            var parseResult = ParseUpdateVersionOptions(args, "--check-for-updates");
            if (!parseResult.IsValid || parseResult.Options is null)
            {
                await error.WriteLineAsync(parseResult.ErrorMessage.AsMemory(), cancellationToken).ConfigureAwait(false);
                await error.WriteLineAsync(CreateHelpText().AsMemory(), cancellationToken).ConfigureAwait(false);
                return new CommandLineHandleResult(Handled: true, ExitCode: 2);
            }

            var result = await checkForUpdates(parseResult.Options, cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync(result.Output.AsMemory(), cancellationToken).ConfigureAwait(false);
            return new CommandLineHandleResult(Handled: true, result.ExitCode);
        }

        if (args.Count >= 1
            && string.Equals(args[0].Trim(), "--download-update", StringComparison.OrdinalIgnoreCase))
        {
            if (downloadUpdate is null)
            {
                await error.WriteLineAsync(
                    "--download-update is unavailable in this host.".AsMemory(),
                    cancellationToken).ConfigureAwait(false);
                return new CommandLineHandleResult(Handled: true, ExitCode: 2);
            }

            var parseResult = ParseUpdateVersionOptions(args, "--download-update");
            if (!parseResult.IsValid || parseResult.Options is null)
            {
                await error.WriteLineAsync(parseResult.ErrorMessage.AsMemory(), cancellationToken).ConfigureAwait(false);
                await error.WriteLineAsync(CreateHelpText().AsMemory(), cancellationToken).ConfigureAwait(false);
                return new CommandLineHandleResult(Handled: true, ExitCode: 2);
            }

            var result = await downloadUpdate(parseResult.Options, cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync(result.Output.AsMemory(), cancellationToken).ConfigureAwait(false);
            return new CommandLineHandleResult(Handled: true, result.ExitCode);
        }

        if (args.Count >= 1
            && string.Equals(args[0].Trim(), "--prepare-update-install", StringComparison.OrdinalIgnoreCase))
        {
            if (prepareUpdateInstall is null)
            {
                await error.WriteLineAsync(
                    "--prepare-update-install is unavailable in this host.".AsMemory(),
                    cancellationToken).ConfigureAwait(false);
                return new CommandLineHandleResult(Handled: true, ExitCode: 2);
            }

            var parseResult = ParsePrepareUpdateInstallOptions(args);
            if (!parseResult.IsValid || parseResult.Options is null)
            {
                await error.WriteLineAsync(parseResult.ErrorMessage.AsMemory(), cancellationToken).ConfigureAwait(false);
                await error.WriteLineAsync(CreateHelpText().AsMemory(), cancellationToken).ConfigureAwait(false);
                return new CommandLineHandleResult(Handled: true, ExitCode: 2);
            }

            var result = await prepareUpdateInstall(parseResult.Options, cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync(result.Output.AsMemory(), cancellationToken).ConfigureAwait(false);
            return new CommandLineHandleResult(Handled: true, result.ExitCode);
        }

        if (args.Count >= 1
            && string.Equals(args[0].Trim(), "--launch-prepared-update", StringComparison.OrdinalIgnoreCase))
        {
            if (launchPreparedUpdate is null)
            {
                await error.WriteLineAsync(
                    "--launch-prepared-update is unavailable in this host.".AsMemory(),
                    cancellationToken).ConfigureAwait(false);
                return new CommandLineHandleResult(Handled: true, ExitCode: 2);
            }

            var parseResult = ParseLaunchPreparedUpdateOptions(args);
            if (!parseResult.IsValid || parseResult.Options is null)
            {
                await error.WriteLineAsync(parseResult.ErrorMessage.AsMemory(), cancellationToken).ConfigureAwait(false);
                await error.WriteLineAsync(CreateHelpText().AsMemory(), cancellationToken).ConfigureAwait(false);
                return new CommandLineHandleResult(Handled: true, ExitCode: 2);
            }

            var result = await launchPreparedUpdate(parseResult.Options, cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync(result.Output.AsMemory(), cancellationToken).ConfigureAwait(false);
            return new CommandLineHandleResult(Handled: true, result.ExitCode);
        }

        if (args.Count >= 1
            && string.Equals(args[0].Trim(), "--install-latest-update", StringComparison.OrdinalIgnoreCase))
        {
            if (installLatestUpdate is null)
            {
                await error.WriteLineAsync(
                    "--install-latest-update is unavailable in this host.".AsMemory(),
                    cancellationToken).ConfigureAwait(false);
                return new CommandLineHandleResult(Handled: true, ExitCode: 2);
            }

            var parseResult = ParseInstallLatestUpdateOptions(args);
            if (!parseResult.IsValid || parseResult.Options is null)
            {
                await error.WriteLineAsync(parseResult.ErrorMessage.AsMemory(), cancellationToken).ConfigureAwait(false);
                await error.WriteLineAsync(CreateHelpText().AsMemory(), cancellationToken).ConfigureAwait(false);
                return new CommandLineHandleResult(Handled: true, ExitCode: 2);
            }

            var result = await installLatestUpdate(parseResult.Options, cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync(result.Output.AsMemory(), cancellationToken).ConfigureAwait(false);
            return new CommandLineHandleResult(Handled: true, result.ExitCode);
        }

        if (args.Count >= 1
            && string.Equals(args[0].Trim(), "--run-startup-update-check", StringComparison.OrdinalIgnoreCase))
        {
            if (runStartupUpdate is null)
            {
                await error.WriteLineAsync(
                    "--run-startup-update-check is unavailable in this host.".AsMemory(),
                    cancellationToken).ConfigureAwait(false);
                return new CommandLineHandleResult(Handled: true, ExitCode: 2);
            }

            if (args.Count != 1)
            {
                await error.WriteLineAsync(
                    "--run-startup-update-check does not accept options.".AsMemory(),
                    cancellationToken).ConfigureAwait(false);
                await error.WriteLineAsync(CreateHelpText().AsMemory(), cancellationToken).ConfigureAwait(false);
                return new CommandLineHandleResult(Handled: true, ExitCode: 2);
            }

            var result = await runStartupUpdate(cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync(result.Output.AsMemory(), cancellationToken).ConfigureAwait(false);
            return new CommandLineHandleResult(Handled: true, result.ExitCode);
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

        if (string.Equals(command, "--export-config-backup", StringComparison.OrdinalIgnoreCase))
        {
            if (exportConfigBackup is null)
            {
                await error.WriteLineAsync(
                    "--export-config-backup is unavailable in this host.".AsMemory(),
                    cancellationToken).ConfigureAwait(false);
                return new CommandLineHandleResult(Handled: true, ExitCode: 2);
            }

            var result = await exportConfigBackup(cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync(result.Output.AsMemory(), cancellationToken).ConfigureAwait(false);
            return new CommandLineHandleResult(Handled: true, result.ExitCode);
        }

        if (string.Equals(command, "--health-report", StringComparison.OrdinalIgnoreCase))
        {
            var report = await healthReport(cancellationToken).ConfigureAwait(false);
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
          WinAiUsageBar.App.exe [--help|--version|--smoke-test|--export-diagnostics|--export-config-backup|--health-report|--refresh-once|--set-provider-cli-override|--clear-provider-cli-override|--provider-catalog|--check-for-updates|--download-update|--launch-prepared-update|--install-latest-update|--run-startup-update-check]
          WinAiUsageBar.App.exe --refresh-once --provider <ProviderId> [--source <DataSourceKind>]
          WinAiUsageBar.App.exe --set-provider-cli-override --provider <ProviderId> --command <path-or-command>
          WinAiUsageBar.App.exe --clear-provider-cli-override --provider <ProviderId>
          WinAiUsageBar.App.exe --prune-support-artifacts [--keep-newest <N>]
          WinAiUsageBar.App.exe --check-for-updates [--current-version <version>]
          WinAiUsageBar.App.exe --download-update [--current-version <version>]
          WinAiUsageBar.App.exe --prepare-update-install --package <path> [--install-dir <path>] [--restart-after-install]
          WinAiUsageBar.App.exe --launch-prepared-update --script <path>
          WinAiUsageBar.App.exe --install-latest-update [--restart-after-install] [--current-version <version>]
          WinAiUsageBar.App.exe --validate-config-backup <path>
          WinAiUsageBar.App.exe --restore-config-backup <path> --confirm

        Options:
          --help                Show this help text without launching the app.
          --version             Print the app version without launching the app.
          --smoke-test          Run packaged-app startup checks without launching UI.
          --export-diagnostics  Write a redacted diagnostics export without launching UI.
          --export-config-backup
                                Write a config-only backup without launching UI.
          --health-report       Print local config, cache, and history health without launching UI.
          --refresh-once        Refresh enabled providers once and print a safe snapshot report without launching UI.
          --provider <ProviderId>
                                Limit --refresh-once to one provider.
          --source <DataSourceKind>
                                Temporarily override the selected provider source for --refresh-once.
          --set-provider-cli-override
                                Save a non-secret CLI command override for a CLI/local app-server provider.
          --clear-provider-cli-override
                                Clear the saved CLI command override for a CLI/local app-server provider.
          --command <path-or-command>
                                Command or path to save for --set-provider-cli-override.
          --provider-catalog    Print supported provider descriptors without launching UI.
          --check-for-updates   Check GitHub Releases for a newer package without launching UI.
          --download-update     Download and verify the latest GitHub Release package without installing it.
          --current-version <version>
                                Dogfood update commands as if the current app version were <version>.
          --prune-support-artifacts
                                Prune old config backups and diagnostics exports without launching UI.
          --keep-newest <N>     Keep the newest N matched support artifact files when pruning.
          --prepare-update-install
                                Generate a PowerShell script that applies a staged update after the app exits.
          --package <path>      Staged update zip package to install.
          --install-dir <path>  Install directory to replace. Defaults to the running app directory.
          --restart-after-install
                                Restart WinAI Usage Bar after the generated install script applies the update.
          --launch-prepared-update
                                Launch an app-owned apply-update.ps1 script created by --prepare-update-install.
          --script <path>       Prepared update install script to launch.
          --install-latest-update
                                Check, download, verify, prepare, and launch the latest update install script.
          --run-startup-update-check
                                Run the configured startup update policy once without opening UI.
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

    private static CommandLineRefreshOnceParseResult ParseRefreshOnceOptions(IReadOnlyList<string> args)
    {
        string? providerId = null;
        string? sourceKind = null;

        for (var index = 1; index < args.Count; index++)
        {
            var option = args[index].Trim();
            if (string.Equals(option, "--provider", StringComparison.OrdinalIgnoreCase))
            {
                if (providerId is not null)
                {
                    return CommandLineRefreshOnceParseResult.Invalid("Duplicate --provider option.");
                }

                if (++index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    return CommandLineRefreshOnceParseResult.Invalid("Missing value for --provider.");
                }

                providerId = args[index].Trim();
                continue;
            }

            if (string.Equals(option, "--source", StringComparison.OrdinalIgnoreCase))
            {
                if (sourceKind is not null)
                {
                    return CommandLineRefreshOnceParseResult.Invalid("Duplicate --source option.");
                }

                if (++index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    return CommandLineRefreshOnceParseResult.Invalid("Missing value for --source.");
                }

                sourceKind = args[index].Trim();
                continue;
            }

            return CommandLineRefreshOnceParseResult.Invalid($"Unknown --refresh-once option: {option}");
        }

        if (sourceKind is not null && providerId is null)
        {
            return CommandLineRefreshOnceParseResult.Invalid("--source requires --provider.");
        }

        return CommandLineRefreshOnceParseResult.Valid(new CommandLineRefreshOnceOptions(providerId, sourceKind));
    }

    private static CommandLineSetProviderCliOverrideParseResult ParseSetProviderCliOverrideOptions(
        IReadOnlyList<string> args)
    {
        string? providerId = null;
        string? commandPathOverride = null;

        for (var index = 1; index < args.Count; index++)
        {
            var option = args[index].Trim();
            if (string.Equals(option, "--provider", StringComparison.OrdinalIgnoreCase))
            {
                if (providerId is not null)
                {
                    return CommandLineSetProviderCliOverrideParseResult.Invalid("Duplicate --provider option.");
                }

                if (++index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    return CommandLineSetProviderCliOverrideParseResult.Invalid("Missing value for --provider.");
                }

                providerId = args[index].Trim();
                continue;
            }

            if (string.Equals(option, "--command", StringComparison.OrdinalIgnoreCase))
            {
                if (commandPathOverride is not null)
                {
                    return CommandLineSetProviderCliOverrideParseResult.Invalid("Duplicate --command option.");
                }

                if (++index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    return CommandLineSetProviderCliOverrideParseResult.Invalid("Missing value for --command.");
                }

                commandPathOverride = args[index].Trim();
                continue;
            }

            return CommandLineSetProviderCliOverrideParseResult.Invalid(
                $"Unknown --set-provider-cli-override option: {option}");
        }

        if (providerId is null)
        {
            return CommandLineSetProviderCliOverrideParseResult.Invalid(
                "--set-provider-cli-override requires --provider <ProviderId>.");
        }

        if (commandPathOverride is null)
        {
            return CommandLineSetProviderCliOverrideParseResult.Invalid(
                "--set-provider-cli-override requires --command <path-or-command>.");
        }

        return CommandLineSetProviderCliOverrideParseResult.Valid(
            new CommandLineSetProviderCliOverrideOptions(providerId, commandPathOverride));
    }

    private static CommandLineClearProviderCliOverrideParseResult ParseClearProviderCliOverrideOptions(
        IReadOnlyList<string> args)
    {
        string? providerId = null;

        for (var index = 1; index < args.Count; index++)
        {
            var option = args[index].Trim();
            if (string.Equals(option, "--provider", StringComparison.OrdinalIgnoreCase))
            {
                if (providerId is not null)
                {
                    return CommandLineClearProviderCliOverrideParseResult.Invalid("Duplicate --provider option.");
                }

                if (++index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    return CommandLineClearProviderCliOverrideParseResult.Invalid("Missing value for --provider.");
                }

                providerId = args[index].Trim();
                continue;
            }

            return CommandLineClearProviderCliOverrideParseResult.Invalid(
                $"Unknown --clear-provider-cli-override option: {option}");
        }

        if (providerId is null)
        {
            return CommandLineClearProviderCliOverrideParseResult.Invalid(
                "--clear-provider-cli-override requires --provider <ProviderId>.");
        }

        return CommandLineClearProviderCliOverrideParseResult.Valid(
            new CommandLineClearProviderCliOverrideOptions(providerId));
    }

    private static CommandLinePruneSupportArtifactsParseResult ParsePruneSupportArtifactsOptions(
        IReadOnlyList<string> args)
    {
        int? keepNewest = null;

        for (var index = 1; index < args.Count; index++)
        {
            var option = args[index].Trim();
            if (string.Equals(option, "--keep-newest", StringComparison.OrdinalIgnoreCase))
            {
                if (keepNewest is not null)
                {
                    return CommandLinePruneSupportArtifactsParseResult.Invalid("Duplicate --keep-newest option.");
                }

                if (++index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    return CommandLinePruneSupportArtifactsParseResult.Invalid("Missing value for --keep-newest.");
                }

                if (!int.TryParse(args[index].Trim(), out var parsedKeepNewest) || parsedKeepNewest < 1)
                {
                    return CommandLinePruneSupportArtifactsParseResult.Invalid("--keep-newest must be a positive whole number.");
                }

                keepNewest = parsedKeepNewest;
                continue;
            }

            return CommandLinePruneSupportArtifactsParseResult.Invalid($"Unknown --prune-support-artifacts option: {option}");
        }

        return CommandLinePruneSupportArtifactsParseResult.Valid(
            new CommandLinePruneSupportArtifactsOptions(keepNewest ?? DefaultPruneKeepNewest));
    }

    private static CommandLinePrepareUpdateInstallParseResult ParsePrepareUpdateInstallOptions(
        IReadOnlyList<string> args)
    {
        string? packagePath = null;
        string? installDirectory = null;
        var restartAfterInstall = false;

        for (var index = 1; index < args.Count; index++)
        {
            var option = args[index].Trim();
            if (string.Equals(option, "--package", StringComparison.OrdinalIgnoreCase))
            {
                if (packagePath is not null)
                {
                    return CommandLinePrepareUpdateInstallParseResult.Invalid("Duplicate --package option.");
                }

                if (++index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    return CommandLinePrepareUpdateInstallParseResult.Invalid("Missing value for --package.");
                }

                packagePath = args[index].Trim();
                continue;
            }

            if (string.Equals(option, "--install-dir", StringComparison.OrdinalIgnoreCase))
            {
                if (installDirectory is not null)
                {
                    return CommandLinePrepareUpdateInstallParseResult.Invalid("Duplicate --install-dir option.");
                }

                if (++index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    return CommandLinePrepareUpdateInstallParseResult.Invalid("Missing value for --install-dir.");
                }

                installDirectory = args[index].Trim();
                continue;
            }

            if (string.Equals(option, "--restart-after-install", StringComparison.OrdinalIgnoreCase))
            {
                if (restartAfterInstall)
                {
                    return CommandLinePrepareUpdateInstallParseResult.Invalid("Duplicate --restart-after-install option.");
                }

                restartAfterInstall = true;
                continue;
            }

            return CommandLinePrepareUpdateInstallParseResult.Invalid($"Unknown --prepare-update-install option: {option}");
        }

        if (string.IsNullOrWhiteSpace(packagePath))
        {
            return CommandLinePrepareUpdateInstallParseResult.Invalid("--prepare-update-install requires --package <path>.");
        }

        return CommandLinePrepareUpdateInstallParseResult.Valid(
            new CommandLinePrepareUpdateInstallOptions(packagePath, installDirectory, restartAfterInstall));
    }

    private static CommandLineLaunchPreparedUpdateParseResult ParseLaunchPreparedUpdateOptions(
        IReadOnlyList<string> args)
    {
        string? scriptPath = null;

        for (var index = 1; index < args.Count; index++)
        {
            var option = args[index].Trim();
            if (string.Equals(option, "--script", StringComparison.OrdinalIgnoreCase))
            {
                if (scriptPath is not null)
                {
                    return CommandLineLaunchPreparedUpdateParseResult.Invalid("Duplicate --script option.");
                }

                if (++index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    return CommandLineLaunchPreparedUpdateParseResult.Invalid("Missing value for --script.");
                }

                scriptPath = args[index].Trim();
                continue;
            }

            return CommandLineLaunchPreparedUpdateParseResult.Invalid($"Unknown --launch-prepared-update option: {option}");
        }

        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            return CommandLineLaunchPreparedUpdateParseResult.Invalid("--launch-prepared-update requires --script <path>.");
        }

        return CommandLineLaunchPreparedUpdateParseResult.Valid(
            new CommandLineLaunchPreparedUpdateOptions(scriptPath));
    }

    private static CommandLineInstallLatestUpdateParseResult ParseInstallLatestUpdateOptions(
        IReadOnlyList<string> args)
    {
        var restartAfterInstall = false;
        string? currentVersion = null;

        for (var index = 1; index < args.Count; index++)
        {
            var option = args[index].Trim();
            if (string.Equals(option, "--restart-after-install", StringComparison.OrdinalIgnoreCase))
            {
                if (restartAfterInstall)
                {
                    return CommandLineInstallLatestUpdateParseResult.Invalid("Duplicate --restart-after-install option.");
                }

                restartAfterInstall = true;
                continue;
            }

            if (string.Equals(option, "--current-version", StringComparison.OrdinalIgnoreCase))
            {
                if (currentVersion is not null)
                {
                    return CommandLineInstallLatestUpdateParseResult.Invalid("Duplicate --current-version option.");
                }

                if (++index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    return CommandLineInstallLatestUpdateParseResult.Invalid("Missing value for --current-version.");
                }

                var validation = ValidateCurrentVersionOverride(args[index]);
                if (!validation.IsValid || validation.Value is null)
                {
                    return CommandLineInstallLatestUpdateParseResult.Invalid(validation.ErrorMessage);
                }

                currentVersion = validation.Value;
                continue;
            }

            return CommandLineInstallLatestUpdateParseResult.Invalid($"Unknown --install-latest-update option: {option}");
        }

        return CommandLineInstallLatestUpdateParseResult.Valid(
            new CommandLineInstallLatestUpdateOptions(restartAfterInstall, currentVersion));
    }

    private static CommandLineUpdateVersionParseResult ParseUpdateVersionOptions(
        IReadOnlyList<string> args,
        string commandName)
    {
        string? currentVersion = null;

        for (var index = 1; index < args.Count; index++)
        {
            var option = args[index].Trim();
            if (string.Equals(option, "--current-version", StringComparison.OrdinalIgnoreCase))
            {
                if (currentVersion is not null)
                {
                    return CommandLineUpdateVersionParseResult.Invalid("Duplicate --current-version option.");
                }

                if (++index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    return CommandLineUpdateVersionParseResult.Invalid("Missing value for --current-version.");
                }

                var validation = ValidateCurrentVersionOverride(args[index]);
                if (!validation.IsValid || validation.Value is null)
                {
                    return CommandLineUpdateVersionParseResult.Invalid(validation.ErrorMessage);
                }

                currentVersion = validation.Value;
                continue;
            }

            return CommandLineUpdateVersionParseResult.Invalid($"Unknown {commandName} option: {option}");
        }

        return CommandLineUpdateVersionParseResult.Valid(
            new CommandLineUpdateVersionOptions(currentVersion));
    }

    private static CurrentVersionOverrideValidationResult ValidateCurrentVersionOverride(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length > 64)
        {
            return CurrentVersionOverrideValidationResult.Invalid("--current-version must be 64 characters or fewer.");
        }

        if (trimmed.Contains('\r') || trimmed.Contains('\n'))
        {
            return CurrentVersionOverrideValidationResult.Invalid("--current-version must be a single value.");
        }

        var normalized = trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase)
            ? trimmed[1..]
            : trimmed;
        var metadataIndex = normalized.IndexOfAny(['+', '-']);
        if (metadataIndex >= 0)
        {
            normalized = normalized[..metadataIndex];
        }

        var versionParts = normalized.Split('.');
        if (versionParts.Length != 3
            || versionParts.Any(part => !int.TryParse(part, out _))
            || !Version.TryParse(normalized, out _))
        {
            return CurrentVersionOverrideValidationResult.Invalid("--current-version must be a SemVer-like version such as 0.1.2.");
        }

        return CurrentVersionOverrideValidationResult.Valid(trimmed);
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

internal sealed record CommandLineRefreshOnceParseResult(
    bool IsValid,
    CommandLineRefreshOnceOptions? Options,
    string ErrorMessage)
{
    public static CommandLineRefreshOnceParseResult Valid(CommandLineRefreshOnceOptions options)
    {
        return new CommandLineRefreshOnceParseResult(true, options, string.Empty);
    }

    public static CommandLineRefreshOnceParseResult Invalid(string errorMessage)
    {
        return new CommandLineRefreshOnceParseResult(false, null, errorMessage);
    }
}

internal sealed record CommandLinePruneSupportArtifactsParseResult(
    bool IsValid,
    CommandLinePruneSupportArtifactsOptions? Options,
    string ErrorMessage)
{
    public static CommandLinePruneSupportArtifactsParseResult Valid(
        CommandLinePruneSupportArtifactsOptions options)
    {
        return new CommandLinePruneSupportArtifactsParseResult(true, options, string.Empty);
    }

    public static CommandLinePruneSupportArtifactsParseResult Invalid(string errorMessage)
    {
        return new CommandLinePruneSupportArtifactsParseResult(false, null, errorMessage);
    }
}

internal sealed record CommandLineUpdateVersionParseResult(
    bool IsValid,
    CommandLineUpdateVersionOptions? Options,
    string ErrorMessage)
{
    public static CommandLineUpdateVersionParseResult Valid(
        CommandLineUpdateVersionOptions options)
    {
        return new CommandLineUpdateVersionParseResult(true, options, string.Empty);
    }

    public static CommandLineUpdateVersionParseResult Invalid(string errorMessage)
    {
        return new CommandLineUpdateVersionParseResult(false, null, errorMessage);
    }
}

internal sealed record CurrentVersionOverrideValidationResult(
    bool IsValid,
    string? Value,
    string ErrorMessage)
{
    public static CurrentVersionOverrideValidationResult Valid(string value)
    {
        return new CurrentVersionOverrideValidationResult(true, value, string.Empty);
    }

    public static CurrentVersionOverrideValidationResult Invalid(string errorMessage)
    {
        return new CurrentVersionOverrideValidationResult(false, null, errorMessage);
    }
}

internal sealed record CommandLineSetProviderCliOverrideParseResult(
    bool IsValid,
    CommandLineSetProviderCliOverrideOptions? Options,
    string ErrorMessage)
{
    public static CommandLineSetProviderCliOverrideParseResult Valid(
        CommandLineSetProviderCliOverrideOptions options)
    {
        return new CommandLineSetProviderCliOverrideParseResult(true, options, string.Empty);
    }

    public static CommandLineSetProviderCliOverrideParseResult Invalid(string errorMessage)
    {
        return new CommandLineSetProviderCliOverrideParseResult(false, null, errorMessage);
    }
}

internal sealed record CommandLineClearProviderCliOverrideParseResult(
    bool IsValid,
    CommandLineClearProviderCliOverrideOptions? Options,
    string ErrorMessage)
{
    public static CommandLineClearProviderCliOverrideParseResult Valid(
        CommandLineClearProviderCliOverrideOptions options)
    {
        return new CommandLineClearProviderCliOverrideParseResult(true, options, string.Empty);
    }

    public static CommandLineClearProviderCliOverrideParseResult Invalid(string errorMessage)
    {
        return new CommandLineClearProviderCliOverrideParseResult(false, null, errorMessage);
    }
}

internal sealed record CommandLinePrepareUpdateInstallParseResult(
    bool IsValid,
    CommandLinePrepareUpdateInstallOptions? Options,
    string ErrorMessage)
{
    public static CommandLinePrepareUpdateInstallParseResult Valid(
        CommandLinePrepareUpdateInstallOptions options)
    {
        return new CommandLinePrepareUpdateInstallParseResult(true, options, string.Empty);
    }

    public static CommandLinePrepareUpdateInstallParseResult Invalid(string errorMessage)
    {
        return new CommandLinePrepareUpdateInstallParseResult(false, null, errorMessage);
    }
}

internal sealed record CommandLineLaunchPreparedUpdateParseResult(
    bool IsValid,
    CommandLineLaunchPreparedUpdateOptions? Options,
    string ErrorMessage)
{
    public static CommandLineLaunchPreparedUpdateParseResult Valid(
        CommandLineLaunchPreparedUpdateOptions options)
    {
        return new CommandLineLaunchPreparedUpdateParseResult(true, options, string.Empty);
    }

    public static CommandLineLaunchPreparedUpdateParseResult Invalid(string errorMessage)
    {
        return new CommandLineLaunchPreparedUpdateParseResult(false, null, errorMessage);
    }
}

internal sealed record CommandLineInstallLatestUpdateParseResult(
    bool IsValid,
    CommandLineInstallLatestUpdateOptions? Options,
    string ErrorMessage)
{
    public static CommandLineInstallLatestUpdateParseResult Valid(
        CommandLineInstallLatestUpdateOptions options)
    {
        return new CommandLineInstallLatestUpdateParseResult(true, options, string.Empty);
    }

    public static CommandLineInstallLatestUpdateParseResult Invalid(string errorMessage)
    {
        return new CommandLineInstallLatestUpdateParseResult(false, null, errorMessage);
    }
}
