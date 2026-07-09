param(
    [string]$TagName = "",
    [string]$ProjectPath = "",
    [string]$ReadmePath = "",
    [string]$ChangelogPath = "",
    [string]$AuditPath = "",
    [string]$SpecPath = "",
    [string]$ProviderDogfoodPath = "",
    [string]$SameInstallDogfoodPath = "",
    [string]$SameInstallReportScriptPath = "",
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

if ([string]::IsNullOrWhiteSpace($SpecPath)) {
    $SpecPath = Join-Path $repoRoot "docs\spec.md"
}

if ([string]::IsNullOrWhiteSpace($ProviderDogfoodPath)) {
    $ProviderDogfoodPath = Join-Path $repoRoot "docs\provider-dogfooding.md"
}

if ([string]::IsNullOrWhiteSpace($SameInstallDogfoodPath)) {
    $SameInstallDogfoodPath = Join-Path $repoRoot "docs\same-install-update-dogfooding.md"
}

if ([string]::IsNullOrWhiteSpace($SameInstallReportScriptPath)) {
    $SameInstallReportScriptPath = Join-Path $repoRoot "scripts\new-same-install-update-report.ps1"
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
Assert-FileExists -Path $SpecPath -Description "Product spec"
Assert-FileExists -Path $ProviderDogfoodPath -Description "Provider dogfooding notes"
Assert-FileExists -Path $SameInstallDogfoodPath -Description "Same-install update dogfooding checklist"
Assert-FileExists -Path $SameInstallReportScriptPath -Description "Same-install update report script"
Assert-ContainsText -Path $ReadmePath -Text "### Unsigned installer notice" -Description "README unsigned installer notice heading"
Assert-ContainsText -Path $ReadmePath -Text "WinAI Usage Bar setup installer and executable are currently unsigned." -Description "README unsigned installer warning"
Assert-ContainsText -Path $ReadmePath -Text "Windows SmartScreen or unknown publisher warnings" -Description "README Windows trust warning"
Assert-ContainsText -Path $ReadmePath -Text "Download only from GitHub Releases" -Description "README GitHub Releases download warning"
Assert-ContainsText -Path $ReadmePath -Text "Verify the published SHA256 checksum" -Description "README SHA256 verification warning"
Assert-ContainsText -Path $ReadmePath -Text "docs/same-install-update-dogfooding.md" -Description "README same-install update dogfood link"
Assert-ContainsText -Path $ReadmePath -Text ".\scripts\new-same-install-update-report.ps1" -Description "README same-install update report script"
Assert-ContainsText -Path $SpecPath -Text "Signing remains future work" -Description "Spec unsigned installer future signing statement"
Assert-ContainsText -Path $SpecPath -Text "while the app is still unsigned, release readiness verification must fail if this warning disappears" -Description "Spec unsigned installer readiness gate"
Assert-ContainsText -Path $SpecPath -Text 'The CLI `--refresh-once` command runs the same enabled-provider refresh pipeline once without launching WinUI windows, composing tray/window/update services, or sending local notifications.' -Description "Spec CLI-only refresh-once composition"
Assert-ContainsText -Path $SpecPath -Text 'GitHub Copilot OfficialApi missing-scope dogfood should return quickly with a safe AuthRequired report' -Description "Spec Copilot refresh-once missing-scope dogfood expectation"
Assert-ContainsText -Path $AuditPath -Text "unsigned installer notice" -Description "Current state audit unsigned installer notice tracking"
Assert-ContainsText -Path $ProviderDogfoodPath -Text "## GitHub Copilot Metrics Failure States" -Description "Provider dogfooding Copilot metrics section"
Assert-ContainsText -Path $ProviderDogfoodPath -Text 'Process-level `--refresh-once` dogfood for missing scope should return quickly' -Description "Provider dogfooding Copilot refresh-once process expectation"
Assert-ContainsText -Path $SameInstallDogfoodPath -Text "Keep automatic install disabled unless explicitly confirmed." -Description "Same-install automatic install safety statement"
Assert-ContainsText -Path $SameInstallDogfoodPath -Text "## Process Shutdown And Install" -Description "Same-install process shutdown checklist"
Assert-ContainsText -Path $SameInstallDogfoodPath -Text "## Normal App-Data Assertions" -Description "Same-install normal app-data checklist"
Assert-ContainsText -Path $SameInstallDogfoodPath -Text "validation.out.txt" -Description "Same-install validation log checklist"
Assert-ContainsText -Path $SameInstallReportScriptPath -Text "same-install-update" -Description "Same-install report filename stem"

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
