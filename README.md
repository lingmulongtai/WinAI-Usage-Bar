# WinAI Usage Bar

WinAI Usage Bar is a personal Windows notification-area app for watching AI provider usage. It is inspired by the idea of a compact usage bar, but the implementation is native Windows: C#, WinUI 3, Windows App SDK, MVVM-style view models, and a clean provider architecture.

Current app version: `0.1.0`.

## Features

- Starts minimized to the Windows tray.
- Prevents duplicate tray instances when launched twice.
- Optional start-at-login registration is available from Appearance settings.
- Left-click tray icon opens a compact usage panel.
- Right-click tray icon shows Show, Show Widget, Refresh Now, Settings, and Exit.
- Settings window uses WinUI `NavigationView`.
- Desktop widget window shows up to three selected providers, remembers placement, and has settings for startup/topmost/provider selection.
- Provider cards show health, usage percentage, reset text, status messages, credits/costs, source, update time, and errors.
- History page summarizes retained `history.ndjson` entries by provider without showing raw snapshot messages.
- Refresh settings include interval, notification enablement, and history retention limits.
- Appearance settings apply System, Light, or Dark theme to app windows.
- Privacy & Data shows a diagnostics summary with local file paths, config version, cached snapshot count, latest update time, and tracked file sizes.
- Privacy & Data can clear cached snapshots and retained history without deleting config or saved secrets.
- Mock and Manual provider modes are implemented.
- Manual mode can track used/remaining percentage, reset datetime/description, credits, currency/unit, month cost, last-31-day tokens, and notes.
- Codex/ChatGPT app-server probing is isolated behind safe abstractions.
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
```

Use `--export-diagnostics` when you want a redacted support bundle on disk. Use `--health-report` when you want a quick non-secret summary printed to the console.

## Release

Release notes are tracked in [CHANGELOG.md](CHANGELOG.md).

To create a draft GitHub Release:

1. Update the app version in `src\WinAiUsageBar.App\WinAiUsageBar.App.csproj`.
2. Commit the version change.
3. Create and push a version tag:

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
- Browser cookie scraping is intentionally not implemented in this MVP.
- Codex integration never reads or displays `auth.json` contents.

## Provider Status

- Mock: implemented for UI development.
- Manual: implemented for every provider.
- Codex / ChatGPT: safe best-effort `codex app-server` JSON-RPC client and parser are implemented; failures return visible provider errors.
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
