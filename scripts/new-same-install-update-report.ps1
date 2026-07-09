param(
    [string]$OutputDirectory = "",
    [string]$ChecklistPath = "",
    [string]$Commit = "",
    [string]$AppVersion = "",
    [string]$SourceVersion = "",
    [string]$TargetVersion = "",
    [string]$InstallPath = "",
    [string]$NormalAppDataPath = "",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$projectPath = Join-Path $repoRoot "src\WinAiUsageBar.App\WinAiUsageBar.App.csproj"

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
| Installed app path | $InstallPath |
| Normal app-data path | $NormalAppDataPath |
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

## Checklist

$checklist
"@

Set-Content -LiteralPath $reportPath -Value $report -Encoding utf8

Write-Host "Created same-install update report $reportPath"
