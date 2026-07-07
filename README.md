# WinAI Usage Bar

WinAI Usage Bar is a personal Windows notification-area app for watching AI provider usage. It is inspired by the idea of a compact usage bar, but the implementation is native Windows: C#, WinUI 3, Windows App SDK, MVVM-style view models, and a clean provider architecture.

## Features

- Starts minimized to the Windows tray.
- Left-click tray icon opens a compact usage panel.
- Right-click tray icon shows Show, Show Widget, Refresh Now, Settings, and Exit.
- Settings window uses WinUI `NavigationView`.
- Desktop widget window shows up to three selected providers and remembers placement.
- Provider cards show health, usage percentage, reset text, credits/costs, source, update time, and errors.
- Mock and Manual provider modes are implemented.
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

The app starts in the tray. Use the tray icon to open the compact panel or settings.

## Privacy

- API keys, tokens, cookies, and auth file contents are not stored in plain text.
- `config.json` stores non-secret settings only.
- `DpapiSecretStore` stores secrets in protected files under `%AppData%\WinAiUsageBar\secrets`.
- Diagnostics pass through redaction before being stored or surfaced.
- Diagnostics can be exported from Privacy & Data; exports redact common secret shapes and never include files under `secrets/`.
- Browser cookie scraping is intentionally not implemented in this MVP.
- Codex integration never reads or displays `auth.json` contents.

## Provider Status

- Mock: implemented for UI development.
- Manual: implemented for every provider.
- Codex / ChatGPT: safe best-effort `codex app-server` JSON-RPC client and parser are implemented; failures return visible provider errors.
- Claude / Claude Code: CLI presence probe only; no private file scraping.
- Gemini: API key secret-name setting; no unofficial usage endpoint.
- OpenCode Zen: API key secret-name setting, manual balance mode, and documented TODO for future official balance API.
- GitHub Copilot: manual mode plus organization/enterprise settings placeholder.

## Roadmap

- Add richer provider-specific settings pages.
- Add official API integrations where documented and permitted.
- Improve compact panel placement near the taskbar.
- Add charts from `history.ndjson`.
- Add Windows local notifications beyond the no-op MVP service.
- Package and sign the app for easier installation.
