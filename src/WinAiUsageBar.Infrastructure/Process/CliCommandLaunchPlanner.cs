using System.Diagnostics;
using System.IO;

namespace WinAiUsageBar.Infrastructure.Process;

public sealed record CliCommandLaunchPlan(
    string FileName,
    IReadOnlyList<string> PrefixArguments,
    string TargetPath,
    bool UsesCommandProcessor)
{
    public ProcessStartInfo CreateStartInfo(
        IReadOnlyList<string> commandArguments,
        bool redirectStandardInput = false)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = FileName,
            RedirectStandardInput = redirectStandardInput,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in PrefixArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (UsesCommandProcessor)
        {
            startInfo.ArgumentList.Add(BuildCommandProcessorInvocation(TargetPath, commandArguments));
        }
        else
        {
            foreach (var argument in commandArguments)
            {
                startInfo.ArgumentList.Add(argument);
            }
        }

        return startInfo;
    }

    private static string BuildCommandProcessorInvocation(
        string targetPath,
        IReadOnlyList<string> commandArguments)
    {
        var parts = new List<string> { "call", QuoteForCommandProcessor(targetPath) };
        parts.AddRange(commandArguments.Select(QuoteForCommandProcessor));
        return string.Join(' ', parts);
    }

    private static string QuoteForCommandProcessor(string value)
    {
        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}

public static class CliCommandLaunchPlanner
{
    public static CliCommandLaunchPlan Create(
        string commandName,
        IReadOnlyList<string> resolvedPaths)
    {
        var targetPath = ChooseTargetPath(commandName, resolvedPaths);
        if (IsCommandProcessorScript(targetPath))
        {
            return new CliCommandLaunchPlan(
                CommandProcessorPath(),
                ["/d", "/c"],
                targetPath,
                UsesCommandProcessor: true);
        }

        return new CliCommandLaunchPlan(
            targetPath,
            [],
            targetPath,
            UsesCommandProcessor: false);
    }

    private static string ChooseTargetPath(
        string commandName,
        IReadOnlyList<string> resolvedPaths)
    {
        return resolvedPaths.FirstOrDefault(IsDirectlyLaunchablePath)
            ?? resolvedPaths.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path))
            ?? commandName;
    }

    private static bool IsDirectlyLaunchablePath(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
            || IsCommandProcessorScript(path);
    }

    private static bool IsCommandProcessorScript(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase);
    }

    private static string CommandProcessorPath()
    {
        return Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
    }
}
