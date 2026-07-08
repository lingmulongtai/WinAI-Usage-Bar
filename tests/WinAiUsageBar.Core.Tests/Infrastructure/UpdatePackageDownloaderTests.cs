using System.Security.Cryptography;
using System.Text;
using WinAiUsageBar.Infrastructure.Updates;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class UpdatePackageDownloaderTests
{
    [Fact]
    public async Task DownloadAndVerifyAsync_WritesPackageAndChecksumWhenHashMatches()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var packageBytes = Encoding.UTF8.GetBytes("package bytes");
        var hash = Sha256(packageBytes);
        var downloader = new UpdatePackageDownloader((uri, _) => Task.FromResult(
            uri.AbsoluteUri.EndsWith(".sha256", StringComparison.Ordinal)
                ? Encoding.UTF8.GetBytes($"{hash}  WinAIUsageBar-0.2.0-win-x64.zip")
                : packageBytes));

        try
        {
            var result = await downloader.DownloadAndVerifyAsync(
                Asset("WinAIUsageBar-0.2.0-win-x64.zip", "https://example.test/package.zip"),
                Asset("WinAIUsageBar-0.2.0-win-x64.zip.sha256", "https://example.test/package.zip.sha256"),
                root,
                CancellationToken.None);

            Assert.Equal(UpdateDownloadStatus.Downloaded, result.Status);
            Assert.Equal(hash, result.ExpectedSha256);
            Assert.Equal(hash, result.ActualSha256);
            Assert.True(File.Exists(result.PackagePath));
            Assert.True(File.Exists(result.ChecksumPath));
            Assert.Equal(packageBytes, await File.ReadAllBytesAsync(result.PackagePath!));
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DownloadAndVerifyAsync_DoesNotWriteFinalPackageWhenHashMismatches()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var downloader = new UpdatePackageDownloader((uri, _) => Task.FromResult(
            uri.AbsoluteUri.EndsWith(".sha256", StringComparison.Ordinal)
                ? Encoding.UTF8.GetBytes($"{new string('0', 64)}  WinAIUsageBar-0.2.0-win-x64.zip")
                : Encoding.UTF8.GetBytes("different package")));

        try
        {
            var result = await downloader.DownloadAndVerifyAsync(
                Asset("WinAIUsageBar-0.2.0-win-x64.zip", "https://example.test/package.zip"),
                Asset("WinAIUsageBar-0.2.0-win-x64.zip.sha256", "https://example.test/package.zip.sha256"),
                root,
                CancellationToken.None);

            Assert.Equal(UpdateDownloadStatus.ChecksumMismatch, result.Status);
            Assert.Null(result.PackagePath);
            Assert.False(File.Exists(Path.Combine(root, "WinAIUsageBar-0.2.0-win-x64.zip")));
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DownloadAndVerifyAsync_RejectsUnsafeAssetNames()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var downloader = new UpdatePackageDownloader((_, _) => Task.FromResult(Array.Empty<byte>()));

        try
        {
            var result = await downloader.DownloadAndVerifyAsync(
                Asset(@"..\WinAIUsageBar-0.2.0-win-x64.zip", "https://example.test/package.zip"),
                Asset("WinAIUsageBar-0.2.0-win-x64.zip.sha256", "https://example.test/package.zip.sha256"),
                root,
                CancellationToken.None);

            Assert.Equal(UpdateDownloadStatus.InvalidAsset, result.Status);
            Assert.False(Directory.Exists(root));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static UpdatePackageAsset Asset(string name, string url)
    {
        return new UpdatePackageAsset(name, new Uri(url), SizeBytes: null);
    }

    private static string Sha256(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
