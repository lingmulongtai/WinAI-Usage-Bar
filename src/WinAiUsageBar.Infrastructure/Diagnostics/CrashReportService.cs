using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WinAiUsageBar.Infrastructure.Security;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Infrastructure.Diagnostics;

public interface ICrashReportService
{
    Task<CrashReportWriteResult> WriteAsync(
        CrashReportRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CrashReportFile>> ListAsync(
        int limit,
        CancellationToken cancellationToken);

    Task<CrashReportPruneResult> PruneAsync(
        int keepNewest,
        CancellationToken cancellationToken);
}

public sealed record CrashReportRequest(
    string Source,
    Exception Exception,
    string? AppVersion = null,
    IReadOnlyDictionary<string, string?>? Context = null);

public sealed record CrashReportWriteResult(
    string Path,
    DateTimeOffset CreatedAt);

public sealed record CrashReportFile(
    string Path,
    DateTimeOffset CreatedAt,
    long SizeBytes);

public sealed record CrashReportPruneResult(
    string DirectoryPath,
    int KeepNewest,
    int MatchedCount,
    int KeptCount,
    int DeletedCount,
    long DeletedBytes,
    DateTimeOffset PrunedAt);

public sealed class CrashReportService(
    AppDataPaths paths,
    Func<DateTimeOffset>? nowProvider = null,
    Func<Guid>? idProvider = null,
    int maxTextLength = 20_000) : ICrashReportService
{
    public const string GeneratedReportSearchPattern = "crash-report-*.json";

    private static readonly Regex CrashReportFileNameRegex = new(
        "^crash-report-[0-9]{8}-[0-9]{6}-[0-9a-fA-F]{32}\\.json$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly Func<DateTimeOffset> nowProvider = nowProvider ?? (() => DateTimeOffset.Now);
    private readonly Func<Guid> idProvider = idProvider ?? Guid.NewGuid;

    public async Task<CrashReportWriteResult> WriteAsync(
        CrashReportRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Exception);

        paths.EnsureCreated();
        var createdAt = nowProvider();
        var report = new CrashReportDocument(
            CreatedAt: createdAt,
            Source: SafeText(request.Source),
            ExceptionType: SafeText(request.Exception.GetType().FullName ?? request.Exception.GetType().Name),
            Message: SafeText(request.Exception.Message),
            StackTrace: SafeText(request.Exception.ToString()),
            AppVersion: SafeNullableText(request.AppVersion),
            Context: SafeContext(request.Context));

        var path = CreateReportPath(createdAt);
        var json = JsonSerializer.Serialize(report, JsonOptions);
        await File.WriteAllTextAsync(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken)
            .ConfigureAwait(false);

        return new CrashReportWriteResult(path, createdAt);
    }

    public Task<IReadOnlyList<CrashReportFile>> ListAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "At least one crash report must be requested.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        paths.EnsureCreated();
        IReadOnlyList<CrashReportFile> reports = EnumerateReports()
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Take(limit)
            .Select(file => new CrashReportFile(
                file.FullName,
                new DateTimeOffset(file.LastWriteTime),
                file.Length))
            .ToList();

        return Task.FromResult(reports);
    }

    public Task<CrashReportPruneResult> PruneAsync(
        int keepNewest,
        CancellationToken cancellationToken)
    {
        if (keepNewest < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(keepNewest), "At least one crash report must be kept.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        paths.EnsureCreated();
        var prunedAt = nowProvider();
        var reports = EnumerateReports()
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToList();
        var toDelete = reports.Skip(keepNewest).ToList();
        long deletedBytes = 0;
        var deletedCount = 0;

        foreach (var file in toDelete)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                deletedBytes += file.Length;
                file.Delete();
                deletedCount++;
            }
            catch (FileNotFoundException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }
        }

        return Task.FromResult(new CrashReportPruneResult(
            paths.CrashReportsDirectory,
            keepNewest,
            reports.Count,
            Math.Min(keepNewest, reports.Count),
            deletedCount,
            deletedBytes,
            prunedAt));
    }

    private string CreateReportPath(DateTimeOffset createdAt)
    {
        for (var attempt = 0; attempt < 1000; attempt++)
        {
            var path = Path.Combine(
                paths.CrashReportsDirectory,
                $"crash-report-{createdAt:yyyyMMdd-HHmmss}-{idProvider():N}.json");
            try
            {
                using var _ = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                return path;
            }
            catch (IOException)
            {
            }
        }

        throw new IOException("Unable to reserve a unique crash report path.");
    }

    private IReadOnlyList<FileInfo> EnumerateReports()
    {
        if (!Directory.Exists(paths.CrashReportsDirectory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(paths.CrashReportsDirectory, GeneratedReportSearchPattern, SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .Where(file => file.Exists && IsGeneratedCrashReportFileName(file.Name))
            .ToList();
    }

    public static bool IsGeneratedCrashReportFileName(string fileName)
    {
        return CrashReportFileNameRegex.IsMatch(fileName);
    }

    private IReadOnlyDictionary<string, string>? SafeContext(IReadOnlyDictionary<string, string?>? context)
    {
        if (context is null || context.Count == 0)
        {
            return null;
        }

        var safeContext = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in context.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            var keyLooksSensitive = LooksSensitiveContextName(item.Key);
            var key = keyLooksSensitive ? "[REDACTED]" : SafeText(item.Key);
            if (safeContext.ContainsKey(key))
            {
                key = $"{key}-{safeContext.Count + 1}";
            }

            safeContext[key] = keyLooksSensitive
                ? "[REDACTED]"
                : SafeNullableText(item.Value) ?? string.Empty;
        }

        return safeContext;
    }

    private static bool LooksSensitiveContextName(string name)
    {
        return name.Contains("token", StringComparison.OrdinalIgnoreCase)
            || name.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || name.Contains("cookie", StringComparison.OrdinalIgnoreCase)
            || name.Contains("authorization", StringComparison.OrdinalIgnoreCase)
            || name.Contains("apiKey", StringComparison.OrdinalIgnoreCase)
            || name.Contains("api_key", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "pat", StringComparison.OrdinalIgnoreCase)
            || name.Contains("patSecret", StringComparison.OrdinalIgnoreCase)
            || name.Contains("githubPat", StringComparison.OrdinalIgnoreCase);
    }

    private string? SafeNullableText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : SafeText(value);
    }

    private string SafeText(string value)
    {
        var redacted = DiagnosticRedactor.RedactForDisplay(value).Trim();
        if (redacted.Length <= maxTextLength)
        {
            return redacted;
        }

        return redacted[..maxTextLength] + "...[truncated]";
    }

    private sealed record CrashReportDocument(
        DateTimeOffset CreatedAt,
        string Source,
        string ExceptionType,
        string Message,
        string StackTrace,
        string? AppVersion,
        IReadOnlyDictionary<string, string>? Context);
}
