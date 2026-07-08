using System.IO.Compression;
using System.Text;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Infrastructure.Updates;

public interface IUpdateInstallPreparationService
{
    Task<UpdateInstallPreparationResult> PrepareAsync(
        UpdateInstallPreparationRequest request,
        CancellationToken cancellationToken);
}

public sealed record UpdateInstallPreparationRequest(
    string PackagePath,
    string InstallDirectory,
    int ProcessIdToWait,
    bool RestartAfterInstall);

public sealed record UpdateInstallPreparationResult(
    UpdateInstallPreparationStatus Status,
    string Message,
    string? ScriptPath,
    string? Command,
    string? PackagePath,
    string? InstallDirectory,
    string? StagingDirectory,
    string? BackupDirectory);

public enum UpdateInstallPreparationStatus
{
    Prepared,
    InvalidPackage,
    InvalidInstallDirectory,
    Error
}

public sealed class UpdateInstallPreparationService(
    AppDataPaths paths,
    Func<DateTimeOffset>? nowProvider = null) : IUpdateInstallPreparationService
{
    private readonly Func<DateTimeOffset> nowProvider = nowProvider ?? (() => DateTimeOffset.Now);

    public async Task<UpdateInstallPreparationResult> PrepareAsync(
        UpdateInstallPreparationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var packagePath = Path.GetFullPath(request.PackagePath);
            if (!File.Exists(packagePath)
                || !string.Equals(Path.GetExtension(packagePath), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                return Failure(
                    UpdateInstallPreparationStatus.InvalidPackage,
                    "Update package must be an existing .zip file.");
            }

            if (!PackageContainsAppExecutable(packagePath))
            {
                return Failure(
                    UpdateInstallPreparationStatus.InvalidPackage,
                    "Update package must contain WinAiUsageBar.App.exe at the archive root.");
            }

            var installDirectory = Path.GetFullPath(request.InstallDirectory);
            if (!Directory.Exists(installDirectory))
            {
                return Failure(
                    UpdateInstallPreparationStatus.InvalidInstallDirectory,
                    "Install directory was not found.");
            }

            var installedExe = Path.Combine(installDirectory, "WinAiUsageBar.App.exe");
            if (!File.Exists(installedExe))
            {
                return Failure(
                    UpdateInstallPreparationStatus.InvalidInstallDirectory,
                    "Install directory must contain WinAiUsageBar.App.exe.");
            }

            paths.EnsureCreated();
            var preparedAt = nowProvider();
            var root = Path.Combine(
                paths.UpdatesDirectory,
                $"install-{preparedAt:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
            var stagingDirectory = Path.Combine(root, "staging");
            var backupDirectory = Path.Combine(root, "backup");
            Directory.CreateDirectory(root);

            var scriptPath = Path.Combine(root, "apply-update.ps1");
            var script = CreateScript(
                packagePath,
                installDirectory,
                stagingDirectory,
                backupDirectory,
                Math.Max(request.ProcessIdToWait, 0),
                request.RestartAfterInstall);
            await File.WriteAllTextAsync(scriptPath, script, new UTF8Encoding(false), cancellationToken)
                .ConfigureAwait(false);

            var command = $"powershell -ExecutionPolicy Bypass -File \"{scriptPath}\"";
            return new UpdateInstallPreparationResult(
                UpdateInstallPreparationStatus.Prepared,
                "Update install script prepared. Close WinAI Usage Bar before running it.",
                scriptPath,
                command,
                packagePath,
                installDirectory,
                stagingDirectory,
                backupDirectory);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Failure(
                UpdateInstallPreparationStatus.Error,
                $"Update install preparation failed: {ex.Message}");
        }
    }

    private static bool PackageContainsAppExecutable(string packagePath)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        return archive.Entries.Any(entry =>
            string.Equals(
                entry.FullName.Replace('\\', '/'),
                "WinAiUsageBar.App.exe",
                StringComparison.OrdinalIgnoreCase));
    }

    private static string CreateScript(
        string packagePath,
        string installDirectory,
        string stagingDirectory,
        string backupDirectory,
        int processIdToWait,
        bool restartAfterInstall)
    {
        return $$"""
        $ErrorActionPreference = 'Stop'

        $PackagePath = {{PowerShellLiteral(packagePath)}}
        $InstallDirectory = {{PowerShellLiteral(installDirectory)}}
        $StagingDirectory = {{PowerShellLiteral(stagingDirectory)}}
        $BackupDirectory = {{PowerShellLiteral(backupDirectory)}}
        $ProcessIdToWait = {{processIdToWait}}
        $RestartAfterInstall = ${{restartAfterInstall.ToString().ToLowerInvariant()}}

        if ($ProcessIdToWait -gt 0) {
            try {
                Wait-Process -Id $ProcessIdToWait -ErrorAction SilentlyContinue
            }
            catch {
            }
        }

        if (-not (Test-Path -LiteralPath $PackagePath -PathType Leaf)) {
            throw "Update package was not found: $PackagePath"
        }

        if (-not (Test-Path -LiteralPath $InstallDirectory -PathType Container)) {
            throw "Install directory was not found: $InstallDirectory"
        }

        Remove-Item -LiteralPath $StagingDirectory -Recurse -Force -ErrorAction SilentlyContinue
        New-Item -ItemType Directory -Force -Path $StagingDirectory | Out-Null
        Expand-Archive -LiteralPath $PackagePath -DestinationPath $StagingDirectory -Force

        $StagedExe = Join-Path $StagingDirectory 'WinAiUsageBar.App.exe'
        if (-not (Test-Path -LiteralPath $StagedExe -PathType Leaf)) {
            throw "Staged update does not contain WinAiUsageBar.App.exe."
        }

        Remove-Item -LiteralPath $BackupDirectory -Recurse -Force -ErrorAction SilentlyContinue
        New-Item -ItemType Directory -Force -Path $BackupDirectory | Out-Null

        Get-ChildItem -LiteralPath $InstallDirectory -Force | ForEach-Object {
            Move-Item -LiteralPath $_.FullName -Destination (Join-Path $BackupDirectory $_.Name) -Force
        }

        Get-ChildItem -LiteralPath $StagingDirectory -Force | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination $InstallDirectory -Recurse -Force
        }

        $RestartExe = Join-Path $InstallDirectory 'WinAiUsageBar.App.exe'
        if ($RestartAfterInstall -and (Test-Path -LiteralPath $RestartExe -PathType Leaf)) {
            Start-Process -FilePath $RestartExe -WorkingDirectory $InstallDirectory
        }
        """;
    }

    private static string PowerShellLiteral(string value)
    {
        return $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    }

    private static UpdateInstallPreparationResult Failure(
        UpdateInstallPreparationStatus status,
        string message)
    {
        return new UpdateInstallPreparationResult(
            status,
            message,
            ScriptPath: null,
            Command: null,
            PackagePath: null,
            InstallDirectory: null,
            StagingDirectory: null,
            BackupDirectory: null);
    }
}
