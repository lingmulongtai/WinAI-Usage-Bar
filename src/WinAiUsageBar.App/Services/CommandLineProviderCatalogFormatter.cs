using System.Text;
using WinAiUsageBar.Core.Models;

namespace WinAiUsageBar.App.Services;

public static class CommandLineProviderCatalogFormatter
{
    public static string Format(IReadOnlyList<ProviderDescriptor> descriptors)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Provider catalog");
        builder.AppendLine($"Total providers: {descriptors.Count}");

        foreach (var descriptor in descriptors)
        {
            builder.AppendLine();
            builder.AppendLine($"{descriptor.DisplayName} ({descriptor.Id})");
            builder.AppendLine($"  Short name: {descriptor.ShortName}");
            builder.AppendLine($"  Enabled by default: {FormatBoolean(descriptor.IsEnabledByDefault)}");
            builder.AppendLine($"  Supports login: {FormatBoolean(descriptor.SupportsLogin)}");
            builder.AppendLine($"  Supports credits: {FormatBoolean(descriptor.SupportsCredits)}");
            builder.AppendLine($"  Supports status polling: {FormatBoolean(descriptor.SupportsStatusPolling)}");
            builder.AppendLine($"  Sources: {string.Join(", ", descriptor.SupportedSources)}");
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatBoolean(bool value)
    {
        return value ? "yes" : "no";
    }
}
