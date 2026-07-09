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
        var userPath = @"C:\Users\person\OneDrive - School Name\WinAI\WinAiUsageBar.App.exe";
        var exception = new InvalidOperationException($"startup failed at {userPath} token=raw-secret {githubToken}");
        var context = new Dictionary<string, string?>
        {
            ["stage"] = "startup",
            ["path"] = @"C:\App\WinAiUsageBar.App.exe",
            ["userPath"] = userPath,
            ["patSecretName"] = "copilot-secret-ref",
            ["note"] = "Authorization: Bearer abc123"
        };

        try
        {
            var result = await service.WriteAsync(
                new CrashReportRequest(
                    "startup token=source-secret",
                    exception,
                    AppVersion: $"0.1.4+local {userPath}",
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
            Assert.Equal("startup token=[REDACTED]", rootElement.GetProperty("Source").GetString());
            Assert.Equal(typeof(InvalidOperationException).FullName, rootElement.GetProperty("ExceptionType").GetString());
            Assert.Contains("[LOCAL_PATH]", rootElement.GetProperty("AppVersion").GetString(), StringComparison.Ordinal);
            Assert.Contains("[REDACTED]", rootElement.GetProperty("Message").GetString(), StringComparison.Ordinal);
            Assert.Contains("[LOCAL_PATH]", rootElement.GetProperty("Message").GetString(), StringComparison.Ordinal);
            Assert.Contains("[REDACTED]", rootElement.GetProperty("StackTrace").GetString(), StringComparison.Ordinal);
            Assert.Contains("[LOCAL_PATH]", rootElement.GetProperty("StackTrace").GetString(), StringComparison.Ordinal);
            var contextElement = rootElement.GetProperty("Context");
            Assert.Equal(@"C:\App\WinAiUsageBar.App.exe", contextElement.GetProperty("path").GetString());
            Assert.Equal("[LOCAL_PATH]", contextElement.GetProperty("userPath").GetString());
            Assert.Equal("startup", contextElement.GetProperty("stage").GetString());
            Assert.Equal("Authorization: Bearer [REDACTED]", contextElement.GetProperty("note").GetString());
            Assert.Contains("[REDACTED]", contextElement.GetRawText(), StringComparison.Ordinal);
            Assert.DoesNotContain(@"C:\\Users", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("person", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("School Name", json, StringComparison.OrdinalIgnoreCase);
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
    public async Task ListAsync_ReturnsNewestMatchedTopLevelReportsAndHandlesUnreadableMetadata()
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
            Assert.False(reports[0].MetadataAvailable);
            Assert.Equal("Metadata unreadable.", reports[0].MetadataStatus);
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
    public async Task ListAsync_ReadsOnlySafeMetadataFromValidReports()
    {
        var root = TestRoot();
        var paths = new AppDataPaths(root);
        paths.EnsureCreated();
        var validReportPath = await WriteTimestampedFileAsync(
            paths.CrashReportsDirectory,
            "crash-report-20260709-120000-33333333333333333333333333333333.json",
            """
            {
              "CreatedAt": "2026-07-09T12:00:00+00:00",
              "Source": "startup token=raw-secret",
              "ExceptionType": "System.InvalidOperationException",
              "Message": "message must stay out of metadata raw-secret",
              "StackTrace": "stack trace must stay out of metadata raw-secret",
              "AppVersion": "0.1.4+local"
            }
            """,
            new DateTime(2026, 7, 9, 12, 1, 0, DateTimeKind.Utc));
        var service = new CrashReportService(paths);

        try
        {
            var reports = await service.ListAsync(limit: 5, CancellationToken.None);
            var report = Assert.Single(reports);
            var visibleMetadata = string.Join(
                Environment.NewLine,
                report.Source,
                report.ExceptionType,
                report.AppVersion,
                report.MetadataStatus);

            Assert.Equal(validReportPath, report.Path);
            Assert.True(report.MetadataAvailable);
            Assert.Equal(new DateTimeOffset(2026, 7, 9, 12, 0, 0, TimeSpan.Zero), report.CreatedAt);
            Assert.Equal("startup [REDACTED]", report.Source);
            Assert.Equal("System.InvalidOperationException", report.ExceptionType);
            Assert.Equal("0.1.4+local", report.AppVersion);
            Assert.Equal("Metadata parsed.", report.MetadataStatus);
            Assert.DoesNotContain("raw-secret", visibleMetadata, StringComparison.Ordinal);
            Assert.DoesNotContain("message must stay out", visibleMetadata, StringComparison.Ordinal);
            Assert.DoesNotContain("stack trace must stay out", visibleMetadata, StringComparison.Ordinal);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task ReadDetailAsync_ReturnsRedactedBoundedMessageWithoutStackTrace()
    {
        var root = TestRoot();
        var paths = new AppDataPaths(root);
        paths.EnsureCreated();
        var path = GeneratedReportPath(paths, "20260709-121500", "44444444444444444444444444444444");
        var githubToken = "gh" + "p_" + new string('a', 12);
        var longSuffix = new string('x', 1_200);
        var report = new
        {
            CreatedAt = "2026-07-09T12:15:00+00:00",
            Source = "startup token=source-secret",
            ExceptionType = "System.InvalidOperationException",
            AppVersion = @"0.1.6 C:\Users\person\AppData\Local\WinAI",
            Message = $@"failed at C:\Users\person\Tools\codex.cmd authorization: bearer message-secret {githubToken} {longSuffix}",
            StackTrace = "stack trace must not be displayed stack-secret"
        };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report));
        var service = new CrashReportService(paths);

        try
        {
            var detail = await service.ReadDetailAsync(path, CancellationToken.None);
            var visibleText = string.Join(
                Environment.NewLine,
                detail.Source,
                detail.ExceptionType,
                detail.AppVersion,
                detail.MessagePreview,
                detail.StatusMessage);

            Assert.Equal(CrashReportDetailStatus.Available, detail.Status);
            Assert.Equal(Path.GetFileName(path), detail.FileName);
            Assert.Equal(new DateTimeOffset(2026, 7, 9, 12, 15, 0, TimeSpan.Zero), detail.CreatedAt);
            Assert.True(detail.SizeBytes > 0);
            Assert.True(detail.MessageTruncated);
            Assert.Contains("[REDACTED]", visibleText, StringComparison.Ordinal);
            Assert.Contains("[LOCAL_PATH]", visibleText, StringComparison.Ordinal);
            Assert.Contains("System.InvalidOperationException", visibleText, StringComparison.Ordinal);
            Assert.DoesNotContain("source-secret", visibleText, StringComparison.Ordinal);
            Assert.DoesNotContain("message-secret", visibleText, StringComparison.Ordinal);
            Assert.DoesNotContain(githubToken, visibleText, StringComparison.Ordinal);
            Assert.DoesNotContain("stack-secret", visibleText, StringComparison.Ordinal);
            Assert.DoesNotContain("StackTrace", visibleText, StringComparison.Ordinal);
            Assert.DoesNotContain(@"C:\Users", visibleText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("person", visibleText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task ReadDetailAsync_ReturnsMissingForDeletedGeneratedReport()
    {
        var root = TestRoot();
        var paths = new AppDataPaths(root);
        paths.EnsureCreated();
        var path = GeneratedReportPath(paths, "20260709-123000", "55555555555555555555555555555555");
        var service = new CrashReportService(paths);

        try
        {
            var detail = await service.ReadDetailAsync(path, CancellationToken.None);

            Assert.Equal(CrashReportDetailStatus.Missing, detail.Status);
            Assert.Equal("Crash report file is missing.", detail.StatusMessage);
            Assert.Equal(Path.GetFileName(path), detail.FileName);
            Assert.Null(detail.MessagePreview);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task ReadDetailAsync_ReturnsMalformedForInvalidJson()
    {
        var root = TestRoot();
        var paths = new AppDataPaths(root);
        paths.EnsureCreated();
        var path = GeneratedReportPath(paths, "20260709-124500", "66666666666666666666666666666666");
        await File.WriteAllTextAsync(path, "{ malformed");
        var service = new CrashReportService(paths);

        try
        {
            var detail = await service.ReadDetailAsync(path, CancellationToken.None);

            Assert.Equal(CrashReportDetailStatus.Malformed, detail.Status);
            Assert.Equal("Crash report JSON is malformed.", detail.StatusMessage);
            Assert.Equal(Path.GetFileName(path), detail.FileName);
            Assert.Null(detail.MessagePreview);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task ReadDetailAsync_ReturnsTooLargeForOversizedReport()
    {
        var root = TestRoot();
        var paths = new AppDataPaths(root);
        paths.EnsureCreated();
        var path = GeneratedReportPath(paths, "20260709-130000", "77777777777777777777777777777777");
        await File.WriteAllTextAsync(path, new string('x', 256_001));
        var service = new CrashReportService(paths);

        try
        {
            var detail = await service.ReadDetailAsync(path, CancellationToken.None);

            Assert.Equal(CrashReportDetailStatus.TooLarge, detail.Status);
            Assert.Equal("Crash report is too large to preview safely.", detail.StatusMessage);
            Assert.Equal(256_001, detail.SizeBytes);
            Assert.Null(detail.MessagePreview);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task ReadDetailAsync_ReturnsUnavailableForLockedReport()
    {
        var root = TestRoot();
        var paths = new AppDataPaths(root);
        paths.EnsureCreated();
        var path = GeneratedReportPath(paths, "20260709-131500", "88888888888888888888888888888888");
        await File.WriteAllTextAsync(path, """{"Message":"locked"}""");
        var service = new CrashReportService(paths);

        try
        {
            await using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            var detail = await service.ReadDetailAsync(path, CancellationToken.None);

            Assert.Equal(CrashReportDetailStatus.Unavailable, detail.Status);
            Assert.Equal("Crash report file is currently unavailable.", detail.StatusMessage);
            Assert.Equal(Path.GetFileName(path), detail.FileName);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task ReadDetailAsync_RejectsUnsafeOrNestedReportPath()
    {
        var root = TestRoot();
        var paths = new AppDataPaths(root);
        paths.EnsureCreated();
        var nestedDirectory = Path.Combine(paths.CrashReportsDirectory, "nested");
        Directory.CreateDirectory(nestedDirectory);
        var nestedPath = Path.Combine(
            nestedDirectory,
            "crash-report-20260709-133000-99999999999999999999999999999999.json");
        await File.WriteAllTextAsync(nestedPath, """{"Message":"nested"}""");
        var service = new CrashReportService(paths);

        try
        {
            var detail = await service.ReadDetailAsync(nestedPath, CancellationToken.None);

            Assert.Equal(CrashReportDetailStatus.InvalidPath, detail.Status);
            Assert.Contains("app-generated top-level", detail.StatusMessage, StringComparison.Ordinal);
            Assert.Null(detail.MessagePreview);
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

    private static string GeneratedReportPath(AppDataPaths paths, string timestamp, string id)
    {
        return Path.Combine(paths.CrashReportsDirectory, $"crash-report-{timestamp}-{id}.json");
    }
}
