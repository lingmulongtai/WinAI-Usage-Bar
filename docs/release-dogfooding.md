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

The helper creates an isolated work directory under `artifacts\update-dogfood`, redirects command output to logs, sets `WINAIUSAGEBAR_APPDATA` for the app process, falls back to the script-adjacent `install-result.json` when redirected GUI output mangles a non-ASCII `Result:` path, and refuses to apply the generated script unless the install directory is inside the work directory. Current generated scripts also run the updated app's `--smoke-test` before reporting success, retain `validation.out.txt` and `validation.err.txt` beside `install-result.json`, restore the backup if validation fails, and write `validationStatus` plus validation log path/byte metadata to `install-result.json`. With `-Apply`, the helper verifies that validation log metadata points beside the result file.

## Published Release-to-Latest Update Helper

Use this helper to dogfood an already published release against the real GitHub latest-release endpoint:

```powershell
.\scripts\test-published-update-flow.ps1 -FromTag v0.1.2 -ExpectedLatestTag v0.1.3
```

The helper downloads the older release zip from GitHub, extracts it into an isolated temp workspace, redirects command output to logs, sets `WINAIUSAGEBAR_APPDATA` to isolated app data, and runs `--check-for-updates`. Releases before `v0.1.3` do not support `WINAIUSAGEBAR_APPDATA`, so the helper stops after discovery for those versions to avoid writing to normal app data. For source releases `v0.1.3` and newer, `-Apply` can dogfood `--download-update`, `--prepare-update-install`, and the generated update script against the disposable extracted install directory. When the source release writes `validationStatus`, the helper requires it to be `Passed`; when it writes validation log metadata, the helper verifies the log files stay beside `install-result.json`.

Add `-AssertNormalAppDataUnchanged` to snapshot the normal `%AppData%\WinAiUsageBar\updates` directory before and after the isolated run. The helper fails if files are added, removed, or changed there.

Add `-StartupPolicy` to exercise the normal startup update policy entrypoint instead of the explicit update commands:

```powershell
.\scripts\test-published-update-flow.ps1 -FromTag v0.1.5 -ExpectedLatestTag v0.1.6 -StartupPolicy -Apply -AssertNormalAppDataUnchanged
```

The startup-policy mode requires a source release that exposes `--run-startup-update-check` (`v0.1.5` or newer). The example above is for the next release after `v0.1.5`; replace `v0.1.6` with the actual latest tag once it exists. It creates isolated app data, enables startup update checks, automatic download, and guarded automatic install launch in the extracted release's `config.json`, then runs the startup policy command. With `-Apply`, it waits for the startup policy-launched script to update only the disposable extracted install directory, verifies the updated version, checks `install-result.json` when the source release reports a result path, requires `validationStatus: Passed` when the source release writes it, verifies validation log metadata when the source release writes it, runs `--health-report`, and verifies the reconciled install result status when the target release supports that newer reconciliation behavior.

## Same-Install Update Dogfooding

Use the same-install checklist only after disposable update helpers pass for the same target release:

```powershell
.\scripts\new-same-install-update-report.ps1 -SourceVersion 0.1.5 -TargetVersion 0.1.6
```

Then follow `docs/same-install-update-dogfooding.md` against the normal installed app and normal `%AppData%\WinAiUsageBar` root. Keep automatic install disabled unless the run explicitly confirms startup-policy install launch. Same-install reports should cover backup/rollback, process shutdown, restart behavior, validation logs, install result reconciliation, and normal app-data assertions. Do not paste secrets or local account identifiers into the report.

## 2026-07-09 - Published v0.1.4 Startup Policy Guard

Result: guard corrected.

What was checked:

- Tried the startup-policy dogfood path against published `v0.1.4` after publishing `v0.1.5`.
- Confirmed `v0.1.4` does not expose `--run-startup-update-check`.
- Updated the helper to reject `-StartupPolicy` for source releases older than `v0.1.5` before downloading any artifact.

Observed output summary:

```text
Unknown command-line argument(s): --run-startup-update-check
```

## 2026-07-09 - Published v0.1.3 Startup Policy Guard

Result: guard passed.

What was checked:

- Tried the startup-policy dogfood path against published `v0.1.3`.
- Confirmed `v0.1.3` does not expose `--run-startup-update-check`.
- Updated the helper to reject `-StartupPolicy` for source releases older than `v0.1.5` before downloading any artifact.

Observed output summary:

```text
Startup policy dogfooding requires a source release v0.1.5 or newer because earlier releases do not expose --run-startup-update-check.
```

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

## 2026-07-08 - Published v0.1.3 Applies v0.1.4 With Normal App-Data Guard

Result: passed.

What was checked:

- Ran `scripts/test-published-update-flow.ps1 -FromTag v0.1.3 -ExpectedLatestTag v0.1.4 -Apply -AssertNormalAppDataUnchanged`.
- Downloaded the published `v0.1.3` zip from GitHub Releases.
- Used isolated temp work, logs, app data, update staging, and extracted install directories.
- Confirmed the published `v0.1.3` app sees `v0.1.4` through the real latest-release endpoint.
- Downloaded and SHA256-verified the published `v0.1.4` zip and checksum assets.
- Prepared and applied `apply-update.ps1` only to the disposable extracted install directory.
- Confirmed the disposable install reports `v0.1.4` after apply.
- Confirmed the normal `%AppData%\WinAiUsageBar\updates` directory was unchanged before and after the isolated run.

Observed output summary:

```text
Status: UpdateAvailable
Current version: 0.1.3
Latest version: 0.1.4
Download status: Downloaded
Expected SHA256: 0792ab8e18166a5a507a2224000084653edce3df37ba9401969e8da288124b46
Actual SHA256: 0792ab8e18166a5a507a2224000084653edce3df37ba9401969e8da288124b46
Updated version: WinAI Usage Bar 0.1.4+89b1bc79e0e8428550fffb09fa647f0badc2e537
Normal app data updates directory unchanged: %AppData%\WinAiUsageBar\updates
```

## Update CLI Current-Version Override

Current `main` supports `--current-version <version>` on update CLI commands. Use it with isolated app data when you want to exercise the current safe update code as though it were an older installed version:

```powershell
$env:WINAIUSAGEBAR_APPDATA = "$pwd\artifacts\update-dogfood\override-appdata"
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --download-update --current-version 0.1.2
```

This override is for dogfooding only. It is not persisted and does not affect WinUI or startup update checks.

## Startup Update Policy CLI Dogfood

Use this helper to run the configured startup update policy once without opening WinUI:

```powershell
$env:WINAIUSAGEBAR_APPDATA = "$pwd\artifacts\update-dogfood\startup-policy-appdata"
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --run-startup-update-check
Remove-Item Env:\WINAIUSAGEBAR_APPDATA
```

This command uses the real app version and the current config under the selected app-data root. It intentionally does not accept `--current-version`; older-version update paths belong to the explicit update dogfood helpers above.

## 2026-07-08 - Startup Update Policy CLI No-Update And Cooldown

Result: passed.

What was checked:

- Ran current debug build with isolated `WINAIUSAGEBAR_APPDATA` under `artifacts/startup-update-dogfood`.
- Ran `--run-startup-update-check` twice.
- Confirmed the first run checks the real latest-release endpoint using the real app version.
- Confirmed the second run skips because the startup update cooldown is still fresh.
- Confirmed the isolated `config.json` records `lastStatus`, `lastMessage`, `lastCurrentVersion`, `lastLatestVersion`, and `lastCheckedAt`.

Observed output summary:

```text
First run:
Status: NoUpdate
Message: The current app version is up to date.
Current version: 0.1.4+6b6c81be95e60d864227316f2b72f3ec04598548
Latest version: 0.1.4

Second run:
Status: SkippedRecentCheck
Message: Startup update check skipped. Last check is still fresh for about 24 hours.
Latest version: 0.1.4
```

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
