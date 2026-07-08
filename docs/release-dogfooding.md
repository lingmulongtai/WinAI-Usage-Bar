# Release Dogfooding

This file records small release-to-release checks that use published artifacts. Do not paste secrets, tokens, cookies, or local account data here.

## 2026-07-08 - v0.1.1 Sees v0.1.2

Result: passed.

What was checked:

- Downloaded the published `WinAIUsageBar-0.1.1-win-x64.zip` asset into an ignored `.tmp/release-dogfood/` directory.
- Extracted the zip without touching the normal install directory.
- Ran the `v0.1.1` executable with `--check-for-updates` against the real GitHub latest-release endpoint.

Observed output summary:

```text
Status: UpdateAvailable
Current version: 0.1.1
Latest version: 0.1.2
Update available: yes
Package: WinAIUsageBar-0.1.2-win-x64.zip
Checksum: WinAIUsageBar-0.1.2-win-x64.zip.sha256
```

Caveat:

- `WinAiUsageBar.App.exe` is a Windows GUI executable, so PowerShell verification scripts should use `Start-Process -Wait -PassThru` with redirected output when command completion matters.
- This check only verified release discovery. It did not run the download, prepare, launch, or install stages.

## Disposable Prepared Install Script

Use this helper to dogfood update preparation and optional application against a temporary install directory:

```powershell
.\scripts\test-update-prepare-apply.ps1 -PackagePath .\artifacts\packages\WinAIUsageBar-0.1.2-win-x64.zip -Apply
```

The helper creates an isolated work directory under `artifacts\update-dogfood`, redirects command output to logs, sets `WINAIUSAGEBAR_APPDATA` for the app process, and refuses to apply the generated script unless the install directory is inside the work directory.

## Published Release-to-Latest Update Helper

Use this helper to dogfood an already published release against the real GitHub latest-release endpoint:

```powershell
.\scripts\test-published-update-flow.ps1 -FromTag v0.1.2 -ExpectedLatestTag v0.1.3
```

The helper downloads the older release zip from GitHub, extracts it into an isolated temp workspace, redirects command output to logs, sets `WINAIUSAGEBAR_APPDATA` to isolated app data, and runs `--check-for-updates`. Releases before `v0.1.3` do not support `WINAIUSAGEBAR_APPDATA`, so the helper stops after discovery for those versions to avoid writing to normal app data. For source releases `v0.1.3` and newer, `-Apply` can dogfood `--download-update`, `--prepare-update-install`, and the generated update script against the disposable extracted install directory.

## 2026-07-08 - Prepared Apply Script Handles Unicode Workspace Path

Result: passed on current `main` after fixing generated script encoding.

What was checked:

- Downloaded the published `WinAIUsageBar-0.1.2-win-x64.zip` asset into ignored local temp storage.
- Ran current `main` with `WINAIUSAGEBAR_APPDATA` isolated by `scripts/test-update-prepare-apply.ps1`.
- Prepared `apply-update.ps1` against a disposable install directory under `artifacts/update-dogfood/issue-112`.
- Applied the prepared script with Windows PowerShell against that disposable directory only.
- Confirmed the installed temp app launches with `--version`.

Observed output summary:

```text
Prepared update script: artifacts\update-dogfood\issue-112\appdata\updates\...\apply-update.ps1
Applied prepared update script to disposable install directory.
Installed version: WinAI Usage Bar 0.1.2+8485a3b7884f196599cbf38dba44eb10199e9c74
Generated script BOM: EF BB BF
apply.err.txt: empty
```

Finding fixed during this check:

- Windows PowerShell 5.1 misread UTF-8 no-BOM `apply-update.ps1` files when the workspace path contained Japanese characters. Current `main` now writes the prepared update script with a UTF-8 BOM so non-ASCII install, staging, backup, and package paths survive the update apply step.

## 2026-07-08 - v0.1.2 Sees v0.1.3 With Legacy App-Data Guard

Result: passed.

What was checked:

- Ran `scripts/test-published-update-flow.ps1` against the published `v0.1.2` zip and expected latest `v0.1.3`.
- Used isolated temp work, logs, download, and extracted install directories.
- Confirmed `v0.1.2` sees `v0.1.3` through the real latest-release endpoint.
- Confirmed the helper refuses to continue into download, prepare, or apply for `v0.1.2` because that release predates `WINAIUSAGEBAR_APPDATA` and cannot be safely isolated.

Observed output summary:

```text
WARNING: Release v0.1.2 does not support WINAIUSAGEBAR_APPDATA, so download, prepare, and apply are skipped to avoid writing to normal app data.
Published update discovery passed.
Check output: ...\check.out.txt
```

Finding:

- The first manual run proved that `v0.1.2` writes update downloads under normal `%AppData%\WinAiUsageBar\updates` because isolated app-data override support was added later. The helper now guards that case before the download stage. Full isolated published release-to-release download/apply dogfooding should use `v0.1.3` or newer as the source release.

## Update CLI Current-Version Override

Current `main` supports `--current-version <version>` on update CLI commands. Use it with isolated app data when you want to exercise the current safe update code as though it were an older installed version:

```powershell
$env:WINAIUSAGEBAR_APPDATA = "$pwd\artifacts\update-dogfood\override-appdata"
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --download-update --current-version 0.1.2
```

This override is for dogfooding only. It is not persisted and does not affect WinUI or startup update checks.

For the full isolated update path, use the helper:

```powershell
.\scripts\test-current-update-flow.ps1 -CurrentVersion 0.1.2 -ExpectedLatestTag v0.1.3 -Apply
```

The helper copies the current build to a disposable install directory, uses isolated app data, runs `--check-for-updates --current-version`, runs `--download-update --current-version`, prepares an update script for the disposable install directory, and refuses to apply unless the install directory is under the dogfood work directory.

## 2026-07-08 - Current Main Simulates v0.1.2 Update Check

Result: passed.

What was checked:

- Ran current debug build with isolated `WINAIUSAGEBAR_APPDATA`.
- Passed `--check-for-updates --current-version 0.1.2`.
- Confirmed the real latest-release endpoint reports `v0.1.3` as available without changing the app assembly version.

Observed output summary:

```text
Status: UpdateAvailable
Current version: 0.1.2
Latest version: 0.1.3
Update available: yes
```

## 2026-07-08 - Current Main Downloads and Applies v0.1.3 in Disposable Install

Result: passed.

What was checked:

- Ran `scripts/test-current-update-flow.ps1 -CurrentVersion 0.1.2 -ExpectedLatestTag v0.1.3 -Apply`.
- Used a disposable copy of the current build as the install directory.
- Used isolated `WINAIUSAGEBAR_APPDATA` under `artifacts/update-dogfood`.
- Confirmed the current updater sees `v0.1.3` when simulating current version `0.1.2`.
- Downloaded and SHA256-verified the published `v0.1.3` zip and checksum assets.
- Prepared `apply-update.ps1` under isolated app data.
- Applied the prepared script only to the disposable install directory.
- Confirmed the updated disposable install launches and reports version `0.1.3`.

Observed output summary:

```text
Current updater flow prepared successfully.
Applied prepared update script to disposable install directory.
Updated version: WinAI Usage Bar 0.1.3+b4b9c249abe10dde9442a2236d245d54e7ce6072
apply.err.txt: empty
```
