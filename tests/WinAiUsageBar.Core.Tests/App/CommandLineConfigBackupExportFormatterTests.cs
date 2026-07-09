using WinAiUsageBar.App.Services;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class CommandLineConfigBackupExportFormatterTests
{
    [Fact]
    public void Format_PrintsAsciiFileMetadataForNonAsciiDirectoryPath()
    {
        var fileName = "config-backup-20260709-211016.json";
        var path = Path.Combine(@"C:\Temp", "\u5b66\u6821\u6cd5\u4eba", "config-backups", fileName);
        var result = new ConfigBackupResult(
            path,
            new DateTimeOffset(2026, 7, 9, 21, 10, 16, TimeSpan.FromHours(9)));

        var text = CommandLineConfigBackupExportFormatter.Format(result);

        Assert.Contains("Config backup export", text, StringComparison.Ordinal);
        Assert.Contains($"Path: {path}", text, StringComparison.Ordinal);
        Assert.Contains($"File name: {fileName}", text, StringComparison.Ordinal);
        Assert.Contains($"Relative path: {Path.Combine("config-backups", fileName)}", text, StringComparison.Ordinal);
    }
}
