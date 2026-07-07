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

GitHub Copilot supports personal Manual mode without organization metrics. Organization or Enterprise metrics preparation stores only organization/enterprise identifiers and a PAT secret name in config; the PAT value itself must be stored through `ISecretStore`.

When GitHub Copilot uses `OfficialApi`, the app resolves the configured PAT secret name through `ISecretStore` and requests the latest 28-day organization or enterprise Copilot usage metrics report metadata. The app displays report availability and date range only; it does not display signed download URLs or raw report contents.

Gemini and OpenCode Zen support provider-specific API settings for future integrations. The settings page stores only an API key secret name in `config.json`; the key value itself must be stored through `ISecretStore`. Manual mode remains valid without an API key reference.

## UI

- App starts minimized to tray.
- App uses a single-instance guard so launching it twice does not create duplicate tray icons or refresh loops.
- Tray left-click opens a compact panel.
- Tray right-click menu includes Show, Show Widget, Refresh Now, Settings, Exit.
- Main settings window contains Overview, Providers, Appearance, Widget, Refresh, Privacy & Data, and About pages.
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
- Credits/cost line
- Source kind
- Updated time
- Error message

## Storage

Files are stored under `%AppData%\WinAiUsageBar`:

- `config.json`
- `snapshots.json`
- `history.ndjson`
- `secrets/`

Secrets must go through `ISecretStore`; the DPAPI implementation protects values for the current Windows user.

The Privacy & Data page provides secret management by secret name. Users can save, check, and delete secret values. Secret values are never displayed back to the user, written to config, logged, or included in diagnostics exports.

The Privacy & Data page also shows a non-secret diagnostics summary with app data paths, config version, enabled provider count, refresh/notification state, cached snapshot count, latest snapshot update time, history retention settings, and tracked file sizes. The summary must not include secret values or configured secret reference names.

## Refresh

Supported intervals:

- Manual
- 1m
- 2m
- 5m
- 15m

The refresh service updates enabled providers asynchronously, caches snapshots, appends history, and keeps the previous successful usage data visible when a provider reports an error. Saving refresh settings restarts the timer so interval changes apply without restarting the app. Periodic refresh-level failures are written to diagnostics and future ticks continue.

Refresh settings also expose notification enablement and history retention limits. History max days is clamped to 1-3650 and max bytes is clamped to 100000-500000000 before saving.

## Notifications

When notifications are enabled, auth-required snapshots and providers with less than 20% remaining quota can emit Windows App SDK local app notifications. The notification service registers lazily and falls back silently when the current Windows runtime does not support app notifications, so refreshes do not fail because toast delivery failed.

## Manual Input Validation

Manual provider values are validated before saving:

- Blank numeric fields are saved as unknown values.
- Percent fields must parse as numbers. Values below 0 or above 100 are clamped to the nearest bound before saving.
- When both Used % and Remaining % are provided, they must add up to 100.
- Reset datetime must parse as an ISO-like datetime such as `2026-07-08T12:00:00Z`.
- Credits and month cost must parse as non-negative numbers and are rounded to two decimal places using away-from-zero midpoint rounding.
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

## Startup

Start-at-login uses the current user's Windows Run registry key and stores only the quoted app executable command. The setting is surfaced through Appearance settings and mirrored in `config.json` as `startup.launchOnLogin`.
