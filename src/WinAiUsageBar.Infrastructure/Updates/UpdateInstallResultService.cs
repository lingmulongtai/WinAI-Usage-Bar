using System.Globalization;
using System.Text.Json;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Infrastructure.Security;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Infrastructure.Updates;

public interface IUpdateInstallResultService
{
    Task<UpdateInstallResultRefreshResult> RefreshAsync(
        AppConfig config,
        CancellationToken cancellationToken);
}

public sealed record UpdateInstallResultRefreshResult(
    UpdateInstallResultRefreshStatus Status,
    string Message,
    string? ResultPath,
    string? InstallStatus,
    DateTimeOffset? CompletedAt,
    string? ValidationStatus = null,
    int? ValidationExitCode = null,
    string? ValidationOutputPath = null,
    long? ValidationOutputBytes = null,
    string? ValidationErrorPath = null,
    long? ValidationErrorBytes = null);

public enum UpdateInstallResultRefreshStatus
{
    NoResultPath,
    UnsafePath,
    Missing,
    Oversized,
    InvalidJson,
    Error,
    Unchanged,
    Updated
}

public sealed class UpdateInstallResultService(AppDataPaths paths) : IUpdateInstallResultService
{
    private const long MaxResultBytes = 64 * 1024;

    public async Task<UpdateInstallResultRefreshResult> RefreshAsync(
        AppConfig config,
        CancellationToken cancellationToken)
    {
        var resultPath = config.Updates.LastInstallResultPath;
        if (string.IsNullOrWhiteSpace(resultPath))
        {
            return Result(
                UpdateInstallResultRefreshStatus.NoResultPath,
                "No pending update install result path is recorded.",
                resultPath);
        }

        if (!TryNormalizeSafeResultPath(resultPath, out var safePath))
        {
            return Result(
                UpdateInstallResultRefreshStatus.UnsafePath,
                "Update install result path was ignored because it is outside the app-owned updates directory.",
                resultPath);
        }

        if (!File.Exists(safePath))
        {
            return Result(
                UpdateInstallResultRefreshStatus.Missing,
                "Update install result file has not been written yet.",
                safePath);
        }

        try
        {
            var fileInfo = new FileInfo(safePath);
            if (fileInfo.Length > MaxResultBytes)
            {
                return Result(
                    UpdateInstallResultRefreshStatus.Oversized,
                    "Update install result file was ignored because it is too large.",
                    safePath);
            }

            await using var stream = new FileStream(
                safePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var root = document.RootElement;
            var installStatus = Sanitize(GetString(root, "status"), maxLength: 64);
            var message = Sanitize(GetString(root, "message"), maxLength: 1000);
            var validationStatus = Sanitize(GetString(root, "validationStatus"), maxLength: 64);
            var validationExitCode = GetInt32(root, "validationExitCode");
            var resultDirectory = Path.GetDirectoryName(safePath) ?? paths.UpdatesDirectory;
            var validationOutput = ReadValidationLog(
                root,
                "validationOutputPath",
                "validationOutputBytes",
                resultDirectory,
                "validation.out.txt");
            var validationError = ReadValidationLog(
                root,
                "validationErrorPath",
                "validationErrorBytes",
                resultDirectory,
                "validation.err.txt");
            var completedAt = ParseCompletedAt(
                GetString(root, "completedAtUtc")
                ?? GetString(root, "completedAt"));

            if (installStatus is null)
            {
                return Result(
                    UpdateInstallResultRefreshStatus.InvalidJson,
                    "Update install result JSON did not contain a valid status field.",
                    safePath);
            }

            message ??= string.Empty;

            var changed = !string.Equals(config.Updates.LastInstallResultStatus, installStatus, StringComparison.Ordinal)
                || !string.Equals(config.Updates.LastInstallResultMessage, message, StringComparison.Ordinal)
                || config.Updates.LastInstallResultCompletedAt != completedAt
                || !string.Equals(config.Updates.LastInstallValidationStatus, validationStatus, StringComparison.Ordinal)
                || config.Updates.LastInstallValidationExitCode != validationExitCode
                || !string.Equals(config.Updates.LastInstallValidationOutputPath, validationOutput.Path, StringComparison.Ordinal)
                || config.Updates.LastInstallValidationOutputBytes != validationOutput.Bytes
                || !string.Equals(config.Updates.LastInstallValidationErrorPath, validationError.Path, StringComparison.Ordinal)
                || config.Updates.LastInstallValidationErrorBytes != validationError.Bytes;

            if (changed)
            {
                config.Updates.LastInstallResultStatus = installStatus;
                config.Updates.LastInstallResultMessage = message;
                config.Updates.LastInstallResultCompletedAt = completedAt;
                config.Updates.LastInstallValidationStatus = validationStatus;
                config.Updates.LastInstallValidationExitCode = validationExitCode;
                config.Updates.LastInstallValidationOutputPath = validationOutput.Path;
                config.Updates.LastInstallValidationOutputBytes = validationOutput.Bytes;
                config.Updates.LastInstallValidationErrorPath = validationError.Path;
                config.Updates.LastInstallValidationErrorBytes = validationError.Bytes;
            }

            return new UpdateInstallResultRefreshResult(
                changed
                    ? UpdateInstallResultRefreshStatus.Updated
                    : UpdateInstallResultRefreshStatus.Unchanged,
                changed
                    ? "Update install result was reconciled from disk."
                    : "Update install result was already current.",
                safePath,
                installStatus,
                completedAt,
                validationStatus,
                validationExitCode,
                validationOutput.Path,
                validationOutput.Bytes,
                validationError.Path,
                validationError.Bytes);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            return Result(
                ex is JsonException or NotSupportedException
                    ? UpdateInstallResultRefreshStatus.InvalidJson
                    : UpdateInstallResultRefreshStatus.Error,
                $"Update install result could not be read: {DiagnosticRedactor.Redact(ex.Message)}",
                safePath);
        }
    }

    private bool TryNormalizeSafeResultPath(string path, out string safePath)
    {
        safePath = string.Empty;
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!string.Equals(Path.GetFileName(fullPath), "install-result.json", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var updatesRoot = EnsureTrailingSeparator(Path.GetFullPath(paths.UpdatesDirectory));
            if (!fullPath.StartsWith(updatesRoot, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            safePath = fullPath;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static string? GetString(JsonElement root, string propertyName)
    {
        return root.ValueKind is JsonValueKind.Object
            && root.TryGetProperty(propertyName, out var property)
            && property.ValueKind is JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static int? GetInt32(JsonElement root, string propertyName)
    {
        if (root.ValueKind is not JsonValueKind.Object
            || !root.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out var value) => value,
            JsonValueKind.String when int.TryParse(
                property.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var value) => value,
            _ => null
        };
    }

    private static long? GetInt64(JsonElement root, string propertyName)
    {
        if (root.ValueKind is not JsonValueKind.Object
            || !root.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        var value = property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(
                property.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var number) => number,
            _ => -1
        };

        return value >= 0 ? value : null;
    }

    private static ValidationLogMetadata ReadValidationLog(
        JsonElement root,
        string pathPropertyName,
        string bytesPropertyName,
        string resultDirectory,
        string expectedFileName)
    {
        var value = GetString(root, pathPropertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return new ValidationLogMetadata(Path: null, Bytes: null);
        }

        try
        {
            var fullPath = Path.GetFullPath(value);
            var fullResultDirectory = Path.GetFullPath(resultDirectory);
            if (!string.Equals(Path.GetFileName(fullPath), expectedFileName, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Path.GetDirectoryName(fullPath), fullResultDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return new ValidationLogMetadata(Path: null, Bytes: null);
            }

            var bytes = File.Exists(fullPath)
                ? new FileInfo(fullPath).Length
                : GetInt64(root, bytesPropertyName);
            return new ValidationLogMetadata(Sanitize(fullPath, maxLength: 1000), bytes);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new ValidationLogMetadata(Path: null, Bytes: null);
        }
    }

    private static DateTimeOffset? ParseCompletedAt(string? value)
    {
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var completedAt)
            ? completedAt
            : null;
    }

    private static string? Sanitize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var safeValue = DiagnosticRedactor.Redact(value).Trim();
        if (safeValue.Length <= maxLength)
        {
            return safeValue;
        }

        return safeValue[..maxLength];
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static UpdateInstallResultRefreshResult Result(
        UpdateInstallResultRefreshStatus status,
        string message,
        string? resultPath)
    {
        return new UpdateInstallResultRefreshResult(
            status,
            message,
            resultPath,
            InstallStatus: null,
            CompletedAt: null);
    }

    private sealed record ValidationLogMetadata(string? Path, long? Bytes);
}
