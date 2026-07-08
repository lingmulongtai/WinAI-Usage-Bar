param(
    [string]$AppExePath = "",
    [string]$CurrentVersion = "0.1.2",
    [string]$ExpectedLatestTag = "",
    [string]$WorkDirectory = "",
    [switch]$Apply
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

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
        throw "Release tag must look like v0.1.3 or 0.1.3. Value: $TagName"
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

function Resolve-AppExePath {
    if (-not [string]::IsNullOrWhiteSpace($AppExePath)) {
        return Resolve-ExistingFile -Path $AppExePath -Description "App executable"
    }

    $candidates = @(
        (Join-Path $repoRoot "src\WinAiUsageBar.App\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\WinAiUsageBar.App.exe"),
        (Join-Path $repoRoot "src\WinAiUsageBar.App\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\WinAiUsageBar.App.exe"),
        (Join-Path $repoRoot "artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw "App executable was not found. Build or publish the app first, or pass -AppExePath."
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

$currentVersionText = ConvertTo-VersionFromTag -TagName $CurrentVersion
$expectedLatestVersion = if ([string]::IsNullOrWhiteSpace($ExpectedLatestTag)) {
    ""
}
else {
    ConvertTo-VersionFromTag -TagName $ExpectedLatestTag
}

if ([string]::IsNullOrWhiteSpace($WorkDirectory)) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $WorkDirectory = Join-Path $repoRoot "artifacts\update-dogfood\current-update-$currentVersionText-$timestamp"
}

$sourceExe = Resolve-AppExePath
$sourceRoot = Split-Path -Parent $sourceExe
$workRoot = Resolve-OrCreateDirectory -Path $WorkDirectory
$logsRoot = Resolve-OrCreateDirectory -Path (Join-Path $workRoot "logs")
$appDataRoot = Resolve-OrCreateDirectory -Path (Join-Path $workRoot "appdata")
$installRoot = Join-Path $workRoot "install"

Assert-PathInside -ChildPath $logsRoot -ParentPath $workRoot -Description "Logs directory"
Assert-PathInside -ChildPath $appDataRoot -ParentPath $workRoot -Description "App data directory"
Assert-PathInside -ChildPath $installRoot -ParentPath $workRoot -Description "Install directory"

if (Test-Path -LiteralPath $installRoot) {
    Remove-Item -LiteralPath $installRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $installRoot | Out-Null
Get-ChildItem -LiteralPath $sourceRoot -Force | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $installRoot -Recurse -Force
}

$installedExe = Join-Path $installRoot "WinAiUsageBar.App.exe"
if (-not (Test-Path -LiteralPath $installedExe -PathType Leaf)) {
    throw "Disposable install does not contain WinAiUsageBar.App.exe: $installRoot"
}

$oldOverride = [Environment]::GetEnvironmentVariable("WINAIUSAGEBAR_APPDATA", "Process")
[Environment]::SetEnvironmentVariable("WINAIUSAGEBAR_APPDATA", $appDataRoot, "Process")
try {
    $checkOut = Join-Path $logsRoot "check.out.txt"
    $checkErr = Join-Path $logsRoot "check.err.txt"
    $checkExit = Invoke-LoggedProcess `
        -FilePath $installedExe `
        -Arguments @(
            "--check-for-updates",
            "--current-version",
            $currentVersionText
        ) `
        -OutputPath $checkOut `
        -ErrorPath $checkErr
    $checkText = Read-RequiredText -Path $checkOut
    if ($checkExit -ne 0) {
        throw "Update check failed with exit code $checkExit. See $checkOut and $checkErr"
    }

    Assert-OutputContains -Text $checkText -Expected "Status: UpdateAvailable" -Description "Update check output"
    Assert-OutputContains -Text $checkText -Expected "Current version: $currentVersionText" -Description "Update check output"
    if (-not [string]::IsNullOrWhiteSpace($expectedLatestVersion)) {
        Assert-OutputContains -Text $checkText -Expected "Latest version: $expectedLatestVersion" -Description "Update check output"
    }

    $downloadOut = Join-Path $logsRoot "download.out.txt"
    $downloadErr = Join-Path $logsRoot "download.err.txt"
    $downloadExit = Invoke-LoggedProcess `
        -FilePath $installedExe `
        -Arguments @(
            "--download-update",
            "--current-version",
            $currentVersionText
        ) `
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
        -FilePath $installedExe `
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
    $resultPath = Get-OutputValue -Text $prepareText -Label "Result"
    if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
        $scriptPath = Get-ChildItem -LiteralPath (Join-Path $appDataRoot "updates") -Recurse -Filter "apply-update.ps1" -File |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -ExpandProperty FullName -First 1
    }

    if ([string]::IsNullOrWhiteSpace($scriptPath) -or -not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
        throw "Prepared update script was not found under isolated app data. See $prepareOut"
    }

    Assert-PathInside -ChildPath $scriptPath -ParentPath $appDataRoot -Description "Prepared update script"
    if ([string]::IsNullOrWhiteSpace($resultPath) -or $resultPath.Equals("n/a", [StringComparison]::OrdinalIgnoreCase)) {
        $resultPath = Join-Path (Split-Path -Parent $scriptPath) "install-result.json"
    }

    Assert-PathInside -ChildPath $resultPath -ParentPath $appDataRoot -Description "Prepared update result"

    Write-Host "Current updater flow prepared successfully."
    Write-Host "Work directory: $workRoot"
    Write-Host "Source executable: $sourceExe"
    Write-Host "Disposable install: $installRoot"
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
            -FilePath $installedExe `
            -Arguments @("--version") `
            -OutputPath $updatedVersionOut `
            -ErrorPath $updatedVersionErr
        $updatedVersionText = Read-RequiredText -Path $updatedVersionOut
        if ($updatedVersionExit -ne 0) {
            throw "Updated disposable install failed with exit code $updatedVersionExit. See $updatedVersionOut and $updatedVersionErr"
        }

        if (-not [string]::IsNullOrWhiteSpace($expectedLatestVersion)) {
            Assert-OutputContains -Text $updatedVersionText -Expected $expectedLatestVersion -Description "Updated app version output"
        }

        if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
            throw "Prepared update script did not write install-result.json: $resultPath"
        }

        $installResult = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
        if ($installResult.status -ne "Succeeded") {
            throw "install-result.json did not report success. Status: $($installResult.status)"
        }

        if ($installResult.validationStatus -ne "Passed") {
            throw "install-result.json did not report passed post-install validation. Validation status: $($installResult.validationStatus)"
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
