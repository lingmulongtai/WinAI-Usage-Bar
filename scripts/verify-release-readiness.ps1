param(
    [string]$TagName = "",
    [string]$ProjectPath = "",
    [string]$ReadmePath = "",
    [string]$ChangelogPath = "",
    [string]$AuditPath = "",
    [string]$PublishedAppPath = "",
    [string]$PackageDirectory = "",
    [string]$InstallerDirectory = "",
    [string]$VerificationReportPath = "",
    [switch]$RequireVerificationReport,
    [switch]$RequireInstaller
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Join-Path $repoRoot "src\WinAiUsageBar.App\WinAiUsageBar.App.csproj"
}

if ([string]::IsNullOrWhiteSpace($ReadmePath)) {
    $ReadmePath = Join-Path $repoRoot "README.md"
}

if ([string]::IsNullOrWhiteSpace($ChangelogPath)) {
    $ChangelogPath = Join-Path $repoRoot "CHANGELOG.md"
}

if ([string]::IsNullOrWhiteSpace($AuditPath)) {
    $AuditPath = Join-Path $repoRoot "docs\current-state-audit.md"
}

if ([string]::IsNullOrWhiteSpace($PublishedAppPath)) {
    $PublishedAppPath = Join-Path $repoRoot "artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe"
}

if ([string]::IsNullOrWhiteSpace($PackageDirectory)) {
    $PackageDirectory = Join-Path $repoRoot "artifacts\packages"
}

if ([string]::IsNullOrWhiteSpace($InstallerDirectory)) {
    $InstallerDirectory = Join-Path $repoRoot "artifacts\installer"
}

function Get-FirstTextValue {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Values,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $value = $Values |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "$Name was not found in $ProjectPath"
    }

    return [string]$value
}

function Assert-FileExists {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Description was not found: $Path"
    }
}

function Assert-ContainsText {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Text,
        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $content = Get-Content -LiteralPath $Path -Raw
    if ($content.IndexOf($Text, [StringComparison]::Ordinal) -lt 0) {
        throw "$Description was not found in $Path. Expected to find: $Text"
    }
}

function Get-ChangelogReleaseDate {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Version
    )

    $content = Get-Content -LiteralPath $Path -Raw
    $escapedVersion = [regex]::Escape($Version)
    $match = [regex]::Match($content, "(?m)^##\s+$escapedVersion\s+-\s+(\d{4}-\d{2}-\d{2})\s*$")
    if (-not $match.Success) {
        throw "CHANGELOG release date was not found for version $Version in $Path."
    }

    return $match.Groups[1].Value
}

function Assert-ChecksumMatches {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ZipPath,
        [Parameter(Mandatory = $true)]
        [string]$ChecksumPath
    )

    $expectedLine = (Get-Content -LiteralPath $ChecksumPath -Raw).Trim()
    $parts = $expectedLine -split "\s+", 2
    if ($parts.Count -lt 2) {
        throw "Checksum file has invalid format: $ChecksumPath"
    }

    $expectedHash = $parts[0].ToLowerInvariant()
    $expectedFile = $parts[1].Trim()
    $actualFile = Split-Path -Leaf $ZipPath
    if ($expectedFile -ne $actualFile) {
        throw "Checksum file references $expectedFile, but package is $actualFile."
    }

    $actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $ZipPath).Hash.ToLowerInvariant()
    if ($actualHash -ne $expectedHash) {
        throw "Checksum mismatch for $ZipPath. Expected $expectedHash, got $actualHash."
    }
}

$metadataScript = Join-Path $scriptRoot "verify-release.ps1"
$metadataArgs = @{
    ProjectPath = $ProjectPath
    ReadmePath = $ReadmePath
    ChangelogPath = $ChangelogPath
}

if (-not [string]::IsNullOrWhiteSpace($TagName)) {
    $metadataArgs.TagName = $TagName
}

& $metadataScript @metadataArgs

[xml]$projectXml = Get-Content -LiteralPath $ProjectPath
$propertyGroups = @($projectXml.Project.PropertyGroup)
$version = Get-FirstTextValue -Values ($propertyGroups | ForEach-Object { $_.Version }) -Name "Version"
$releaseDate = Get-ChangelogReleaseDate -Path $ChangelogPath -Version $version

Assert-FileExists -Path $AuditPath -Description "Current state audit"
Assert-ContainsText -Path $AuditPath -Text "Date: $releaseDate" -Description "Current state audit date for release"

Assert-FileExists -Path $PublishedAppPath -Description "Published app executable"
$smokeProcess = Start-Process `
    -FilePath $PublishedAppPath `
    -ArgumentList "--smoke-test" `
    -Wait `
    -PassThru `
    -WindowStyle Hidden
if ($smokeProcess.ExitCode -ne 0) {
    throw "Published app smoke test failed with exit code $($smokeProcess.ExitCode)."
}

if (-not (Test-Path -LiteralPath $PackageDirectory -PathType Container)) {
    throw "Package directory was not found: $PackageDirectory"
}

$zipName = "WinAIUsageBar-$version-win-x64.zip"
$zipPath = Join-Path $PackageDirectory $zipName
$checksumPath = "$zipPath.sha256"
Assert-FileExists -Path $zipPath -Description "Release zip package"
Assert-FileExists -Path $checksumPath -Description "Release zip checksum"
Assert-ChecksumMatches -ZipPath $zipPath -ChecksumPath $checksumPath

if ($RequireInstaller) {
    if (-not (Test-Path -LiteralPath $InstallerDirectory -PathType Container)) {
        throw "Installer directory was not found: $InstallerDirectory"
    }

    $installerName = "WinAIUsageBar-$version-setup.exe"
    $installerPath = Join-Path $InstallerDirectory $installerName
    $installerChecksumPath = "$installerPath.sha256"
    Assert-FileExists -Path $installerPath -Description "Release setup installer"
    Assert-FileExists -Path $installerChecksumPath -Description "Release setup installer checksum"
    Assert-ChecksumMatches -ZipPath $installerPath -ChecksumPath $installerChecksumPath
}

if ($RequireVerificationReport -and [string]::IsNullOrWhiteSpace($VerificationReportPath)) {
    throw "VerificationReportPath is required when -RequireVerificationReport is set."
}

if (-not [string]::IsNullOrWhiteSpace($VerificationReportPath)) {
    Assert-FileExists -Path $VerificationReportPath -Description "Windows verification report"
    Assert-ContainsText -Path $VerificationReportPath -Text "App version: $version" -Description "Verification report app version"
    Assert-ContainsText -Path $VerificationReportPath -Text "Repository commit:" -Description "Verification report repository commit"
    Assert-ContainsText -Path $VerificationReportPath -Text "## Checklist" -Description "Verification report checklist section"
}

Write-Host "Release readiness verified for version $version"
