# Agent Notes

This repo is a Windows desktop MVP. Keep changes small and preserve the layer boundaries:

- `WinAiUsageBar.Core`: provider models, descriptors, mapping, adapters, JSON-RPC parsing, and logic that can be unit tested without Windows UI or external CLIs.
- `WinAiUsageBar.Infrastructure`: storage, DPAPI secret store, redaction, tray service, process execution, scheduling, notifications, and placement persistence.
- `WinAiUsageBar.App`: WinUI 3 windows, view models, and application composition.

Rules of thumb:

- Do not put provider-specific business logic in WinUI windows.
- Do not read, display, log, or commit API keys, tokens, cookies, or `auth.json` contents.
- Do not implement browser cookie scraping unless the product spec is explicitly revised.
- Prefer Manual mode fallbacks for provider failures.
- Tests must run without `codex`, `claude`, or other external CLIs installed.
- Build and test with `-p:Platform=x64` because the app uses Windows App SDK self-contained output.

Commit style should use Conventional Commits, for example `feat: add manual provider editor` or `test: cover codex parser`.
