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

Codex/ChatGPT app-server initialization is required, but account, rate-limit, and usage method calls should be treated as optional data sources after initialization. A non-auth JSON-RPC error from one optional method should be recorded as a redacted diagnostic while the client continues to later methods. Auth, login, unauthorized, malformed JSON, closed streams, and process startup failures should still become visible provider failures.

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

Provider repair guidance is derived from provider ID, health, and source kind. It should suggest safe next steps such as reconnecting credentials through Privacy & Data, checking CLI availability, switching to Manual mode, exporting diagnostics, or refreshing again. It must not display secret names, secret values, tokens, raw diagnostics, or configured account scope values.

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

The Privacy & Data page also shows a non-secret diagnostics summary with app data paths, config version, enabled provider count, refresh/notification state, cached snapshot count, latest snapshot update time, history retention settings, and tracked file sizes. The summary must not include secret values or configured secret reference names.

The Privacy & Data page shows storage pressure guidance derived from the non-secret diagnostics summary. Guidance should cover retained history size versus configured max bytes, config backup count/total size, and diagnostics log size. It must not inspect files under `secrets/` or display secret names, secret values, auth tokens, or provider auth file contents.

The Privacy & Data page shows recovery guidance derived from the non-secret diagnostics summary. Guidance should help users choose between exporting a config backup, restoring the latest backup, resetting config to defaults, and exporting diagnostics. It must not inspect, display, or depend on secret names or secret values.

The Privacy & Data page can clear `snapshots.json` and `history.ndjson` as local maintenance actions. These actions must not delete `config.json`, diagnostics exports, or files under `secrets/`.

The Privacy & Data page can export a timestamped `config.json` backup under `config-backups/`. Backups contain configuration settings only. They must not copy secret store files or secret values; configured secret names may remain as non-secret references so users can reconnect existing local secrets after restore.

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

Refresh settings also expose notification enablement and history retention limits. History max days is clamped to 1-3650 and max bytes is clamped to 100000-500000000 before saving.

## Notifications

When notifications are enabled, auth-required snapshots and providers with less than 20% remaining quota can emit Windows App SDK local app notifications. The notification service registers lazily and falls back silently when the current Windows runtime does not support app notifications, so refreshes do not fail because toast delivery failed.

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

The Privacy & Data page can create a diagnostics export under `%AppData%\WinAiUsageBar\diagnostics-exports`. The export may include `config.json`, `snapshots.json`, `history.ndjson`, and `diagnostics.log`, but it must redact common secret shapes at export time and must never include files from `secrets/`.

Diagnostics summary is separate from diagnostics export: the summary is for quick on-screen troubleshooting, while the export creates a redacted text bundle for deeper inspection.

The CLI health report includes a safe CLI environment section for `codex`, `claude`, `gh`, and `git`. It may resolve PATH entries and run short startup checks such as `--version`, but it must not read provider auth files, cookies, or secret store values.

## Startup

Start-at-login uses the current user's Windows Run registry key and stores only the quoted app executable command. The setting is surfaced through Appearance settings and mirrored in `config.json` as `startup.launchOnLogin`.
