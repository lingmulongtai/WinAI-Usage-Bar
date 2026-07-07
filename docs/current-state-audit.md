# Current State Audit

Date: 2026-07-08

This audit is intentionally strict. The repository has moved past a throwaway scaffold, but it is still an MVP. The design foundation is much stronger than the product completeness.

## Scorecard

| Area | Score | Read |
| --- | ---: | --- |
| Architecture foundation | 8/10 | The layer split is real, testable, and mostly respected. |
| Provider extensibility | 7/10 | Descriptors, adapters, manual mode, and safe failure states are in place. Real provider depth is still thin. |
| Security posture | 7/10 | Good defaults around DPAPI, redaction, and no cookie scraping. Needs more adversarial review before wider distribution. |
| Windows shell integration | 5/10 | Tray, windows, placement, startup registration, and notifications exist, but shell behavior still needs hands-on verification. |
| Product usability | 5/10 | The app can be dogfooded, but it is mostly useful with Mock/Manual data today. |
| Packaging and release | 6/10 | Self-contained publish, zip packaging, checksums, artifacts, and draft release workflow exist. No installer, signing, or update path yet. |
| Test confidence | 8/10 | Core, infrastructure, view model, CLI, storage, parser, refresh, and packaging smoke paths are covered without external CLIs. UI runtime coverage remains limited. |
| Observability and support | 7/10 | Diagnostics summary, redacted export, health report, and logs are solid for an MVP. No structured crash reports or user-facing repair flow yet. |

Overall:

- Design foundation: about 75-80% of the intended MVP foundation.
- Personal dogfooding readiness: about 55-60%.
- Public release readiness: about 35-40%.

## What Is Strong

- The repo has a real solution structure: `Core`, `Infrastructure`, `App`, and tests are separated.
- Provider-specific logic is not leaking into WinUI windows in any serious way.
- Manual mode is treated as a first-class fallback, which is the right decision for this app category.
- Provider failures are designed to become visible snapshots instead of crashes.
- Secrets are stored through an abstraction and DPAPI implementation, not plain config fields.
- Diagnostics exports redact common secret shapes and exclude `secrets/`.
- Config, snapshots, history, diagnostics, and maintenance flows are all represented in code and tests.
- CI now builds, tests, publishes, smoke-tests, packages, and uploads artifacts on `main`.
- The CLI surface gives useful non-UI checks: help, version, smoke test, diagnostics export, and health report.
- The issue and commit history is becoming meaningful rather than fake contribution noise.

## What Is Weak

- Automatic provider integrations are not yet deep enough to make the app valuable without manual input.
- Codex/ChatGPT integration is still best-effort and depends on `codex app-server` behavior that can change.
- Claude, Claude Code, Gemini, and OpenCode Zen are mostly descriptors, manual mode, or placeholders.
- GitHub Copilot support targets organization or enterprise metrics and is not a complete personal usage experience.
- The UI is functional but still not visually or ergonomically proven with extended daily use.
- Tray behavior, taskbar-near placement, topmost widget behavior, and notification delivery need real Windows manual testing.
- There is no installer, MSIX, code signing, auto-update, or uninstall story.
- There is no first-run onboarding or guided setup for providers.
- There is no import/export of non-secret config, backup/restore, or reset-to-known-good flow.
- There is no visual regression or automated UI smoke test for WinUI windows.

## Risk Register

| Risk | Severity | Current mitigation | Remaining work |
| --- | --- | --- | --- |
| Provider APIs are undocumented or unstable | High | Manual mode and unsupported states | Track documented endpoints only and isolate each integration behind tests. |
| Secret leakage through diagnostics | High | DPAPI store, redactor, export exclusions | Add more redaction test cases and review every provider diagnostic path. |
| Tray/window behavior differs across Windows setups | Medium | Placement service and single-instance guard tests | Manual matrix across taskbar edges, DPI, multi-monitor, startup, and theme modes. |
| CI restore flakiness blocks progress | Medium | Retry script and NuGet audit disabled by default | Keep restore helper simple and inspect future failures quickly. |
| App feels like a demo because provider data is manual | High | Mock and Manual are stable | Prioritize one reliable real provider path end to end. |
| Public binaries are not trusted by Windows | High | Zip and checksum exist | Add signing or at least documented install warnings before public release. |
| Local data files grow too much | Medium | History retention by days and bytes | Add UI display for current storage pressure and backup/compact actions. |
| Config corruption causes user confusion | Medium | Corrupt config backup and default migration | Add visible recovery messaging and backup export/import. |

## Current MVP Reality

The app is credible as a personal development project. It is not yet credible as a polished public utility.

The foundation is good because the hardest long-term boundaries are already present:

- provider adapters instead of UI-specific provider code
- secret references instead of raw secret config
- refresh service instead of ad hoc button-only fetching
- history and diagnostics instead of invisible local state
- CI and package artifacts instead of local-only builds

The weak point is value density. A usage bar is only as useful as the data it can fetch automatically. Today the app has strong plumbing, but only partial real-world data acquisition.

## Next Work, In Priority Order

1. Pick one real provider path and make it genuinely useful.
   Codex/ChatGPT is the best candidate because the local app-server path already exists.

2. Add a provider details page.
   The compact cards are not enough for diagnostics, setup, and source-specific guidance.

3. Add a first-run setup flow.
   A new user should see enabled providers, source mode, and manual fallback without digging through settings.

4. Add config backup and restore.
   This protects dogfooding data and makes experimentation safer.

5. Add a manual Windows verification checklist.
   Cover tray click, context menu, widget placement, topmost behavior, notifications, startup registration, DPI, taskbar position, and multi-monitor.

6. Add one automated UI launch check if feasible.
   Even a minimal app-start plus window activation check would catch major WinUI regressions.

7. Add installer or MSIX investigation.
   Zip artifacts are fine for development, but not a smooth Windows app distribution path.

8. Add release readiness gates.
   Require changelog entry, version consistency, package checksum, smoke test, and a current audit update before tags.

## Contribution Strategy Assessment

The current contribution strategy is healthy:

- Issues are specific and close with verification notes.
- Commits use conventional messages.
- CI validates each pushed change.
- The history shows useful product movement, not empty churn.

Keep using small issue-sized changes. The best contribution growth from here is not more tiny README edits; it is a sequence of narrow, test-backed improvements that move the app from "well-structured MVP" to "daily-use tool".
