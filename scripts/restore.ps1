param(
    [string]$SolutionPath = "",
    [int]$Attempts = 3,
    [int]$InitialDelaySeconds = 10,
    [switch]$EnableNuGetAudit
)

$ErrorActionPreference = "Stop"
if (Get-Variable -Name PSNativeCommandUseErrorActionPreference -Scope Global -ErrorAction SilentlyContinue) {
    $PSNativeCommandUseErrorActionPreference = $false
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

if ([string]::IsNullOrWhiteSpace($SolutionPath)) {
    $SolutionPath = Join-Path $repoRoot "WinAIUsageBar.sln"
}

if ($Attempts -lt 1) {
    throw "Attempts must be at least 1."
}

$restoreArgs = @("restore", $SolutionPath)
if (-not $EnableNuGetAudit) {
    $restoreArgs += "-p:NuGetAudit=false"
}

$lastExitCode = 0
for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
    Write-Host "dotnet restore attempt $attempt of $Attempts"
    & dotnet @restoreArgs
    $lastExitCode = $LASTEXITCODE
    if ($lastExitCode -eq 0) {
        return
    }

    if ($attempt -lt $Attempts) {
        $delay = $InitialDelaySeconds * $attempt
        Write-Warning "dotnet restore failed with exit code $lastExitCode. Retrying in $delay seconds."
        Start-Sleep -Seconds $delay
    }
}

throw "dotnet restore failed with exit code $lastExitCode after $Attempts attempt(s)."
