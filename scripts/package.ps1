param(
    [string]$PublishPath = "",
    [string]$OutputDirectory = "",
    [string]$Runtime = "win-x64",
    [string]$PackageName = ""
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$projectPath = Join-Path $repoRoot "src\WinAiUsageBar.App\WinAiUsageBar.App.csproj"

if ([string]::IsNullOrWhiteSpace($PublishPath)) {
    $PublishPath = Join-Path $repoRoot "artifacts\publish\WinAIUsageBar-win-x64"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\packages"
}

if ([string]::IsNullOrWhiteSpace($PackageName)) {
    [xml]$projectXml = Get-Content -LiteralPath $projectPath
    $version = $projectXml.Project.PropertyGroup.Version |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace($version)) {
        throw "App Version was not found in $projectPath"
    }

    $PackageName = "WinAIUsageBar-$version-$Runtime"
}

$publishRoot = (Resolve-Path -LiteralPath $PublishPath).Path
$requiredExe = Join-Path $publishRoot "WinAiUsageBar.App.exe"
if (-not (Test-Path -LiteralPath $requiredExe)) {
    throw "Published app executable was not found: $requiredExe"
}

function Get-RelativeEntryName {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $rootFullPath = [System.IO.Path]::GetFullPath($Root)
    $rootPrefix = $rootFullPath
    if (-not $rootPrefix.EndsWith([System.IO.Path]::DirectorySeparatorChar.ToString(), [StringComparison]::Ordinal)) {
        $rootPrefix = "$rootPrefix$([System.IO.Path]::DirectorySeparatorChar)"
    }

    $fileFullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not $fileFullPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Package file is outside the publish root: $Path"
    }

    return $fileFullPath.Substring($rootPrefix.Length)
}

function Get-Sha256Hex {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $getFileHash = Get-Command "Get-FileHash" -ErrorAction SilentlyContinue
    if ($getFileHash) {
        return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
    }

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $stream = [System.IO.File]::OpenRead($Path)
        try {
            $hashBytes = $sha256.ComputeHash($stream)
            return ([System.BitConverter]::ToString($hashBytes)).Replace("-", "").ToLowerInvariant()
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $sha256.Dispose()
    }
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$outputRoot = (Resolve-Path -LiteralPath $OutputDirectory).Path
$zipPath = Join-Path $outputRoot "$PackageName.zip"
$checksumPath = "$zipPath.sha256"

Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $checksumPath -Force -ErrorAction SilentlyContinue

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$fixedTimestamp = [DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
$zipStream = [System.IO.File]::Open($zipPath, [System.IO.FileMode]::CreateNew)
try {
    $archive = [System.IO.Compression.ZipArchive]::new(
        $zipStream,
        [System.IO.Compression.ZipArchiveMode]::Create,
        $false)
    try {
        $files = Get-ChildItem -LiteralPath $publishRoot -Recurse -File -Force |
            Sort-Object FullName

        foreach ($file in $files) {
            $relativePath = Get-RelativeEntryName -Root $publishRoot -Path $file.FullName
            $entryName = $relativePath.Replace([System.IO.Path]::DirectorySeparatorChar, "/").Replace([System.IO.Path]::AltDirectorySeparatorChar, "/")
            $entry = $archive.CreateEntry($entryName, [System.IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = $fixedTimestamp

            $entryStream = $entry.Open()
            try {
                $fileStream = [System.IO.File]::OpenRead($file.FullName)
                try {
                    $fileStream.CopyTo($entryStream)
                }
                finally {
                    $fileStream.Dispose()
                }
            }
            finally {
                $entryStream.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}
finally {
    $zipStream.Dispose()
}

$hash = Get-Sha256Hex -Path $zipPath
"$hash  $(Split-Path -Leaf $zipPath)" |
    Set-Content -LiteralPath $checksumPath -Encoding ascii

Write-Host "Created package $zipPath"
Write-Host "Created checksum $checksumPath"
