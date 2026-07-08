using System.Text.Json;
using WinAiUsageBar.Infrastructure.Diagnostics;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class CrashReportServiceTests
{
    [Fact]
    public async Task WriteAsync_WritesStructuredRedactedCrashReport()
    {
        var root = TestRoot();
        var paths = new AppDataPaths(root);
        var createdAt = new DateTimeOffset(2026, 7, 9, 9, 30, 0, TimeSpan.Zero);
        var service = new CrashReportService(paths, () => createdAt, () => Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var githubToken = "gh" + "p_123456789012345";
        var exception = new InvalidOperationException($"startup failed token=raw-secret {githubToken}");
        var context = new Dictionary<string, string?>
        {
            ["stage"] = "startup",
            ["path"] = @"C:\App\WinAiUsageBar.App.exe",
            ["patSecretName"] = "copilot-secret-ref",
            ["note"] = "Authorization: Bearer abc123"
        };

        try
        {
            var result = await service.WriteAsync(
                new CrashReportRequest(
                    "startup token=source-secret",
                    exception,
                    AppVersion: "0.1.4+local",
                    Context: context),
                CancellationToken.None);
            var json = await File.ReadAllTextAsync(result.Path);
            using var document = JsonDocument.Parse(json);
            var rootElement = document.RootElement;

            Assert.Equal(createdAt, result.CreatedAt);
            Assert.Equal(paths.CrashReportsDirectory, Path.GetDirectoryName(result.Path));
            Assert.Equal(
                "crash-report-20260709-093000-11111111111111111111111111111111.json",
                Path.GetFileName(result.Path));
            Assert.Equal("startup [REDACTED]", rootElement.GetProperty("Source").GetString());
            Assert.Equal(typeof(InvalidOperationException).FullName, rootElement.GetProperty("ExceptionType").GetString());
            Assert.Equal("0.1.4+local", rootElement.GetProperty("AppVersion").GetString());
            Assert.Contains("[REDACTED]", rootElement.GetProperty("Message").GetString(), StringComparison.Ordinal);
            Assert.Contains("[REDACTED]", rootElement.GetProperty("StackTrace").GetString(), StringComparison.Ordinal);
            var contextElement = rootElement.GetProperty("Context");
            Assert.Equal(@"C:\App\WinAiUsageBar.App.exe", contextElement.GetProperty("path").GetString());
            Assert.Equal("startup", contextElement.GetProperty("stage").GetString());
            Assert.Equal("[REDACTED]", contextElement.GetProperty("note").GetString());
            Assert.Contains("[REDACTED]", contextElement.GetRawText(), StringComparison.Ordinal);
            Assert.DoesNotContain("raw-secret", json, StringComparison.Ordinal);
            Assert.DoesNotContain(githubToken, json, StringComparison.Ordinal);
            Assert.DoesNotContain("copilot-secret-ref", json, StringComparison.Ordinal);
            Assert.DoesNotContain("abc123", json, StringComparison.Ordinal);
            Assert.DoesNotContain("source-secret", json, StringComparison.Ordinal);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task WriteAsync_UsesCollisionResistantNames()
    {
        var root = TestRoot();
        var paths = new AppDataPaths(root);
        var createdAt = new DateTimeOffset(2026, 7, 9, 10, 0, 0, TimeSpan.Zero);
        var ids = new Queue<Guid>([
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222")
        ]);
        var service = new CrashReportService(paths, () => createdAt, () => ids.Dequeue());

        try
        {
            var first = await service.WriteAsync(Request(), CancellationToken.None);
            var second = await service.WriteAsync(Request(), CancellationToken.None);

            Assert.NotEqual(first.Path, second.Path);
            Assert.Equal(
                "crash-report-20260709-100000-11111111111111111111111111111111.json",
                Path.GetFileName(first.Path));
            Assert.Equal(
                "crash-report-20260709-100000-22222222222222222222222222222222.json",
                Path.GetFileName(second.Path));
            Assert.True(File.Exists(first.Path));
            Assert.True(File.Exists(second.Path));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task ListAsync_ReturnsNewestMatchedTopLevelReportsWithoutReadingContents()
    {
        var root = TestRoot();
        var paths = new AppDataPaths(root);
        paths.EnsureCreated();
        var older = await WriteTimestampedFileAsync(
            paths.CrashReportsDirectory,
            "crash-report-20260709-090000-11111111111111111111111111111111.json",
            "{ malformed",
            new DateTime(2026, 7, 9, 9, 0, 0, DateTimeKind.Utc));
        var newer = await WriteTimestampedFileAsync(
            paths.CrashReportsDirectory,
            "crash-report-20260709-100000-22222222222222222222222222222222.json",
            "{ malformed",
            new DateTime(2026, 7, 9, 10, 0, 0, DateTimeKind.Utc));
        var malformedName = await WriteTimestampedFileAsync(
            paths.CrashReportsDirectory,
            "crash-report-20260709-110000-not-a-guid.json",
            "keep",
            new DateTime(2026, 7, 9, 11, 0, 0, DateTimeKind.Utc));
        var unrelated = await WriteTimestampedFileAsync(
            paths.CrashReportsDirectory,
            "manual-note.json",
            "keep",
            new DateTime(2026, 7, 9, 11, 0, 0, DateTimeKind.Utc));
        var nested = Path.Combine(paths.CrashReportsDirectory, "nested");
        Directory.CreateDirectory(nested);
        await File.WriteAllTextAsync(Path.Combine(nested, "crash-report-20260709-120000-c.json"), "nested");
        var service = new CrashReportService(paths);

        try
        {
            var reports = await service.ListAsync(limit: 1, CancellationToken.None);

            Assert.Single(reports);
            Assert.Equal(newer, reports[0].Path);
            Assert.True(File.Exists(older));
            Assert.True(File.Exists(malformedName));
            Assert.True(File.Exists(unrelated));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task PruneAsync_DeletesOnlyOldMatchedTopLevelReports()
    {
        var root = TestRoot();
        var paths = new AppDataPaths(root);
        paths.EnsureCreated();
        var oldReport = await WriteTimestampedFileAsync(
            paths.CrashReportsDirectory,
            "crash-report-20260709-090000-11111111111111111111111111111111.json",
            "old",
            new DateTime(2026, 7, 9, 9, 0, 0, DateTimeKind.Utc));
        var keptReport = await WriteTimestampedFileAsync(
            paths.CrashReportsDirectory,
            "crash-report-20260709-100000-22222222222222222222222222222222.json",
            "new",
            new DateTime(2026, 7, 9, 10, 0, 0, DateTimeKind.Utc));
        var unrelated = await WriteTimestampedFileAsync(
            paths.CrashReportsDirectory,
            "crash-report-note.json",
            "keep",
            new DateTime(2026, 7, 9, 8, 0, 0, DateTimeKind.Utc));
        var deletedBytes = new FileInfo(oldReport).Length;
        var prunedAt = new DateTimeOffset(2026, 7, 9, 10, 30, 0, TimeSpan.Zero);
        var service = new CrashReportService(paths, () => prunedAt);

        try
        {
            var result = await service.PruneAsync(keepNewest: 1, CancellationToken.None);

            Assert.Equal(paths.CrashReportsDirectory, result.DirectoryPath);
            Assert.Equal(1, result.KeepNewest);
            Assert.Equal(2, result.MatchedCount);
            Assert.Equal(1, result.KeptCount);
            Assert.Equal(1, result.DeletedCount);
            Assert.Equal(deletedBytes, result.DeletedBytes);
            Assert.Equal(prunedAt, result.PrunedAt);
            Assert.False(File.Exists(oldReport));
            Assert.True(File.Exists(unrelated));
            Assert.True(File.Exists(keptReport));
            Assert.True(Directory.Exists(paths.SecretsDirectory));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task WriteAsync_TruncatesVeryLargeFields()
    {
        var root = TestRoot();
        var paths = new AppDataPaths(root);
        var service = new CrashReportService(
            paths,
            () => new DateTimeOffset(2026, 7, 9, 11, 0, 0, TimeSpan.Zero),
            () => Guid.Parse("11111111-1111-1111-1111-111111111111"),
            maxTextLength: 32);

        try
        {
            var result = await service.WriteAsync(
                new CrashReportRequest(
                    "test",
                    new InvalidOperationException(new string('x', 200)),
                    Context: new Dictionary<string, string?> { ["large"] = new string('y', 200) }),
                CancellationToken.None);
            var json = await File.ReadAllTextAsync(result.Path);

            Assert.Contains("...[truncated]", json, StringComparison.Ordinal);
            Assert.DoesNotContain(new string('x', 80), json, StringComparison.Ordinal);
            Assert.DoesNotContain(new string('y', 80), json, StringComparison.Ordinal);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task PruneAsync_RequiresAtLeastOneKeptReport()
    {
        var root = TestRoot();
        var service = new CrashReportService(new AppDataPaths(root));

        try
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => service.PruneAsync(0, CancellationToken.None));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static CrashReportRequest Request()
    {
        return new CrashReportRequest("test", new InvalidOperationException("boom"));
    }

    private static string TestRoot()
    {
        return Path.Combine(Path.GetTempPath(), "WinAiUsageBarTests", Guid.NewGuid().ToString("N"));
    }

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<string> WriteTimestampedFileAsync(
        string directory,
        string fileName,
        string content,
        DateTime lastWriteTimeUtc)
    {
        var path = Path.Combine(directory, fileName);
        await File.WriteAllTextAsync(path, content);
        File.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
        return path;
    }
}
