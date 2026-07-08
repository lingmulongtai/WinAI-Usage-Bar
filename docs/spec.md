# Product Spec

## Summary

WinAI Usage Bar is a Windows tray app for monitoring usage across AI coding and chat providers. It uses native WinUI 3 controls and keeps the default Fluent look.

## Providers

Initial provider IDs:

- ChatGPT
- Codex
- Gemini
- Claude
- ClaudeCode
- OpenCodeZen
- GitHubCopilot

Every provider supports Manual mode first. Automatic integrations are best-effort and must return `AuthRequired`, `Unsupported`, or `Error` snapshots instead of crashing.

CLI-based integrations use `ICommandProbe` to separate a missing command from a discovered command. Infrastructure launch code should use the resolved Windows command path when available, prefer launchable `.exe`, `.cmd`, or `.bat` paths over extensionless aliases, and route `.cmd`/`.bat` shims through the Windows command processor. Provider adapters should classify Windows startup failures, such as app execution alias or permission problems, as visible repairable failures instead of generic provider errors.

Provider settings may store a non-secret CLI command override for providers that support `Cli` or `LocalAppServer` sources. The override is a single command/path string, must not contain tokens, cookies, or auth values, and must not be echoed in setup guidance. Balanced outer quotes pasted around a Windows path should be normalized away; partial or embedded quotes should be rejected. Codex/ChatGPT LocalAppServer refresh should try the configured override before normal PATH discovery so users can bypass broken WindowsApps aliases.

The CLI can persist the same provider command override with `--set-provider-cli-override --provider <ProviderId> --command <path-or-command>`. The command must support only providers with `Cli` or `LocalAppServer` sources, reuse the same quote normalization and sensitive marker validation as the settings UI, save only the normalized non-secret command/path string to config, and print a status that does not echo the configured command.

Codex/ChatGPT app-server initialization is required, but account, rate-limit, and usage method calls should be treated as optional data sources after initialization. A non-auth JSON-RPC error or timeout from one optional method should be recorded as a redacted diagnostic while the client continues to later methods. Auth, login, unauthorized, malformed JSON, closed streams, and process startup failures should still become visible provider failures.

Codex/ChatGPT app-server parsing should tolerate common usage and reset timestamp shapes, including ISO reset strings, Unix seconds, Unix milliseconds, and relative reset seconds, while ignoring sensitive-looking token/auth/secret/cookie/key fields.

GitHub Copilot supports personal Manual mode without organization metrics. Organization or Enterprise metrics preparation stores only organization/enterprise identifiers and a PAT secret name in config; the PAT value itself must be stored through `ISecretStore`.

When GitHub Copilot uses `OfficialApi`, the app resolves the configured PAT secret name through `ISecretStore` and requests the latest 28-day organization or enterprise Copilot usage metrics report metadata. The app displays report availability and date range only; it does not display signed download URLs or raw report contents.

Gemini and OpenCode Zen support provider-specific API settings for future integrations. The settings page stores only an API key secret name in `config.json`; the key value itself must be stored through `ISecretStore`. Manual mode remains valid without an API key reference.

## UI

- App starts minimized to tray.
- App uses a single-instance guard so launching it twice does not create duplicate tray icons or refresh loops.
- Tray left-click opens a compact panel.
- Tray right-click menu includes Show, Show Widget, Refresh Now, Settings, Exit.
- Main settings window contains Overview, Providers, Provider Details, Appearance, Widget, History, Refresh, Privacy & Data, and About pages.
- Overview shows a first-run setup checklist with action buttons until the user marks setup complete.
- Providers shows per-provider setup guidance derived from descriptors and current source selection. Guidance should explain enabled/disabled state, source support, Manual fallback, CLI/app-server caveats, and API reference requirements. It must not echo configured secret names, secret values, organization names, enterprise slugs, PAT names, tokens, cookies, or auth file contents.
- Compact panel shows enabled provider cards.
- Widget window shows one to three provider cards and remembers size and position.
- Widget settings validate that one to three providers are selected and expose show-on-startup and always-on-top toggles.
- Appearance settings can enable start-at-login registration for the current Windows user.
- Appearance settings apply the saved System, Light, or Dark theme to settings, compact, and widget windows.

Tray command routing is covered with fake services. Real `NotifyIcon` rendering, WinUI window activation, and operating-system left-click behavior remain manual verification points because they require the Windows shell and WinUI runtime.

Compact panel placement uses the monitor under the current cursor position, reads that monitor's working area, infers the taskbar edge from the working-area inset, and clamps the panel inside the work area. If screen placement fails, the app falls back to a centered placement.

Provider cards show:

- Display name
- Health badge text
- Usage bar
- Reset text
- Status message
- Credits/cost line
- Source kind
- Updated time
- Error message

Provider Details shows current provider snapshots in a fuller non-secret format:

- Provider ID, health, source, and updated age
- Identity summary when the snapshot provides one
- Primary and secondary usage windows
- Credits, cost, and token totals
- Redacted status and error text
- Non-secret repair guidance for warning, auth-required, unsupported, error, and unknown states

The History page summarizes retained `history.ndjson` data without displaying raw snapshot content. It shows total valid entries, invalid line count, earliest/latest update timestamps, and per-provider entry count, latest health, latest remaining percent, and latest source kind. It must not display raw status messages, error messages, account identities, or other unredacted snapshot payload text from history.

Provider repair guidance is derived from provider ID, health, source kind, and non-secret failure category. It should suggest safe next steps such as reconnecting credentials through Privacy & Data, checking CLI availability, switching to Manual mode, exporting diagnostics, or refreshing again. Codex/ChatGPT LocalAppServer startup failures that mention WindowsApps, App Execution Alias, access denial, or command startup failure should point users toward installing a launchable Codex CLI outside WindowsApps, setting a provider CLI command override to that launchable path, or repairing package permissions before rerunning the health report. It must not display secret names, secret values, tokens, raw diagnostics, or configured account scope values.

The first-run setup checklist guides users through enabling providers, choosing supported source modes, and preparing API credential or scope references when an API-backed source needs them. Checklist text may describe provider setup state, but it must not display secret names, secret values, or account scope values. Provider and source-mode checklist actions should navigate to Providers. Missing API-reference checklist actions should navigate to Privacy & Data first so users can save secret values before storing only secret-name references in provider settings.

## Storage

Files are stored under `%AppData%\WinAiUsageBar`:

- `config.json`
- `snapshots.json`
- `history.ndjson`
- `config-backups/`
- `secrets/`

`config.json` also stores non-secret onboarding state, including whether first-run setup has been completed and when it was completed.

Config saves should write through a per-save unique temporary file before replacing `config.json`, so simultaneous CLI commands or app processes do not collide on a fixed `config.json.tmp` path. Best-effort cleanup should remove abandoned per-save temp files after failed saves when possible.

Secrets must go through `ISecretStore`; the DPAPI implementation protects values for the current Windows user.

The Privacy & Data page provides secret management by secret name. Users can save, check, and delete secret values. Secret values are never displayed back to the user, written to config, logged, or included in diagnostics exports.

The Privacy & Data page also shows a non-secret diagnostics summary with app data paths, config version, enabled provider count, refresh/notification state, cached snapshot count, latest snapshot update time, config backup count/size, diagnostics export count/size, history retention settings, and tracked file sizes. The summary must not include secret values or configured secret reference names.

The Privacy & Data page shows storage pressure guidance derived from the non-secret diagnostics summary. Guidance should cover retained history size versus configured max bytes, config backup count/total size, diagnostics export count/total size, and diagnostics log size. It must not inspect files under `secrets/` or display secret names, secret values, auth tokens, or provider auth file contents.

The Privacy & Data page shows recovery guidance derived from the non-secret diagnostics summary. Guidance should help users choose between exporting a config backup, restoring the latest backup, resetting config to defaults, and exporting diagnostics. It must not inspect, display, or depend on secret names or secret values.

The Privacy & Data page can clear `snapshots.json` and `history.ndjson` as local maintenance actions. These actions must not delete `config.json`, diagnostics exports, or files under `secrets/`.

The Privacy & Data page can prune old retained support artifacts for config backups and diagnostics exports while keeping the newest 5 matched files. Pruning must only match app-owned top-level filename patterns under `config-backups/` and `diagnostics-exports/`. It must not delete `config.json`, `snapshots.json`, `history.ndjson`, `diagnostics.log`, unrelated files, nested files, or files under `secrets/`.

The CLI can run the same support artifact pruning with `--prune-support-artifacts [--keep-newest <N>]`. The default keep count is 5. Invalid, missing, duplicate, or unknown prune options must exit with code 2 and print help. Successful output must include non-secret matched, kept, deleted, and freed-byte counts for config backups and diagnostics exports.

The CLI can export a config-only backup with `--export-config-backup`. It must use the same config backup writer as Privacy & Data, include configuration settings only, leave `secrets/` untouched, and print the created path and timestamp without launching UI.

The CLI can check for GitHub Release updates with `--check-for-updates`. The check should use the public latest-release endpoint for `lingmulongtai/WinAI-Usage-Bar`, require no authentication, parse release tags such as `v0.1.1`, compare them with the current app informational version after stripping build metadata, and locate `WinAIUsageBar-<version>-win-x64.zip` plus its `.sha256` asset. Missing releases, missing assets, invalid versions, and network errors must be reported as non-crashing status output. This is the foundation for the later download/install updater flow.

The CLI can download a newer GitHub Release package with `--download-update`. The command should first run the same update check, skip safely when no update is available, download the zip and `.sha256` assets when an update is available, parse the checksum in `<sha256>  <filename>` format, verify the downloaded zip SHA256, and stage verified files under the app-owned `updates/` directory. Asset names must be simple file names, temp files must use unique names, and checksum mismatches must not leave a final staged package. This command does not apply or install the package yet.

For update dogfooding, `--check-for-updates`, `--download-update`, and `--install-latest-update` can accept `--current-version <version>`. This override is only for headless CLI update testing; it must be validated as a simple SemVer-like version string, must not be persisted, and must not affect WinUI, startup update policy, or normal update checks when omitted.

The CLI can prepare a staged update install with `--prepare-update-install --package <path> [--install-dir <path>] [--restart-after-install]`. The command validates that the package is an existing zip with `WinAiUsageBar.App.exe` at the archive root, rejects unsafe archive entries such as traversal paths, absolute paths, or invalid Windows file-name segments, validates that the install directory exists and contains `WinAiUsageBar.App.exe`, and writes an app-owned PowerShell apply script under `updates/`. The script should wait for the specified app process to exit, extract the package to a staging directory, back up the current install directory, copy staged files into the install directory, restore from backup if the copy phase fails, and optionally restart the app. Preparing an install must not execute the script automatically.

The CLI can launch a prepared update install script with `--launch-prepared-update --script <path>`. The command must only launch an existing `apply-update.ps1` under the app-owned `updates/` directory, start PowerShell with `-NoProfile -ExecutionPolicy Bypass -File <script>`, and return a non-secret launch status. It must reject missing scripts, wrong file names, and paths outside the app-owned update staging area so this command cannot become a general-purpose arbitrary script runner.

The CLI can explicitly install the latest GitHub Release with `--install-latest-update [--restart-after-install]`. This command should orchestrate the same safe primitives as separate commands: check latest release, skip with exit code 0 when up to date, download the package and checksum only when an update is available, verify SHA256 before preparing, generate an app-owned apply script for the current install directory, and launch only that guarded prepared script. This command must report which stage failed and return non-zero for update check, download, preparation, or launch failures.

At app startup, `IStartupUpdateService` may run the same safe update primitives according to `config.updates`. `checkOnStartup` enables the background latest-release check and records a non-crashing status in config. `minimumCheckIntervalHours` defaults to 24, can be set to 0 for every-startup checks, and must prevent unnecessary GitHub calls while preserving the last real check timestamp. `downloadAutomatically` allows downloading and SHA256-verifying a newer package into the app-owned updates directory. `installAutomatically` requires automatic download and allows preparing and launching only the app-owned update install script with restart-after-install enabled. The service records `lastInstallLaunchedVersion` so the same release version is not launched repeatedly across app starts. Startup update failures must be recorded as status/message fields and must not crash app startup.

The Refresh settings page also has a manual "Check for Updates Now" action. This action uses the same GitHub latest-release check as the CLI and startup update service, records non-secret status into `config.updates`, and must not download, prepare, launch, or install update packages.

The Refresh settings page has a separate explicit "Install Latest Update Now" action. This action must be disabled until the user checks an in-app confirmation checkbox. It uses the same safe latest-update install service as `--install-latest-update`, records non-secret status into `config.updates`, downloads only verified packages, prepares only app-owned update scripts, and launches only guarded update scripts under the app-owned updates directory.

The repository should support a setup-program build path. The installer uses Inno Setup and must package the published `WinAIUsageBar-win-x64` folder, install per user by default, create Start Menu shortcuts, optionally create a desktop shortcut, and launch the app after install when selected. The installer build script should locate `ISCC.exe`, publish the app unless skipped, pass version and path defines to the installer script, write a SHA256 checksum for the setup executable, and fail with a clear message when Inno Setup is missing. Main-branch CI should upload a setup artifact, and tag releases should attach the setup executable plus checksum to the draft GitHub Release.

The Privacy & Data page can export a timestamped `config.json` backup under `config-backups/`. Backups contain configuration settings only. They must not copy secret store files or secret values; configured secret names may remain as non-secret references so users can reconnect existing local secrets after restore. Backup export and rollback writes should use per-write unique temp files and add a numeric filename suffix when the timestamp-based backup name already exists.

The CLI can validate a config backup with `--validate-config-backup <path>`. Validation parses the file, runs current config migrations, and reports non-secret counts and warnings without applying the backup or modifying app data.

The CLI can restore a config backup with `--restore-config-backup <path> --confirm`. Restore must validate and migrate the backup before applying it, create a rollback backup of the current `config.json` under `config-backups/`, save only configuration settings, and never copy, delete, or modify files under `secrets/`. Invalid backups, missing files, and calls without `--confirm` must return a non-zero exit code and leave the current config unchanged.

The Privacy & Data page can validate and restore the latest config backup from `config-backups/`. In-app restore must require explicit confirmation, use the same validation and rollback restore services as the CLI, leave `secrets/` untouched, and restart the refresh schedule after a successful restore.

The Privacy & Data page can reset `config.json` to the app defaults. In-app reset must require explicit confirmation, create a rollback backup of the current `config.json` under `config-backups/`, save only default configuration settings, leave `secrets/` untouched, and restart the refresh schedule after a successful reset.

## Refresh

Supported intervals:

- Manual
- 1m
- 2m
- 5m
- 15m

The refresh service updates enabled providers asynchronously, caches snapshots, appends history, and keeps the previous successful usage data visible when a provider reports an error. Saving refresh settings restarts the timer so interval changes apply without restarting the app. Periodic refresh-level failures are written to diagnostics and future ticks continue.

The CLI `--refresh-once` command runs the same enabled-provider refresh pipeline once without launching WinUI windows or sending local notifications. It should update snapshots/history, then print provider name, health, source, updated time, remaining percent, reset text, credits, redacted status/error summaries, and non-secret repair guidance for non-OK snapshots. Auth-required snapshots should use generic CLI status/error text instead of echoing provider messages. It must not print raw diagnostics, identity fields, secret values, token values, cookies, auth file contents, configured organization names, configured enterprise slugs, configured secret names, or unredacted provider messages.

`--refresh-once --provider <ProviderId>` limits the run to one provider. `--refresh-once --provider <ProviderId> --source <DataSourceKind>` temporarily overrides that provider's source for the one-shot run. These overrides must be validated against provider descriptors and must not be saved to `config.json`.

Refresh settings also expose notification enablement, history retention limits, and startup update policy. History max days is clamped to 1-3650, max bytes is clamped to 100000-500000000, and startup update interval hours is clamped to 0-168 before saving. Automatic startup install must be rejected unless automatic startup download is enabled.

## Notifications

When notifications are enabled, auth-required snapshots and providers with less than 20% remaining quota can emit Windows App SDK local app notifications. The notification service registers lazily and falls back silently when the current Windows runtime does not support app notifications, so refreshes do not fail because toast delivery failed. Refresh scheduling must suppress duplicate notifications for the same provider and reason while the condition remains unchanged, notify again when the reason changes, and clear remembered notification state after recovery or while notifications are disabled.

## Manual Input Validation

Manual provider values are validated before saving:

- Blank numeric fields are saved as unknown values.
- Percent fields must parse as numbers. Values below 0 or above 100 are clamped to the nearest bound before saving.
- When both Used % and Remaining % are provided, they must add up to 100.
- Reset datetime must parse as an ISO-like datetime such as `2026-07-08T12:00:00Z`.
- Reset description and notes are trimmed before saving.
- Credits and month cost must parse as non-negative numbers and are rounded to two decimal places using away-from-zero midpoint rounding.
- Currency/unit is optional, trimmed before saving, and limited to 16 characters.
- Tokens last 31 days is optional and must parse as a non-negative whole number.
- Invalid text keeps the settings page open and is shown as a non-crashing validation error.

## Security

- No plaintext secrets in config.
- No secret values in diagnostics.
- No automatic browser cookie scraping in MVP.
- Codex integration does not read or display `auth.json` contents.
- Tests cover diagnostic redaction.

## Diagnostics Export

The CLI `--smoke-test` command runs without opening WinUI windows. It should use temporary app data to verify default config creation, config round-trip, DPAPI secret round-trip, provider descriptor registration, app service composition, refresh-service initialization, and one non-UI refresh pipeline run with default safe provider sources. It must clean up temporary app data and must not send local notifications.

The Privacy & Data page can create a diagnostics export under `%AppData%\WinAiUsageBar\diagnostics-exports`. The export may include `config.json`, `snapshots.json`, `history.ndjson`, and `diagnostics.log`, but it must redact common secret shapes at export time and must never include files from `secrets/`. Diagnostics export writes should use create-new semantics and add a numeric filename suffix when the timestamp-based export name already exists.

Diagnostics summary is separate from diagnostics export: the summary is for quick on-screen troubleshooting, while the export creates a redacted text bundle for deeper inspection.

The CLI health report includes a storage pressure section derived from the same non-secret guidance used by Privacy & Data. It should list retained history, config backups, diagnostics exports, and diagnostics log pressure with level, detail, and recommendation text.

The CLI health report includes a recovery guidance section derived from the same non-secret recovery guidance used by Privacy & Data. It should list config backup export, latest-backup restore, reset-to-defaults, and diagnostics export actions with availability and recommendation text. The CLI report should not add raw safety notes that mention secret-store paths; it must not include secret names, secret values, tokens, cookies, or raw auth details.

The CLI health report also includes a safe CLI environment section for `codex`, `claude`, `gh`, and `git`. It may resolve PATH entries, show the launch target selected by the shared CLI launch planner, and run short startup checks such as `--version`, but it must not read provider auth files, cookies, or secret store values. Startup failures should include short non-secret repair hints for common local issues such as WindowsApps access denial, command shims, stale PATH entries, timeouts, interactive login prompts, or setting a provider CLI override to a launchable path.

## Startup

Start-at-login uses the current user's Windows Run registry key and stores only the quoted app executable command. The setting is surfaced through Appearance settings and mirrored in `config.json` as `startup.launchOnLogin`.
