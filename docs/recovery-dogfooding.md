# Recovery Dogfooding

This file records config backup, restore, reset, and recovery-guidance checks. Do not paste API keys, tokens, cookies, secret values, secret file names, local account data, or raw provider auth contents here.

## 2026-07-09 - Isolated Config Backup Restore Reset

Result: passed, with one follow-up.

Context:

- Issue: #202
- App build: `WinAI Usage Bar 0.1.6+cfd73526a695e6e821d0bae26b01bbb243304ab2`
- Isolated app data: `artifacts/recovery-dogfood/issue-202-20260709-211014/appdata`
- Logs: `artifacts/recovery-dogfood/issue-202-20260709-211014/logs`
- Backup file: `config-backup-20260709-211016.json`
- Backup size: `7069` bytes

What was checked:

- Ran `--health-report` against isolated app data to create and inspect default local state.
- Created a non-secret sentinel file under isolated `secrets/` and recorded its SHA256 before recovery operations.
- Ran `--set-provider-cli-override --provider Codex --command <path>` to make `config.json` intentionally non-default without printing the configured value.
- Ran `--export-config-backup`.
- Ran `--list-config-backups --limit 5`.
- Ran `--validate-config-backup <backup>`.
- Ran `--validate-latest-config-backup`.
- Ran `--clear-provider-cli-override --provider Codex` and confirmed the override was removed before restore.
- Ran `--restore-latest-config-backup --confirm` and confirmed the backed-up Codex CLI override was restored.
- Ran `--reset-config-to-defaults --confirm` and confirmed the Codex CLI override was removed again.
- Recomputed the sentinel file SHA256 and confirmed it was unchanged.
- Checked combined command output for the sentinel content, sentinel file name, and configured CLI override path; none were printed.

Observed result:

```text
SentinelHashUnchanged: true
RestoreRecoveredCodexOverride: true
ResetRemovedCodexOverride: true
BackupCount: 3
```

Finding:

- The first attempt created the backup correctly but failed when the script parsed the printed `Path:` value from redirected GUI stdout because non-ASCII path segments were mojibake. The successful run discovered the latest backup from the filesystem instead of trusting the printed path.
- Follow-up: https://github.com/lingmulongtai/WinAI-Usage-Bar/issues/209

Product expectation:

- Backup, latest-backup validation, latest-backup restore, and reset-to-defaults are safe to dogfood with isolated app data.
- These flows must not copy, delete, modify, or print files under `secrets/`.
- Scripts should avoid relying on redirected GUI stdout for non-ASCII paths until #209 is fixed.
