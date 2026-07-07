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
        Func<AppInfo> appInfoProvider,
        CancellationToken cancellationToken)
    {
        if (args.Count == 0)
        {
            return new CommandLineHandleResult(Handled: false, ExitCode: 0);
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

        await WriteUnknownArgumentsAsync(args, error, cancellationToken).ConfigureAwait(false);
        return new CommandLineHandleResult(Handled: true, ExitCode: 2);
    }

    public static string CreateHelpText()
    {
        return """
        WinAI Usage Bar

        Usage:
          WinAiUsageBar.App.exe [--help|--version|--smoke-test]

        Options:
          --help        Show this help text without launching the app.
          --version     Print the app version without launching the app.
          --smoke-test  Run packaged-app startup checks without launching UI.
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
