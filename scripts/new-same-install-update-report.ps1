param(
    [string]$OutputDirectory = "",
    [string]$ChecklistPath = "",
    [string]$Commit = "",
    [string]$AppVersion = "",
    [string]$SourceVersion = "",
    [string]$TargetVersion = "",
    [string]$InstallPath = "",
    [string]$NormalAppDataPath = "",
    [string]$InstalledAppExe = "",
    [switch]$Preflight,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$projectPath = Join-Path $repoRoot "src\WinAiUsageBar.App\WinAiUsageBar.App.csproj"

function Resolve-InstalledAppExe {
    param(
        [string]$ExplicitPath,
        [string]$RepositoryRoot
    )

    $candidates = @()
    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        $candidates += $ExplicitPath
    }

    $candidates += @(
        (Join-Path $env:LOCALAPPDATA "Programs\WinAI Usage Bar\WinAiUsageBar.App.exe"),
        (Join-Path $env:LOCALAPPDATA "WinAI Usage Bar\WinAiUsageBar.App.exe"),
        (Join-Path $env:ProgramFiles "WinAI Usage Bar\WinAiUsageBar.App.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "WinAI Usage Bar\WinAiUsageBar.App.exe"),
        (Join-Path $RepositoryRoot "artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe")
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    foreach ($candidate in $candidates) {
        $fullPath = [System.IO.Path]::GetFullPath($candidate)
        if (Test-Path -LiteralPath $fullPath -PathType Leaf) {
            return $fullPath
        }
    }

    return ""
}

function Invoke-AppVersion {
    param(
        [string]$AppExe
    )

    if ([string]::IsNullOrWhiteSpace($AppExe) -or -not (Test-Path -LiteralPath $AppExe -PathType Leaf)) {
        return "Not available"
    }

    $stem = "winai-version-$PID-$([System.Guid]::NewGuid().ToString("N"))"
    $stdout = Join-Path ([System.IO.Path]::GetTempPath()) "$stem.out.txt"
    $stderr = Join-Path ([System.IO.Path]::GetTempPath()) "$stem.err.txt"

    try {
        $process = Start-Process `
            -FilePath $AppExe `
            -ArgumentList "--version" `
            -Wait `
            -PassThru `
            -RedirectStandardOutput $stdout `
            -RedirectStandardError $stderr `
            -WindowStyle Hidden

        if ($process.ExitCode -ne 0) {
            return "Version command failed with exit code $($process.ExitCode)"
        }

        $text = Get-Content -Raw -LiteralPath $stdout -ErrorAction SilentlyContinue
        if ([string]::IsNullOrWhiteSpace($text)) {
            return "Version command returned no output"
        }

        return (($text -split "\r?\n") | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1).Trim()
    }
    finally {
        Remove-Item -LiteralPath $stdout, $stderr -Force -ErrorAction SilentlyContinue
    }
}

function Get-SemVerText {
    param(
        [string]$Value
    )

    if ($Value -match "v?(\d+\.\d+\.\d+)") {
        return $Matches[1]
    }

    return ""
}

function Get-LatestReleasePreflight {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        return [pscustomobject]@{
            Tag = ""
            Url = ""
            AssetSummary = "GitHub CLI is not available."
        }
    }

    try {
        $json = & gh release view --json tagName,url,publishedAt,assets 2>$null
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($json)) {
            throw "gh release view failed."
        }

        $release = $json | ConvertFrom-Json
        $assetNames = @($release.assets | ForEach-Object { $_.name })
        return [pscustomobject]@{
            Tag = [string]$release.tagName
            Url = [string]$release.url
            AssetSummary = if ($assetNames.Count -eq 0) { "No assets found." } else { ($assetNames -join ", ") }
        }
    }
    catch {
        return [pscustomobject]@{
            Tag = ""
            Url = ""
            AssetSummary = "Latest release could not be read: $($_.Exception.Message)"
        }
    }
}

function Get-AppDataPreflight {
    param(
        [string]$Root
    )

    $configPath = Join-Path $Root "config.json"
    $secretsPath = Join-Path $Root "secrets"
    $updatesPath = Join-Path $Root "updates"
    $updatesCount = 0
    if (Test-Path -LiteralPath $updatesPath -PathType Container) {
        $updatesCount = @(Get-ChildItem -LiteralPath $updatesPath -Force -ErrorAction SilentlyContinue).Count
    }

    return [pscustomobject]@{
        RootExists = Test-Path -LiteralPath $Root -PathType Container
        ConfigExists = Test-Path -LiteralPath $configPath -PathType Leaf
        SecretsDirectoryExists = Test-Path -LiteralPath $secretsPath -PathType Container
        UpdatesDirectoryExists = Test-Path -LiteralPath $updatesPath -PathType Container
        UpdatesTopLevelItemCount = $updatesCount
    }
}

function ConvertTo-ReportPath {
    param(
        [string]$Path,
        [string]$RepositoryRoot
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ""
    }

    try {
        $fullPath = [System.IO.Path]::GetFullPath($Path)
    }
    catch {
        return $Path
    }

    $replacements = @(
        [pscustomobject]@{ Prefix = [Environment]::GetFolderPath("LocalApplicationData"); Token = "%LOCALAPPDATA%" },
        [pscustomobject]@{ Prefix = [Environment]::GetFolderPath("ApplicationData"); Token = "%APPDATA%" },
        [pscustomobject]@{ Prefix = [Environment]::GetFolderPath("UserProfile"); Token = "%USERPROFILE%" },
        [pscustomobject]@{ Prefix = $RepositoryRoot; Token = "<repo>" }
    ) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_.Prefix) } |
        Sort-Object { $_.Prefix.Length } -Descending

    foreach ($replacement in $replacements) {
        $prefix = [System.IO.Path]::GetFullPath($replacement.Prefix)
        if ($fullPath.Equals($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $replacement.Token
        }

        $prefixWithSeparator = $prefix.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) +
            [System.IO.Path]::DirectorySeparatorChar
        if ($fullPath.StartsWith($prefixWithSeparator, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $replacement.Token + "\" + $fullPath.Substring($prefixWithSeparator.Length)
        }
    }

    return $fullPath
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\verification"
}

if ([string]::IsNullOrWhiteSpace($ChecklistPath)) {
    $ChecklistPath = Join-Path $repoRoot "docs\same-install-update-dogfooding.md"
}

if ([string]::IsNullOrWhiteSpace($NormalAppDataPath)) {
    $NormalAppDataPath = Join-Path ([Environment]::GetFolderPath("ApplicationData")) "WinAiUsageBar"
}

$checklistResolved = (Resolve-Path -LiteralPath $ChecklistPath).Path
$relativeChecklist = [System.IO.Path]::GetRelativePath($repoRoot, $checklistResolved)

if ([string]::IsNullOrWhiteSpace($Commit)) {
    $Commit = (& git -C $repoRoot rev-parse --short HEAD 2>$null)
    if ([string]::IsNullOrWhiteSpace($Commit)) {
        $Commit = "unknown"
    }
}

if ([string]::IsNullOrWhiteSpace($AppVersion)) {
    [xml]$projectXml = Get-Content -LiteralPath $projectPath
    $AppVersion = $projectXml.Project.PropertyGroup.Version |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace($AppVersion)) {
        $AppVersion = "unknown"
    }
}

$preflightInstalledAppExe = ""
$preflightInstalledVersion = "Not checked"
$preflightLatestRelease = [pscustomobject]@{
    Tag = ""
    Url = ""
    AssetSummary = "Not checked"
}
$preflightAppData = [pscustomobject]@{
    RootExists = $false
    ConfigExists = $false
    SecretsDirectoryExists = $false
    UpdatesDirectoryExists = $false
    UpdatesTopLevelItemCount = 0
}
$preflightProcessCount = "Not checked"
$preflightConclusion = "Preflight was not requested."

if ($Preflight) {
    $preflightInstalledAppExe = Resolve-InstalledAppExe -ExplicitPath $InstalledAppExe -RepositoryRoot $repoRoot
    if ([string]::IsNullOrWhiteSpace($InstallPath) -and -not [string]::IsNullOrWhiteSpace($preflightInstalledAppExe)) {
        $InstallPath = Split-Path -Parent $preflightInstalledAppExe
    }

    $preflightInstalledVersion = Invoke-AppVersion -AppExe $preflightInstalledAppExe
    if ([string]::IsNullOrWhiteSpace($SourceVersion)) {
        $SourceVersion = Get-SemVerText $preflightInstalledVersion
    }

    $preflightLatestRelease = Get-LatestReleasePreflight
    if ([string]::IsNullOrWhiteSpace($TargetVersion) -and -not [string]::IsNullOrWhiteSpace($preflightLatestRelease.Tag)) {
        $TargetVersion = Get-SemVerText $preflightLatestRelease.Tag
    }

    $preflightAppData = Get-AppDataPreflight -Root $NormalAppDataPath
    $preflightProcessCount = @(Get-Process WinAiUsageBar.App -ErrorAction SilentlyContinue).Count

    if ([string]::IsNullOrWhiteSpace($preflightInstalledAppExe)) {
        $preflightConclusion = "No installed app executable was found. Install a source release before a same-install update run."
    }
    elseif ([string]::IsNullOrWhiteSpace($SourceVersion) -or [string]::IsNullOrWhiteSpace($TargetVersion)) {
        $preflightConclusion = "Source or target version could not be determined. Fill them in before running the checklist."
    }
    elseif ($SourceVersion -eq $TargetVersion) {
        $preflightConclusion = "No release-to-release update is currently available because the installed version matches the latest release."
    }
    else {
        $preflightConclusion = "A release-to-release update path appears available. Review the checklist before running install actions."
    }
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$outputRoot = (Resolve-Path -LiteralPath $OutputDirectory).Path

$safeCommit = $Commit -replace "[^A-Za-z0-9._-]", "-"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $outputRoot "same-install-update-$timestamp-$safeCommit.md"

if ((Test-Path -LiteralPath $reportPath) -and -not $Force) {
    throw "Same-install update report already exists: $reportPath. Pass -Force to overwrite."
}

$generatedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss zzz"
$checklist = Get-Content -Raw -LiteralPath $checklistResolved
$reportInstallPath = ConvertTo-ReportPath -Path $InstallPath -RepositoryRoot $repoRoot
$reportNormalAppDataPath = ConvertTo-ReportPath -Path $NormalAppDataPath -RepositoryRoot $repoRoot

$report = @"
# Same-Install Update Dogfood Report

Generated: $generatedAt
Repository commit: $Commit
App version: $AppVersion
Checklist source: $relativeChecklist

Do not paste API keys, tokens, cookies, auth.json contents, account identifiers, organization names, enterprise slugs, PAT names, secret references, or secret values into this report.
Keep automatic install disabled unless explicitly confirmed for this run.

## Run Context

| Field | Value |
| --- | --- |
| Tester |  |
| Source installed version | $SourceVersion |
| Target release version | $TargetVersion |
| Installed app path | $reportInstallPath |
| Normal app-data path | $reportNormalAppDataPath |
| Install mode | Manual / CLI / startup policy |
| Automatic install explicitly confirmed? | No |
| Backup path |  |
| Install result path |  |
| Validation status |  |
| Windows version |  |
| Start time |  |
| End time |  |
| Result |  |
| Notes |  |

## Preflight

| Field | Value |
| --- | --- |
| Preflight requested | $Preflight |
| Installed app executable found | $(if ([string]::IsNullOrWhiteSpace($preflightInstalledAppExe)) { "No" } else { "Yes" }) |
| Installed version output | $preflightInstalledVersion |
| Latest release tag | $($preflightLatestRelease.Tag) |
| Latest release URL | $($preflightLatestRelease.Url) |
| Latest release assets | $($preflightLatestRelease.AssetSummary) |
| Normal app-data exists | $($preflightAppData.RootExists) |
| Normal config exists | $($preflightAppData.ConfigExists) |
| Normal secrets directory exists | $($preflightAppData.SecretsDirectoryExists) |
| Normal updates directory exists | $($preflightAppData.UpdatesDirectoryExists) |
| Normal updates top-level item count | $($preflightAppData.UpdatesTopLevelItemCount) |
| Running app process count | $preflightProcessCount |
| Preflight conclusion | $preflightConclusion |

## Checklist

$checklist
"@

Set-Content -LiteralPath $reportPath -Value $report -Encoding utf8

Write-Host "Created same-install update report $reportPath"
