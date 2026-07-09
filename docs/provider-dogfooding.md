# Provider Dogfooding

This file records provider-integration checks that touch local tooling. Do not paste API keys, tokens, cookies, account JSON, `auth.json` contents, or raw provider responses here.

## 2026-07-08 - Codex WindowsApps CLI Access Denied

Result: expected unsupported state.

What was checked:

- `where codex` resolved to the Codex Desktop packaged CLI under `C:\Program Files\WindowsApps\OpenAI.Codex_26.623.19656.0_x64__2p2nqsd0c76g0\app\resources\`.
- `codex --version` failed from PowerShell with `Access is denied.`
- `cmd.exe /d /c codex --version` failed with the same access denial.

Product expectation:

- `--health-report` should report the command as found but startup failed.
- Provider refresh for Codex/ChatGPT LocalAppServer should return a visible unsupported/auth/error state, not crash the app.
- Repair guidance should suggest Manual mode and a launchable Codex CLI command override outside WindowsApps.

Safe workaround:

Use Manual mode until a launchable Codex CLI path is available. If a separate launchable CLI is installed, save it from Providers settings or with:

```powershell
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --set-provider-cli-override --provider Codex --command C:\Tools\codex.cmd
```

Clear the workaround with:

```powershell
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --clear-provider-cli-override --provider Codex
```

## Claude And Claude Code CLI Placeholder

Result expectation: unsupported automatic usage state, not a crash.

What to check:

- Set Claude or Claude Code to `Cli` source.
- Run `--refresh-once --provider Claude --source Cli` or `--refresh-once --provider ClaudeCode --source Cli`.
- Repeat once with a provider CLI command override if PATH discovery is unreliable.

Product expectation:

- The adapter reports a visible `Unsupported` state when the `claude` command is missing or when the command/override is present but automatic usage retrieval is unavailable.
- Diagnostics may say that the `claude` command was found or that a provider CLI command override is configured, but must not echo the override path.
- Repair guidance should say this is a safe readiness placeholder, not real usage retrieval.
- The app must not run interactive `/usage` commands, scrape private local files, read auth files, or invent unofficial endpoints.
- Manual mode remains the recommended fallback until official usage telemetry, SDK, or documented API support is added.

Safe workaround:

Use Manual mode for usage values today. If checking CLI readiness, save only a launchable command override, never tokens or auth values:

```powershell
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --set-provider-cli-override --provider ClaudeCode --command C:\Tools\claude.cmd
```

## GitHub Copilot Metrics Failure States

Result expectation: personal users stay in Manual mode; organization or enterprise metrics failures are auth-required and non-secret.

What to check:

- Leave GitHub Copilot in `Manual` mode for personal-only tracking.
- Switch GitHub Copilot to `OfficialApi` only when testing organization or enterprise metrics.
- Test missing scope with a PAT secret reference configured.
- Test missing PAT secret name with an organization or enterprise scope configured.
- Test a PAT secret name whose value is absent from the secret store.
- Test a permission-denied metrics response with organization/enterprise scope and PAT configured.

Product expectation:

- Missing organization/enterprise scope reports `AuthRequired`, explains that personal users can stay in Manual mode, and does not resolve or require the PAT secret.
- Missing PAT secret name reports `AuthRequired` only after a scope is configured and tells the user to keep only a secret-name reference in provider settings.
- Missing PAT secret value routes the user to Privacy & Data without echoing the secret reference.
- Permission denied, missing permissions, 401, 403, and 404 metrics responses report `AuthRequired` with generic scope/PAT guidance.
- Provider Details, `--refresh-once`, diagnostics, and dogfood notes must not echo tokens, PAT secret names, organization names, enterprise slugs, signed download links, or raw API response bodies.

Safe dogfood examples:

```powershell
# Missing scope: should not require or resolve the configured PAT reference.
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --refresh-once --provider GitHubCopilot --source OfficialApi

# Personal tracking: stay in Manual mode unless organization/enterprise metrics are explicitly being tested.
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --refresh-once --provider GitHubCopilot --source Manual
```
