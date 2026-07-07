# Provider Authoring

Providers are descriptor-driven. Add new provider work in `WinAiUsageBar.Core` first, then wire any Windows-specific process, storage, or secure secret behavior through Infrastructure abstractions.

## Steps

1. Add or update a `ProviderDescriptor` in `ProviderDescriptors`.
2. Add provider-specific logic under `src/WinAiUsageBar.Core/Providers/<ProviderName>/`.
3. Implement `IProviderAdapter`.
4. Keep process execution behind an abstraction such as `ICommandProbe` or a provider client interface.
5. Return `ProviderFetchResult` with a snapshot for all expected states.
6. Use `ProviderHealth.AuthRequired`, `ProviderHealth.Unsupported`, or `ProviderHealth.Error` for non-crashing failures.
7. Add focused tests that do not require external CLIs or network access.

## Fetch Result Contract

Provider adapters must return one `ProviderFetchResult` per fetch attempt. The refresh service and UI depend on these meanings:

| Field | Meaning |
| --- | --- |
| `Success` | `true` only when the snapshot represents fresh provider data for the selected source. |
| `Snapshot` | Should be non-null for both success and expected failures so the UI can show provider health. |
| `ErrorMessage` | Set when `Success` is `false`; safe to display to the user. |
| `Diagnostics` | Safe implementation details for troubleshooting. These must never contain API keys, tokens, cookies, or auth file contents. |
| `Health` | `Ok`, `Warning`, and `Error` describe usable provider data; `AuthRequired` means credentials or permissions are missing; `Unsupported` means the selected source is not available in the MVP or environment. |

Expected failures should not throw. Return a failure result instead:

| Case | Required result |
| --- | --- |
| Missing external CLI | `Success = false`, `Health = Unsupported`, source set to the attempted source. |
| CLI found but cannot start | `Success = false`, usually `Health = Unsupported`, with a repair-oriented user message and safe diagnostics. |
| Missing or expired credentials | `Success = false`, `Health = AuthRequired`. |
| Documented unsupported endpoint | `Success = false`, `Health = Unsupported`. |
| Provider-specific recoverable error | `Success = false`, `Health = Error`. |
| Caller cancellation | Throw `OperationCanceledException` so app shutdown and user cancellation stay responsive. |

The refresh service catches unexpected provider exceptions and per-provider timeouts, converts them to `Error` snapshots, and continues refreshing other providers. If a provider fails after a previous successful snapshot, the refresh service may keep the previous usage values while updating health, status, source, and error fields. This stale-cache behavior keeps the UI useful without pretending the failed provider returned fresh usage.

## Manual Mode

Manual mode should remain available even after automatic integrations are added. The current `ManualProviderAdapter` maps `ManualUsageSettings` into a normal `UsageSnapshot`.

## Secrets

Do not add secret properties to `config.json`. Store only secret names or handles in config and store secret values through `ISecretStore`.

## Diagnostics

Diagnostics must be useful but safe. Apply `DiagnosticRedactor` before storing or displaying process output, API errors, or raw response excerpts.
