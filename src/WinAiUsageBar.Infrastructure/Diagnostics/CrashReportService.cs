using System.Globalization;
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

    Task<CrashReportDetail> ReadDetailAsync(
        string path,
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
    long SizeBytes,
    string Source = "Unknown",
    string ExceptionType = "Unknown",
    string? AppVersion = null,
    bool MetadataAvailable = false,
    string MetadataStatus = "Metadata not read");

public enum CrashReportDetailStatus
{
    Available,
    InvalidPath,
    Missing,
    TooLarge,
    Malformed,
    Unavailable
}

public sealed record CrashReportDetail(
    string Path,
    string FileName,
    CrashReportDetailStatus Status,
    string StatusMessage,
    DateTimeOffset? CreatedAt,
    long SizeBytes,
    string Source = "Unknown",
    string ExceptionType = "Unknown",
    string? AppVersion = null,
    string? MessagePreview = null,
    bool MessageTruncated = false);

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
    private const long MaxMetadataReadBytes = 256_000;
    private const int MaxMessagePreviewLength = 1_000;
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

    public async Task<CrashReportDetail> ReadDetailAsync(
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        paths.EnsureCreated();

        var safePath = ResolveSafeCrashReportPath(path);
        if (safePath is null)
        {
            return CreateUnavailableDetail(
                path,
                CrashReportDetailStatus.InvalidPath,
                "Crash report details can only be read from app-generated top-level crash report files.");
        }

        var file = new FileInfo(safePath);
        if (!file.Exists)
        {
            return CreateUnavailableDetail(
                safePath,
                CrashReportDetailStatus.Missing,
                "Crash report file is missing.");
        }

        if (file.Length > MaxMetadataReadBytes)
        {
            return new CrashReportDetail(
                file.FullName,
                file.Name,
                CrashReportDetailStatus.TooLarge,
                "Crash report is too large to preview safely.",
                CreatedAt: null,
                file.Length);
        }

        try
        {
            await using var stream = new FileStream(
                file.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 4096,
                useAsync: true);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var root = document.RootElement;
            var fallbackCreatedAt = new DateTimeOffset(file.LastWriteTime);
            var message = SafeDetailMessage(ReadString(root, "Message"), out var messageTruncated);

            return new CrashReportDetail(
                file.FullName,
                file.Name,
                CrashReportDetailStatus.Available,
                "Crash report detail parsed.",
                ReadDateTimeOffset(root, "CreatedAt") ?? fallbackCreatedAt,
                file.Length,
                SafeDetailMetadata(ReadString(root, "Source") ?? "Unknown"),
                SafeDetailMetadata(ReadString(root, "ExceptionType") ?? "Unknown"),
                SafeNullableDetailMetadata(ReadString(root, "AppVersion")),
                message,
                messageTruncated);
        }
        catch (JsonException)
        {
            return new CrashReportDetail(
                file.FullName,
                file.Name,
                CrashReportDetailStatus.Malformed,
                "Crash report JSON is malformed.",
                CreatedAt: null,
                file.Length);
        }
        catch (IOException)
        {
            return new CrashReportDetail(
                file.FullName,
                file.Name,
                CrashReportDetailStatus.Unavailable,
                "Crash report file is currently unavailable.",
                CreatedAt: null,
                file.Length);
        }
        catch (UnauthorizedAccessException)
        {
            return new CrashReportDetail(
                file.FullName,
                file.Name,
                CrashReportDetailStatus.Unavailable,
                "Crash report file is currently unavailable.",
                CreatedAt: null,
                file.Length);
        }
    }

    public async Task<IReadOnlyList<CrashReportFile>> ListAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "At least one crash report must be requested.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        paths.EnsureCreated();
        var files = EnumerateReports()
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Take(limit)
            .ToList();

        var reports = new List<CrashReportFile>(files.Count);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            reports.Add(await ReadMetadataAsync(file, cancellationToken).ConfigureAwait(false));
        }

        return reports;
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

    private string? ResolveSafeCrashReportPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            var reportDirectory = Path.GetFullPath(paths.CrashReportsDirectory);
            var directory = Path.GetDirectoryName(fullPath);
            var fileName = Path.GetFileName(fullPath);

            if (!string.Equals(directory, reportDirectory, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(fileName)
                || !IsGeneratedCrashReportFileName(fileName))
            {
                return null;
            }

            return fullPath;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static CrashReportDetail CreateUnavailableDetail(
        string path,
        CrashReportDetailStatus status,
        string statusMessage)
    {
        var fileName = string.IsNullOrWhiteSpace(path)
            ? "n/a"
            : Path.GetFileName(path);
        return new CrashReportDetail(
            path,
            string.IsNullOrWhiteSpace(fileName) ? "n/a" : fileName,
            status,
            statusMessage,
            CreatedAt: null,
            SizeBytes: 0);
    }

    private static async Task<CrashReportFile> ReadMetadataAsync(
        FileInfo file,
        CancellationToken cancellationToken)
    {
        var fallbackCreatedAt = new DateTimeOffset(file.LastWriteTime);
        if (file.Length > MaxMetadataReadBytes)
        {
            return new CrashReportFile(
                file.FullName,
                fallbackCreatedAt,
                file.Length,
                MetadataStatus: "Metadata skipped because the report is too large.");
        }

        try
        {
            await using var stream = new FileStream(
                file.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 4096,
                useAsync: true);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var root = document.RootElement;
            var createdAt = ReadDateTimeOffset(root, "CreatedAt") ?? fallbackCreatedAt;

            return new CrashReportFile(
                file.FullName,
                createdAt,
                file.Length,
                SafeMetadata(ReadString(root, "Source") ?? "Unknown"),
                SafeMetadata(ReadString(root, "ExceptionType") ?? "Unknown"),
                SafeNullableMetadata(ReadString(root, "AppVersion")),
                MetadataAvailable: true,
                MetadataStatus: "Metadata parsed.");
        }
        catch (JsonException)
        {
            return new CrashReportFile(
                file.FullName,
                fallbackCreatedAt,
                file.Length,
                MetadataStatus: "Metadata unreadable.");
        }
        catch (IOException)
        {
            return new CrashReportFile(
                file.FullName,
                fallbackCreatedAt,
                file.Length,
                MetadataStatus: "Metadata unavailable.");
        }
        catch (UnauthorizedAccessException)
        {
            return new CrashReportFile(
                file.FullName,
                fallbackCreatedAt,
                file.Length,
                MetadataStatus: "Metadata unavailable.");
        }
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        return root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static DateTimeOffset? ReadDateTimeOffset(JsonElement root, string propertyName)
    {
        var value = ReadString(root, propertyName);
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed
            : null;
    }

    private static string? SafeNullableMetadata(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : SafeMetadata(value);
    }

    private static string SafeMetadata(string value)
    {
        var redacted = DiagnosticRedactor.RedactForDisplay(value).Trim();
        return redacted.Length <= 240
            ? redacted
            : redacted[..240] + "...[truncated]";
    }

    private static string? SafeNullableDetailMetadata(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : SafeDetailMetadata(value);
    }

    private static string SafeDetailMetadata(string value)
    {
        var redacted = DiagnosticRedactor.RedactForSupportExport(value).Trim();
        redacted = DiagnosticRedactor.RedactForDisplay(redacted).Trim();
        return redacted.Length <= 240
            ? redacted
            : redacted[..240] + "...[truncated]";
    }

    private static string? SafeDetailMessage(string? value, out bool truncated)
    {
        truncated = false;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var redacted = DiagnosticRedactor.RedactForSupportExport(value).Trim();
        if (redacted.Length <= MaxMessagePreviewLength)
        {
            return redacted;
        }

        truncated = true;
        return redacted[..MaxMessagePreviewLength] + "...[truncated]";
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
        var redacted = DiagnosticRedactor.RedactForSupportExport(value).Trim();
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
