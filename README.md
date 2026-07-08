# WinAI Usage Bar

WinAI Usage Bar is a personal Windows notification-area app for watching AI provider usage. It is inspired by the idea of a compact usage bar, but the implementation is native Windows: C#, WinUI 3, Windows App SDK, MVVM-style view models, and a clean provider architecture.

Current app version: `0.1.0`.

For a strict status check, see [docs/current-state-audit.md](docs/current-state-audit.md).
For Windows shell dogfooding checks, see [docs/windows-manual-verification.md](docs/windows-manual-verification.md).
Create a timestamped local verification report with `.\scripts\new-windows-verification-report.ps1`.

## Features

- Starts minimized to the Windows tray.
- Prevents duplicate tray instances when launched twice.
- Optional start-at-login registration is available from Appearance settings.
- Left-click tray icon opens a compact usage panel.
- Right-click tray icon shows Show, Show Widget, Refresh Now, Settings, and Exit.
- Settings window uses WinUI `NavigationView`.
- Overview includes a first-run setup section until setup is marked complete.
- Desktop widget window shows up to three selected providers, remembers placement, and has settings for startup/topmost/provider selection.
- Provider cards show health, usage percentage, reset text, status messages, credits/costs, source, update time, and errors.
- Provider Details shows non-secret snapshot details for identity, usage windows, credits, status, and errors.
- History page summarizes retained `history.ndjson` entries by provider without showing raw snapshot messages.
- Refresh settings include interval, notification enablement, and history retention limits.
- Appearance settings apply System, Light, or Dark theme to app windows.
- Privacy & Data shows a diagnostics summary with local file paths, config version, cached snapshot count, latest update time, and tracked file sizes.
- Privacy & Data can clear cached snapshots and retained history without deleting config or saved secrets.
- Privacy & Data can export a timestamped `config.json` backup without copying secret values.
- Privacy & Data can validate and restore the latest config backup after explicit confirmation.
- Privacy & Data can reset `config.json` to defaults after explicit confirmation, creating a rollback backup and leaving saved secrets untouched.
- Mock and Manual provider modes are implemented.
- Manual mode can track used/remaining percentage, reset datetime/description, credits, currency/unit, month cost, last-31-day tokens, and notes.
- Codex/ChatGPT app-server probing is isolated behind safe abstractions.
- Codex CLI startup failures are classified separately from auth and JSON-RPC errors, with repair-oriented messages for Windows app alias or permission problems.
- Claude, Claude Code, Gemini, OpenCode Zen, and GitHub Copilot have MVP-safe descriptors and manual mode support.
- Gemini and OpenCode Zen expose API key secret-name fields without storing API key values in config.
- JSON config, snapshot cache, and history are stored under `%AppData%\WinAiUsageBar`.

## Build And Run

Requirements:

- Windows 10/11
- .NET 8 SDK
- Windows App SDK dependencies restored from NuGet

```powershell
dotnet restore
dotnet build .\WinAIUsageBar.sln -p:Platform=x64
dotnet test .\tests\WinAiUsageBar.Core.Tests\WinAiUsageBar.Core.Tests.csproj -p:Platform=x64
dotnet run --project .\src\WinAiUsageBar.App\WinAiUsageBar.App.csproj -p:Platform=x64
```

If NuGet restore is flaky, use `.\scripts\restore.ps1` for bounded restore retries.

The app starts in the tray. Use the tray icon to open the compact panel or settings.

## Publish

Create a local self-contained build:

```powershell
.\scripts\publish.ps1
```

The default output is `artifacts\publish\WinAIUsageBar-win-x64`. Run `WinAiUsageBar.App.exe` from that folder. Pushes to `main` also upload a `WinAIUsageBar-win-x64` artifact from GitHub Actions.

Create a zip package and SHA256 checksum from the published output:

```powershell
.\scripts\package.ps1
Get-Content .\artifacts\packages\WinAIUsageBar-0.1.0-win-x64.zip.sha256
Get-FileHash .\artifacts\packages\WinAIUsageBar-0.1.0-win-x64.zip -Algorithm SHA256
```

By default, the package script reads the app version from `WinAiUsageBar.App.csproj` and creates `WinAIUsageBar-<version>-win-x64.zip`. Pass `-PackageName WinAIUsageBar-custom-name` to override the generated name.

Pushes to `main` upload both the publish directory and a `WinAIUsageBar-win-x64-package` artifact containing the versioned zip and `.sha256` file.

Run the published-app smoke test without opening UI:

```powershell
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --smoke-test
```

Published builds also support lightweight command-line checks:

```powershell
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --help
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --version
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --smoke-test
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --export-diagnostics
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --health-report
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --provider-catalog
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --validate-config-backup .\config-backup.json
.\artifacts\publish\WinAIUsageBar-win-x64\WinAiUsageBar.App.exe --restore-config-backup .\config-backup.json --confirm
```

Use `--export-diagnostics` when you want a redacted support bundle on disk. Use `--health-report` when you want a quick non-secret summary printed to the console, including safe CLI environment checks for `codex`, `claude`, `gh`, and `git`. Use `--provider-catalog` to inspect the built-in provider descriptors without reading local config. Use `--validate-config-backup` to check a backup file before applying it. Use `--restore-config-backup <path> --confirm` to validate and restore a config backup after creating a rollback copy of the current `config.json`.

## Release

Release notes are tracked in [CHANGELOG.md](CHANGELOG.md).

To create a draft GitHub Release:

1. Update the app version in `src\WinAiUsageBar.App\WinAiUsageBar.App.csproj`.
2. Commit the version change.
3. Generate and complete a Windows verification report:

```powershell
.\scripts\new-windows-verification-report.ps1
```

4. Publish, package, and verify release readiness:

```powershell
.\scripts\publish.ps1
.\scripts\package.ps1
$report = Get-ChildItem .\artifacts\verification\windows-verification-*.md |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 1
.\scripts\verify-release-readiness.ps1 -TagName v0.1.0 -VerificationReportPath $report.FullName -RequireVerificationReport
```

5. Create and push a version tag:

```powershell
git tag v0.1.0
git push origin v0.1.0
```

The release workflow builds, tests, publishes, smoke-tests, packages the app, and creates a draft release with the versioned zip and checksum attached. Review the draft in GitHub before publishing it.

## Privacy

- API keys, tokens, cookies, and auth file contents are not stored in plain text.
- `config.json` stores non-secret settings only.
- `DpapiSecretStore` stores secrets in protected files under `%AppData%\WinAiUsageBar\secrets`.
- Diagnostics pass through redaction before being stored or surfaced.
- Privacy & Data shows non-secret diagnostics metadata only; it does not list secret names or values.
- Diagnostics can be exported from Privacy & Data; exports redact common secret shapes and never include files under `secrets/`.
- Secret values can be saved or deleted by secret name from Privacy & Data; values are never displayed back.
- Snapshot cache and retained history can be cleared from Privacy & Data; `config.json` and `secrets/` are left untouched.
- `config.json` can be backed up from Privacy & Data; backup files include non-secret settings only and do not copy `secrets/`.
- Config backups can be restored from the CLI with an explicit `--confirm`, or from Privacy & Data using the latest backup and an in-app confirmation checkbox; restore creates a rollback backup first and does not copy or modify files under `secrets/`.
- `config.json` can be reset to defaults from Privacy & Data after an in-app confirmation checkbox; reset creates a rollback backup first and does not delete or modify files under `secrets/`.
- Browser cookie scraping is intentionally not implemented in this MVP.
- Codex integration never reads or displays `auth.json` contents.

## Provider Status

- Mock: implemented for UI development.
- Manual: implemented for every provider.
- Codex / ChatGPT: safe best-effort `codex app-server` JSON-RPC client and parser are implemented; missing CLI, startup failure, auth failure, and JSON-RPC failure return visible provider errors.
- Claude / Claude Code: CLI presence probe only; no private file scraping.
- Gemini: API key secret-name setting; no unofficial usage endpoint.
- OpenCode Zen: API key secret-name setting, manual balance mode, and documented TODO for future official balance API.
- GitHub Copilot: manual mode plus organization/enterprise metrics report metadata via the GitHub Copilot usage metrics API.

## Roadmap

- Add richer provider-specific settings pages.
- Add official API integrations where documented and permitted.
- Improve compact panel placement near the taskbar.
- Add charts from `history.ndjson`.
- Add Windows local notifications beyond the no-op MVP service.
- Package and sign the app for easier installation.
