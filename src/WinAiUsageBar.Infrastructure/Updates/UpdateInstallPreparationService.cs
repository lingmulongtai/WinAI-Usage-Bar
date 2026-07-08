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
    string? BackupDirectory)
{
    public string? ResultPath { get; init; }
}

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

            var packageValidation = ValidatePackage(packagePath);
            if (!packageValidation.IsValid)
            {
                return Failure(
                    UpdateInstallPreparationStatus.InvalidPackage,
                    packageValidation.ErrorMessage);
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
            var resultPath = Path.Combine(root, "install-result.json");
            var script = CreateScript(
                packagePath,
                installDirectory,
                stagingDirectory,
                backupDirectory,
                resultPath,
                Math.Max(request.ProcessIdToWait, 0),
                request.RestartAfterInstall);
            await File.WriteAllTextAsync(scriptPath, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken)
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
                backupDirectory)
            {
                ResultPath = resultPath
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Failure(
                UpdateInstallPreparationStatus.Error,
                $"Update install preparation failed: {ex.Message}");
        }
    }

    private static PackageValidationResult ValidatePackage(string packagePath)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        var containsAppExecutable = false;
        foreach (var entry in archive.Entries)
        {
            var normalizedName = NormalizeArchiveEntryName(entry.FullName);
            if (IsUnsafeArchiveEntryName(normalizedName))
            {
                return PackageValidationResult.Invalid(
                    $"Update package contains an unsafe archive entry: {entry.FullName}");
            }

            if (string.Equals(
                normalizedName,
                "WinAiUsageBar.App.exe",
                StringComparison.OrdinalIgnoreCase))
            {
                containsAppExecutable = true;
            }
        }

        return containsAppExecutable
            ? PackageValidationResult.Valid()
            : PackageValidationResult.Invalid(
                "Update package must contain WinAiUsageBar.App.exe at the archive root.");
    }

    private static string NormalizeArchiveEntryName(string entryName)
    {
        return entryName.Replace('\\', '/');
    }

    private static bool IsUnsafeArchiveEntryName(string normalizedName)
    {
        if (string.IsNullOrWhiteSpace(normalizedName)
            || normalizedName.StartsWith("/", StringComparison.Ordinal))
        {
            return true;
        }

        var invalidFileNameCharacters = Path.GetInvalidFileNameChars();
        var segments = normalizedName.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 0
            || segments.Any(segment =>
                string.Equals(segment, ".", StringComparison.Ordinal)
                || string.Equals(segment, "..", StringComparison.Ordinal)
                || segment.IndexOfAny(invalidFileNameCharacters) >= 0);
    }

    private static string CreateScript(
        string packagePath,
        string installDirectory,
        string stagingDirectory,
        string backupDirectory,
        string resultPath,
        int processIdToWait,
        bool restartAfterInstall)
    {
        return $$"""
        {{UpdateInstallScriptMarkers.MarkerLine}}
        {{UpdateInstallScriptMarkers.VersionLine}}
        $ErrorActionPreference = 'Stop'

        $PackagePath = {{PowerShellLiteral(packagePath)}}
        $InstallDirectory = {{PowerShellLiteral(installDirectory)}}
        $StagingDirectory = {{PowerShellLiteral(stagingDirectory)}}
        $BackupDirectory = {{PowerShellLiteral(backupDirectory)}}
        $ResultPath = {{PowerShellLiteral(resultPath)}}
        $ProcessIdToWait = {{processIdToWait}}
        $RestartAfterInstall = ${{restartAfterInstall.ToString().ToLowerInvariant()}}

        function Write-InstallResult {
            param(
                [Parameter(Mandatory = $true)]
                [string]$Status,
                [Parameter(Mandatory = $true)]
                [string]$Message
            )

            $Result = [ordered]@{
                status = $Status
                message = $Message
                completedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
                packageFileName = [System.IO.Path]::GetFileName($PackagePath)
                installDirectory = $InstallDirectory
            }

            $Result | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $ResultPath -Encoding UTF8
        }

        function Restore-Backup {
            if (-not (Test-Path -LiteralPath $BackupDirectory -PathType Container)) {
                return
            }

            Get-ChildItem -LiteralPath $InstallDirectory -Force -ErrorAction SilentlyContinue | ForEach-Object {
                Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
            }

            Get-ChildItem -LiteralPath $BackupDirectory -Force | ForEach-Object {
                Copy-Item -LiteralPath $_.FullName -Destination $InstallDirectory -Recurse -Force
            }
        }

        try {
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
                Copy-Item -LiteralPath $_.FullName -Destination $BackupDirectory -Recurse -Force
            }

            try {
                Get-ChildItem -LiteralPath $InstallDirectory -Force | ForEach-Object {
                    Remove-Item -LiteralPath $_.FullName -Recurse -Force
                }

                Get-ChildItem -LiteralPath $StagingDirectory -Force | ForEach-Object {
                    Copy-Item -LiteralPath $_.FullName -Destination $InstallDirectory -Recurse -Force
                }

                $InstalledExe = Join-Path $InstallDirectory 'WinAiUsageBar.App.exe'
                if (-not (Test-Path -LiteralPath $InstalledExe -PathType Leaf)) {
                    throw "Installed update does not contain WinAiUsageBar.App.exe."
                }
            }
            catch {
                try {
                    Restore-Backup
                }
                catch {
                    Write-Warning "Update install failed and rollback also failed: $($_.Exception.Message)"
                }

                throw
            }

            Write-InstallResult -Status 'Succeeded' -Message 'Update installed successfully.'

            $RestartExe = Join-Path $InstallDirectory 'WinAiUsageBar.App.exe'
            if ($RestartAfterInstall -and (Test-Path -LiteralPath $RestartExe -PathType Leaf)) {
                Start-Process -FilePath $RestartExe -WorkingDirectory $InstallDirectory
            }
        }
        catch {
            $FailureMessage = $_.Exception.Message
            try {
                Write-InstallResult -Status 'Failed' -Message $FailureMessage
            }
            catch {
                Write-Warning "Update install failed and writing install-result.json also failed: $($_.Exception.Message)"
            }

            throw
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
            BackupDirectory: null)
        {
            ResultPath = null
        };
    }

    private sealed record PackageValidationResult(bool IsValid, string ErrorMessage)
    {
        public static PackageValidationResult Valid()
        {
            return new PackageValidationResult(true, string.Empty);
        }

        public static PackageValidationResult Invalid(string errorMessage)
        {
            return new PackageValidationResult(false, errorMessage);
        }
    }
}
