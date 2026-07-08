param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$PublishPath = "",
    [string]$OutputDirectory = "",
    [string]$InnoSetupCompiler = "",
    [switch]$SkipPublish,
    [switch]$EnableNuGetAudit
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$projectPath = Join-Path $repoRoot "src\WinAiUsageBar.App\WinAiUsageBar.App.csproj"
$installerScript = Join-Path $repoRoot "installer\WinAIUsageBar.iss"

if ([string]::IsNullOrWhiteSpace($PublishPath)) {
    $PublishPath = Join-Path $repoRoot "artifacts\publish\WinAIUsageBar-$Runtime"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\installer"
}

[xml]$projectXml = Get-Content -LiteralPath $projectPath
$version = $projectXml.Project.PropertyGroup.Version |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($version)) {
    throw "App Version was not found in $projectPath"
}

if (-not $SkipPublish) {
    $publishArgs = @(
        "-File",
        (Join-Path $scriptRoot "publish.ps1"),
        "-Configuration",
        $Configuration,
        "-Runtime",
        $Runtime,
        "-OutputPath",
        $PublishPath
    )

    if ($EnableNuGetAudit) {
        $publishArgs += "-EnableNuGetAudit"
    }

    powershell @publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw "publish.ps1 failed with exit code $LASTEXITCODE"
    }
}

$publishRoot = (Resolve-Path -LiteralPath $PublishPath).Path
$requiredExe = Join-Path $publishRoot "WinAiUsageBar.App.exe"
if (-not (Test-Path -LiteralPath $requiredExe)) {
    throw "Published app executable was not found: $requiredExe"
}

$iscc = $InnoSetupCompiler
if ([string]::IsNullOrWhiteSpace($iscc)) {
    $isccCommand = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($isccCommand -and -not [string]::IsNullOrWhiteSpace($isccCommand.Source)) {
        $iscc = $isccCommand.Source
    }
}

if ([string]::IsNullOrWhiteSpace($iscc)) {
    $candidatePaths = @(
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    $iscc = $candidatePaths |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
        Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($iscc) -or -not (Test-Path -LiteralPath $iscc -PathType Leaf)) {
    throw "Inno Setup compiler ISCC.exe was not found. Install Inno Setup 6, add ISCC.exe to PATH, or pass -InnoSetupCompiler <path>."
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$outputRoot = (Resolve-Path -LiteralPath $OutputDirectory).Path

$isccArgs = @(
    "/DAppVersion=$version",
    "/DPublishPath=$publishRoot",
    "/DOutputDir=$outputRoot",
    $installerScript
)

& $iscc @isccArgs
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compiler failed with exit code $LASTEXITCODE"
}

Write-Host "Created installer output under $outputRoot"
