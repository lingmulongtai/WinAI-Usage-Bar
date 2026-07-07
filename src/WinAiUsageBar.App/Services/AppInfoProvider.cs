using System.Reflection;

namespace WinAiUsageBar.App.Services;

public sealed record AppInfo(
    string ProductName,
    string Version,
    string InformationalVersion);

public static class AppInfoProvider
{
    public static AppInfo Get()
    {
        return Get(typeof(AppInfoProvider).Assembly);
    }

    public static AppInfo Get(Assembly assembly)
    {
        var productName = assembly
            .GetCustomAttribute<AssemblyProductAttribute>()
            ?.Product;
        var version = assembly.GetName().Version?.ToString();
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        return new AppInfo(
            string.IsNullOrWhiteSpace(productName) ? "WinAI Usage Bar" : productName,
            string.IsNullOrWhiteSpace(version) ? "0.0.0.0" : version,
            string.IsNullOrWhiteSpace(informationalVersion) ? version ?? "0.0.0" : informationalVersion);
    }
}
