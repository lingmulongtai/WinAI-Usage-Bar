using System.Text;
using WinAiUsageBar.Infrastructure.Security;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Infrastructure.Diagnostics;

public interface IDiagnosticsExportService
{
    Task<DiagnosticsExportResult> ExportAsync(CancellationToken cancellationToken);
}

public sealed record DiagnosticsExportResult(
    string Path,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> IncludedSections);

public sealed class DiagnosticsExportService(
    AppDataPaths paths,
    Func<DateTimeOffset>? nowProvider = null,
    int maxBytesPerFile = 200_000) : IDiagnosticsExportService
{
    private readonly Func<DateTimeOffset> nowProvider = nowProvider ?? (() => DateTimeOffset.Now);

    public async Task<DiagnosticsExportResult> ExportAsync(CancellationToken cancellationToken)
    {
        paths.EnsureCreated();
        var createdAt = nowProvider();
        var sections = new List<string>();

        var export = CreateUniqueExportStream(createdAt);
        var exportPath = export.Path;
        await using var stream = export.Stream;
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        await writer.WriteLineAsync("WinAI Usage Bar Diagnostics Export").ConfigureAwait(false);
        await writer.WriteLineAsync($"CreatedAt: {createdAt:O}").ConfigureAwait(false);
        await writer.WriteLineAsync($"RootDirectory: {paths.RootDirectory}").ConfigureAwait(false);
        await writer.WriteLineAsync("SecretsDirectory: [omitted]").ConfigureAwait(false);
        await writer.WriteLineAsync("Note: file contents are redacted at export time; secret files are never included.").ConfigureAwait(false);

        await AppendFileSectionAsync(writer, sections, "config.json", paths.ConfigPath, cancellationToken).ConfigureAwait(false);
        await AppendFileSectionAsync(writer, sections, "snapshots.json", paths.SnapshotsPath, cancellationToken).ConfigureAwait(false);
        await AppendFileSectionAsync(writer, sections, "history.ndjson", paths.HistoryPath, cancellationToken).ConfigureAwait(false);
        await AppendFileSectionAsync(writer, sections, "diagnostics.log", paths.DiagnosticsLogPath, cancellationToken).ConfigureAwait(false);

        return new DiagnosticsExportResult(exportPath, createdAt, sections);
    }

    private DiagnosticsExportStream CreateUniqueExportStream(DateTimeOffset createdAt)
    {
        var baseFileName = $"diagnostics-export-{createdAt:yyyyMMdd-HHmmss}";
        for (var suffix = 0; suffix < 1000; suffix++)
        {
            var fileName = suffix == 0
                ? $"{baseFileName}.txt"
                : $"{baseFileName}-{suffix}.txt";
            var path = Path.Combine(paths.DiagnosticsExportsDirectory, fileName);
            try
            {
                var stream = new FileStream(
                    path,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None);
                return new DiagnosticsExportStream(path, stream);
            }
            catch (IOException)
            {
            }
        }

        throw new IOException("Unable to reserve a unique diagnostics export path.");
    }

    private sealed record DiagnosticsExportStream(
        string Path,
        FileStream Stream);

    private async Task AppendFileSectionAsync(
        TextWriter writer,
        ICollection<string> sections,
        string label,
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        sections.Add(label);

        await writer.WriteLineAsync().ConfigureAwait(false);
        await writer.WriteLineAsync($"--- {label} ---").ConfigureAwait(false);

        if (!File.Exists(path))
        {
            await writer.WriteLineAsync("[missing]").ConfigureAwait(false);
            return;
        }

        var content = await ReadTailAsync(path, cancellationToken).ConfigureAwait(false);
        await writer.WriteLineAsync(DiagnosticRedactor.RedactForSupportExport(content)).ConfigureAwait(false);
    }

    private async Task<string> ReadTailAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var prefix = string.Empty;

        if (stream.Length > maxBytesPerFile)
        {
            stream.Seek(-maxBytesPerFile, SeekOrigin.End);
            prefix = $"[truncated to last {maxBytesPerFile} bytes]{Environment.NewLine}";
        }

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        return prefix + content;
    }
}
