using Microsoft.Win32;

namespace WinAiUsageBar.Infrastructure.Windows;

public interface IStartupRegistrationService
{
    Task<StartupRegistrationStatus> GetStatusAsync(CancellationToken cancellationToken);

    Task SetEnabledAsync(bool isEnabled, CancellationToken cancellationToken);
}

public sealed record StartupRegistrationStatus(
    bool IsSupported,
    bool IsEnabled,
    string? Command,
    string StatusMessage);

public interface IStartupRunKey
{
    string? GetStringValue(string name);

    void SetStringValue(string name, string value);

    void DeleteValue(string name);
}

public sealed class RunKeyStartupRegistrationService(
    IStartupRunKey runKey,
    Func<string?>? processPathProvider = null) : IStartupRegistrationService
{
    public const string ValueName = "WinAI Usage Bar";
    private readonly Func<string?> processPathProvider = processPathProvider ?? (() => Environment.ProcessPath);

    public Task<StartupRegistrationStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var command = runKey.GetStringValue(ValueName);
        var isEnabled = !string.IsNullOrWhiteSpace(command);
        var status = isEnabled
            ? "WinAI Usage Bar is registered to start when you sign in."
            : "WinAI Usage Bar is not registered to start when you sign in.";

        return Task.FromResult(new StartupRegistrationStatus(
            IsSupported: ResolveCommand() is not null,
            isEnabled,
            command,
            status));
    }

    public Task SetEnabledAsync(bool isEnabled, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!isEnabled)
        {
            runKey.DeleteValue(ValueName);
            return Task.CompletedTask;
        }

        var command = ResolveCommand()
            ?? throw new InvalidOperationException("Cannot resolve the app executable path for startup registration.");

        runKey.SetStringValue(ValueName, command);
        return Task.CompletedTask;
    }

    private string? ResolveCommand()
    {
        var processPath = processPathProvider();
        return string.IsNullOrWhiteSpace(processPath)
            ? null
            : Quote(processPath);
    }

    private static string Quote(string path)
    {
        return $"\"{path}\"";
    }
}

public sealed class RegistryStartupRunKey : IStartupRunKey
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public string? GetStringValue(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(name) as string;
    }

    public void SetStringValue(string name, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Cannot open the CurrentUser Run registry key.");
        key.SetValue(name, value, RegistryValueKind.String);
    }

    public void DeleteValue(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }
}
