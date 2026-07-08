# Changelog

All notable changes to WinAI Usage Bar are documented here.

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
