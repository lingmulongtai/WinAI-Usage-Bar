# Current State Audit

Date: 2026-07-08

This audit is intentionally strict. The repository has moved past a throwaway scaffold, but it is still an MVP. The design foundation is much stronger than the product completeness.

Latest published release: `v0.1.4`. It adds English release-note generation from the changelog, safer update CLI version override dogfooding, published-release discovery dogfooding, and published `v0.1.3 -> v0.1.4` disposable update download/prepare/apply dogfooding.

## Scorecard

| Area | Score | Read |
| --- | ---: | --- |
| Architecture foundation | 8/10 | The layer split is real, testable, and mostly respected. |
| Provider extensibility | 7/10 | Descriptors, adapters, manual mode, and safe failure states are in place. Real provider depth is still thin. |
| Security posture | 8/10 | Good defaults around DPAPI, redaction, no cookie scraping, checksum verification, guarded update launch, rollback, unsafe update zip rejection, and disposable update-apply dogfooding. Needs more adversarial review before wider distribution. |
| Windows shell integration | 6/10 | Tray, windows, placement, startup registration, and notifications exist, with duplicate notification suppression plus a manual verification checklist and report scaffold. Actual shell behavior still needs hands-on runs. |
| Product usability | 6/10 | The app now has guided first-run checklist state, provider details, backup export/restore, and recovery checks, but it is still mostly useful with Mock/Manual data today. |
| Packaging and release | 9/10 | Self-contained publish, zip packaging, checksums, readiness gates, artifacts, release workflow, English release notes generated from the changelog, published setup assets, update-check, verified download, install-script preparation, rollback-capable guarded script launch, explicit latest-update install orchestration, setup installer artifact/release paths, throttled startup update policy, current-version update dogfooding, and published disposable release-to-release update dogfooding exist. No signing yet. |
| Test confidence | 8/10 | Core, infrastructure, view model, CLI, storage, parser, refresh, app-composition smoke, and packaging smoke paths are covered without external CLIs. UI runtime coverage remains limited. |
| Observability and support | 8/10 | Diagnostics summary, provider repair guidance, recovery guidance, redacted export, health report, isolated app-data dogfooding, provider dogfooding notes, release dogfooding notes, and logs are solid for an MVP. No structured crash reports yet. |

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
- Diagnostics exports redact common secret shapes and exclude `secrets/`.
- Config, snapshots, history, diagnostics, and maintenance flows are all represented in code and tests.
- CI now builds, tests, publishes, smoke-tests app service composition, packages, and uploads artifacts on `main`.
- The CLI surface gives useful non-UI checks: help, version, smoke test with app service composition and refresh pipeline coverage, diagnostics export, config backup export, health report with storage pressure guidance, recovery guidance, launch targets, and repair hints, provider catalog, provider CLI override setting, support artifact pruning, update checks, verified update downloads, staged install script preparation, guarded prepared-script launch, explicit latest-update install orchestration, headless startup update policy execution, and headless refresh-once.
- Release readiness checks now cover version metadata, changelog, audit date, published-app smoke test, package presence, installer presence, and checksum validity.
- The repo now has an Inno Setup build path, checksum generation, CI artifact upload, and release asset wiring for setup executables.
- `v0.1.4` is published as the latest release with English release notes plus zip, zip checksum, setup executable, and setup checksum assets, and the latest-release endpoint resolves it correctly.
- Refresh settings can run a manual latest-release check or explicitly launch the confirmation-gated safe latest-update install flow, while startup update policy checks releases on a conservative interval, can download verified packages, can launch guarded install scripts, avoids repeatedly launching the same release version, records redacted current/latest/package/script status details, and can now be exercised once from CLI without opening WinUI. The headless startup policy command has passed an isolated no-update plus cooldown dogfood run.
- Generated update apply scripts now back up by copying, restore from backup if the install copy phase fails, reject unsafe archive entries before an install script is prepared, and are written with a UTF-8 BOM so Windows PowerShell 5.1 can read non-ASCII package, staging, backup, and install paths.
- `WINAIUSAGEBAR_APPDATA` lets CLI dogfooding run against isolated app data instead of the user's normal `%AppData%\WinAiUsageBar` tree.
- Update CLI commands now support `--current-version <version>` for headless dogfooding of older-version update paths without changing assembly metadata or normal startup/UI behavior.
- Release notes for future releases are generated explicitly from `CHANGELOG.md`, checked for English text, and passed to `gh release create` as a notes file instead of relying on generated GitHub notes.
- Release dogfooding now includes a disposable prepared-update apply script that refuses to apply outside its work directory, a published-release discovery helper with legacy app-data guards, a current-updater full-flow helper that can simulate an older current version, and a real published `v0.1.3 -> v0.1.4` disposable update run that detected latest, downloaded, SHA256-verified, prepared, applied the update, and asserted the normal `%AppData%\WinAiUsageBar\updates` directory stayed unchanged.
- Provider notifications now suppress repeated alerts for the same provider/reason during periodic refresh while still notifying when severity changes or after recovery.
- Guided first-run checklist state, Provider Details, config backup export, backup validation, confirmed CLI restore, latest-backup in-app restore, and confirmed reset-to-default recovery are implemented.
- Providers now includes non-secret setup guidance for source choices, Manual fallback, API references, Copilot metrics requirements, and CLI/app-server caveats.
- Provider Details now includes non-secret repair guidance for warning, auth-required, unsupported, error, and unknown provider states.
- CLI launch now supports provider command overrides that can be configured from UI or headless CLI, cleared again from headless CLI, then prefers resolved Windows `.exe`, `.cmd`, or `.bat` paths from command discovery and routes command shims through the command processor, so health checks and Codex app-server use the same safer startup path. Health reports label configured overrides when used, and provider repair guidance also shows non-secret hints for startup failures, including WindowsApps/App Execution Alias Codex failures observed on this machine.
- Codex app-server parsing now handles common absolute and relative reset timestamp shapes in addition to basic usage and rate-limit percentages, and the client can keep partial account/rate-limit/usage data when one optional method is unavailable.
- Headless `--refresh-once` can exercise the real enabled-provider refresh pipeline and print safe snapshot summaries plus non-secret repair guidance without opening WinUI windows, including one-shot provider/source overrides for dogfooding paths such as Codex LocalAppServer.
- Config saves use per-save unique temporary files, avoiding fixed `config.json.tmp` collisions when headless commands are run in parallel, and read-only diagnostics no longer rewrite already-normalized config files.
- Config backup exports and restore/reset rollback backups use unique temp files and suffix duplicate timestamp names instead of overwriting same-second backups.
- Diagnostics exports also use create-new writes and suffix duplicate timestamp names instead of overwriting same-second support bundles.
- Privacy & Data and `--health-report` now include non-secret storage pressure guidance for history, backups, diagnostics exports, and diagnostics logs, plus recovery guidance for backup, restore, reset, diagnostics export choices, and persisted startup update status.
- Privacy & Data and the CLI can prune old config backups and diagnostics exports while keeping the newest matched files and leaving config, cache, logs, and `secrets/` alone.
- Windows shell dogfooding now has a concrete manual verification checklist and a timestamped local report script.
- The issue and commit history is becoming meaningful rather than fake contribution noise.

## What Is Weak

- Automatic provider integrations are not yet deep enough to make the app valuable without manual input.
- Codex/ChatGPT integration is still best-effort and depends on `codex app-server` behavior that can change.
- Claude, Claude Code, Gemini, and OpenCode Zen are mostly descriptors, manual mode, or placeholders.
- GitHub Copilot support targets organization or enterprise metrics and is not a complete personal usage experience.
- The UI is functional but still not visually or ergonomically proven with extended daily use.
- Tray behavior, taskbar-near placement, topmost widget behavior, and notification delivery need real Windows manual testing.
- There is no MSIX, code signing, or installer trust story yet. Startup auto-update policy exists, disposable prepared-apply dogfooding has passed, the current updater can simulate older-version full update flows, and a published `v0.1.3 -> v0.1.4` disposable update has passed, but real same-install release-to-release update installs still need careful repetition before automatic install should be treated as safe-by-default.
- The latest release line is still moving quickly; v0.1.4 should be treated as a dogfood patch, not a polished public utility.
- First-run setup has a basic checklist with action targets and Providers has setup guidance, but it is not yet a full guided wizard with inline provider-specific decisions.
- Config backup and reset recovery now exist with basic decision guidance, but they still need repeated dogfooding before they can be treated as comfort features.
- Local storage growth is visible for history, backups, diagnostics exports, and diagnostics logs, with basic pruning for backups and exports, but the maintenance flow still needs real-use tuning.
- Local CLI discovery can still be messy on Windows. Provider Details has generic CLI repair guidance, but deeper provider-specific repair checks are still needed for future CLI-backed providers.
- There is no visual regression or automated UI smoke test for WinUI windows.

## Risk Register

| Risk | Severity | Current mitigation | Remaining work |
| --- | --- | --- | --- |
| Provider APIs are undocumented or unstable | High | Manual mode and unsupported states | Track documented endpoints only and isolate each integration behind tests. |
| Secret leakage through diagnostics | High | DPAPI store, redactor, export exclusions | Add more redaction test cases and review every provider diagnostic path. |
| Tray/window behavior differs across Windows setups | Medium | Placement service, single-instance guard tests, manual checklist, and report scaffold | Run and record the checklist across taskbar edges, DPI, multi-monitor, startup, and theme modes. |
| CI restore flakiness blocks progress | Medium | Retry script and NuGet audit disabled by default | Keep restore helper simple and inspect future failures quickly. |
| App feels like a demo because provider data is manual | High | Mock, Manual, broader Codex reset parser tests, provider details, and headless refresh-once are stable | Prioritize one reliable real provider path end to end. |
| Public binaries are not trusted by Windows | High | Zip, checksum, release workflow, published setup assets, update check, checksum-verified download path, install-script preparation, rollback-capable guarded script launch, explicit latest-update install orchestration, throttled startup update policy, and disposable update-apply dogfooding exist | Add signing or at least documented install warnings before public release. |
| Self-update can damage an install | High | SHA256 verification, app-owned update staging, guarded script launch, same-version relaunch suppression, rollback on copy failure, unsafe zip-entry rejection, UTF-8 BOM generated scripts for non-ASCII paths, current-version update dogfooding, and published disposable release-to-release update dogfooding with normal app-data unchanged assertions | Dogfood same-install release-to-release update flows repeatedly before recommending automatic install. |
| Local data files grow too much | Medium | History retention by days and bytes; Privacy & Data storage pressure guidance includes history, backups, diagnostics exports, and diagnostics logs; backups and diagnostics exports can be pruned from UI or CLI while keeping newest matched files | Dogfood pressure thresholds and add richer compaction or per-folder controls if needed. |
| Config corruption causes user confusion | Medium | Corrupt config backup, default migration, unique temp files for config and backup saves, collision-resistant backup/export names, config export, validation, confirmed CLI restore, latest-backup in-app restore, reset-to-default recovery, and recovery guidance | Dogfood restore and reset repeatedly, then tighten recovery copy and guidance based on real failures. |
| CLI availability is ambiguous on Windows | Medium | Safe health report checks command discovery, selected launch targets, repair hints, and short startup; command launch prefers resolved `.exe`/shim paths; Codex provider classifies startup failures; Provider Details gives generic CLI repair guidance | Extend provider-specific repair checks to every future CLI-backed provider. |

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
   `v0.1.1` seeing `v0.1.2`, `v0.1.2` seeing `v0.1.3` with a legacy app-data guard, current-updater simulated `0.1.2 -> v0.1.3`, current-updater simulated `0.1.3 -> v0.1.4`, and published `v0.1.3 -> v0.1.4` download/prepare/apply against a disposable install have passed. Next, repeat real same-install release-to-release update checks and startup update policy checks before trusting automatic install by default.

2. Run the manual Windows verification checklist.
   Cover tray click, context menu, widget placement, topmost behavior, notifications, startup registration, DPI, taskbar position, and multi-monitor.

3. Pick one real provider path and make it genuinely useful.
   Codex/ChatGPT is the best candidate because the local app-server path already exists.

4. Run the Provider Details page through dogfooding.
   The page exists now, but it needs real snapshot data and daily-use feedback.

5. Dogfood the guided first-run setup checklist and decide whether it should become a deeper wizard.
   A new user can jump to Providers or Privacy & Data from checklist items now, and Providers explains source setup, but the flow still does not apply provider-specific setup decisions inline.

6. Dogfood config backup, restore, and reset.
   CLI restore, latest-backup in-app restore, and reset-to-default recovery exist now, but they need repeated real-use recovery checks before they become comfort features.

7. Dogfood recovery decision guidance.
   The app can explain the basic backup, restore, reset, and diagnostics choices now, but the copy and placement still need real-use validation.

8. Add one automated UI launch check if feasible.
   Even a minimal app-start plus window activation check would catch major WinUI regressions.

9. Dogfood the startup update policy.
   The app can now check GitHub release metadata on startup with a cooldown and optionally download or launch a prepared install script without repeating the same release launch. A headless isolated no-update/cooldown run has passed. Next it needs real release-to-release startup-policy testing and a more explicit in-app confirmation story before wider use.

10. Keep release readiness gates strict as distribution matures.
   Current gates cover metadata, audit date, smoke test, package, installer, checksum, and optional manual verification report evidence.

## Contribution Strategy Assessment

The current contribution strategy is healthy:

- Issues are specific and close with verification notes.
- Commits use conventional messages.
- CI validates each pushed change.
- The history shows useful product movement, not empty churn.

Keep using small issue-sized changes. The best contribution growth from here is not more tiny README edits; it is a sequence of narrow, test-backed improvements that move the app from "well-structured MVP" to "daily-use tool".
