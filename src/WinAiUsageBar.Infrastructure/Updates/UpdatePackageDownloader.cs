using System.Security.Cryptography;
using System.Text;

namespace WinAiUsageBar.Infrastructure.Updates;

public interface IUpdatePackageDownloader
{
    Task<UpdateDownloadResult> DownloadAndVerifyAsync(
        UpdatePackageAsset package,
        UpdatePackageAsset checksum,
        string targetDirectory,
        CancellationToken cancellationToken);
}

public sealed record UpdateDownloadResult(
    UpdateDownloadStatus Status,
    string Message,
    string? PackagePath,
    string? ChecksumPath,
    string? ExpectedSha256,
    string? ActualSha256);

public enum UpdateDownloadStatus
{
    Downloaded,
    InvalidAsset,
    ChecksumMismatch,
    Error
}

public sealed class UpdatePackageDownloader(
    Func<Uri, CancellationToken, Task<byte[]>>? downloadBytes = null) : IUpdatePackageDownloader
{
    private readonly Func<Uri, CancellationToken, Task<byte[]>> downloadBytes =
        downloadBytes ?? DownloadBytesAsync;

    public async Task<UpdateDownloadResult> DownloadAndVerifyAsync(
        UpdatePackageAsset package,
        UpdatePackageAsset checksum,
        string targetDirectory,
        CancellationToken cancellationToken)
    {
        try
        {
            var packageFileName = GetSafeFileName(package.Name);
            var checksumFileName = GetSafeFileName(checksum.Name);
            if (packageFileName is null || checksumFileName is null)
            {
                return Failure(
                    UpdateDownloadStatus.InvalidAsset,
                    "Update asset names must be simple file names.",
                    actualSha256: null);
            }

            Directory.CreateDirectory(targetDirectory);
            var checksumBytes = await downloadBytes(checksum.DownloadUrl, cancellationToken).ConfigureAwait(false);
            var checksumText = Encoding.UTF8.GetString(checksumBytes);
            var parseResult = ParseChecksum(checksumText);
            if (!parseResult.IsValid || parseResult.Hash is null || parseResult.FileName is null)
            {
                return Failure(
                    UpdateDownloadStatus.InvalidAsset,
                    parseResult.ErrorMessage,
                    actualSha256: null);
            }

            if (!string.Equals(parseResult.FileName, packageFileName, StringComparison.OrdinalIgnoreCase))
            {
                return Failure(
                    UpdateDownloadStatus.InvalidAsset,
                    $"Checksum file references {parseResult.FileName}, expected {packageFileName}.",
                    actualSha256: null);
            }

            var packageBytes = await downloadBytes(package.DownloadUrl, cancellationToken).ConfigureAwait(false);
            var actualHash = Convert.ToHexString(SHA256.HashData(packageBytes)).ToLowerInvariant();
            if (!string.Equals(actualHash, parseResult.Hash, StringComparison.OrdinalIgnoreCase))
            {
                return Failure(
                    UpdateDownloadStatus.ChecksumMismatch,
                    "Downloaded update package checksum did not match the release checksum.",
                    actualHash,
                    parseResult.Hash);
            }

            var packagePath = Path.Combine(targetDirectory, packageFileName);
            var checksumPath = Path.Combine(targetDirectory, checksumFileName);
            await WriteFinalFileAsync(packagePath, packageBytes, cancellationToken).ConfigureAwait(false);
            await WriteFinalFileAsync(checksumPath, checksumBytes, cancellationToken).ConfigureAwait(false);

            return new UpdateDownloadResult(
                UpdateDownloadStatus.Downloaded,
                "Update package downloaded and verified.",
                packagePath,
                checksumPath,
                parseResult.Hash,
                actualHash);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Failure(
                UpdateDownloadStatus.Error,
                $"Update package download failed: {ex.Message}",
                actualSha256: null);
        }
    }

    private static async Task<byte[]> DownloadBytesAsync(
        Uri uri,
        CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("WinAIUsageBar/0.1");
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string? GetSafeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var fileName = Path.GetFileName(value);
        return string.Equals(fileName, value, StringComparison.Ordinal)
            ? fileName
            : null;
    }

    private static ChecksumParseResult ParseChecksum(string checksumText)
    {
        var line = checksumText
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(line))
        {
            return ChecksumParseResult.Invalid("Checksum file was empty.");
        }

        var parts = line.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return ChecksumParseResult.Invalid("Checksum file must contain a hash and file name.");
        }

        var hash = parts[0].Trim().ToLowerInvariant();
        if (hash.Length != 64 || hash.Any(character => !Uri.IsHexDigit(character)))
        {
            return ChecksumParseResult.Invalid("Checksum hash must be a SHA256 hex string.");
        }

        var fileName = GetSafeFileName(parts[1].Trim());
        return fileName is null
            ? ChecksumParseResult.Invalid("Checksum file name must be a simple file name.")
            : ChecksumParseResult.Valid(hash, fileName);
    }

    private static async Task WriteFinalFileAsync(
        string finalPath,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        var tempPath = $"{finalPath}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllBytesAsync(tempPath, bytes, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, finalPath, overwrite: true);
        }
        finally
        {
            TryDeleteTempFile(tempPath);
        }
    }

    private static void TryDeleteTempFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Keep the original update download failure visible.
        }
    }

    private static UpdateDownloadResult Failure(
        UpdateDownloadStatus status,
        string message,
        string? actualSha256,
        string? expectedSha256 = null)
    {
        return new UpdateDownloadResult(
            status,
            message,
            PackagePath: null,
            ChecksumPath: null,
            expectedSha256,
            actualSha256);
    }

    private sealed record ChecksumParseResult(
        bool IsValid,
        string? Hash,
        string? FileName,
        string ErrorMessage)
    {
        public static ChecksumParseResult Valid(string hash, string fileName)
        {
            return new ChecksumParseResult(true, hash, fileName, string.Empty);
        }

        public static ChecksumParseResult Invalid(string errorMessage)
        {
            return new ChecksumParseResult(false, null, null, errorMessage);
        }
    }
}
