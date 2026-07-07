using WinAiUsageBar.Core.Abstractions;
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
        if (!await commandProbe.ExistsAsync("codex", cancellationToken).ConfigureAwait(false))
        {
            return ProviderFetchResult.Failure(
                Descriptor,
                ProviderHealth.Unsupported,
                DataSourceKind.LocalAppServer,
                context.Now,
                "Codex CLI was not found. Use Manual mode or install Codex.",
                "codex command was not found on PATH.");
        }

        try
        {
            var data = await client.FetchAccountUsageAsync(cancellationToken).ConfigureAwait(false);
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
        catch (Exception ex)
        {
            return ProviderFetchResult.Failure(
                Descriptor,
                ProviderHealth.Error,
                DataSourceKind.LocalAppServer,
                context.Now,
                "Codex app-server failed. Manual mode is still available.",
                ex.Message);
        }
    }
}
