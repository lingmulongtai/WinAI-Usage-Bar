param(
    [string]$TagName = "",
    [string]$ProjectPath = "",
    [string]$ReadmePath = "",
    [string]$ChangelogPath = ""
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

if (-not (Test-Path -LiteralPath $ProjectPath)) {
    throw "Project file was not found: $ProjectPath"
}

if (-not (Test-Path -LiteralPath $ReadmePath)) {
    throw "README was not found: $ReadmePath"
}

if (-not (Test-Path -LiteralPath $ChangelogPath)) {
    throw "CHANGELOG was not found: $ChangelogPath"
}

[xml]$projectXml = Get-Content -LiteralPath $ProjectPath
$propertyGroups = @($projectXml.Project.PropertyGroup)
$version = Get-FirstTextValue -Values ($propertyGroups | ForEach-Object { $_.Version }) -Name "Version"
$assemblyVersion = Get-FirstTextValue -Values ($propertyGroups | ForEach-Object { $_.AssemblyVersion }) -Name "AssemblyVersion"
$fileVersion = Get-FirstTextValue -Values ($propertyGroups | ForEach-Object { $_.FileVersion }) -Name "FileVersion"
$informationalVersion = Get-FirstTextValue -Values ($propertyGroups | ForEach-Object { $_.InformationalVersion }) -Name "InformationalVersion"

if ($informationalVersion -ne $version) {
    throw "InformationalVersion ($informationalVersion) must match Version ($version)."
}

if (-not $assemblyVersion.StartsWith("$version.", [StringComparison]::Ordinal)) {
    throw "AssemblyVersion ($assemblyVersion) must start with Version plus patch component ($version.)."
}

if (-not $fileVersion.StartsWith("$version.", [StringComparison]::Ordinal)) {
    throw "FileVersion ($fileVersion) must start with Version plus patch component ($version.)."
}

Assert-ContainsText -Path $ReadmePath -Text "Current app version: ``$version``." -Description "README current app version"
Assert-ContainsText -Path $ChangelogPath -Text "## $version -" -Description "CHANGELOG release heading"

if (-not [string]::IsNullOrWhiteSpace($TagName)) {
    $expectedTag = "v$version"
    if ($TagName -ne $expectedTag) {
        throw "Release tag ($TagName) must match app version tag ($expectedTag)."
    }

    Write-Host "Release tag matches app version: $TagName"
}

Write-Host "Release metadata verified for version $version"
