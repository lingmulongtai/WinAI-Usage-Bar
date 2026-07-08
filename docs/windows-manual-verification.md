# Windows Manual Verification Checklist

Use this checklist before dogfooding builds and before tagging a release. Do not paste API keys, tokens, cookies, `auth.json` contents, or secret values into notes.

Create a timestamped local report from this checklist before a run:

```powershell
.\scripts\new-windows-verification-report.ps1
```

Reports are written under `artifacts\verification` by default so local run notes do not become accidental source changes.

## Test Matrix

Record the environment for each run.

| Field | Value |
| --- | --- |
| Date |  |
| App version / commit |  |
| Windows version |  |
| Display scale |  |
| Monitor count |  |
| Taskbar edge |  |
| Theme |  |
| Install source | Zip / local publish / other |

## Startup And Tray

| Check | Expected result | Pass | Notes |
| --- | --- | --- | --- |
| Launch `WinAiUsageBar.App.exe` from a clean publish folder | App starts without opening duplicate settings windows |  |  |
| Confirm tray icon appears | One WinAI Usage Bar tray icon is visible |  |  |
| Launch the app a second time | No duplicate tray icon or refresh loop appears |  |  |
| Left-click tray icon | Compact usage panel opens or toggles predictably |  |  |
| Right-click tray icon | Context menu shows Show, Show Widget, Refresh Now, Settings, Exit |  |  |
| Click Refresh Now from tray menu | Enabled providers refresh without blocking the shell |  |  |
| Click Settings from tray menu | Settings window opens and activates |  |  |
| Click Exit from tray menu | Tray icon is removed and process exits |  |  |

## Compact Panel

| Check | Expected result | Pass | Notes |
| --- | --- | --- | --- |
| Open compact panel near bottom taskbar | Panel fits inside the working area |  |  |
| Open compact panel near top taskbar | Panel fits inside the working area |  |  |
| Open compact panel with left or right taskbar | Panel fits inside the working area |  |  |
| Resize display scale to 125% or 150% | Text remains readable and controls do not overlap |  |  |
| Refresh while compact panel is open | Provider cards update without flicker or crash |  |  |
| No enabled providers | Empty state is understandable |  |  |

## Settings Window

| Check | Expected result | Pass | Notes |
| --- | --- | --- | --- |
| Open Overview | Provider cards, actions, and first-run setup checklist action buttons render |  |  |
| Open Providers | Provider editors render, setup guidance updates with source changes, and save validation works |  |  |
| Open Provider Details | Detailed snapshot rows and repair guidance render without secret values |  |  |
| Open Appearance | Theme and start-at-login controls render |  |  |
| Open Widget | Widget settings render and enforce one to three providers |  |  |
| Open History | History summary renders without raw snapshot messages |  |  |
| Open Refresh | Interval, notification, and retention settings save |  |  |
| Open Privacy & Data | Diagnostics summary, secret actions, maintenance, and export buttons render |  |  |
| Open About | Version is shown |  |  |

## Widget

| Check | Expected result | Pass | Notes |
| --- | --- | --- | --- |
| Show widget from tray menu | Widget window opens |  |  |
| Move and resize widget | Placement is persisted after reopening |  |  |
| Enable always-on-top | Widget stays above normal windows |  |  |
| Disable always-on-top | Widget returns to normal z-order behavior |  |  |
| Select one provider | Widget shows that provider |  |  |
| Select three providers | Widget shows all selected providers without clipping |  |  |
| Try selecting zero or more than three providers | Settings validation blocks the invalid state |  |  |

## Notifications And Startup

| Check | Expected result | Pass | Notes |
| --- | --- | --- | --- |
| Enable notifications | Refresh can emit supported local notifications |  |  |
| Disable notifications | Low quota and auth-required refreshes do not notify |  |  |
| Enable start at login | Current-user Run registration is created |  |  |
| Disable start at login | Current-user Run registration is removed |  |  |
| Reboot or sign out/in with start at login enabled | App starts once and shows one tray icon |  |  |

## Data Safety

| Check | Expected result | Pass | Notes |
| --- | --- | --- | --- |
| Save a secret value | Value is accepted but never displayed back |  |  |
| Check a secret name | UI reports present or missing without showing the value |  |  |
| Review provider setup guidance | Guidance explains source requirements without echoing secret references, org names, enterprise slugs, PAT names, tokens, or auth contents |  |  |
| Review storage pressure in Privacy & Data | Guidance explains history, backup, and diagnostics log pressure without showing secret names or values |  |  |
| Review recovery guidance in Privacy & Data | Guidance explains backup, restore, reset, and diagnostics choices without showing secret names or values |  |  |
| Export diagnostics | Export excludes `secrets/` and redacts common secret shapes |  |  |
| Export config backup | Backup includes config only and does not copy `secrets/` |  |  |
| Validate latest config backup from Privacy & Data | Latest backup is validated and result does not expose secret values |  |  |
| Restore latest config backup from Privacy & Data | Restore requires confirmation, creates rollback backup, and restarts refresh |  |  |
| Reset config to defaults from Privacy & Data | Reset requires confirmation, creates rollback backup, keeps `secrets/` unchanged, and restarts refresh |  |  |
| Run `--refresh-once` from CLI | Enabled providers refresh once, snapshots/history update, and output contains no secret values or identity fields |  |  |
| Run `--refresh-once --provider Codex --source LocalAppServer` | Codex source is tested for this run only and `config.json` keeps the previously saved provider settings |  |  |
| Restore config backup from CLI with `--confirm` | Current config is backed up before restore and `secrets/` is unchanged |  |  |
| Attempt restore without `--confirm` | Command exits non-zero and config is unchanged |  |  |

## Provider Flows

| Check | Expected result | Pass | Notes |
| --- | --- | --- | --- |
| Mock provider enabled | Realistic mock usage appears |  |  |
| Manual provider values saved | Manual usage appears after refresh |  |  |
| Codex source without usable CLI/app-server | Provider returns visible unsupported/auth/error state, not a crash |  |  |
| Claude CLI probe without usage support | Provider returns visible unsupported/auth state, not a crash |  |  |
| GitHub Copilot organization metrics without permissions | Provider returns auth-required state without leaking token text |  |  |

## Release Decision

| Question | Answer |
| --- | --- |
| Any crash or hang? |  |
| Any visual overlap or unreadable text? |  |
| Any secret value displayed or exported? |  |
| Any duplicated tray icon or process? |  |
| Ready to dogfood? |  |
