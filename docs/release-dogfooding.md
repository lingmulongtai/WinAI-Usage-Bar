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
