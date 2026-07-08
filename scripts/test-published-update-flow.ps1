param(
    [string]$FromTag = "v0.1.2",
    [string]$ExpectedLatestTag = "",
    [string]$Repository = "lingmulongtai/WinAI-Usage-Bar",
    [string]$WorkDirectory = "",
    [switch]$Apply
)

$ErrorActionPreference = "Stop"

function ConvertTo-VersionFromTag {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TagName
    )

    $version = $TagName.Trim()
    if ($version.StartsWith("v", [StringComparison]::OrdinalIgnoreCase)) {
        $version = $version.Substring(1)
    }

    if ($version -notmatch "^[0-9]+\.[0-9]+\.[0-9]+$") {
        throw "Release tag must look like v0.1.2 or 0.1.2. Value: $TagName"
    }

    return $version
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

function Read-RequiredText {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Expected output file was not written: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw
}

function Assert-OutputContains {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text,
        [Parameter(Mandatory = $true)]
        [string]$Expected,
        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    if ($Text.IndexOf($Expected, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw "$Description did not contain '$Expected'."
    }
}

function Get-OutputValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text,
        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    $match = [regex]::Match($Text, "(?m)^$([regex]::Escape($Label)):\s*(.+)$")
    if (-not $match.Success) {
        throw "Could not find '${Label}:' in command output."
    }

    return $match.Groups[1].Value.Trim()
}

$fromVersion = ConvertTo-VersionFromTag -TagName $FromTag
$expectedLatestVersion = if ([string]::IsNullOrWhiteSpace($ExpectedLatestTag)) {
    ""
}
else {
    ConvertTo-VersionFromTag -TagName $ExpectedLatestTag
}
$supportsIsolatedAppData = [version]$fromVersion -ge [version]"0.1.3"

if ([string]::IsNullOrWhiteSpace($WorkDirectory)) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $WorkDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "WinAIUsageBar\published-update-$fromVersion-$timestamp"
}

$workRoot = Resolve-OrCreateDirectory -Path $WorkDirectory
$downloadsRoot = Resolve-OrCreateDirectory -Path (Join-Path $workRoot "downloads")
$installRoot = Resolve-OrCreateDirectory -Path (Join-Path $workRoot "install")
$logsRoot = Resolve-OrCreateDirectory -Path (Join-Path $workRoot "logs")
$appDataRoot = Resolve-OrCreateDirectory -Path (Join-Path $workRoot "appdata")

Assert-PathInside -ChildPath $downloadsRoot -ParentPath $workRoot -Description "Downloads directory"
Assert-PathInside -ChildPath $installRoot -ParentPath $workRoot -Description "Install directory"
Assert-PathInside -ChildPath $logsRoot -ParentPath $workRoot -Description "Logs directory"
Assert-PathInside -ChildPath $appDataRoot -ParentPath $workRoot -Description "App data directory"

$assetName = "WinAIUsageBar-$fromVersion-win-x64.zip"
$assetUri = "https://github.com/$Repository/releases/download/v$fromVersion/$assetName"
$downloadPath = Join-Path $downloadsRoot $assetName

Write-Host "Downloading $assetUri"
Invoke-WebRequest -Uri $assetUri -OutFile $downloadPath -UseBasicParsing

if (Test-Path -LiteralPath $installRoot) {
    Get-ChildItem -LiteralPath $installRoot -Force | Remove-Item -Recurse -Force
}

Expand-Archive -LiteralPath $downloadPath -DestinationPath $installRoot -Force
$appExe = Join-Path $installRoot "WinAiUsageBar.App.exe"
if (-not (Test-Path -LiteralPath $appExe -PathType Leaf)) {
    throw "Extracted release does not contain WinAiUsageBar.App.exe: $installRoot"
}

$oldOverride = [Environment]::GetEnvironmentVariable("WINAIUSAGEBAR_APPDATA", "Process")
[Environment]::SetEnvironmentVariable("WINAIUSAGEBAR_APPDATA", $appDataRoot, "Process")
try {
    $versionOut = Join-Path $logsRoot "version.out.txt"
    $versionErr = Join-Path $logsRoot "version.err.txt"
    $versionExit = Invoke-LoggedProcess `
        -FilePath $appExe `
        -Arguments @("--version") `
        -OutputPath $versionOut `
        -ErrorPath $versionErr
    $versionText = Read-RequiredText -Path $versionOut
    if ($versionExit -ne 0) {
        throw "Version check failed with exit code $versionExit. See $versionOut and $versionErr"
    }

    Assert-OutputContains -Text $versionText -Expected $fromVersion -Description "Extracted app version output"

    $checkOut = Join-Path $logsRoot "check.out.txt"
    $checkErr = Join-Path $logsRoot "check.err.txt"
    $checkExit = Invoke-LoggedProcess `
        -FilePath $appExe `
        -Arguments @("--check-for-updates") `
        -OutputPath $checkOut `
        -ErrorPath $checkErr
    $checkText = Read-RequiredText -Path $checkOut
    if ($checkExit -ne 0) {
        throw "Update check failed with exit code $checkExit. See $checkOut and $checkErr"
    }

    Assert-OutputContains -Text $checkText -Expected "Status: UpdateAvailable" -Description "Update check output"
    if (-not [string]::IsNullOrWhiteSpace($expectedLatestVersion)) {
        Assert-OutputContains -Text $checkText -Expected "Latest version: $expectedLatestVersion" -Description "Update check output"
    }

    if (-not $supportsIsolatedAppData) {
        $message = "Release v$fromVersion does not support WINAIUSAGEBAR_APPDATA, so download, prepare, and apply are skipped to avoid writing to normal app data."
        if ($Apply) {
            throw "$message Re-run without -Apply for a discovery-only check, or use a source release v0.1.3 or newer for isolated full-flow dogfooding."
        }

        Write-Warning $message
        Write-Host "Published update discovery passed."
        Write-Host "Work directory: $workRoot"
        Write-Host "Version output: $versionOut"
        Write-Host "Check output: $checkOut"
        return
    }

    $downloadOut = Join-Path $logsRoot "download.out.txt"
    $downloadErr = Join-Path $logsRoot "download.err.txt"
    $downloadExit = Invoke-LoggedProcess `
        -FilePath $appExe `
        -Arguments @("--download-update") `
        -OutputPath $downloadOut `
        -ErrorPath $downloadErr
    $downloadText = Read-RequiredText -Path $downloadOut
    if ($downloadExit -ne 0) {
        throw "Update download failed with exit code $downloadExit. See $downloadOut and $downloadErr"
    }

    Assert-OutputContains -Text $downloadText -Expected "Download status: Downloaded" -Description "Update download output"
    if (-not [string]::IsNullOrWhiteSpace($expectedLatestVersion)) {
        Assert-OutputContains -Text $downloadText -Expected "Latest version: $expectedLatestVersion" -Description "Update download output"
    }

    $packagePath = Get-OutputValue -Text $downloadText -Label "Package path"
    if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
        throw "Downloaded update package was not found: $packagePath"
    }

    Assert-PathInside -ChildPath $packagePath -ParentPath $appDataRoot -Description "Downloaded update package"

    $prepareOut = Join-Path $logsRoot "prepare.out.txt"
    $prepareErr = Join-Path $logsRoot "prepare.err.txt"
    $prepareExit = Invoke-LoggedProcess `
        -FilePath $appExe `
        -Arguments @(
            "--prepare-update-install",
            "--package",
            $packagePath,
            "--install-dir",
            $installRoot
        ) `
        -OutputPath $prepareOut `
        -ErrorPath $prepareErr
    $prepareText = Read-RequiredText -Path $prepareOut
    if ($prepareExit -ne 0) {
        throw "Prepare update install failed with exit code $prepareExit. See $prepareOut and $prepareErr"
    }

    Assert-OutputContains -Text $prepareText -Expected "Status: Prepared" -Description "Prepare update output"
    $scriptPath = Get-OutputValue -Text $prepareText -Label "Script"
    if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
        throw "Prepared update script was not found: $scriptPath"
    }

    Assert-PathInside -ChildPath $scriptPath -ParentPath $appDataRoot -Description "Prepared update script"

    Write-Host "Published update flow prepared successfully."
    Write-Host "Work directory: $workRoot"
    Write-Host "Version output: $versionOut"
    Write-Host "Check output: $checkOut"
    Write-Host "Download output: $downloadOut"
    Write-Host "Prepare output: $prepareOut"
    Write-Host "Prepared update script: $scriptPath"

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
                $scriptPath
            ) `
            -OutputPath $applyOut `
            -ErrorPath $applyErr

        if ($applyExit -ne 0) {
            throw "Prepared update script failed with exit code $applyExit. See $applyOut and $applyErr"
        }

        $updatedVersionOut = Join-Path $logsRoot "updated-version.out.txt"
        $updatedVersionErr = Join-Path $logsRoot "updated-version.err.txt"
        $updatedVersionExit = Invoke-LoggedProcess `
            -FilePath $appExe `
            -Arguments @("--version") `
            -OutputPath $updatedVersionOut `
            -ErrorPath $updatedVersionErr
        $updatedVersionText = Read-RequiredText -Path $updatedVersionOut
        if ($updatedVersionExit -ne 0) {
            throw "Updated version check failed with exit code $updatedVersionExit. See $updatedVersionOut and $updatedVersionErr"
        }

        if (-not [string]::IsNullOrWhiteSpace($expectedLatestVersion)) {
            Assert-OutputContains -Text $updatedVersionText -Expected $expectedLatestVersion -Description "Updated app version output"
        }

        Write-Host "Applied prepared update script to disposable install directory."
        Write-Host "Apply output: $applyOut"
        Write-Host "Updated version output: $updatedVersionOut"
    }
}
finally {
    [Environment]::SetEnvironmentVariable("WINAIUSAGEBAR_APPDATA", $oldOverride, "Process")
}
