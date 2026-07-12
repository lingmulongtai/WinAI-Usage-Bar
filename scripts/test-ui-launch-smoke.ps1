param(
    [string]$AppExe = "",
    [switch]$Publish,
    [int]$HoldSeconds = 5,
    [int]$TimeoutSeconds = 20,
    [ValidateSet("Minimal", "Settings", "SettingsPages")]
    [string]$Target = "Minimal",
    [string]$AppDataRoot = "",
    [switch]$SyntaxOnly
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

if ($SyntaxOnly) {
    Write-Host "UI launch smoke script syntax verified."
    return
}

if ($HoldSeconds -lt 1 -or $HoldSeconds -gt 60) {
    throw "HoldSeconds must be from 1 to 60."
}

if ($TimeoutSeconds -lt ($HoldSeconds + 5)) {
    throw "TimeoutSeconds must be at least HoldSeconds + 5."
}

if ($Publish) {
    & (Join-Path $scriptRoot "publish.ps1") `
        -Configuration Release `
        -Runtime win-x64 `
        -OutputPath (Join-Path $repoRoot "artifacts\publish\WinAIUsageBar-win-x64")
}

if ([string]::IsNullOrWhiteSpace($AppExe)) {
    $AppExe = Join-Path $repoRoot "artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe"
}

$resolvedAppExe = [System.IO.Path]::GetFullPath($AppExe)
if (-not (Test-Path -LiteralPath $resolvedAppExe -PathType Leaf)) {
    throw "App executable was not found: $resolvedAppExe. Run scripts\publish.ps1 first or pass -Publish."
}

if ([string]::IsNullOrWhiteSpace($AppDataRoot)) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $AppDataRoot = Join-Path $repoRoot "artifacts\ui-launch-smoke\appdata-$stamp"
}

$resolvedAppDataRoot = [System.IO.Path]::GetFullPath($AppDataRoot)
New-Item -ItemType Directory -Force -Path $resolvedAppDataRoot | Out-Null

$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $resolvedAppExe
$startInfo.Arguments = "--ui-launch-smoke --hold-seconds $HoldSeconds --target $Target"
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
$startInfo.EnvironmentVariables["WINAIUSAGEBAR_APPDATA"] = $resolvedAppDataRoot

$process = [System.Diagnostics.Process]::new()
$process.StartInfo = $startInfo

Write-Host "Launching UI smoke: $resolvedAppExe"
Write-Host "Target: $Target"
Write-Host "Isolated app data: $resolvedAppDataRoot"
if ($Target -eq "SettingsPages") {
    Write-Host "SettingsPages target will open Settings and visit the main navigation pages."
}

if (-not $process.Start()) {
    throw "UI launch smoke process could not be started."
}

$aliveCheckDelayMs = [Math]::Min(2000, [Math]::Max(500, [int](($HoldSeconds * 1000) / 2)))
Start-Sleep -Milliseconds $aliveCheckDelayMs

if ($process.HasExited) {
    throw "UI launch smoke process exited before the alive check. Exit code: $($process.ExitCode)."
}

if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
    try {
        $process.Kill($true)
    }
    catch {
        Write-Warning "Failed to kill timed-out UI launch smoke process: $($_.Exception.Message)"
    }

    throw "UI launch smoke process did not exit within $TimeoutSeconds seconds."
}

if ($process.ExitCode -ne 0) {
    throw "UI launch smoke process failed with exit code $($process.ExitCode)."
}

Write-Host "UI launch smoke passed."
