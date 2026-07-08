using WinAiUsageBar.Infrastructure.Diagnostics;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class DiagnosticsExportServiceTests
{
    [Fact]
    public async Task ExportAsync_WritesRedactedBundleWithoutSecretFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        paths.EnsureCreated();
        await File.WriteAllTextAsync(
            paths.ConfigPath,
            """
            {
              "apiKey": "sample-api-key-value",
              "secretName": "gemini-reference-name",
              "visible": "keep this"
            }
            """);
        await File.WriteAllTextAsync(paths.SnapshotsPath, "Authorization: Bearer abc123");
        await File.WriteAllTextAsync(paths.HistoryPath, "access_token=history-secret");
        await File.WriteAllTextAsync(paths.DiagnosticsLogPath, "token=diagnostics-token-value");
        await File.WriteAllTextAsync(Path.Combine(paths.SecretsDirectory, "stored-secret"), "never-export-me");
        var service = new DiagnosticsExportService(
            paths,
            () => new DateTimeOffset(2026, 7, 8, 12, 34, 56, TimeSpan.Zero));

        try
        {
            var result = await service.ExportAsync(CancellationToken.None);
            var export = await File.ReadAllTextAsync(result.Path);

            Assert.True(File.Exists(result.Path));
            Assert.Equal(["config.json", "snapshots.json", "history.ndjson", "diagnostics.log"], result.IncludedSections);
            Assert.Contains("--- config.json ---", export);
            Assert.Contains("keep this", export);
            Assert.Contains("SecretsDirectory: [omitted]", export);
            Assert.DoesNotContain("sample-api-key-value", export);
            Assert.DoesNotContain("gemini-reference-name", export);
            Assert.DoesNotContain("abc123", export);
            Assert.DoesNotContain("history-secret", export);
            Assert.DoesNotContain("diagnostics-token-value", export);
            Assert.DoesNotContain("never-export-me", export);
            Assert.Contains("[REDACTED]", export);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExportAsync_NotesMissingFilesAndTruncatesLargeFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        paths.EnsureCreated();
        await File.WriteAllTextAsync(paths.DiagnosticsLogPath, "0123456789abcdef");
        var service = new DiagnosticsExportService(
            paths,
            () => new DateTimeOffset(2026, 7, 8, 12, 34, 57, TimeSpan.Zero),
            maxBytesPerFile: 8);

        try
        {
            var result = await service.ExportAsync(CancellationToken.None);
            var export = await File.ReadAllTextAsync(result.Path);

            Assert.Contains("[missing]", export);
            Assert.Contains("[truncated to last 8 bytes]", export);
            Assert.Contains("89abcdef", export);
            Assert.DoesNotContain("01234567", export);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExportAsync_UsesUniquePathWhenSameSecondExportExists()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
        var paths = new AppDataPaths(root);
        paths.EnsureCreated();
        await File.WriteAllTextAsync(paths.ConfigPath, """{"visible":"keep"}""");
        var now = new DateTimeOffset(2026, 7, 8, 12, 35, 0, TimeSpan.Zero);
        var service = new DiagnosticsExportService(paths, () => now);

        try
        {
            var first = await service.ExportAsync(CancellationToken.None);
            var second = await service.ExportAsync(CancellationToken.None);

            Assert.Equal("diagnostics-export-20260708-123500.txt", Path.GetFileName(first.Path));
            Assert.Equal("diagnostics-export-20260708-123500-1.txt", Path.GetFileName(second.Path));
            Assert.True(File.Exists(first.Path));
            Assert.True(File.Exists(second.Path));
            Assert.Contains("keep", await File.ReadAllTextAsync(first.Path));
            Assert.Contains("keep", await File.ReadAllTextAsync(second.Path));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
