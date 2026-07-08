namespace WinAiUsageBar.Infrastructure.Updates;

internal static class UpdateInstallScriptMarkers
{
    public const string MarkerLine = "# WinAI Usage Bar generated update script";
    public const string VersionLine = "$WinAiUsageBarGeneratedUpdateScriptVersion = 1";
    public const int HeaderProbeByteCount = 8192;

    public static bool IsGeneratedScriptHeader(string header)
    {
        return header.Contains(MarkerLine, StringComparison.Ordinal)
            && header.Contains(VersionLine, StringComparison.Ordinal);
    }
}
