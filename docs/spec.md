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

## UI

- App starts minimized to tray.
- Tray left-click opens a compact panel.
- Tray right-click menu includes Show, Show Widget, Refresh Now, Settings, Exit.
- Main settings window contains Overview, Providers, Appearance, Refresh, Privacy & Data, and About pages.
- Compact panel shows enabled provider cards.
- Widget window shows one to three provider cards and remembers size and position.

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

## Refresh

Supported intervals:

- Manual
- 1m
- 2m
- 5m
- 15m

The refresh service updates enabled providers asynchronously, caches snapshots, appends history, and keeps the previous successful usage data visible when a provider reports an error.

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
