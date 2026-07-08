using System.ComponentModel;
using WinAiUsageBar.Core.Abstractions;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;

namespace WinAiUsageBar.Core.Providers.Codex;

public sealed class CodexAppServerProviderAdapter(
    ProviderDescriptor descriptor,
    ICommandProbe commandProbe,
    ICodexAppServerClient client) : IProviderAdapter
{
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
                DataSourceKind.LocalAppServer,
                context.Now,
                "Codex CLI was not found. Use Manual mode or install Codex.",
                probe.StatusMessage);
        }

        try
        {
            var data = await client.FetchAccountUsageAsync(probe, cancellationToken).ConfigureAwait(false);
            var snapshot = CodexJsonRpcParser.CreateSnapshot(Descriptor, data, context.Now);

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
                DataSourceKind.LocalAppServer,
                context.Now,
                "Codex app-server requires authentication.",
                ex.Message);
        }
        catch (Win32Exception ex)
        {
            return ProviderFetchResult.Failure(
                Descriptor,
                ProviderHealth.Unsupported,
                DataSourceKind.LocalAppServer,
                context.Now,
                $"Codex CLI was found but Windows could not start it. Check the app execution alias or reinstall Codex. Details: {ex.Message}",
                probe.StatusMessage,
                $"Codex startup failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ProviderFetchResult.Failure(
                Descriptor,
                ProviderHealth.Error,
                DataSourceKind.LocalAppServer,
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
