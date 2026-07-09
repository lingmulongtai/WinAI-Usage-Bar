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
    int maxBytesPerFile = 200_000,
    int redactionContextBytes = 4_096) : IDiagnosticsExportService
{
    private readonly Func<DateTimeOffset> nowProvider = nowProvider ?? (() => DateTimeOffset.Now);
    private readonly int maxFileBytes = Math.Max(1, maxBytesPerFile);
    private readonly int redactionContextByteCount = Math.Max(0, redactionContextBytes);

    public async Task<DiagnosticsExportResult> ExportAsync(CancellationToken cancellationToken)
    {
        paths.EnsureCreated();
        var createdAt = nowProvider();
        var sections = new List<string>();
        var exportTargets = CreateExportTargets();

        var export = CreateUniqueExportStream(createdAt);
        var exportPath = export.Path;
        await using var stream = export.Stream;
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        await writer.WriteLineAsync("WinAI Usage Bar Diagnostics Export").ConfigureAwait(false);
        await writer.WriteLineAsync($"CreatedAt: {createdAt:O}").ConfigureAwait(false);
        await writer.WriteLineAsync("RootDirectory: [omitted]").ConfigureAwait(false);
        await writer.WriteLineAsync("SecretsDirectory: [omitted]").ConfigureAwait(false);
        await writer.WriteLineAsync("Note: file contents are redacted at export time; local root paths and secret files are never included.").ConfigureAwait(false);
        await WriteManifestSummaryAsync(writer, exportTargets).ConfigureAwait(false);

        foreach (var target in exportTargets)
        {
            await AppendFileSectionAsync(writer, sections, target, cancellationToken).ConfigureAwait(false);
        }

        return new DiagnosticsExportResult(exportPath, createdAt, sections);
    }

    private IReadOnlyList<DiagnosticsExportTarget> CreateExportTargets()
    {
        return
        [
            new DiagnosticsExportTarget("config.json", "Configuration", paths.ConfigPath),
            new DiagnosticsExportTarget("snapshots.json", "Snapshot cache", paths.SnapshotsPath),
            new DiagnosticsExportTarget("history.ndjson", "Usage history", paths.HistoryPath),
            new DiagnosticsExportTarget("diagnostics.log", "Diagnostics log", paths.DiagnosticsLogPath)
        ];
    }

    private static async Task WriteManifestSummaryAsync(
        TextWriter writer,
        IReadOnlyList<DiagnosticsExportTarget> targets)
    {
        var statuses = targets
            .Select(target => new DiagnosticsExportTargetStatus(
                target.Label,
                target.Category,
                File.Exists(target.Path)))
            .ToList();
        var includedCount = statuses.Count(status => status.Exists);
        var missingCount = statuses.Count - includedCount;

        await writer.WriteLineAsync().ConfigureAwait(false);
        await writer.WriteLineAsync("--- manifest-summary ---").ConfigureAwait(false);
        await writer.WriteLineAsync($"Files: {statuses.Count}").ConfigureAwait(false);
        await writer.WriteLineAsync($"IncludedFiles: {includedCount}").ConfigureAwait(false);
        await writer.WriteLineAsync($"MissingFiles: {missingCount}").ConfigureAwait(false);
        await writer.WriteLineAsync("Categories: Configuration, Snapshot cache, Usage history, Diagnostics log").ConfigureAwait(false);
        await writer.WriteLineAsync("Entries:").ConfigureAwait(false);
        foreach (var status in statuses)
        {
            var state = status.Exists ? "included" : "missing";
            await writer.WriteLineAsync($"- {status.Label} | {status.Category} | {state}").ConfigureAwait(false);
        }

        await writer.WriteLineAsync("RedactionNotes:").ConfigureAwait(false);
        await writer.WriteLineAsync("- Local app data roots, user profile paths, account identifiers, provider scopes, secret references, API keys, tokens, cookies, and auth values are redacted or omitted.").ConfigureAwait(false);
        await writer.WriteLineAsync("- Secret store files and provider auth file contents are excluded.").ConfigureAwait(false);
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

    private sealed record DiagnosticsExportTarget(
        string Label,
        string Category,
        string Path);

    private sealed record DiagnosticsExportTargetStatus(
        string Label,
        string Category,
        bool Exists);

    private async Task AppendFileSectionAsync(
        TextWriter writer,
        ICollection<string> sections,
        DiagnosticsExportTarget target,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        sections.Add(target.Label);

        await writer.WriteLineAsync().ConfigureAwait(false);
        await writer.WriteLineAsync($"--- {target.Label} ---").ConfigureAwait(false);

        if (!File.Exists(target.Path))
        {
            await writer.WriteLineAsync("[missing]").ConfigureAwait(false);
            return;
        }

        var content = await ReadTailAsync(target.Path, cancellationToken).ConfigureAwait(false);
        await writer.WriteLineAsync(DiagnosticRedactor.RedactForSupportExport(content)).ConfigureAwait(false);
    }

    private async Task<string> ReadTailAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var prefix = string.Empty;

        if (stream.Length > maxFileBytes)
        {
            var visibleStartOffset = Math.Max(0, stream.Length - maxFileBytes);
            var readStartOffset = Math.Max(0, visibleStartOffset - redactionContextByteCount);
            var contextBytesUsed = visibleStartOffset - readStartOffset;

            stream.Seek(readStartOffset, SeekOrigin.Begin);
            prefix = contextBytesUsed > 0
                ? $"[truncated to last {maxFileBytes} bytes with {contextBytesUsed} bytes of redaction context]{Environment.NewLine}"
                : $"[truncated to last {maxFileBytes} bytes]{Environment.NewLine}";
        }

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        return prefix + content;
    }
}
