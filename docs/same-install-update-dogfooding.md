# Same-Install Update Dogfooding

Use this checklist only after the disposable update helpers have passed for the same target release. This run intentionally touches a normal installed copy and normal app data, so keep notes precise and do not paste API keys, tokens, cookies, auth file contents, account identifiers, organization names, enterprise slugs, PAT names, or secret references.

Prefer the setup installer or explicit in-app/CLI update action for the first same-install pass. Keep automatic install disabled unless explicitly confirmed.

Create a timestamped local report before a run:

```powershell
.\scripts\new-same-install-update-report.ps1
```

Reports are written under `artifacts\verification` by default so local run notes do not become accidental source changes.

## Preconditions

| Check | Expected result | Pass | Notes |
| --- | --- | --- | --- |
| Disposable published update flow passed for the same target release | `scripts\test-published-update-flow.ps1` passed with `-Apply` and `-AssertNormalAppDataUnchanged` where supported |  |  |
| Current updater flow passed for the same target release | `scripts\test-current-update-flow.ps1` passed with `-Apply` against a disposable install |  |  |
| Target release assets are published | GitHub Release contains versioned zip, zip checksum, setup exe, and setup checksum assets |  |  |
| Setup executable and zip checksums were verified | SHA256 checksums match published `.sha256` files |  |  |
| Unsigned installer warning was acknowledged | Tester expects SmartScreen or unknown-publisher prompts until signing exists |  |  |
| Automatic install is off by default | Refresh settings do not have automatic install launch enabled unless this run explicitly tests startup policy |  |  |
| Manual confirmation is recorded | Tester records whether this run is explicit manual install, explicit CLI install, or explicitly confirmed startup-policy install |  |  |

## Baseline

| Check | Expected result | Pass | Notes |
| --- | --- | --- | --- |
| Record installed version | About page or `WinAiUsageBar.App.exe --version` shows the source version |  |  |
| Record install directory | The path is the real install directory intended for this run |  |  |
| Record normal app-data directory | `%AppData%\WinAiUsageBar` exists or absence is recorded before the run |  |  |
| Export config backup | Backup is created from Privacy & Data or `--export-config-backup` before changing update settings |  |  |
| Run health report | `--health-report` completes and captures current update status without secret values |  |  |
| Snapshot updates directory | File list, sizes, and timestamps under `%AppData%\WinAiUsageBar\updates` are captured before the run |  |  |
| Snapshot running processes | Number of `WinAiUsageBar.App.exe` processes is recorded before install launch |  |  |

## Manual Update Path

Use this section for explicit manual update checks from Refresh settings or CLI. Skip it when the run is only the startup-policy path.

| Check | Expected result | Pass | Notes |
| --- | --- | --- | --- |
| Click Check for Updates Now or run `--check-for-updates` | Newer version is detected and release page plus asset names are shown without downloading secrets |  |  |
| Run explicit install action | In-app confirmation or `--install-latest-update` is used; install is not launched merely by checking |  |  |
| Download is verified | Package and checksum are staged under app-owned updates storage after SHA256 verification |  |  |
| Apply script is app-owned | `apply-update.ps1` lives under `%AppData%\WinAiUsageBar\updates` and uses the generated-script marker |  |  |
| No arbitrary script path is accepted | Launch flow rejects scripts outside the app-owned updates directory |  |  |

## Startup Policy Path

Use this section only when explicitly testing automatic startup update behavior. Keep automatic install disabled unless the purpose of this run is to test guarded automatic install launch.

| Check | Expected result | Pass | Notes |
| --- | --- | --- | --- |
| Enable startup update checks | Setting is saved intentionally before restart |  |  |
| Enable automatic download only when intended | Download can be enabled without enabling install launch |  |  |
| Enable automatic install launch only with explicit confirmation | UI requires confirmation and persisted settings show download plus install launch enabled |  |  |
| Restart app once | Startup policy records latest-release status without blocking tray startup |  |  |
| Restart again before cooldown expires | Startup policy skips recent check and does not repeatedly launch the same version |  |  |
| Newer release install launch is guarded | Launch happens only after verified download and script preparation |  |  |

## Process Shutdown And Install

| Check | Expected result | Pass | Notes |
| --- | --- | --- | --- |
| App exits before copy | Apply script waits for or causes the source app process to exit before replacing files |  |  |
| No duplicate processes remain | No extra `WinAiUsageBar.App.exe` processes remain during the copy phase |  |  |
| Backup directory is created | The previous install directory is backed up before replacement |  |  |
| Copy phase succeeds or rolls back | On copy failure, the previous install is restored |  |  |
| Post-install smoke test runs | Updated app runs `--smoke-test` before success is reported |  |  |
| Validation logs are retained | `validation.out.txt` and `validation.err.txt` are written beside `install-result.json` |  |  |
| Validation log metadata is safe | Health report or Refresh settings show only redacted paths and byte counts, not log contents |  |  |
| Install result is written | `install-result.json` records success/failure, version details, rollback state, and validation status |  |  |

## Restart And Reconciliation

| Check | Expected result | Pass | Notes |
| --- | --- | --- | --- |
| Restart requested app | Updated app starts once when restart-after-install is selected |  |  |
| Tray icon appears once | Only one tray icon and one running app instance are visible |  |  |
| Version changed | About page or `--version` reports the target release |  |  |
| Settings opens | Overview, Refresh, Privacy & Data, and About open without crash |  |  |
| Health report reconciles result | `--health-report` records the app-owned install result and validation metadata |  |  |
| Refresh settings reconcile result | Refresh page shows the latest non-secret update result after restart |  |  |
| Same release is not relaunched | Restarting again does not launch another install for the same latest version |  |  |

## Normal App-Data Assertions

| Check | Expected result | Pass | Notes |
| --- | --- | --- | --- |
| `config.json` is preserved | Existing user settings remain unless intentionally changed for the run |  |  |
| `secrets/` is preserved | Secret files are not copied into reports, logs, or backups |  |  |
| Snapshots and history remain readable | Existing provider cache/history loads after update |  |  |
| Updates directory changes are expected | Only app-owned update staging, script, result, backup, and validation files are added or changed |  |  |
| No disposable paths leaked into normal state | Paths from isolated dogfood helpers are not recorded in normal app data |  |  |
| Config backup can be restored | Backup validation succeeds if rollback is needed |  |  |

## Release Decision

| Question | Answer |
| --- | --- |
| Source version |  |
| Target version |  |
| Install path |  |
| Normal app-data path |  |
| Install mode | Manual / CLI / startup policy |
| Automatic install explicitly confirmed? | No / Yes |
| Backup path |  |
| Install result path |  |
| Validation status |  |
| Rollback needed? |  |
| Any crash or hang? |  |
| Any duplicate process or tray icon? |  |
| Any secret value displayed, logged, or exported? |  |
| Ready to trust this update path more? |  |
