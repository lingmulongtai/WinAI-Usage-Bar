using WinAiUsageBar.App.ViewModels;
using WinAiUsageBar.Infrastructure.Diagnostics;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class CrashReportDetailViewModelTests
{
    [Fact]
    public void Constructor_FormatsAvailableDetailWithoutStackTraceOrRawSecrets()
    {
        var token = "gh" + "p_" + new string('a', 12);
        var detail = new CrashReportDetail(
            @"C:\Users\person\AppData\Roaming\WinAiUsageBar\crash-reports\crash-report-20260709-120000-11111111111111111111111111111111.json",
            "crash-report-20260709-120000-11111111111111111111111111111111.json",
            CrashReportDetailStatus.Available,
            "Crash report detail parsed.",
            new DateTimeOffset(2026, 7, 9, 12, 0, 0, TimeSpan.Zero),
            2048,
            "startup token=source-secret",
            "System.InvalidOperationException",
            "0.1.6",
            $"failed authorization: bearer {token}",
            MessageTruncated: true);

        var viewModel = new CrashReportDetailViewModel(detail);
        var visibleText = string.Join(
            Environment.NewLine,
            viewModel.MetadataLines.Append(viewModel.StatusText).Append(viewModel.MessageText));

        Assert.True(viewModel.IsAvailable);
        Assert.True(viewModel.HasMessage);
        Assert.Contains("Message preview: truncated", viewModel.MetadataLines);
        Assert.Contains("System.InvalidOperationException", visibleText, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", visibleText, StringComparison.Ordinal);
        Assert.DoesNotContain("source-secret", visibleText, StringComparison.Ordinal);
        Assert.DoesNotContain(token, visibleText, StringComparison.Ordinal);
        Assert.DoesNotContain("StackTrace", visibleText, StringComparison.Ordinal);
        Assert.DoesNotContain(@"C:\Users\person", visibleText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_FormatsUnavailableDetail()
    {
        var detail = new CrashReportDetail(
            "missing",
            "missing",
            CrashReportDetailStatus.Missing,
            "Crash report file is missing.",
            CreatedAt: null,
            SizeBytes: 0);

        var viewModel = new CrashReportDetailViewModel(detail);
        var visibleText = string.Join(Environment.NewLine, viewModel.MetadataLines);

        Assert.False(viewModel.IsAvailable);
        Assert.False(viewModel.HasMessage);
        Assert.Equal("No redacted message preview is available.", viewModel.MessageText);
        Assert.Contains("Status: Missing", visibleText, StringComparison.Ordinal);
        Assert.Contains("Created: n/a", visibleText, StringComparison.Ordinal);
    }
}
