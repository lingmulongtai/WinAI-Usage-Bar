using System.Text.Json;
using WinAiUsageBar.Core.Configuration;

namespace WinAiUsageBar.Infrastructure.Storage;

internal sealed class ConfigBackupFileWriter(
    JsonSerializerOptions options)
{
    public async Task<string> WriteAsync(
        AppDataPaths paths,
        string baseFileNameWithoutExtension,
        AppConfig config,
        CancellationToken cancellationToken)
    {
        paths.EnsureCreated();

        for (var suffix = 0; suffix < 1000; suffix++)
        {
            var backupPath = BuildBackupPath(
                paths.ConfigBackupsDirectory,
                baseFileNameWithoutExtension,
                suffix);
            if (File.Exists(backupPath))
            {
                continue;
            }

            var tempPath = CreateTempPath(paths.ConfigBackupsDirectory, baseFileNameWithoutExtension);
            try
            {
                await using (var stream = new FileStream(
                    tempPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                    await JsonSerializer.SerializeAsync(stream, config, options, cancellationToken)
                        .ConfigureAwait(false);
                }

                File.Move(tempPath, backupPath, overwrite: false);
                return backupPath;
            }
            catch (IOException) when (File.Exists(backupPath))
            {
                // Another operation won this filename; try the next suffix.
            }
            finally
            {
                TryDeleteTempFile(tempPath);
            }
        }

        throw new IOException("Unable to reserve a unique config backup path.");
    }

    private static string BuildBackupPath(
        string directory,
        string baseFileNameWithoutExtension,
        int suffix)
    {
        var fileName = suffix == 0
            ? $"{baseFileNameWithoutExtension}.json"
            : $"{baseFileNameWithoutExtension}-{suffix}.json";
        return Path.Combine(directory, fileName);
    }

    private static string CreateTempPath(
        string directory,
        string baseFileNameWithoutExtension)
    {
        return Path.Combine(
            directory,
            $"{baseFileNameWithoutExtension}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");
    }

    private static void TryDeleteTempFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch
        {
            // Keep the original write failure visible; temp cleanup is best-effort.
        }
    }
}
