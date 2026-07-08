# Changelog

All notable changes to WinAI Usage Bar are documented here.

## Unreleased

### Added

- Added a confirmation-gated `--restore-latest-config-backup` CLI recovery command.
- Added a confirmation-gated `--reset-config-to-defaults` CLI recovery command.
- Added a `--validate-latest-config-backup` CLI recovery check.
- Added a `--list-config-backups` CLI recovery inventory command.
- `--refresh-once` now prints a redacted secondary usage window summary when provider snapshots include one.
- Codex app-server usage parsing now recognizes more real-world quota and reset aliases.
- Codex app-server usage parsing now recognizes nested usage/rate-limit windows without mixing unrelated candidates.
- Codex app-server usage parsing now recognizes top-level usage arrays and plural window containers such as `rateLimits` and `usageWindows`.
- Codex provider snapshots now preserve the selected `Cli` or `LocalAppServer` source kind instead of always reporting `LocalAppServer`.
- ChatGPT app-server snapshots now use ChatGPT-specific usage and rate-limit window labels instead of Codex labels.
- Codex app-server JSON-RPC envelope parsing now ignores nested notification ids and matches only top-level response ids.
- Codex app-server initialization now reports the current app informational version in `clientInfo.version`.
- Latest-release checks now persist the observed GitHub Release page URL for Refresh settings and health reports.
- A repository test guard now rejects common secret-shaped fixture patterns before they can trigger scanner noise again.
- Latest-release checks now persist observed zip package and checksum asset names for Refresh settings and health reports.
- Latest-release checks now persist observed setup installer asset status for Refresh settings and health reports.
- Added local redacted crash report files under app data with bounded catalog/pruning support for unexpected app failures.
- Diagnostics summary, health reports, storage pressure guidance, and support artifact pruning now include local crash report metadata.
- Real-version `--download-update` runs now persist downloaded package/checksum paths for Refresh settings and health reports without persisting dogfood version override paths.
- Real-version `--install-latest-update` runs now persist install status, staged package/checksum paths, prepared script/result paths, and launched version details without overwriting real install state during dogfood version override runs.
- Update checks now report setup installer and setup checksum asset availability alongside the zip package assets.
- Published release dogfooding can now exercise the startup update policy path with isolated app data.
- Reconcile app-owned update `install-result.json` files into saved update status during startup checks and health reports.
- Generated update apply scripts now run the updated app's `--smoke-test` before reporting success, roll back on validation failure, and persist non-secret validation status.
- Generated update apply scripts now retain post-install validation stdout/stderr logs beside `install-result.json`, and startup/health reconciliation persists only redacted log paths plus byte counts.
- Update dogfood helpers now fall back to script-adjacent result paths when redirected GUI output mangles non-ASCII paths, and current-flow helpers verify validation log metadata.
- Added a headless UI composition smoke test that constructs the primary settings, provider, widget, diagnostics, history, and shell view models without launching WinUI windows.
- Release documentation and readiness checks now require an unsigned installer warning with GitHub Releases and SHA256 verification guidance.
- Privacy & Data now lists recent crash report metadata without displaying crash messages or stack trace contents.
- Compact panel placement tests now cover taskbar-edge work areas, negative-coordinate monitors, and oversized panel clamping.
- First-run setup now shows provider-specific setup decisions for Manual, Mock, CLI, local app-server, API, disabled, and unsupported source states without exposing secrets.
- Provider Details now includes Claude and Claude Code CLI repair guidance without echoing paths, scopes, or secret-shaped diagnostics.
- Provider Details now flags stale cached snapshots and future-dated timestamps so clock skew or old data is visible.

## 0.1.4 - 2026-07-08

### Added

- Generated English GitHub Release notes from `CHANGELOG.md` during the release workflow.
- Added `--current-version <version>` for update CLI dogfooding without changing the app assembly version.
- Added a published release update discovery dogfood helper.
- Added a current-updater full-flow dogfood helper that downloads, verifies, prepares, and applies updates against a disposable install directory.

### Fixed

- Made the release notes English check safe under Windows PowerShell 5.1.
- Guarded legacy published-release dogfooding when the source version predates isolated app-data support.
- Hardened update dogfood helpers for non-ASCII workspace paths by falling back to isolated app-data file discovery.

## 0.1.3 - 2026-07-08

### Added

- Isolated CLI dogfooding app-data override through `WINAIUSAGEBAR_APPDATA`.
- Provider dogfooding notes for Codex WindowsApps startup-denied environments.
- Disposable prepared-update dogfood script for update apply checks against temporary install directories.

### Fixed

- Wrote generated update apply scripts with a UTF-8 BOM so Windows PowerShell 5.1 preserves Japanese and other non-ASCII paths.
- Documented and tested Codex WindowsApps/App Execution Alias startup failures with provider CLI override repair guidance.

## 0.1.2 - 2026-07-08

### Added

- Startup update policy for checking, downloading, and guarded install launching without repeatedly launching the same release.
- Manual Refresh settings actions for checking the latest GitHub Release and explicitly launching the latest update install flow.
- Provider CLI command path overrides, including a headless setter for Codex and other CLI-backed sources.
- CLI config backup export for safer support and recovery workflows.

### Fixed

- Suppressed duplicate provider notifications until the alert reason changes or the provider recovers.
- Tolerated optional Codex app-server method timeouts while continuing later usage calls.
- Added Codex WindowsApps/app execution alias repair guidance.
- Required explicit confirmation before manually launching update installs from the UI.
- Added rollback to generated update apply scripts when the install copy phase fails.
- Rejected unsafe update package archive entries before preparing an install script.

## 0.1.1 - 2026-07-08

### Added

- GitHub Release update flow commands for checking, downloading, verifying, preparing, launching, and explicitly installing the latest update.
- Inno Setup installer scaffold, setup executable build script, installer checksum generation, and installer verification script.
- Main CI installer artifact upload and draft release setup executable assets.

## 0.1.0 - 2026-07-08

### Added

- WinUI 3 tray-first MVP with compact panel, settings window, and desktop widget window.
- Provider architecture with Mock, Manual, Codex/ChatGPT app-server probing, CLI probes, and GitHub Copilot metrics metadata support.
- Manual provider editing for usage percentages, reset details, credits, currency/unit, cost, token counts, and notes.
- Local JSON config, snapshot cache, retained history, diagnostics export, and local data maintenance actions.
- DPAPI-backed secret storage abstraction and Privacy & Data secret management.
- Refresh scheduling, notification settings, history retention settings, and startup registration.
- Published-app smoke test mode, packaged zip artifacts, SHA256 checksums, and draft release workflow.
