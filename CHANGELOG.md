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
- Latest-release checks now persist observed setup installer asset status for Refresh settings and health reports.
- Update checks now report setup installer and setup checksum asset availability alongside the zip package assets.
- Published release dogfooding can now exercise the startup update policy path with isolated app data.
- Reconcile app-owned update `install-result.json` files into saved update status during startup checks and health reports.

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
