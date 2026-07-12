using System.ComponentModel;
using WinAiUsageBar.Core.Abstractions;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;

namespace WinAiUsageBar.Core.Providers.Codex;

public sealed class CodexAppServerProviderAdapter(
    ProviderDescriptor descriptor,
    ICommandProbe commandProbe,
    ICodexAppServerClient client,
    DataSourceKind sourceKind = DataSourceKind.LocalAppServer) : IProviderAdapter
{
    private const string CodexCliCannotStartMessage =
        "Codex CLI was found but Windows could not start it. Check the app execution alias, reinstall Codex outside WindowsApps, or set a provider CLI command override to a launchable path.";

    public ProviderDescriptor Descriptor { get; } = descriptor;

    public async Task<ProviderFetchResult> FetchAsync(
        ProviderFetchContext context,
        CancellationToken cancellationToken)
    {
        var probe = CreateProbeFromOverride(context.ProviderConfig.Cli.CommandPathOverride)
            ?? await commandProbe.InspectAsync("codex", cancellationToken).ConfigureAwait(false);
        if (!probe.IsFound)
        {
            return ProviderFetchResult.Failure(
                Descriptor,
                ProviderHealth.Unsupported,
                sourceKind,
                context.Now,
                "Codex CLI was not found. Use Manual mode or install Codex.",
                probe.StatusMessage);
        }

        try
        {
            var data = await client.FetchAccountUsageAsync(probe, cancellationToken).ConfigureAwait(false);
            var snapshot = CodexJsonRpcParser.CreateSnapshot(Descriptor, data, context.Now, sourceKind);

            return new ProviderFetchResult(
                snapshot,
                true,
                null,
                data.Diagnostics);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            return ProviderFetchResult.Failure(
                Descriptor,
                ProviderHealth.AuthRequired,
                sourceKind,
                context.Now,
                "Codex app-server requires authentication.",
                ex.Message);
        }
        catch (Win32Exception ex)
        {
            return ProviderFetchResult.Failure(
                Descriptor,
                ProviderHealth.Unsupported,
                sourceKind,
                context.Now,
                CodexCliCannotStartMessage,
                probe.StatusMessage,
                $"Codex startup failed with Win32 error {ex.NativeErrorCode}.");
        }
        catch (Exception ex)
        {
            return ProviderFetchResult.Failure(
                Descriptor,
                ProviderHealth.Error,
                sourceKind,
                context.Now,
                "Codex app-server failed. Manual mode is still available.",
                probe.StatusMessage,
                ex.Message);
        }
    }

    private static CommandProbeResult? CreateProbeFromOverride(string? commandPathOverride)
    {
        var normalizedOverride = CliCommandSettings.NormalizeCommandPathOverride(commandPathOverride);
        return normalizedOverride is null
            ? null
            : CommandProbeResult.Configured("codex", normalizedOverride);
    }
}
