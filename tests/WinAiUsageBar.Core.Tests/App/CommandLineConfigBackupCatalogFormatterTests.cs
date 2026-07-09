using WinAiUsageBar.App.Services;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Core.Tests.App;

public sealed class CommandLineConfigBackupCatalogFormatterTests
{
    [Fact]
    public void Format_PrintsAsciiFileMetadataForNonAsciiDirectoryPath()
    {
        var fileName = "config-backup-20260709-211016.json";
        var path = Path.Combine(@"C:\Temp", "\u5b66\u6821\u6cd5\u4eba", "config-backups", fileName);
        var result = new ConfigBackupCatalogResult(
            DirectoryPath: Path.GetDirectoryName(path)!,
            SearchPattern: "config-backup-*.json",
            Limit: 10,
            TotalCount: 1,
            TotalBytes: 7069,
            Backups:
            [
                new ConfigBackupCatalogEntry(
                    path,
                    fileName,
                    SizeBytes: 7069,
                    CreatedAt: new DateTimeOffset(2026, 7, 9, 21, 10, 16, TimeSpan.FromHours(9)),
                    ModifiedAt: new DateTimeOffset(2026, 7, 9, 21, 10, 16, TimeSpan.FromHours(9)))
            ]);

        var text = CommandLineConfigBackupCatalogFormatter.Format(result);

        Assert.Contains($"Path: {path}", text, StringComparison.Ordinal);
        Assert.Contains($"File name: {fileName}", text, StringComparison.Ordinal);
        Assert.Contains($"Relative path: {Path.Combine("config-backups", fileName)}", text, StringComparison.Ordinal);
    }
}
