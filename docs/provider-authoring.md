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

## Manual Mode

Manual mode should remain available even after automatic integrations are added. The current `ManualProviderAdapter` maps `ManualUsageSettings` into a normal `UsageSnapshot`.

## Secrets

Do not add secret properties to `config.json`. Store only secret names or handles in config and store secret values through `ISecretStore`.

## Diagnostics

Diagnostics must be useful but safe. Apply `DiagnosticRedactor` before storing or displaying process output, API errors, or raw response excerpts.
