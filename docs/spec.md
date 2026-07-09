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

The CLI can persist the same provider command override with `--set-provider-cli-override --provider <ProviderId> --command <path-or-command>`. The command must support only providers with `Cli` or `LocalAppServer` sources, reuse the same quote normalization and sensitive marker validation as the settings UI, save only the normalized non-secret command/path string to config, and print a status that does not echo the configured command. The CLI can remove the override with `--clear-provider-cli-override --provider <ProviderId>`; this clear command must use the same provider support validation and must not print the previous command/path value.

Codex/ChatGPT app-server initialization is required, but account, rate-limit, and usage method calls should be treated as optional data sources after initialization. A non-auth JSON-RPC error or timeout from one optional method should be recorded as a redacted diagnostic while the client continues to later methods. Successful optional calls may record method-name-only diagnostics, but must not log response bodies or user/account values. Auth, login, unauthorized, malformed JSON, closed streams, and process startup failures should still become visible provider failures.

Codex/ChatGPT usage parsing should recognize safe percent, ratio, fraction, amount, remaining, limit, reset timestamp, and reset duration aliases from top-level and nested usage/rate-limit windows. Ratio or fraction aliases with values from 0 to 1 should be converted to percentages; values above 1 should be treated as already-percent values. Sensitive-looking fields containing auth, token, secret, cookie, or key markers must be ignored even if their names otherwise resemble usage aliases.

Codex/ChatGPT credit parsing should recognize safe balance, currency, month-to-date cost, and last-31-day token aliases, including common snake_case shapes. Sensitive-looking fields must still be ignored unless they are exact safe token counter aliases.

The app-server `initialize` request should send `clientInfo.name` as `WinAI Usage Bar` and `clientInfo.version` from the app informational version, with a safe non-empty fallback when composition has no version metadata.

Codex/ChatGPT app-server JSON-RPC response matching must use only top-level envelope `id` values. Notifications and events without a top-level `id`, even when they contain nested fields such as `params.id`, must not be treated as responses or buffered pending responses.

Codex app-server adapters may be created for either `Cli` or `LocalAppServer` source selections. Successful snapshots and failure snapshots must preserve the configured source kind so provider cards, refresh output, diagnostics, and repair guidance describe the user's selected source accurately. ChatGPT app-server usage remains `LocalAppServer`.

Shared Codex/ChatGPT app-server snapshots must use provider-specific usage window labels, such as `Codex usage`, `Codex rate limit`, `ChatGPT usage`, and `ChatGPT rate limit`, so Provider Details and compact cards do not display Codex labels for ChatGPT snapshots.

Codex/ChatGPT app-server parsing should tolerate common usage and reset timestamp shapes, including ISO reset strings, Unix seconds, Unix milliseconds, relative reset seconds, and generic relative reset aliases such as `resetAfter`, `retryAfter`, and `retry_after`, while ignoring sensitive-looking token/auth/secret/cookie/key fields. Usage parsing should also recognize top-level usage window arrays and nested usage/rate-limit window objects or arrays under common names such as `usage`, `quota`, `limit`, `limits`, `rateLimit`, `rate_limit`, `rateLimits`, `rate_limits`, `window`, `windows`, `usageWindow`, `usage_window`, `usageWindows`, `usage_windows`, `current`, and `data`; low-level single-window parsing keeps top-level fields first, and nested candidates must be parsed as coherent windows in deterministic document order instead of mixing values from unrelated objects. Provider snapshots should promote the recognized usage or rate-limit window with the lowest remaining percent to `PrimaryWindow`, because that is the quota most likely to block the user first.

GitHub Copilot supports personal Manual mode without organization metrics. Organization or Enterprise metrics preparation stores only organization/enterprise identifiers and a PAT secret name in config; the PAT value itself must be stored through `ISecretStore`. The app should validate organization/enterprise scope before resolving the PAT secret so personal users can remain in Manual mode without needing a token.

When GitHub Copilot uses `OfficialApi`, the app resolves the configured PAT secret name through `ISecretStore` and requests the latest 28-day organization or enterprise Copilot usage metrics report metadata. The app displays report availability and date range only; it does not display signed download URLs or raw report contents. Missing scopes, missing PAT references, missing PAT values, and permission-denied metrics responses must surface as non-secret auth-required states with generic organization/enterprise permission guidance and Manual-mode fallback text.

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

Tray command routing is covered with fake services. Real `NotifyIcon` rendering and operating-system left-click behavior remain manual verification points because they require the Windows shell and WinUI runtime. A local opt-in UI launch smoke path may start the packaged app, activate either a minimal WinUI smoke window or the real Settings window briefly, verify that the process stays alive during the check, and exit cleanly; CI should only run syntax-safe checks for this helper unless a reliable interactive Windows desktop runner is available.

Compact panel placement uses the monitor under the current cursor position, reads that monitor's working area, infers the taskbar edge from the working-area inset, and clamps the panel inside the work area. If screen placement fails, the app falls back to a centered placement.

Provider cards show:

- Display name
- Health badge text
- Usage bar
- Reset text
- Redacted status message
- Credits/cost line
- Source kind
- Updated time and stale/future timestamp warning when applicable
- Redacted error message

Provider Details shows current provider snapshots in a fuller non-secret format:

- Provider ID, health, source, and updated age
- Identity summary when the snapshot provides one
- Primary and secondary usage windows
- Credits, cost, and token totals
- Redacted status and error text
- Non-secret repair guidance for warning, auth-required, unsupported, error, and unknown states

The History page summarizes retained `history.ndjson` data without displaying raw snapshot content. It shows total valid entries, invalid line count, earliest/latest update timestamps, and per-provider entry count, latest health, latest remaining percent, and latest source kind. It must not display raw status messages, error messages, account identities, or other unredacted snapshot payload text from history.

Provider repair guidance is derived from provider ID, health, source kind, and non-secret failure category. It should suggest safe next steps such as reconnecting credentials through Privacy & Data, checking CLI availability, switching to Manual mode, exporting diagnostics, or refreshing again. Codex/ChatGPT LocalAppServer startup failures that mention WindowsApps, App Execution Alias, access denial, or command startup failure should point users toward installing a launchable Codex CLI outside WindowsApps, setting a provider CLI command override to that launchable path, or repairing package permissions before rerunning the health report. Claude and Claude Code CLI failures should point users toward checking a launchable `claude` command, completing the provider CLI sign-in flow if needed, using Manual fallback, or setting a provider CLI command override when PATH discovery is unreliable. Claude and Claude Code CLI mode is a readiness placeholder until official usage telemetry, SDK, or documented API support is added; it must not run interactive `/usage` commands, scrape private local files, read auth files, or invent unofficial endpoints. It must not display secret names, secret values, tokens, raw diagnostics, command paths, or configured account scope values. Provider Details should also call out timestamp freshness problems without reading extra provider data. Future-dated snapshots beyond a small clock-skew tolerance should tell the user to check the system clock or refresh again. Cached snapshots older than the app's refresh-oriented freshness threshold should be marked stale so retained usage data is not mistaken for fresh provider data.

The first-run setup checklist guides users through enabling providers, choosing supported source modes, and preparing API credential or scope references when an API-backed source needs them. Checklist text may describe provider setup state, but it must not display secret names, secret values, organization names, enterprise slugs, PAT names, tokens, cookies, auth file contents, or account scope values. Provider and source-mode checklist actions should navigate to Providers. Missing API-reference checklist actions should navigate to Privacy & Data first so users can save secret values before storing only non-secret references in provider settings.

The first-run setup panel also shows provider-specific setup decisions for every descriptor. Decisions should cover disabled providers, Manual fallback, unsupported source modes, Mock mode, CLI setup, local app-server setup, API references that are ready, and API references that still need Privacy & Data setup. API decisions should show a short step list for saving the value in Privacy & Data, returning to Providers to configure non-secret references or scopes, refreshing, and using Manual mode while permissions or endpoints are not ready. Decision text must stay non-secret and route missing API references to both Privacy & Data and Providers while routing other setup decisions to Providers. Safe inline actions may enable a provider and apply Manual, Mock, or LocalAppServer source choices directly from first-run setup. Inline actions must not apply OfficialApi, save secret references, echo secret names, or perform provider-specific business logic in WinUI windows.

## Storage

Files are stored under `%AppData%\WinAiUsageBar`:

- `config.json`
- `snapshots.json`
- `history.ndjson`
- `config-backups/`
- `crash-reports/`
- `secrets/`

`config.json` also stores non-secret onboarding state, including whether first-run setup has been completed and when it was completed.

Config saves should write through a per-save unique temporary file before replacing `config.json`, so simultaneous CLI commands or app processes do not collide on a fixed `config.json.tmp` path. Best-effort cleanup should remove abandoned per-save temp files after failed saves when possible. Loading an already-normalized current config should not rewrite the file, while missing, corrupt, or migration-normalized configs should still be repaired and saved.

Snapshot cache and history rewrites should also write through per-save unique temporary files before replacing `snapshots.json` or `history.ndjson`, so overlapping refresh, CLI, or maintenance paths do not collide on fixed `.tmp` paths. Best-effort cleanup should remove abandoned snapshot/history temp files after failed writes when possible.

Secrets must go through `ISecretStore`; the DPAPI implementation protects values for the current Windows user.

The Privacy & Data page provides secret management by secret name. Users can save, check, and delete secret values. Secret values are never displayed back to the user, written to config, logged, or included in diagnostics exports.

The Privacy & Data page also shows a non-secret diagnostics summary with app data paths, config version, enabled provider count, refresh/notification state, cached snapshot count, latest snapshot update time, config backup count/size, diagnostics export count/size, recent crash report metadata, history retention settings, and tracked file sizes. The summary must not include secret values or configured secret reference names.

The Privacy & Data page shows storage pressure guidance derived from the non-secret diagnostics summary. Guidance should cover retained history size versus configured max bytes, config backup count/total size, diagnostics export count/total size, and diagnostics log size. It must not inspect files under `secrets/` or display secret names, secret values, auth tokens, or provider auth file contents.

The Privacy & Data page shows recovery guidance derived from the non-secret diagnostics summary. Guidance should help users choose between exporting a config backup, restoring the latest backup, resetting config to defaults, and exporting diagnostics. It must not inspect, display, or depend on secret names or secret values.

The Privacy & Data page can clear `snapshots.json` and `history.ndjson` as local maintenance actions. These actions must not delete `config.json`, diagnostics exports, or files under `secrets/`.

The Privacy & Data page can prune old retained support artifacts for config backups, diagnostics exports, and crash reports while keeping the newest 5 matched files. Pruning must only match app-owned top-level filename patterns under `config-backups/`, `diagnostics-exports/`, and `crash-reports/`. Crash report pruning must only match app-generated `crash-report-<timestamp>-<id>.json` names. It must not delete `config.json`, `snapshots.json`, `history.ndjson`, `diagnostics.log`, updates, unrelated files, nested files, or files under `secrets/`.

Unexpected startup and WinUI failures should write a local structured crash report under `crash-reports/`. Crash reports must be JSON with timestamp, source, exception type, redacted message, redacted stack trace, app version when available, and optional redacted context. The writer must never read provider auth files or secret-store contents, must redact sensitive-looking context names and values plus raw or JSON-escaped local Windows user profile paths in payload text, must use collision-resistant generated file names, and must prune to a bounded recent set when the app writes reports.

The Privacy & Data page can list recent app-generated crash report metadata from top-level `crash-report-<timestamp>-<id>.json` files only. The list may show timestamp, source, exception type, app version, file size, file path, and metadata parse status after redaction. It may offer an explicit read-only detail action for those same app-generated top-level report files; the detail view may show redacted metadata and a bounded redacted `Message` preview only. It must not display stack trace contents, context values, secret names, or secret values. Missing, oversized, locked, malformed, unsafe-path, or unreadable crash reports should appear as unavailable or unreadable metadata/details without crashing the page.

The CLI can run the same support artifact pruning with `--prune-support-artifacts [--keep-newest <N>]`. The default keep count is 5. Invalid, missing, duplicate, or unknown prune options must exit with code 2 and print help. Successful output must include non-secret matched, kept, deleted, and freed-byte counts for config backups, diagnostics exports, and crash reports.

The CLI can export a config-only backup with `--export-config-backup`. It must use the same config backup writer as Privacy & Data, include configuration settings only, leave `secrets/` untouched, and print the created path and timestamp without launching UI.

The CLI can list app-owned config backups with `--list-config-backups [--limit <N>]`. The default limit is 10, and `--limit` must accept only whole numbers from 1 to 100. Missing, duplicate, non-numeric, out-of-range, or unknown list options must exit with code 2 and print help. Listing must read only top-level files matching `config-backup-*.json` under `config-backups/`, include non-secret matched/listed counts, total bytes, path, size, created time, and modified time, and never read backup contents, inspect `secrets/`, create backups, restore, reset, prune, or modify files.

The CLI can check for GitHub Release updates with `--check-for-updates`. The check should use the public latest-release endpoint for `lingmulongtai/WinAI-Usage-Bar`, require no authentication, parse release tags such as `v0.1.1`, compare them with the current app informational version after stripping build metadata, and locate `WinAIUsageBar-<version>-win-x64.zip` plus its `.sha256` asset. It should also report whether `WinAIUsageBar-<version>-setup.exe` and its `.sha256` asset are present so manual installer availability is visible, but missing setup assets must not block the zip-based self-update path when the zip and checksum are valid. Missing releases, missing required zip assets, invalid versions, and network errors must be reported as non-crashing status output. This is the foundation for the later download/install updater flow.

Successful latest-release checks should persist the last observed setup installer asset name and setup checksum asset name in `config.updates` as non-secret status metadata. A release check that runs and finds missing setup assets should clear those observed names to `null`; startup checks that are disabled or skipped by cooldown should preserve the previous observed installer asset status.

The CLI can download a newer GitHub Release package with `--download-update`. The command should first run the same update check, skip safely when no update is available, download the zip and `.sha256` assets when an update is available, parse the checksum in `<sha256>  <filename>` format, verify the downloaded zip SHA256, and stage verified files under the app-owned `updates/` directory. Real-version runs must persist non-secret download status, release metadata, package/checksum asset names, staged package path, staged checksum path, and last checked time into `config.updates` so later health reports can explain what was staged. Asset names must be simple file names, temp files must use unique names, and checksum mismatches must not leave a final staged package or stale persisted staged paths. This command does not apply or install the package yet.

For update dogfooding, `--check-for-updates`, `--download-update`, and `--install-latest-update` can accept `--current-version <version>`. This override is only for headless CLI update testing; it must be validated as a simple SemVer-like version string, must not be persisted, must not persist staged package/checksum/script/result paths as real installed state, and must not affect WinUI, startup update policy, or normal update checks when omitted.

All update-related CLI formatters must redact user-controlled display fields before printing. This includes release/update messages, paths, URLs, command lines, and version strings accepted for dogfooding. Status enum names and normal non-secret package names should remain readable.

The CLI can prepare a staged update install with `--prepare-update-install --package <path> [--install-dir <path>] [--restart-after-install]`. The command validates that the package is an existing zip with `WinAiUsageBar.App.exe` at the archive root, rejects unsafe archive entries such as traversal paths, absolute paths, or invalid Windows file-name segments, validates that the install directory exists and contains `WinAiUsageBar.App.exe`, and writes an app-owned PowerShell apply script under `updates/`. The script should wait for the specified app process to exit, extract the package to a staging directory, back up the current install directory, copy staged files into the install directory, run the updated `WinAiUsageBar.App.exe --smoke-test`, capture smoke-test stdout/stderr to `validation.out.txt` and `validation.err.txt` beside `install-result.json`, restore from backup if the copy phase or post-install smoke test fails, write a non-secret `install-result.json` beside `apply-update.ps1` on success or failure, and optionally restart the app. Successful result files should include non-secret validation status, validation exit code, validation log paths, and validation log byte counts. Preparing an install must not execute the script automatically.

The CLI can launch a prepared update install script with `--launch-prepared-update --script <path>`. The command must only launch an existing `apply-update.ps1` under the app-owned `updates/` directory that includes the generated-script marker written by the preparation service, start PowerShell with `-NoProfile -ExecutionPolicy Bypass -File <script>`, and return a non-secret launch status. It must reject missing scripts, wrong file names, markerless scripts, and paths outside the app-owned update staging area so this command cannot become a general-purpose arbitrary script runner.

The CLI can explicitly install the latest GitHub Release with `--install-latest-update [--restart-after-install]`. This command should orchestrate the same safe primitives as separate commands: check latest release, skip with exit code 0 when up to date, download the package and checksum only when an update is available, verify SHA256 before preparing, generate an app-owned apply script for the current install directory, and launch only that guarded prepared script. Real-version runs must persist non-secret status, message, current/latest version, release metadata, observed release asset names, staged package/checksum paths when download succeeds, prepared install script/result paths when preparation succeeds, launched version when launch succeeds, and checked time when a release check ran. No-update and failed pre-download runs must clear stale staged package/checksum/script/result paths. This command must report which stage failed and return non-zero for update check, download, preparation, or launch failures.

At app startup, `IStartupUpdateService` may run the same safe update primitives according to `config.updates`. `checkOnStartup` enables the background latest-release check and records a non-crashing status in config. `minimumCheckIntervalHours` defaults to 24, can be set to 0 for every-startup checks, and must prevent unnecessary GitHub calls while preserving the last real check timestamp. `downloadAutomatically` allows downloading and SHA256-verifying a newer package into the app-owned updates directory. `installAutomatically` requires automatic download and allows preparing and launching only the app-owned update install script with restart-after-install enabled. The service records `lastInstallLaunchedVersion` so the same release version is not launched repeatedly across app starts. Startup update failures must be recorded as status/message fields and must not crash app startup.

Startup update checks must also reconcile any previously recorded `lastInstallResultPath` before recording the next startup update status. Reconciliation may only read app-owned `updates/**/install-result.json` files, must ignore missing, unsafe, oversized, or malformed files without crashing, and must persist only redacted `status`, `message`, validation status, validation exit code, parsed completion timestamp, validation log paths, and validation log byte counts back into `config.updates`. Validation log paths are accepted only when they point to the expected file names beside the reconciled result file; reconciliation must not read or print validation log contents.

The CLI can run the configured startup update policy once with `--run-startup-update-check` for headless dogfooding. This command must use the real app version, selected app-data root, and persisted config. It must not accept `--current-version`, because startup update behavior must match normal app startup.

The Refresh settings page also has a manual "Check for Updates Now" action. This action uses the same GitHub latest-release check as the CLI and startup update service, records non-secret status into `config.updates`, and must not download, prepare, launch, or install update packages.

The Refresh settings page has a separate explicit "Install Latest Update Now" action. This action must be disabled until the user checks an in-app confirmation checkbox. It uses the same safe latest-update install service as `--install-latest-update`, records non-secret status into `config.updates`, downloads only verified packages, prepares only app-owned update scripts, and launches only guarded update scripts under the app-owned updates directory.

The Refresh settings page must display the last non-secret update status in a readable summary. When recorded, the summary includes the last checked timestamp, startup update interval, status, latest version, current version, last launched install version, observed zip package/checksum asset names, staged package path, staged checksum path, observed setup installer asset names, prepared install script path, prepared install result path, install result status, install result completion timestamp, install result message, install validation status, install validation exit code, validation stdout/stderr log paths and byte counts, and update message. User-controlled strings and paths must be redacted before display. Missing package/installer/script/result/current-version/validation fields should be omitted instead of shown as empty values. The page must not read or display validation log contents.

The repository should support a setup-program build path. The installer uses Inno Setup and must package the published `WinAIUsageBar-win-x64` folder, install per user by default, create Start Menu shortcuts, optionally create a desktop shortcut, and launch the app after install when selected. The installer build script should locate `ISCC.exe`, publish the app unless skipped, pass version and path defines to the installer script, write a SHA256 checksum for the setup executable, and fail with a clear message when Inno Setup is missing. Main-branch CI should upload a setup artifact, and tag releases should attach the setup executable plus checksum to the draft GitHub Release.

Until code signing is implemented, release documentation must include an `Unsigned installer notice` for the setup installer and executable. The notice must state that the app is currently unsigned, Windows SmartScreen or unknown publisher warnings may appear, downloads should come only from GitHub Releases, and users should verify the published SHA256 checksum before running the zip package or setup installer. Signing remains future work; while the app is still unsigned, release readiness verification must fail if this warning disappears.

The Privacy & Data page can export a timestamped `config.json` backup under `config-backups/`. Backups contain configuration settings only. They must not copy secret store files or secret values; configured secret names may remain as non-secret references so users can reconnect existing local secrets after restore. Backup export and rollback writes should use per-write unique temp files and add a numeric filename suffix when the timestamp-based backup name already exists.

The CLI can validate a config backup with `--validate-config-backup <path>`. It can also validate the newest app-owned backup discovered from diagnostics metadata with `--validate-latest-config-backup`. Validation parses the file, runs current config migrations, and reports non-secret counts and warnings without applying the backup or modifying app data. Missing latest backups must return a non-zero exit code without creating, restoring, resetting, or modifying config or secrets. Unknown or duplicate latest-validation options must exit with code 2 and print help.

The CLI can restore a config backup with `--restore-config-backup <path> --confirm`. It can also restore the newest app-owned backup discovered from diagnostics metadata with `--restore-latest-config-backup --confirm`. Restore must validate and migrate the backup before applying it, create a rollback backup of the current `config.json` under `config-backups/`, save only configuration settings, and never copy, delete, or modify files under `secrets/`. Invalid backups, missing files, missing latest backups, unknown restore options, and calls without `--confirm` must return a non-zero exit code and leave the current config unchanged.

The CLI can reset `config.json` to the app defaults with `--reset-config-to-defaults --confirm`. Reset must create a rollback backup of the current `config.json` under `config-backups/`, save only default configuration settings, print non-secret config/provider counts and the rollback backup path, and never copy, delete, or modify files under `secrets/`. Missing confirmation, duplicate reset options, unknown reset options, and unavailable host handlers must return a non-zero exit code and leave the current config unchanged.

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

The CLI `--refresh-once` command runs the same enabled-provider refresh pipeline once without launching WinUI windows, composing tray/window/update services, or sending local notifications. It should use a CLI-only refresh composition, update snapshots/history, then print provider name, health, source, updated time, primary remaining percent, primary reset text, secondary usage/rate-limit window label/remaining/reset when present, credits, redacted status/error summaries, and non-secret repair guidance for non-OK snapshots. Auth-required snapshots should use generic CLI status/error text instead of echoing provider messages. GitHub Copilot OfficialApi missing-scope dogfood should return quickly with a safe AuthRequired report and should not leave snapshot/history temp files behind. It must not print raw diagnostics, identity fields, secret values, token values, cookies, auth file contents, configured organization names, configured enterprise slugs, configured secret names, or unredacted provider messages.

`--refresh-once --provider <ProviderId>` limits the run to one provider. `--refresh-once --provider <ProviderId> --source <DataSourceKind>` temporarily overrides that provider's source for the one-shot run. These overrides must be validated against provider descriptors and must not be saved to `config.json`.

Refresh settings also expose notification enablement, history retention limits, and startup update policy. History max days is clamped to 1-3650, max bytes is clamped to 100000-500000000, and startup update interval hours is clamped to 0-168 before saving. Automatic startup install must be rejected unless automatic startup download is enabled and the user checks a dedicated save-time confirmation box for automatic install launch.

## Notifications

When notifications are enabled, auth-required snapshots and providers with less than 20% remaining quota can emit Windows App SDK local app notifications. Notification titles and bodies must redact snapshot-derived provider names, error messages, and reset descriptions before display. The notification service registers lazily and falls back silently when the current Windows runtime does not support app notifications, so refreshes do not fail because toast delivery failed. Refresh scheduling must suppress duplicate notifications for the same provider and reason while the condition remains unchanged, notify again when the reason changes, and clear remembered notification state after recovery or while notifications are disabled.

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

The test suite should also include a headless UI composition smoke test that constructs the primary shell, provider card, provider details, provider settings, widget settings, first-run setup, refresh settings, diagnostics summary, history summary, and secret editor view models from isolated app data and sample snapshots without launching WinUI windows. This does not replace manual or future automated visual WinUI verification; it is a CI-safe guard against broken non-window UI composition.

The UI launch smoke helper should be opt-in for local Windows desktop dogfooding. It should support a minimal WinUI smoke target and a real Settings window target. Its command-line parser and script syntax should be covered in normal tests/CI, but the real WinUI activation run should not be required on headless CI hosts.

The Privacy & Data page can create a diagnostics export under `%AppData%\WinAiUsageBar\diagnostics-exports`. The export may include `config.json`, `snapshots.json`, `history.ndjson`, and `diagnostics.log`, but it must redact common secret shapes, account identifiers, organization/workspace scopes, enterprise slugs, secret reference names, PAT reference names, raw and JSON-escaped local Windows user profile paths, and CLI override paths at export time, must use a small bounded context window before truncated large-file tails so key/value pairs split by the truncation boundary can still be redacted, must omit local app-data and secret-store root paths from export metadata, and must never include files from `secrets/`. Diagnostics exports must include a small manifest summary with file counts, categories, included/missing states, and redaction notes without local app-data roots, usernames, paths, or secret names. Diagnostics export writes should use create-new semantics and add a numeric filename suffix when the timestamp-based export name already exists.

Diagnostics summary is separate from diagnostics export: the summary is for quick on-screen troubleshooting, while the export creates a redacted text bundle for deeper inspection.

Crash reports are local support artifacts only. They must not be uploaded automatically, and catalog/pruning helpers must only match top-level app-generated `crash-report-<timestamp>-<id>.json` files under `crash-reports/`.

The CLI health report includes a storage pressure section derived from the same non-secret guidance used by Privacy & Data. It should list retained history, config backups, diagnostics exports, crash reports, and diagnostics log pressure with level, detail, and recommendation text. When `config.json` is already normalized, this read-only report should not rewrite the config file.

The CLI health report includes a recovery guidance section derived from the same non-secret recovery guidance used by Privacy & Data. It should list config backup export, latest-backup restore, reset-to-defaults, and diagnostics export actions with availability and recommendation text. The CLI report should not add raw safety notes that mention secret-store paths; it must not include secret names, secret values, tokens, cookies, or raw auth details.

The CLI health report includes an Updates section derived from `config.updates`. It should list startup update enablement, interval, automatic download/install launch policy, last checked time, last status, current/latest versions, last launched install version, staged package path, observed setup installer asset names, prepared install script path, prepared install result path, install result status, install result completion timestamp, install result message, install validation status, install validation exit code, validation stdout/stderr log paths and byte counts, and update message when present. User-controlled strings and paths must be redacted before display, missing optional fields should appear as `n/a` without blank values, and validation log contents must not be read or printed.

Before printing the Updates section, the CLI health report should run the same safe install-result reconciliation used by startup update checks and save the config only when the recorded result changes.

The CLI health report also includes a safe CLI environment section for `codex`, `claude`, `gh`, and `git`. It should use configured provider CLI overrides for matching CLI/local app-server providers before falling back to PATH discovery, label those configured overrides in output, show the launch target selected by the shared CLI launch planner, and run short startup checks such as `--version`, but it must not read provider auth files, cookies, or secret store values. Startup failures should include short non-secret repair hints for common local issues such as WindowsApps access denial, command shims, stale PATH entries, timeouts, interactive login prompts, or setting a provider CLI override to a launchable path.

## Startup

Start-at-login uses the current user's Windows Run registry key and stores only the quoted app executable command. The setting is surfaced through Appearance settings and mirrored in `config.json` as `startup.launchOnLogin`.
