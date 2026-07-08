param(
    [string]$AppExePath = "",
    [string]$PackagePath = "",
    [string]$InstallDirectory = "",
    [string]$WorkDirectory = "",
    [switch]$Apply
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

if ([string]::IsNullOrWhiteSpace($AppExePath)) {
    $AppExePath = Join-Path $repoRoot "artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe"
}

if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    throw "PackagePath is required. Pass a release zip with -PackagePath."
}

if ([string]::IsNullOrWhiteSpace($WorkDirectory)) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $WorkDirectory = Join-Path $repoRoot "artifacts\update-dogfood\prepared-update-$timestamp"
}

function Resolve-ExistingFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Description was not found: $Path"
    }

    return (Resolve-Path -LiteralPath $Path).Path
}

function Resolve-OrCreateDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    New-Item -ItemType Directory -Force -Path $Path | Out-Null
    return (Resolve-Path -LiteralPath $Path).Path
}

function Assert-PathInside {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ChildPath,
        [Parameter(Mandatory = $true)]
        [string]$ParentPath,
        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $child = [System.IO.Path]::GetFullPath($ChildPath)
    $parent = [System.IO.Path]::GetFullPath($ParentPath)
    $parentWithSeparator = $parent.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

    if (-not ($child.Equals($parent, [StringComparison]::OrdinalIgnoreCase) -or
        $child.StartsWith($parentWithSeparator, [StringComparison]::OrdinalIgnoreCase))) {
        throw "$Description must stay under the work directory. Path: $child Work directory: $parent"
    }
}

function Test-PathInside {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ChildPath,
        [Parameter(Mandatory = $true)]
        [string]$ParentPath
    )

    try {
        $child = [System.IO.Path]::GetFullPath($ChildPath)
        $parent = [System.IO.Path]::GetFullPath($ParentPath)
        $parentWithSeparator = $parent.TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

        return $child.Equals($parent, [StringComparison]::OrdinalIgnoreCase) -or
            $child.StartsWith($parentWithSeparator, [StringComparison]::OrdinalIgnoreCase)
    }
    catch {
        return $false
    }
}

function Resolve-PreparedResultPath {
    param(
        [string]$CandidatePath,
        [Parameter(Mandatory = $true)]
        [string]$ScriptPath,
        [Parameter(Mandatory = $true)]
        [string]$AppDataRoot
    )

    $fallback = Join-Path (Split-Path -Parent $ScriptPath) "install-result.json"
    if ([string]::IsNullOrWhiteSpace($CandidatePath) -or
        $CandidatePath.Equals("n/a", [StringComparison]::OrdinalIgnoreCase)) {
        return $fallback
    }

    try {
        $candidateFull = [System.IO.Path]::GetFullPath($CandidatePath)
        $candidateDirectory = Split-Path -Parent $candidateFull
        if ([string]::IsNullOrWhiteSpace($candidateDirectory) -or
            -not (Test-Path -LiteralPath $candidateDirectory -PathType Container) -or
            -not (Test-PathInside -ChildPath $candidateFull -ParentPath $AppDataRoot)) {
            return $fallback
        }

        return $candidateFull
    }
    catch {
        return $fallback
    }
}

function Assert-ValidationLogMetadata {
    param(
        [Parameter(Mandatory = $true)]
        [object]$InstallResult,
        [Parameter(Mandatory = $true)]
        [string]$ResultPath
    )

    $resultDirectory = Split-Path -Parent ([System.IO.Path]::GetFullPath($ResultPath))
    $expectedLogs = @(
        @{ PathProperty = "validationOutputPath"; BytesProperty = "validationOutputBytes"; FileName = "validation.out.txt" },
        @{ PathProperty = "validationErrorPath"; BytesProperty = "validationErrorBytes"; FileName = "validation.err.txt" }
    )

    foreach ($expectedLog in $expectedLogs) {
        $pathProperty = $InstallResult.PSObject.Properties[$expectedLog.PathProperty]
        $bytesProperty = $InstallResult.PSObject.Properties[$expectedLog.BytesProperty]
        if ($null -eq $pathProperty -or [string]::IsNullOrWhiteSpace([string]$pathProperty.Value)) {
            throw "install-result.json did not include $($expectedLog.PathProperty)."
        }

        $logPath = [System.IO.Path]::GetFullPath([string]$pathProperty.Value)
        $expectedPath = [System.IO.Path]::GetFullPath((Join-Path $resultDirectory $expectedLog.FileName))
        if (-not $logPath.Equals($expectedPath, [StringComparison]::OrdinalIgnoreCase)) {
            throw "$($expectedLog.PathProperty) must stay beside install-result.json. Path: $logPath Expected: $expectedPath"
        }

        if (-not (Test-Path -LiteralPath $logPath -PathType Leaf)) {
            throw "Validation log was not written: $logPath"
        }

        if ($null -eq $bytesProperty -or [int64]$bytesProperty.Value -lt 0) {
            throw "install-result.json did not include a non-negative $($expectedLog.BytesProperty)."
        }
    }
}

function Test-ScriptWritesInstallResult {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ScriptPath
    )

    if (-not (Test-Path -LiteralPath $ScriptPath -PathType Leaf)) {
        return $false
    }

    $scriptText = Get-Content -LiteralPath $ScriptPath -Raw
    return $scriptText.IndexOf("Write-InstallResult", [StringComparison]::OrdinalIgnoreCase) -ge 0
}

function Quote-ProcessArgument {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    if ($Value.IndexOfAny([char[]]@(" ", "`t", "`r", "`n", '"')) -lt 0) {
        return $Value
    }

    return '"' + $Value.Replace('"', '\"') + '"'
}

function Join-ProcessArguments {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    return ($Arguments | ForEach-Object { Quote-ProcessArgument $_ }) -join " "
}

function Invoke-LoggedProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$OutputPath,
        [Parameter(Mandatory = $true)]
        [string]$ErrorPath
    )

    Remove-Item -LiteralPath $OutputPath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $ErrorPath -Force -ErrorAction SilentlyContinue

    $argumentText = Join-ProcessArguments -Arguments $Arguments
    $process = Start-Process `
        -FilePath $FilePath `
        -ArgumentList $argumentText `
        -Wait `
        -PassThru `
        -RedirectStandardOutput $OutputPath `
        -RedirectStandardError $ErrorPath `
        -WindowStyle Hidden

    return $process.ExitCode
}

$appExe = Resolve-ExistingFile -Path $AppExePath -Description "App executable"
$package = Resolve-ExistingFile -Path $PackagePath -Description "Update package"
$workRoot = Resolve-OrCreateDirectory -Path $WorkDirectory
$logsRoot = Resolve-OrCreateDirectory -Path (Join-Path $workRoot "logs")
$appDataRoot = Resolve-OrCreateDirectory -Path (Join-Path $workRoot "appdata")

if ([string]::IsNullOrWhiteSpace($InstallDirectory)) {
    $InstallDirectory = Join-Path $workRoot "install"
    $appRoot = Split-Path -Parent $appExe
    if (Test-Path -LiteralPath $InstallDirectory) {
        Remove-Item -LiteralPath $InstallDirectory -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $InstallDirectory | Out-Null
    Get-ChildItem -LiteralPath $appRoot -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $InstallDirectory -Recurse -Force
    }
}

$installRoot = Resolve-OrCreateDirectory -Path $InstallDirectory
Assert-PathInside -ChildPath $installRoot -ParentPath $workRoot -Description "Install directory"

$installedExe = Join-Path $installRoot "WinAiUsageBar.App.exe"
if (-not (Test-Path -LiteralPath $installedExe -PathType Leaf)) {
    throw "Install directory must contain WinAiUsageBar.App.exe: $installRoot"
}

$oldOverride = [Environment]::GetEnvironmentVariable("WINAIUSAGEBAR_APPDATA", "Process")
[Environment]::SetEnvironmentVariable("WINAIUSAGEBAR_APPDATA", $appDataRoot, "Process")
try {
    $prepareOut = Join-Path $logsRoot "prepare.out.txt"
    $prepareErr = Join-Path $logsRoot "prepare.err.txt"
    $prepareExit = Invoke-LoggedProcess `
        -FilePath $appExe `
        -Arguments @(
            "--prepare-update-install",
            "--package",
            $package,
            "--install-dir",
            $installRoot
        ) `
        -OutputPath $prepareOut `
        -ErrorPath $prepareErr

    $prepareText = Get-Content -LiteralPath $prepareOut -Raw
    if ($prepareExit -ne 0) {
        throw "Prepare update install failed with exit code $prepareExit. See $prepareOut and $prepareErr"
    }

    if ($prepareText.IndexOf("Status: Prepared", [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw "Prepare output did not report Status: Prepared. See $prepareOut"
    }

    $script = $null
    $scriptMatch = [regex]::Match($prepareText, "(?m)^Script:\s*(.+)$")
    if ($scriptMatch.Success) {
        $scriptPath = $scriptMatch.Groups[1].Value.Trim()
        if (Test-Path -LiteralPath $scriptPath -PathType Leaf) {
            $script = (Resolve-Path -LiteralPath $scriptPath).Path
        }
    }

    $resultPath = ""
    $resultMatch = [regex]::Match($prepareText, "(?m)^Result:\s*(.+)$")
    if ($resultMatch.Success) {
        $candidateResultPath = $resultMatch.Groups[1].Value.Trim()
        if (-not $candidateResultPath.Equals("n/a", [StringComparison]::OrdinalIgnoreCase)) {
            $resultPath = $candidateResultPath
        }
    }

    if ([string]::IsNullOrWhiteSpace($script)) {
        $script = Get-ChildItem -LiteralPath (Join-Path $appDataRoot "updates") -Recurse -Filter "apply-update.ps1" -File |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -ExpandProperty FullName -First 1
    }

    if ([string]::IsNullOrWhiteSpace($script) -or -not (Test-Path -LiteralPath $script -PathType Leaf)) {
        throw "Prepared update script was not found under isolated app data. See $prepareOut"
    }

    Assert-PathInside -ChildPath $script -ParentPath $appDataRoot -Description "Prepared update script"
    $resultPath = Resolve-PreparedResultPath -CandidatePath $resultPath -ScriptPath $script -AppDataRoot $appDataRoot

    Assert-PathInside -ChildPath $resultPath -ParentPath $appDataRoot -Description "Prepared update result"

    Write-Host "Prepared update script: $script"
    Write-Host "Expected install result: $resultPath"
    Write-Host "Prepare output: $prepareOut"
    Write-Host "Install directory: $installRoot"

    if ($Apply) {
        Assert-PathInside -ChildPath $installRoot -ParentPath $workRoot -Description "Apply install directory"

        $applyOut = Join-Path $logsRoot "apply.out.txt"
        $applyErr = Join-Path $logsRoot "apply.err.txt"
        $applyExit = Invoke-LoggedProcess `
            -FilePath "powershell.exe" `
            -Arguments @(
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                $script
            ) `
            -OutputPath $applyOut `
            -ErrorPath $applyErr

        if ($applyExit -ne 0) {
            throw "Prepared update script failed with exit code $applyExit. See $applyOut and $applyErr"
        }

        if (-not (Test-Path -LiteralPath $installedExe -PathType Leaf)) {
            throw "Applied update did not leave WinAiUsageBar.App.exe in the install directory."
        }

        if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
            if (Test-ScriptWritesInstallResult -ScriptPath $script) {
                throw "Prepared update script did not write install-result.json: $resultPath"
            }

            Write-Warning "Prepared update script did not write install-result.json. The source executable may predate install-result reporting."
        }
        else {
            $installResult = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
            if ($installResult.status -ne "Succeeded") {
                throw "install-result.json did not report success. Status: $($installResult.status)"
            }

            if ($installResult.validationStatus -ne "Passed") {
                throw "install-result.json did not report passed post-install validation. Validation status: $($installResult.validationStatus)"
            }

            Assert-ValidationLogMetadata -InstallResult $installResult -ResultPath $resultPath
        }

        Write-Host "Applied prepared update script to disposable install directory."
        Write-Host "Install result: $resultPath"
        Write-Host "Apply output: $applyOut"
    }
}
finally {
    [Environment]::SetEnvironmentVariable("WINAIUSAGEBAR_APPDATA", $oldOverride, "Process")
}
