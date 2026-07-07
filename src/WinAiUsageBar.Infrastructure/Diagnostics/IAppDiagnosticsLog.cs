using WinAiUsageBar.Infrastructure.Security;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Infrastructure.Diagnostics;

public interface IAppDiagnosticsLog
{
    Task InfoAsync(string message, CancellationToken cancellationToken);

    Task ErrorAsync(string message, Exception exception, CancellationToken cancellationToken);
}

public sealed class FileAppDiagnosticsLog(AppDataPaths paths) : IAppDiagnosticsLog
{
    private readonly SemaphoreSlim writeLock = new(1, 1);

    public Task InfoAsync(string message, CancellationToken cancellationToken)
    {
        return WriteAsync("INFO", message, exception: null, cancellationToken);
    }

    public Task ErrorAsync(string message, Exception exception, CancellationToken cancellationToken)
    {
        return WriteAsync("ERROR", message, exception, cancellationToken);
    }

    private async Task WriteAsync(
        string level,
        string message,
        Exception? exception,
        CancellationToken cancellationToken)
    {
        paths.EnsureCreated();
        var safeMessage = DiagnosticRedactor.Redact(message);
        var safeException = exception is null
            ? null
            : DiagnosticRedactor.Redact($"{exception.GetType().Name}: {exception.Message}");

        var line = safeException is null
            ? $"{DateTimeOffset.Now:O} [{level}] {safeMessage}"
            : $"{DateTimeOffset.Now:O} [{level}] {safeMessage} {safeException}";

        await writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await File.AppendAllTextAsync(
                paths.DiagnosticsLogPath,
                line + Environment.NewLine,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            writeLock.Release();
        }
    }
}
