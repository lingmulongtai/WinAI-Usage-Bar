param(
    [string]$InstallerScript = "",
    [string]$BuildScript = ""
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

if ([string]::IsNullOrWhiteSpace($InstallerScript)) {
    $InstallerScript = Join-Path $repoRoot "installer\WinAIUsageBar.iss"
}

if ([string]::IsNullOrWhiteSpace($BuildScript)) {
    $BuildScript = Join-Path $repoRoot "scripts\build-installer.ps1"
}

if (-not (Test-Path -LiteralPath $InstallerScript -PathType Leaf)) {
    throw "Installer script was not found: $InstallerScript"
}

if (-not (Test-Path -LiteralPath $BuildScript -PathType Leaf)) {
    throw "Build script was not found: $BuildScript"
}

$installerText = Get-Content -LiteralPath $InstallerScript -Raw
$buildText = Get-Content -LiteralPath $BuildScript -Raw

$requiredInstallerSnippets = @(
    "WinAiUsageBar.App.exe",
    "OutputDir={#OutputDir}",
    "Source: ""{#PublishPath}\*""",
    "UninstallDisplayIcon={app}\{#AppExeName}",
    "DefaultDirName={localappdata}\Programs\WinAI Usage Bar",
    "PrivilegesRequired=lowest",
    "[Files]",
    "[Icons]",
    "[Run]"
)

foreach ($snippet in $requiredInstallerSnippets) {
    if (-not $installerText.Contains($snippet)) {
        throw "Installer script is missing required snippet: $snippet"
    }
}

$requiredBuildSnippets = @(
    "ISCC.exe",
    "publish.ps1",
    "/DAppVersion=",
    "/DPublishPath=",
    "/DOutputDir=",
    "WinAiUsageBar.App.exe"
)

foreach ($snippet in $requiredBuildSnippets) {
    if (-not $buildText.Contains($snippet)) {
        throw "Build script is missing required snippet: $snippet"
    }
}

Write-Host "Installer script verification passed."
