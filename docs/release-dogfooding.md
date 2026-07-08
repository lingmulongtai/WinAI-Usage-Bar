# Release Dogfooding

This file records small release-to-release checks that use published artifacts. Do not paste secrets, tokens, cookies, or local account data here.

## 2026-07-08 - v0.1.1 Sees v0.1.2

Result: passed.

What was checked:

- Downloaded the published `WinAIUsageBar-0.1.1-win-x64.zip` asset into an ignored `.tmp/release-dogfood/` directory.
- Extracted the zip without touching the normal install directory.
- Ran the `v0.1.1` executable with `--check-for-updates` against the real GitHub latest-release endpoint.

Observed output summary:

```text
Status: UpdateAvailable
Current version: 0.1.1
Latest version: 0.1.2
Update available: yes
Package: WinAIUsageBar-0.1.2-win-x64.zip
Checksum: WinAIUsageBar-0.1.2-win-x64.zip.sha256
```

Caveat:

- `WinAiUsageBar.App.exe` is a Windows GUI executable, so PowerShell verification scripts should use `Start-Process -Wait -PassThru` with redirected output when command completion matters.
- This check only verified release discovery. It did not run the download, prepare, launch, or install stages.
