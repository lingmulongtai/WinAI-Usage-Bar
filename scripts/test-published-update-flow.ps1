param(
    [string]$FromTag = "v0.1.2",
    [string]$ExpectedLatestTag = "",
    [string]$Repository = "lingmulongtai/WinAI-Usage-Bar",
    [string]$WorkDirectory = "",
    [switch]$Apply,
    [switch]$StartupPolicy,
    [switch]$AssertNormalAppDataUnchanged
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

function Write-ValidationLogMetadataStatus {
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
    $missing = @()

    foreach ($expectedLog in $expectedLogs) {
        $pathProperty = $InstallResult.PSObject.Properties[$expectedLog.PathProperty]
        $bytesProperty = $InstallResult.PSObject.Properties[$expectedLog.BytesProperty]
        if ($null -eq $pathProperty -or [string]::IsNullOrWhiteSpace([string]$pathProperty.Value) -or $null -eq $bytesProperty) {
            $missing += $expectedLog.PathProperty
            continue
        }

        $logPath = [System.IO.Path]::GetFullPath([string]$pathProperty.Value)
        $expectedPath = [System.IO.Path]::GetFullPath((Join-Path $resultDirectory $expectedLog.FileName))
        if (-not $logPath.Equals($expectedPath, [StringComparison]::OrdinalIgnoreCase)) {
            throw "$($expectedLog.PathProperty) must stay beside install-result.json. Path: $logPath Expected: $expectedPath"
        }

        if (-not (Test-Path -LiteralPath $logPath -PathType Leaf)) {
            throw "Validation log was not written: $logPath"
        }

        if ([int64]$bytesProperty.Value -lt 0) {
            throw "install-result.json included a negative $($expectedLog.BytesProperty)."
        }
    }

    if ($missing.Count -gt 0) {
        Write-Warning "install-result.json did not include validation log metadata. The source published release may predate validation log retention."
    }
    else {
        Write-Host "install-result.json includes validation stdout/stderr log metadata."
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

function Get-OptionalOutputValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text,
        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    $match = [regex]::Match($Text, "(?m)^$([regex]::Escape($Label)):\s*(.+)$")
    if (-not $match.Success) {
        return ""
    }

    $value = $match.Groups[1].Value.Trim()
    if ($value.Equals("n/a", [StringComparison]::OrdinalIgnoreCase)) {
        return ""
    }

    return $value
}

function Enable-StartupUpdatePolicy {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ConfigPath
    )

    if (-not (Test-Path -LiteralPath $ConfigPath -PathType Leaf)) {
        throw "Config was not created: $ConfigPath"
    }

    $config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
    if ($null -eq $config.updates) {
        throw "Config does not contain an updates section: $ConfigPath"
    }

    $config.updates.checkOnStartup = $true
    $config.updates.minimumCheckIntervalHours = 0
    $config.updates.downloadAutomatically = $true
    $config.updates.installAutomatically = $true
    $config.updates.lastCheckedAt = $null
    $config.updates.lastInstallLaunchedVersion = $null
    $config | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $ConfigPath -Encoding UTF8
}

function Wait-ForFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [int]$TimeoutSeconds = 120
    )

    $deadline = [DateTimeOffset]::Now.AddSeconds($TimeoutSeconds)
    while ([DateTimeOffset]::Now -lt $deadline) {
        if (Test-Path -LiteralPath $Path -PathType Leaf) {
            return
        }

        Start-Sleep -Milliseconds 500
    }

    throw "Timed out waiting for file: $Path"
}

function Wait-ForVersionOutput {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(Mandatory = $true)]
        [string]$OutputPath,
        [Parameter(Mandatory = $true)]
        [string]$ErrorPath,
        [string]$ExpectedVersion = "",
        [int]$TimeoutSeconds = 120
    )

    $deadline = [DateTimeOffset]::Now.AddSeconds($TimeoutSeconds)
    do {
        $exitCode = Invoke-LoggedProcess `
            -FilePath $FilePath `
            -Arguments @("--version") `
            -OutputPath $OutputPath `
            -ErrorPath $ErrorPath
        $text = Read-RequiredText -Path $OutputPath
        if ($exitCode -eq 0 -and (
            [string]::IsNullOrWhiteSpace($ExpectedVersion) -or
            $text.IndexOf($ExpectedVersion, [StringComparison]::OrdinalIgnoreCase) -ge 0)) {
            return $text
        }

        Start-Sleep -Milliseconds 500
    } while ([DateTimeOffset]::Now -lt $deadline)

    throw "Timed out waiting for version output to contain '$ExpectedVersion'. See $OutputPath and $ErrorPath"
}

function Get-NormalUpdatesDirectory {
    $appData = [Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData)
    if ([string]::IsNullOrWhiteSpace($appData)) {
        throw "Could not resolve the normal ApplicationData directory."
    }

    return Join-Path (Join-Path $appData "WinAiUsageBar") "updates"
}

function Get-DirectorySnapshot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $entries = @{}
    if (-not (Test-Path -LiteralPath $Path)) {
        return @{
            Exists = $false
            Entries = $entries
        }
    }

    $root = (Resolve-Path -LiteralPath $Path).Path
    $entries["D|."] = "dir"

    Get-ChildItem -LiteralPath $root -Force -Recurse | ForEach-Object {
        $relativePath = $_.FullName.Substring($root.Length).TrimStart(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar)
        $kind = if ($_.PSIsContainer) { "D" } else { "F" }
        if ($_.PSIsContainer) {
            $entries["$kind|$relativePath"] = "dir"
        }
        else {
            $length = $_.Length.ToString([System.Globalization.CultureInfo]::InvariantCulture)
            $entries["$kind|$relativePath"] = "$length|$($_.LastWriteTimeUtc.Ticks)"
        }
    }

    return @{
        Exists = $true
        Entries = $entries
    }
}

function Assert-DirectorySnapshotUnchanged {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Before,
        [Parameter(Mandatory = $true)]
        [hashtable]$After,
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if ($Before.Exists -ne $After.Exists) {
        throw "Normal app data updates directory existence changed while isolated dogfood ran. Path: $Path"
    }

    if (-not $Before.Exists) {
        return
    }

    $changes = @()
    foreach ($key in $Before.Entries.Keys) {
        if (-not $After.Entries.ContainsKey($key)) {
            $changes += "removed $key"
        }
        elseif ($After.Entries[$key] -ne $Before.Entries[$key]) {
            $changes += "changed $key"
        }
    }

    foreach ($key in $After.Entries.Keys) {
        if (-not $Before.Entries.ContainsKey($key)) {
            $changes += "added $key"
        }
    }

    if ($changes.Count -gt 0) {
        $preview = ($changes | Sort-Object | Select-Object -First 20) -join "; "
        throw "Normal app data updates directory changed while isolated dogfood ran. Path: $Path Changes: $preview"
    }
}

$fromVersion = ConvertTo-VersionFromTag -TagName $FromTag
$expectedLatestVersion = if ([string]::IsNullOrWhiteSpace($ExpectedLatestTag)) {
    ""
}
else {
    ConvertTo-VersionFromTag -TagName $ExpectedLatestTag
}
$supportsIsolatedAppData = [version]$fromVersion -ge [version]"0.1.3"

if ($StartupPolicy -and [version]$fromVersion -lt [version]"0.1.5") {
    throw "Startup policy dogfooding requires a source release v0.1.5 or newer because earlier releases do not expose --run-startup-update-check. Value: v$fromVersion"
}

if ([string]::IsNullOrWhiteSpace($WorkDirectory)) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $WorkDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "WinAIUsageBar\published-update-$fromVersion-$timestamp"
}

$workRoot = Resolve-OrCreateDirectory -Path $WorkDirectory
$downloadsRoot = Resolve-OrCreateDirectory -Path (Join-Path $workRoot "downloads")
$installRoot = Resolve-OrCreateDirectory -Path (Join-Path $workRoot "install")
$logsRoot = Resolve-OrCreateDirectory -Path (Join-Path $workRoot "logs")
$appDataRoot = Resolve-OrCreateDirectory -Path (Join-Path $workRoot "appdata")
$normalUpdatesPath = if ($AssertNormalAppDataUnchanged) { Get-NormalUpdatesDirectory } else { "" }
$normalUpdatesBefore = if ($AssertNormalAppDataUnchanged) { Get-DirectorySnapshot -Path $normalUpdatesPath } else { $null }

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
        if ($AssertNormalAppDataUnchanged) {
            $normalUpdatesAfter = Get-DirectorySnapshot -Path $normalUpdatesPath
            Assert-DirectorySnapshotUnchanged -Before $normalUpdatesBefore -After $normalUpdatesAfter -Path $normalUpdatesPath
            Write-Host "Normal app data updates directory unchanged: $normalUpdatesPath"
        }

        Write-Host "Published update discovery passed."
        Write-Host "Work directory: $workRoot"
        Write-Host "Version output: $versionOut"
        Write-Host "Check output: $checkOut"
        return
    }

    if ($StartupPolicy) {
        $healthBeforeOut = Join-Path $logsRoot "startup-policy-health-before.out.txt"
        $healthBeforeErr = Join-Path $logsRoot "startup-policy-health-before.err.txt"
        $healthBeforeExit = Invoke-LoggedProcess `
            -FilePath $appExe `
            -Arguments @("--health-report") `
            -OutputPath $healthBeforeOut `
            -ErrorPath $healthBeforeErr
        if ($healthBeforeExit -ne 0) {
            throw "Startup policy preflight health report failed with exit code $healthBeforeExit. See $healthBeforeOut and $healthBeforeErr"
        }

        $configPath = Join-Path $appDataRoot "config.json"
        Enable-StartupUpdatePolicy -ConfigPath $configPath

        $startupOut = Join-Path $logsRoot "startup-policy.out.txt"
        $startupErr = Join-Path $logsRoot "startup-policy.err.txt"
        $startupExit = Invoke-LoggedProcess `
            -FilePath $appExe `
            -Arguments @("--run-startup-update-check") `
            -OutputPath $startupOut `
            -ErrorPath $startupErr
        $startupText = Read-RequiredText -Path $startupOut
        if ($startupExit -ne 0) {
            throw "Startup update policy failed with exit code $startupExit. See $startupOut and $startupErr"
        }

        Assert-OutputContains -Text $startupText -Expected "Status: InstallLaunched" -Description "Startup policy output"
        if (-not [string]::IsNullOrWhiteSpace($expectedLatestVersion)) {
            Assert-OutputContains -Text $startupText -Expected "Latest version: $expectedLatestVersion" -Description "Startup policy output"
        }

        $packagePath = Get-OutputValue -Text $startupText -Label "Package path"
        $scriptPath = Get-OutputValue -Text $startupText -Label "Install script"
        $resultPath = Get-OptionalOutputValue -Text $startupText -Label "Install result"
        if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
            throw "Startup policy package path was not found: $packagePath"
        }

        if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
            throw "Startup policy install script was not found: $scriptPath"
        }

        Assert-PathInside -ChildPath $packagePath -ParentPath $appDataRoot -Description "Startup policy package path"
        Assert-PathInside -ChildPath $scriptPath -ParentPath $appDataRoot -Description "Startup policy install script"
        if (-not [string]::IsNullOrWhiteSpace($resultPath)) {
            $resultPath = Resolve-PreparedResultPath -CandidatePath $resultPath -ScriptPath $scriptPath -AppDataRoot $appDataRoot
            Assert-PathInside -ChildPath $resultPath -ParentPath $appDataRoot -Description "Startup policy install result path"
        }

        Write-Host "Published startup update policy launched successfully."
        Write-Host "Work directory: $workRoot"
        Write-Host "Version output: $versionOut"
        Write-Host "Check output: $checkOut"
        Write-Host "Preflight health output: $healthBeforeOut"
        Write-Host "Startup policy output: $startupOut"
        Write-Host "Prepared update script: $scriptPath"
        if (-not [string]::IsNullOrWhiteSpace($resultPath)) {
            Write-Host "Expected install result: $resultPath"
        }
        else {
            Write-Warning "Startup policy output did not include an install result path. The source published release may predate install-result path reporting."
        }

        if ($Apply) {
            Assert-PathInside -ChildPath $installRoot -ParentPath $workRoot -Description "Apply install directory"
            if (-not [string]::IsNullOrWhiteSpace($resultPath)) {
                Wait-ForFile -Path $resultPath
            }

            $updatedVersionOut = Join-Path $logsRoot "startup-policy-updated-version.out.txt"
            $updatedVersionErr = Join-Path $logsRoot "startup-policy-updated-version.err.txt"
            $updatedVersionText = Wait-ForVersionOutput `
                -FilePath $appExe `
                -OutputPath $updatedVersionOut `
                -ErrorPath $updatedVersionErr `
                -ExpectedVersion $expectedLatestVersion

            $healthAfterOut = Join-Path $logsRoot "startup-policy-health-after.out.txt"
            $healthAfterErr = Join-Path $logsRoot "startup-policy-health-after.err.txt"
            $healthAfterExit = Invoke-LoggedProcess `
                -FilePath $appExe `
                -Arguments @("--health-report") `
                -OutputPath $healthAfterOut `
                -ErrorPath $healthAfterErr
            $healthAfterText = Read-RequiredText -Path $healthAfterOut
            if ($healthAfterExit -ne 0) {
                throw "Startup policy post-apply health report failed with exit code $healthAfterExit. See $healthAfterOut and $healthAfterErr"
            }

            if (-not [string]::IsNullOrWhiteSpace($resultPath)) {
                $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
                if ($result.status -ne "Succeeded") {
                    throw "Startup policy install-result.json did not report success. Path: $resultPath Status: $($result.status)"
                }

                if ($null -ne $result.validationStatus) {
                    if ($result.validationStatus -ne "Passed") {
                        throw "Startup policy install-result.json did not report passed post-install validation. Path: $resultPath Validation status: $($result.validationStatus)"
                    }

                    Write-Host "Startup policy install-result.json shows passed post-install validation."
                    Write-ValidationLogMetadataStatus -InstallResult $result -ResultPath $resultPath
                }
                else {
                    Write-Warning "Startup policy install-result.json did not include validationStatus. The source published release may predate post-install validation."
                }

                if ($healthAfterText.IndexOf("Install result status: Succeeded", [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                    Write-Host "Post-apply health report shows reconciled install result status."
                }
                else {
                    Write-Warning "Post-apply health report did not show reconciled install result status. The target published release may predate install-result reconciliation."
                }
            }

            Write-Host "Startup policy launched script applied to disposable install directory."
            Write-Host "Updated version output: $updatedVersionOut"
            Write-Host "Post-apply health output: $healthAfterOut"
        }

        if ($AssertNormalAppDataUnchanged) {
            $normalUpdatesAfter = Get-DirectorySnapshot -Path $normalUpdatesPath
            Assert-DirectorySnapshotUnchanged -Before $normalUpdatesBefore -After $normalUpdatesAfter -Path $normalUpdatesPath
            Write-Host "Normal app data updates directory unchanged: $normalUpdatesPath"
        }

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
        $packagePath = Get-ChildItem -LiteralPath (Join-Path $appDataRoot "updates") -Recurse -Filter "WinAIUsageBar-*-win-x64.zip" -File |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -ExpandProperty FullName -First 1
    }

    if ([string]::IsNullOrWhiteSpace($packagePath) -or -not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
        throw "Downloaded update package was not found under isolated app data. See $downloadOut"
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
    $resultPath = Get-OptionalOutputValue -Text $prepareText -Label "Result"
    if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
        $scriptPath = Get-ChildItem -LiteralPath (Join-Path $appDataRoot "updates") -Recurse -Filter "apply-update.ps1" -File |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -ExpandProperty FullName -First 1
    }

    if ([string]::IsNullOrWhiteSpace($scriptPath) -or -not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
        throw "Prepared update script was not found under isolated app data. See $prepareOut"
    }

    Assert-PathInside -ChildPath $scriptPath -ParentPath $appDataRoot -Description "Prepared update script"
    $resultPath = Resolve-PreparedResultPath -CandidatePath $resultPath -ScriptPath $scriptPath -AppDataRoot $appDataRoot

    Assert-PathInside -ChildPath $resultPath -ParentPath $appDataRoot -Description "Prepared update result"

    Write-Host "Published update flow prepared successfully."
    Write-Host "Work directory: $workRoot"
    Write-Host "Version output: $versionOut"
    Write-Host "Check output: $checkOut"
    Write-Host "Download output: $downloadOut"
    Write-Host "Prepare output: $prepareOut"
    Write-Host "Prepared update script: $scriptPath"
    Write-Host "Expected install result: $resultPath"

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

        if (Test-Path -LiteralPath $resultPath -PathType Leaf) {
            $installResult = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
            if ($installResult.status -ne "Succeeded") {
                throw "install-result.json did not report success. Path: $resultPath Status: $($installResult.status)"
            }

            if ($null -ne $installResult.validationStatus) {
                if ($installResult.validationStatus -ne "Passed") {
                    throw "install-result.json did not report passed post-install validation. Path: $resultPath Validation status: $($installResult.validationStatus)"
                }

                Write-Host "install-result.json shows passed post-install validation."
                Write-ValidationLogMetadataStatus -InstallResult $installResult -ResultPath $resultPath
            }
            else {
                Write-Warning "install-result.json did not include validationStatus. The source published release may predate post-install validation."
            }
        }
        else {
            Write-Warning "install-result.json was not found. The source published release may predate install-result reporting. Path: $resultPath"
        }

        Write-Host "Applied prepared update script to disposable install directory."
        Write-Host "Install result: $resultPath"
        Write-Host "Apply output: $applyOut"
        Write-Host "Updated version output: $updatedVersionOut"
    }
}
finally {
    [Environment]::SetEnvironmentVariable("WINAIUSAGEBAR_APPDATA", $oldOverride, "Process")
}

if ($AssertNormalAppDataUnchanged) {
    $normalUpdatesAfter = Get-DirectorySnapshot -Path $normalUpdatesPath
    Assert-DirectorySnapshotUnchanged -Before $normalUpdatesBefore -After $normalUpdatesAfter -Path $normalUpdatesPath
    Write-Host "Normal app data updates directory unchanged: $normalUpdatesPath"
}
