# Current State Audit

Date: 2026-07-08

This audit is intentionally strict. The repository has moved past a throwaway scaffold, but it is still an MVP. The design foundation is much stronger than the product completeness.

Latest published release: `v0.1.4`. It adds English release-note generation from the changelog, safer update CLI version override dogfooding, published-release discovery dogfooding, and published `v0.1.3 -> v0.1.4` disposable update download/prepare/apply dogfooding.

## Scorecard

| Area | Score | Read |
| --- | ---: | --- |
| Architecture foundation | 8/10 | The layer split is real, testable, and mostly respected. |
| Provider extensibility | 7/10 | Descriptors, adapters, manual mode, and safe failure states are in place. Real provider depth is still thin. |
| Security posture | 8/10 | Good defaults around DPAPI, display/notification redaction, no cookie scraping, checksum verification, guarded update launch, post-install validation, rollback, unsafe update zip rejection, and disposable update-apply dogfooding. Needs more adversarial review before wider distribution. |
| Windows shell integration | 6/10 | Tray, windows, placement, startup registration, and notifications exist, with duplicate notification suppression, compact panel placement edge-case tests, a manual verification checklist, and report scaffold. Actual shell behavior still needs hands-on runs. |
| Product usability | 6/10 | The app now has guided first-run checklist state, provider-specific setup decisions, provider details, backup export/restore, and recovery checks, but it is still mostly useful with Mock/Manual data today. |
| Packaging and release | 9/10 | Self-contained publish, zip packaging, checksums, readiness gates, artifacts, release workflow, English release notes generated from the changelog, published setup assets, unsigned installer warning, update-check, verified download, install-script preparation, rollback-capable guarded script launch with post-install smoke validation, explicit latest-update install orchestration, setup installer artifact/release paths, throttled startup update policy, current-version update dogfooding, and published disposable release-to-release update dogfooding exist. No signing yet. |
| Test confidence | 8/10 | Core, infrastructure, view model, CLI, storage, parser, refresh, app-composition smoke, headless UI composition smoke, and packaging smoke paths are covered without external CLIs. Visual UI runtime coverage remains limited. |
| Observability and support | 8/10 | Diagnostics summary, provider repair guidance, recovery guidance, redacted export, health report, isolated app-data dogfooding, provider dogfooding notes, release dogfooding notes, validation log metadata, logs, recent crash report metadata UI, and local redacted crash reports are solid for an MVP. No remote crash reporting or crash-detail viewer yet. |

Overall:

- Design foundation: about 85-90% of the intended MVP foundation.
- Personal dogfooding readiness: about 82-86%.
- Public release readiness: about 55-60%.

## What Is Strong

- The repo has a real solution structure: `Core`, `Infrastructure`, `App`, and tests are separated.
- Provider-specific logic is not leaking into WinUI windows in any serious way.
- Manual mode is treated as a first-class fallback, which is the right decision for this app category.
- Provider failures are designed to become visible snapshots instead of crashes.
- Secrets are stored through an abstraction and DPAPI implementation, not plain config fields.
- Diagnostics exports redact common secret shapes, account identifiers, scope/reference fields, local user profile paths, and CLI override paths, use bounded redaction context for truncated large files, omit local app-data and secret-store root paths, and exclude `secrets/`.
- Tests now include a repository-level guard against common secret-shaped fixture strings, so redaction samples should use obvious placeholders or runtime-composed patterns instead of committed key-looking literals.
- Config, snapshots, history, diagnostics, and maintenance flows are all represented in code and tests.
- CI now builds, tests, publishes, smoke-tests app service composition, packages, and uploads artifacts on `main`.
- Headless UI composition smoke coverage now constructs the primary shell, provider, settings, widget, diagnostics, history, and secret editor view models from isolated app data without launching WinUI windows.
- Compact panel placement tests now cover taskbar-edge work areas, negative-coordinate monitors, and oversized panel clamping in CI.
- The CLI surface gives useful non-UI checks: help, version, smoke test with app service composition and refresh pipeline coverage, diagnostics export, config backup export/list/validation, health report with storage pressure guidance, recovery guidance, launch targets, and repair hints, provider catalog, provider CLI override setting, support artifact pruning, update checks, verified update downloads, staged install script preparation, guarded prepared-script launch, explicit latest-update install orchestration, headless startup update policy execution, and headless refresh-once.
- Release readiness checks now cover version metadata, changelog, audit date, published-app smoke test, package presence, installer presence, and checksum validity.
- The repo now has an Inno Setup build path, checksum generation, CI artifact upload, and release asset wiring for setup executables.
- `v0.1.4` is published as the latest release with English release notes plus zip, zip checksum, setup executable, and setup checksum assets, and the latest-release endpoint resolves it correctly. Update checks now surface and persist the latest GitHub Release page, required zip package/checksum asset names, and setup installer asset visibility while keeping the zip package as the required self-update artifact.
- Refresh settings can run a manual latest-release check or explicitly launch the confirmation-gated safe latest-update install flow, while startup update policy checks releases on a conservative interval, can download verified packages, records staged package/checksum paths for real-version CLI and startup downloads, records real-version CLI latest-install staged package/checksum/script/result status, can launch guarded install scripts, avoids repeatedly launching the same release version, reconciles app-owned `install-result.json` files into redacted status details, and can now be exercised once from CLI without opening WinUI. The headless startup policy command has passed an isolated no-update plus cooldown dogfood run.
- Generated update apply scripts now back up by copying, restore from backup if the install copy phase or post-install smoke validation fails, reject unsafe archive entries before an install script is prepared, include a generated-script marker that the launcher verifies, write `install-result.json` beside the script on success or failure with non-secret validation status, retain validation stdout/stderr log files beside the result, and are written with a UTF-8 BOM so Windows PowerShell 5.1 can read non-ASCII package, staging, backup, and install paths. Startup checks and `--health-report` can read those result files back only from the app-owned updates directory, and they persist/display validation log paths plus byte counts without reading log contents.
- `WINAIUSAGEBAR_APPDATA` lets CLI dogfooding run against isolated app data instead of the user's normal `%AppData%\WinAiUsageBar` tree.
- Update CLI commands now support `--current-version <version>` for headless dogfooding of older-version update paths without changing assembly metadata or normal startup/UI behavior.
- Update CLI formatter output now redacts user-controlled messages, paths, URLs, commands, and dogfood version strings before printing.
- Release notes for future releases are generated explicitly from `CHANGELOG.md`, checked for English text, and passed to `gh release create` as a notes file instead of relying on generated GitHub notes.
- Release dogfooding now includes a disposable prepared-update apply script that refuses to apply outside its work directory, falls back safely when redirected GUI output mangles non-ASCII result paths, verifies validation log metadata for current generated scripts, a published-release discovery helper with legacy app-data guards, a published startup-policy helper guarded to source releases `v0.1.4` or newer, a current-updater full-flow helper that can simulate an older current version, and a real published `v0.1.3 -> v0.1.4` disposable update run that detected latest, downloaded, SHA256-verified, prepared, applied the update, and asserted the normal `%AppData%\WinAiUsageBar\updates` directory stayed unchanged.
- Provider notifications now suppress repeated alerts for the same provider/reason during periodic refresh while still notifying when severity changes or after recovery.
- Guided first-run checklist state, provider-specific first-run setup decisions, Provider Details, config backup export, CLI backup inventory, backup validation, latest-backup CLI validation, confirmed CLI restore by path, confirmed latest-backup CLI restore, latest-backup in-app restore, confirmed CLI reset-to-default recovery, and confirmed in-app reset-to-default recovery are implemented.
- Providers now includes non-secret setup guidance for source choices, Manual fallback, API references, Copilot metrics requirements, and CLI/app-server caveats.
- Provider cards and Provider Details now include stale/future snapshot timestamp warnings. Provider cards and provider notifications redact displayed snapshot-derived text, provider refresh and snapshot storage sanitize snapshot free-text before cache/history writes, legacy history summaries sanitize snapshots before aggregation, and Provider Details includes non-secret repair guidance for warning, auth-required, unsupported, error, and unknown provider states, including more specific Claude/Claude Code CLI repair guidance.
- CLI launch now supports provider command overrides that can be configured from UI or headless CLI, cleared again from headless CLI, then prefers resolved Windows `.exe`, `.cmd`, or `.bat` paths from command discovery and routes command shims through the command processor, so health checks and Codex app-server use the same safer startup path. Health reports label configured overrides when used, and provider repair guidance also shows non-secret hints for startup failures, including WindowsApps/App Execution Alias Codex failures observed on this machine.
- Codex app-server parsing now handles common quota alias shapes, safe ratio/fraction aliases, coherent nested usage/rate-limit window objects or arrays, top-level usage window arrays, absolute and relative reset timestamp shapes, and reset-after aliases in addition to basic usage and rate-limit percentages, and the client can keep partial account/rate-limit/usage data when one optional method is unavailable. JSON-RPC response matching now uses only top-level envelope ids so notifications with nested ids are ignored, and initialize metadata reports the current app informational version. Codex snapshots also preserve whether the user selected `Cli` or `LocalAppServer` so diagnostics and provider cards do not misreport the source, and shared Codex/ChatGPT app-server snapshots use provider-specific usage window labels.
- Headless `--refresh-once` can exercise the real enabled-provider refresh pipeline and print safe snapshot summaries, secondary usage/rate-limit window details when present, plus non-secret repair guidance without opening WinUI windows, including one-shot provider/source overrides for dogfooding paths such as Codex LocalAppServer.
- Config saves and snapshot cache/history rewrites use per-save unique temporary files, avoiding fixed `.tmp` collisions when headless commands are run in parallel, and read-only diagnostics no longer rewrite already-normalized config files.
- Config backup exports and restore/reset rollback backups use unique temp files and suffix duplicate timestamp names instead of overwriting same-second backups.
- Diagnostics exports also use create-new writes and suffix duplicate timestamp names instead of overwriting same-second support bundles.
- Privacy & Data and `--health-report` now include non-secret storage pressure guidance for history, backups, diagnostics exports, crash reports, and diagnostics logs, plus recovery guidance for backup, restore, reset, diagnostics export choices, and persisted startup update status.
- Privacy & Data now lists recent crash report metadata, including timestamp, source, exception type, app version, size, path, and parse status, without showing crash message or stack trace contents.
- Startup and WinUI unhandled failures now write local redacted JSON crash reports under app data, redact local user profile paths in payload text, and prune reports to a bounded recent set.
- Privacy & Data and the CLI can prune old config backups, diagnostics exports, and app-generated crash reports while keeping the newest matched files and leaving config, cache, logs, updates, unrelated files, and `secrets/` alone.
- Windows shell dogfooding now has a concrete manual verification checklist and a timestamped local report script.
- The issue and commit history is becoming meaningful rather than fake contribution noise.

## What Is Weak

- Automatic provider integrations are not yet deep enough to make the app valuable without manual input.
- Codex/ChatGPT integration is still best-effort and depends on `codex app-server` behavior that can change.
- Claude, Claude Code, Gemini, and OpenCode Zen are mostly descriptors, manual mode, or placeholders.
- GitHub Copilot support targets organization or enterprise metrics and is not a complete personal usage experience.
- The UI is functional but still not visually or ergonomically proven with extended daily use.
- Tray behavior, taskbar-near placement, topmost widget behavior, and notification delivery need real Windows manual testing.
- There is no MSIX, code signing, or installer trust story yet beyond a documented unsigned installer warning and checksum verification guidance. Startup auto-update policy exists, disposable prepared-apply dogfooding has passed, the current updater can simulate older-version full update flows, and a published `v0.1.3 -> v0.1.4` disposable update has passed, but real same-install release-to-release update installs still need careful repetition before automatic install should be treated as safe-by-default.
- The latest release line is still moving quickly; v0.1.4 should be treated as a dogfood patch, not a polished public utility.
- First-run setup has a checklist, action targets, and provider-specific setup decisions, but it is not yet a full guided wizard that applies provider setup choices inline.
- Config backup and reset recovery now exist with basic decision guidance, CLI backup inventory, path-based and latest-backup CLI validation/restore entries, and CLI reset-to-default recovery, but they still need repeated dogfooding before they can be treated as comfort features.
- Local storage growth is visible for history, backups, diagnostics exports, and diagnostics logs, with basic pruning for backups and exports, but the maintenance flow still needs real-use tuning.
- Local CLI discovery can still be messy on Windows. Provider Details has Codex WindowsApps and Claude CLI repair guidance, but deeper provider-specific repair checks are still needed for future CLI-backed providers.
- There is no visual regression or automated WinUI window activation test yet. Headless UI composition coverage exists, but it cannot prove layout, focus, shell integration, or rendering behavior.

## Risk Register

| Risk | Severity | Current mitigation | Remaining work |
| --- | --- | --- | --- |
| Provider APIs are undocumented or unstable | High | Manual mode and unsupported states | Track documented endpoints only and isolate each integration behind tests. |
| Secret leakage through diagnostics | High | DPAPI store, redactor, export exclusions | Add more redaction test cases and review every provider diagnostic path. |
| Tray/window behavior differs across Windows setups | Medium | Placement service, compact panel edge-case tests, single-instance guard tests, manual checklist, and report scaffold | Run and record the checklist across taskbar edges, DPI, multi-monitor, startup, and theme modes. |
| CI restore flakiness blocks progress | Medium | Retry script and NuGet audit disabled by default | Keep restore helper simple and inspect future failures quickly. |
| App feels like a demo because provider data is manual | High | Mock, Manual, broader Codex reset parser tests, provider details, and headless refresh-once are stable | Prioritize one reliable real provider path end to end. |
| Public binaries are not trusted by Windows | High | Zip, checksum, release workflow, published setup assets, documented unsigned installer warning, update check, checksum-verified download path, install-script preparation, rollback-capable guarded script launch, explicit latest-update install orchestration, throttled startup update policy, and disposable update-apply dogfooding exist | Add signing before public release. |
| Self-update can damage an install | High | SHA256 verification, app-owned update staging, guarded script launch, same-version relaunch suppression, rollback on copy or post-install smoke-test failure, retained validation log metadata, unsafe zip-entry rejection, UTF-8 BOM generated scripts for non-ASCII paths, current-version update dogfooding, and published disposable release-to-release update dogfooding with normal app-data unchanged assertions | Dogfood same-install release-to-release update flows repeatedly before recommending automatic install. |
| Local data files grow too much | Medium | History retention by days and bytes; Privacy & Data storage pressure guidance includes history, backups, diagnostics exports, and diagnostics logs; backups and diagnostics exports can be pruned from UI or CLI while keeping newest matched files | Dogfood pressure thresholds and add richer compaction or per-folder controls if needed. |
| Config corruption causes user confusion | Medium | Corrupt config backup, default migration, unique temp files for config and backup saves, collision-resistant backup/export names, config export, CLI backup inventory, explicit and latest-backup validation, confirmed CLI restore by path, confirmed latest-backup CLI restore, latest-backup in-app restore, confirmed CLI reset-to-default recovery, confirmed in-app reset-to-default recovery, and recovery guidance | Dogfood listing, validation, restore, and reset repeatedly, then tighten recovery copy and guidance based on real failures. |
| CLI availability is ambiguous on Windows | Medium | Safe health report checks command discovery, selected launch targets, repair hints, and short startup; command launch prefers resolved `.exe`/shim paths; Codex provider classifies startup failures; Provider Details gives generic, Codex-specific, and Claude-specific CLI repair guidance | Extend provider-specific repair checks to every future CLI-backed provider. |

## Current MVP Reality

The app is credible as a personal development project. It is not yet credible as a polished public utility.

The foundation is good because the hardest long-term boundaries are already present:

- provider adapters instead of UI-specific provider code
- secret references instead of raw secret config
- refresh service instead of ad hoc button-only fetching
- history and diagnostics instead of invisible local state
- CI and package artifacts instead of local-only builds

The weak point is value density. A usage bar is only as useful as the data it can fetch automatically. Today the app has strong plumbing, useful recovery and diagnostic tools, and decent provider-state visibility, but only partial real-world data acquisition.

## Next Work, In Priority Order

1. Dogfood release-to-release update flows.
   `v0.1.1` seeing `v0.1.2`, `v0.1.2` seeing `v0.1.3` with a legacy app-data guard, current-updater simulated `0.1.2 -> v0.1.3`, current-updater simulated `0.1.3 -> v0.1.4`, and published `v0.1.3 -> v0.1.4` download/prepare/apply against a disposable install have passed. The helper can now exercise the published startup update policy path too. Next, repeat real same-install release-to-release update checks before trusting automatic install by default.

2. Run the manual Windows verification checklist.
   Cover tray click, context menu, widget placement, topmost behavior, notifications, startup registration, DPI, taskbar position, and multi-monitor.

3. Pick one real provider path and make it genuinely useful.
   Codex/ChatGPT is the best candidate because the local app-server path already exists.

4. Run the Provider Details page through dogfooding.
   The page exists now, but it needs real snapshot data and daily-use feedback.

5. Dogfood the guided first-run setup checklist and decide whether it should become a deeper wizard.
   A new user can see provider-specific setup decisions and jump to Providers or Privacy & Data from the setup panel now, but the flow still does not apply provider-specific setup decisions inline.

6. Dogfood config backup, restore, and reset.
   CLI backup inventory, CLI restore by path, latest-backup CLI validation/restore, latest-backup in-app restore, CLI reset-to-default recovery, and in-app reset-to-default recovery exist now, but they need repeated real-use recovery checks before they become comfort features.

7. Dogfood recovery decision guidance.
   The app can explain the basic backup, restore, reset, and diagnostics choices now, but the copy and placement still need real-use validation.

8. Add one automated UI launch check if feasible.
   Even a minimal app-start plus window activation check would catch major WinUI regressions.

9. Dogfood the startup update policy.
   The app can now check GitHub release metadata on startup with a cooldown and optionally download or launch a prepared install script without repeating the same release launch. A headless isolated no-update/cooldown run has passed, and a published-release startup-policy dogfood helper exists. Next it needs repeated same-install release-to-release startup-policy testing and a more explicit in-app confirmation story before wider use.

10. Keep release readiness gates strict as distribution matures.
   Current gates cover metadata, audit date, smoke test, package, installer, checksum, the unsigned installer notice, and optional manual verification report evidence.

## Contribution Strategy Assessment

The current contribution strategy is healthy:

- Issues are specific and close with verification notes.
- Commits use conventional messages.
- CI validates each pushed change.
- The history shows useful product movement, not empty churn.

Keep using small issue-sized changes. The best contribution growth from here is not more tiny README edits; it is a sequence of narrow, test-backed improvements that move the app from "well-structured MVP" to "daily-use tool".
