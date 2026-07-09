# WinAI Usage Bar

WinAI Usage Bar is a personal Windows notification-area app for watching AI provider usage. It is inspired by the idea of a compact usage bar, but the implementation is native Windows: C#, WinUI 3, Windows App SDK, MVVM-style view models, and a clean provider architecture.

Current app version: `0.1.5`.

For a strict status check, see [docs/current-state-audit.md](docs/current-state-audit.md).
For Windows shell dogfooding checks, see [docs/windows-manual-verification.md](docs/windows-manual-verification.md).
For release-to-release dogfooding notes, see [docs/release-dogfooding.md](docs/release-dogfooding.md).
For provider integration dogfooding notes, see [docs/provider-dogfooding.md](docs/provider-dogfooding.md).
Create a timestamped local verification report with `.\scripts\new-windows-verification-report.ps1`.

## Features

- Starts minimized to the Windows tray.
- Prevents duplicate tray instances when launched twice.
- Optional start-at-login registration is available from Appearance settings.
- Left-click tray icon opens a compact usage panel.
- Right-click tray icon shows Show, Show Widget, Refresh Now, Settings, and Exit.
- Settings window uses WinUI `NavigationView`.
- Overview includes a first-run setup checklist with provider-specific setup decisions and action buttons until setup is marked complete.
- Providers shows per-provider setup guidance for source choices, Manual fallback, CLI/app-server caveats, API reference requirements, and stale/future snapshot timestamp warnings without echoing configured secret or scope values.
- CLI-backed provider settings can store a non-secret command override so refresh can use a known launchable CLI path when PATH discovery finds a broken WindowsApps alias; balanced outer quotes pasted around a Windows path are normalized away.
- Desktop widget window shows up to three selected providers, remembers placement, and has settings for startup/topmost/provider selection.
- Provider cards show health, usage percentage, reset text, redacted status messages, credits/costs, source, update time, stale/future timestamp warnings, and redacted errors.
- Provider Details shows non-secret snapshot details for identity, usage windows, credits, status, errors, and repair guidance.
- History page summarizes retained `history.ndjson` entries by provider without showing raw snapshot messages.
- Refresh settings include interval, notification enablement, history retention limits, a manual update check button, a confirmation-gated latest-update install action, startup update checks, optional verified auto-download/auto-install launch policy with explicit save-time confirmation for automatic install launch, and the last non-secret update status including the observed GitHub Release page, zip package/checksum assets, staged package/checksum/script/result paths, setup installer assets, and reconciled install result plus post-install validation status when present.
- Provider notifications redact snapshot-derived titles and body text before display.
- Appearance settings apply System, Light, or Dark theme to app windows.
- Privacy & Data shows a diagnostics summary with local file paths, config version, cached snapshot count, latest update time, diagnostics export counts, crash report counts, recent crash report metadata, and tracked file sizes.
- Privacy & Data shows storage pressure guidance for retained history, config backups, diagnostics exports, crash reports, and diagnostics log growth.
- Privacy & Data shows recovery guidance for choosing config backup export, latest-backup restore, reset-to-default recovery, or diagnostics export.
- Unexpected startup and WinUI failures write local redacted JSON crash reports under `%AppData%\WinAiUsageBar\crash-reports`; reports are pruned to a bounded recent set and are never sent anywhere automatically.
- Privacy & Data can clear cached snapshots and retained history without deleting config or saved secrets.
- Privacy & Data can export a timestamped `config.json` backup without copying secret values.
- Privacy & Data can validate and restore the latest config backup after explicit confirmation.
- Privacy & Data can reset `config.json` to defaults after explicit confirmation, creating a rollback backup and leaving saved secrets untouched.
- Privacy & Data can prune old config backups, diagnostics exports, and crash reports while keeping the newest 5 matched files.
- Config backup exports and rollback backups avoid same-second filename collisions by adding a numeric suffix when needed.
- Mock and Manual provider modes are implemented.
- Manual mode can track used/remaining percentage, reset datetime/description, credits, currency/unit, month cost, last-31-day tokens, and notes.
- CLI `--refresh-once` can run one headless provider refresh and print a safe snapshot summary, including a redacted secondary usage/rate-limit window when available, without launching UI.
- Tests include a headless UI composition smoke check that constructs the primary shell, provider, settings, widget, diagnostics, history, and secret editor view models without launching WinUI windows. This is not a visual UI automation substitute, but it catches broken non-window UI composition in CI.
- CLI `--set-provider-cli-override` can save a non-secret command override for CLI/local app-server providers without echoing the value, and `--clear-provider-cli-override` can remove it.
- CLI `--prune-support-artifacts` can prune old config backups, diagnostics exports, and crash reports without launching UI.
- CLI `--export-config-backup` can create a config-only backup without launching UI.
- CLI `--list-config-backups [--limit <N>]` can list app-owned config backups without reading or applying them.
- CLI `--validate-latest-config-backup` can validate the newest app-owned config backup without launching UI.
- CLI `--restore-latest-config-backup --confirm` can restore the newest app-owned config backup without launching UI.
- CLI `--reset-config-to-defaults --confirm` can reset `config.json` to defaults after creating a rollback backup without launching UI.
- CLI `--check-for-updates` can check GitHub Releases for a newer zip package, matching checksum, and setup installer asset visibility without launching UI.
- CLI `--download-update` can download a newer release zip plus checksum, stage it after SHA256 verification, and persist the staged package/checksum paths for later Health report and Refresh settings review when running against the real app version.
- CLI update commands support `--current-version <version>` for isolated release dogfooding without changing the app assembly version.
- CLI `--prepare-update-install` can generate a PowerShell script that applies a staged update after the app exits, runs the updated app's `--smoke-test`, captures validation stdout/stderr logs beside the result file, rolls back on validation failure, and writes `install-result.json`.
- CLI `--launch-prepared-update` can launch an app-owned, app-generated prepared update script without accepting arbitrary script paths.
- CLI `--install-latest-update` can explicitly check, download, verify, prepare, launch the latest update install script, and persist the real-version staged package/checksum/script/result status for later review.
- CLI `--run-startup-update-check` can run the configured startup update policy once without launching WinUI.
- Startup update checks record the latest release status at most once every 24 hours by default and, when enabled in Refresh settings, can automatically download verified packages or launch the prepared install script without relaunching the same release version repeatedly. On later startup or `--health-report`, the app safely reconciles app-owned `install-result.json` files back into update status, including post-install validation status and validation log path/byte metadata when present. Refresh settings can also explicitly run the same safe latest-update install flow on demand after in-app confirmation, then show the redacted recorded current/latest version, observed release page, zip package/checksum assets, setup installer assets, and any staged package/checksum, install script, install result, validation status, or validation log metadata.
- Codex/ChatGPT app-server probing is isolated behind safe abstractions, and `--health-report` shows storage pressure guidance, crash report metadata, recovery guidance, startup update status including the last observed release page and release asset names, and the configured provider CLI override or resolved CLI launch target used for startup checks.
- Codex CLI startup uses provider command overrides when configured, otherwise resolved Windows command paths, including `.cmd` shims and `.exe` paths. Startup failures are classified separately from auth and JSON-RPC errors with repair-oriented messages for WindowsApps/App Execution Alias or permission problems, including the option to set a provider CLI override to a launchable path.
- Claude, Claude Code, Gemini, OpenCode Zen, and GitHub Copilot have MVP-safe descriptors and manual mode support.
- Gemini and OpenCode Zen expose API key secret-name fields without storing API key values in config.
- JSON config, snapshot cache, history, diagnostics exports, crash reports, update staging, and local secrets are stored under `%AppData%\WinAiUsageBar`; config saves plus snapshot cache/history rewrites use unique temporary files so parallel CLI commands do not collide on fixed temp files, and loading an already-normalized config does not rewrite it during read-only diagnostics.

## Build And Run

Requirements:

- Windows 10/11
- .NET 8 SDK
- Windows App SDK dependencies restored from NuGet

```powershell
dotnet restore
dotnet build .\WinAIUsageBar.sln -p:Platform=x64
dotnet test .\tests\WinAiUsageBar.Core.Tests\WinAiUsageBar.Core.Tests.csproj -p:Platform=x64
dotnet run --project .\src\WinAiUsageBar.App\WinAiUsageBar.App.csproj -c Debug -r win-x64
```

If NuGet restore is flaky, use `.\scripts\restore.ps1` for bounded restore retries.

The app starts in the tray. Use the tray icon to open the compact panel or settings.

## Publish

Create a local self-contained build:

```powershell
.\scripts\publish.ps1
```

The default output is `artifacts\publish\WinAIUsageBar-win-x64`. Run `WinAiUsageBar.App.exe` from that folder. Pushes to `main` also upload a `WinAIUsageBar-win-x64` artifact from GitHub Actions.

Create a zip package and SHA256 checksum from the published output:

```powershell
.\scripts\package.ps1
Get-Content .\artifacts\packages\WinAIUsageBar-0.1.0-win-x64.zip.sha256
Get-FileHash .\artifacts\packages\WinAIUsageBar-0.1.0-win-x64.zip -Algorithm SHA256
```

By default, the package script reads the app version from `WinAiUsageBar.App.csproj` and creates `WinAIUsageBar-<version>-win-x64.zip`. Pass `-PackageName WinAIUsageBar-custom-name` to override the generated name.

Pushes to `main` upload both the publish directory and a `WinAIUsageBar-win-x64-package` artifact containing the versioned zip and `.sha256` file.

Create a local Windows setup executable with Inno Setup 6 installed:

```powershell
.\scripts\verify-installer-script.ps1
.\scripts\build-installer.ps1
```

The installer script is `installer\WinAIUsageBar.iss`, and the default setup output goes under `artifacts\installer`. The build script publishes the app first unless `-SkipPublish` is passed. It also writes `WinAIUsageBar-<version>-setup.exe.sha256`. If `ISCC.exe` is not on `PATH`, pass `-InnoSetupCompiler <path>` or install Inno Setup 6 locally. Pushes to `main` upload a `WinAIUsageBar-win-x64-installer` artifact from GitHub Actions.

### Unsigned installer notice

WinAI Usage Bar setup installer and executable are currently unsigned. Windows SmartScreen or unknown publisher warnings are expected until code signing is added. Download only from GitHub Releases. Verify the published SHA256 checksum before running the zip package or setup installer. Treat files from other locations as untrusted.

Dogfood a prepared update install against a disposable install directory:

```powershell
.\scripts\test-update-prepare-apply.ps1 -PackagePath .\artifacts\packages\WinAIUsageBar-0.1.3-win-x64.zip -Apply
```

The script uses an isolated app-data root under `artifacts\update-dogfood`, prepares `apply-update.ps1`, falls back to the script-adjacent result path if redirected GUI output mangles a non-ASCII `Result:` path, and only applies it when the install directory stays inside that work directory. With `-Apply`, it also checks that validation stdout/stderr log metadata points beside `install-result.json`.

Dogfood a published release-to-latest update flow without touching normal app data or an installed copy:

```powershell
.\scripts\test-published-update-flow.ps1 -FromTag v0.1.2 -ExpectedLatestTag v0.1.3
```

This downloads the older published zip, extracts it into an isolated temp workspace, and runs the older executable against the real GitHub latest-release endpoint. Releases before `v0.1.3` do not support `WINAIUSAGEBAR_APPDATA`, so the helper safely stops after discovery for those versions. For source releases `v0.1.3` and newer, pass `-Apply` when you want to download, prepare, and apply the latest update to the disposable extracted install directory.
Add `-AssertNormalAppDataUnchanged` when you want the helper to snapshot the normal `%AppData%\WinAiUsageBar\updates` directory before and after the isolated run and fail if it changed.

Dogfood the published startup update policy path with isolated app data:

```powershell
.\scripts\test-published-update-flow.ps1 -FromTag v0.1.4 -ExpectedLatestTag v0.1.5 -StartupPolicy -Apply -AssertNormalAppDataUnchanged
```

`-StartupPolicy` requires a source release that exposes `--run-startup-update-check` (`v0.1.4` or newer). It configures the extracted older release to enable startup update checks, verified automatic download, and guarded automatic install launch, then runs the startup policy command. With `-Apply`, the helper waits for the startup policy-launched script to update only the disposable extracted install directory, checks the updated version, checks `install-result.json` when the source release reports a result path, checks post-install validation status and validation log metadata when the source release writes them, and checks `--health-report` reconciliation when the target release supports it.

Dogfood the current updater implementation as if it were an older installed version:

```powershell
.\scripts\test-current-update-flow.ps1 -CurrentVersion 0.1.2 -ExpectedLatestTag v0.1.3 -Apply
```

This copies the current build to a disposable install directory, uses isolated app data, runs update check/download with `--current-version`, prepares an update script for the disposable install directory, and only applies it there when `-Apply` is passed. The helper verifies validation stdout/stderr log metadata for current generated scripts.

Run the published-app smoke test without opening UI:

```powershell
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --smoke-test
```

Published builds also support lightweight command-line checks:

```powershell
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --help
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --version
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --smoke-test
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --export-diagnostics
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --export-config-backup
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --health-report
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --refresh-once
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --refresh-once --provider Codex --source LocalAppServer
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --set-provider-cli-override --provider Codex --command C:\Tools\codex.cmd
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --clear-provider-cli-override --provider Codex
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --prune-support-artifacts --keep-newest 5
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --check-for-updates
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --download-update
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --download-update --current-version 0.1.2
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --prepare-update-install --package .\WinAIUsageBar-0.2.0-win-x64.zip --install-dir .\artifacts\publish\WinAIUsageBar-win-x64
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --launch-prepared-update --script $env:APPDATA\WinAiUsageBar\updates\install-example\apply-update.ps1
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --install-latest-update --restart-after-install
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --run-startup-update-check
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --provider-catalog
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --list-config-backups --limit 10
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --validate-latest-config-backup
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --validate-config-backup .\config-backup.json
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --restore-config-backup .\config-backup.json --confirm
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --restore-latest-config-backup --confirm
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --reset-config-to-defaults --confirm
```

The published app is a Windows GUI executable, so PowerShell scripts that need to wait for command-line completion should launch it with `Start-Process -Wait -PassThru` and redirect output if needed.

Set `WINAIUSAGEBAR_APPDATA` to an isolated directory when dogfooding update, diagnostics, backup, or refresh CLI flows without touching the normal `%AppData%\WinAiUsageBar` data:

```powershell
$env:WINAIUSAGEBAR_APPDATA = "$PWD\.tmp\winai-appdata"
$process = Start-Process -FilePath .\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe -ArgumentList "--download-update" -Wait -PassThru
$process.ExitCode
Remove-Item Env:\WINAIUSAGEBAR_APPDATA
```

Use `--smoke-test` to check config storage, DPAPI secret storage, provider descriptors, app service composition, and one non-UI refresh pipeline run against temporary app data. Use `--export-diagnostics` when you want a redacted support bundle on disk. Use `--export-config-backup` when you want a known-good config-only backup before changing settings. Use `--health-report` when you want a quick non-secret summary printed to the console, including storage pressure guidance, crash report metadata, recovery guidance, startup update status, observed release page, zip package/checksum assets, setup installer assets, post-install validation status and validation log metadata, safe CLI environment checks, provider CLI overrides, launch targets, and startup repair hints for `codex`, `claude`, `gh`, and `git`. Use `--refresh-once` to run enabled providers once, update local snapshots/history, and print a safe provider summary plus repair guidance without opening WinUI windows. Add `--provider <ProviderId>` and optional `--source <DataSourceKind>` to try one provider/source for that run only; these overrides are not saved to `config.json`. Use `--set-provider-cli-override --provider <ProviderId> --command <path-or-command>` to persist a non-secret CLI command override without printing the configured value, and `--clear-provider-cli-override --provider <ProviderId>` to remove it. Use `--prune-support-artifacts` to prune old config backups, diagnostics exports, and crash reports while keeping the newest 5 files by default, or pass `--keep-newest <N>`. Use `--check-for-updates` to check the latest GitHub Release for the expected versioned zip and `.sha256` assets, plus report whether the matching setup executable and setup checksum are published for manual installation. Use `--download-update` to download and SHA256-verify a newer release package into `%AppData%\WinAiUsageBar\updates`. Add `--current-version <version>` to `--check-for-updates`, `--download-update`, or `--install-latest-update` when dogfooding update behavior as if the current app were an older version; dogfood runs do not overwrite the recorded real current version or staged package/checksum/script/result paths. Use `--prepare-update-install --package <zip>` to generate, but not execute, a PowerShell script that waits for the app to exit, extracts the staged package, backs up the current install directory, copies the new files into place, runs the updated app's `--smoke-test`, writes `validation.out.txt` and `validation.err.txt` beside `install-result.json`, restores the backup if validation fails, writes `install-result.json` beside the script, and can restart the app when `--restart-after-install` is passed. Use `--launch-prepared-update --script <path>` to launch a prepared `apply-update.ps1`; the script must exist under `%AppData%\WinAiUsageBar\updates`, have the expected file name, and include the generated-script marker written by the preparation service. Use `--install-latest-update` to explicitly run the full safe update flow: check, download, verify, prepare, and launch the guarded apply script when a newer GitHub Release exists; real-version runs persist the non-secret staged package/checksum paths, install script/result paths, validation status, validation log paths/byte counts, and launched version status for Refresh settings and health reports. Use `--run-startup-update-check` to run the configured startup update policy once without opening WinUI; this command does not accept `--current-version` because startup policy must use the real app version. Update CLI outputs redact message, path, URL, and command fields before printing, and health/report UI surfaces validation log metadata without reading or printing log contents. Use `--provider-catalog` to inspect the built-in provider descriptors without reading local config. Use `--list-config-backups [--limit <N>]` to list app-owned backups by path, size, and timestamps without reading backup contents. Use `--validate-config-backup` to check a backup file before applying it, or `--validate-latest-config-backup` to check the newest app-owned backup discovered from diagnostics metadata. Use `--restore-config-backup <path> --confirm` to validate and restore a config backup after creating a rollback copy of the current `config.json`, or `--restore-latest-config-backup --confirm` to restore the newest app-owned backup discovered from diagnostics metadata. Use `--reset-config-to-defaults --confirm` to create a rollback backup, save default config settings, and leave saved secrets untouched.

## Release

Release notes are tracked in [CHANGELOG.md](CHANGELOG.md). Release notes must be written in English and generated with `.\scripts\new-release-notes.ps1`.

To create a draft GitHub Release:

1. Update the app version in `src\WinAiUsageBar.App\WinAiUsageBar.App.csproj`.
2. Commit the version change.
3. Generate and complete a Windows verification report:

```powershell
.\scripts\new-windows-verification-report.ps1
```

4. Publish, package, and verify release readiness:

```powershell
.\scripts\publish.ps1
.\scripts\package.ps1
.\scripts\build-installer.ps1 -SkipPublish
.\scripts\new-release-notes.ps1 -TagName v0.1.0 -OutputPath .\artifacts\release-notes.md
$report = Get-ChildItem .\artifacts\verification\windows-verification-*.md |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 1
.\scripts\verify-release-readiness.ps1 -TagName v0.1.0 -VerificationReportPath $report.FullName -RequireVerificationReport -RequireInstaller
```

5. Create and push a version tag:

```powershell
git tag v0.1.0
git push origin v0.1.0
```

The release workflow builds, tests, publishes, smoke-tests, packages the app, builds the setup installer, generates English release notes from `CHANGELOG.md`, and creates a draft release with the versioned zip, zip checksum, setup exe, and setup checksum attached. Review the draft in GitHub before publishing it.

## Privacy

- API keys, tokens, cookies, and auth file contents are not stored in plain text.
- `config.json` stores non-secret settings only.
- `DpapiSecretStore` stores secrets in protected files under `%AppData%\WinAiUsageBar\secrets`.
- Diagnostics pass through redaction before being stored or surfaced.
- Privacy & Data shows non-secret diagnostics metadata only; it does not list secret names or values.
- Privacy & Data recovery guidance is derived from non-secret diagnostics metadata only.
- Diagnostics can be exported from Privacy & Data; exports redact common secret shapes, account identifiers, scope/reference fields, local user profile paths, and CLI override paths, use bounded redaction context when truncating large files, omit local app-data and secret-store root paths, never include files under `secrets/`, and avoid overwriting same-second exports by adding a numeric suffix when needed.
- Crash reports are local JSON files only. They include timestamp, source, exception type, redacted message, redacted stack trace, app version, and optional redacted context; payload text also redacts local user profile paths. Privacy & Data lists recent crash report metadata only; it does not display message or stack trace contents.
- Secret values can be saved or deleted by secret name from Privacy & Data; values are never displayed back.
- Provider snapshots are sanitized before cache/history persistence, and legacy history summaries are sanitized before aggregation.
- Snapshot cache and retained history can be cleared from Privacy & Data; `config.json` and `secrets/` are left untouched.
- `config.json` can be backed up from Privacy & Data; backup files include non-secret settings only and do not copy `secrets/`.
- Config backups can be listed from the CLI with `--list-config-backups`; backups can be validated from the CLI with an explicit path or with `--validate-latest-config-backup`; backups can be restored from the CLI with an explicit path and `--confirm`, from the CLI using the latest app-owned backup with `--restore-latest-config-backup --confirm`, or from Privacy & Data using the latest backup and an in-app confirmation checkbox; restore creates a collision-resistant rollback backup first and does not copy or modify files under `secrets/`.
- `config.json` can be reset to defaults from the CLI with `--reset-config-to-defaults --confirm` or from Privacy & Data after an in-app confirmation checkbox; reset creates a collision-resistant rollback backup first and does not delete or modify files under `secrets/`.
- Old config backups, diagnostics exports, and crash reports can be pruned from Privacy & Data or the CLI; pruning only matches app-owned top-level filename patterns, keeps the newest 5 files by default, and never touches `config.json`, snapshots, history, diagnostics logs, updates, or `secrets/`.
- Browser cookie scraping is intentionally not implemented in this MVP.
- Codex integration never reads or displays `auth.json` contents.

## Provider Status

- Mock: implemented for UI development.
- Manual: implemented for every provider.
- Codex / ChatGPT: safe best-effort `codex app-server` JSON-RPC client and parser are implemented; provider settings can override the CLI command path; Windows command resolution prefers launchable `.exe`, `.cmd`, or `.bat` paths returned by `where.exe`; optional account/rate-limit/usage method failures or timeouts can return partial data, Codex snapshots preserve the selected `Cli` or `LocalAppServer` source kind, shared app-server snapshots use provider-specific usage and rate-limit labels, usage parsing recognizes top-level and nested usage/rate-limit windows, top-level usage arrays, plural window containers, token-counter quota aliases, common quota aliases such as consumed/total/remaining plus percent variants, and credit/cost/token summary aliases, reset timestamps support ISO strings, Unix seconds, Unix milliseconds, relative reset seconds, and generic reset-after aliases such as `resetAfter` and `retry_after`, and missing CLI/startup/auth failures return visible provider errors.
- Claude / Claude Code: CLI presence probe only with provider-specific CLI repair guidance; no private file scraping.
- Gemini: API key secret-name setting; no unofficial usage endpoint.
- OpenCode Zen: API key secret-name setting, manual balance mode, and documented TODO for future official balance API.
- GitHub Copilot: manual mode plus organization/enterprise metrics report metadata via the GitHub Copilot usage metrics API.

## Roadmap

- Add richer provider-specific settings pages.
- Add official API integrations where documented and permitted.
- Improve compact panel placement near the taskbar.
- Add charts from `history.ndjson`.
- Dogfood Windows local notification delivery across packaged and unpackaged runs.
- Package and sign the app for easier installation.
