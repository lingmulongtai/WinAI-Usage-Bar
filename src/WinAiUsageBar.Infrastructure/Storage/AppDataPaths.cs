namespace WinAiUsageBar.Infrastructure.Storage;

public sealed class AppDataPaths
{
    public const string RootOverrideEnvironmentVariable = "WINAIUSAGEBAR_APPDATA";

    public AppDataPaths(string rootDirectory)
    {
        RootDirectory = rootDirectory;
        ConfigPath = Path.Combine(rootDirectory, "config.json");
        SnapshotsPath = Path.Combine(rootDirectory, "snapshots.json");
        HistoryPath = Path.Combine(rootDirectory, "history.ndjson");
        DiagnosticsLogPath = Path.Combine(rootDirectory, "diagnostics.log");
        DiagnosticsExportsDirectory = Path.Combine(rootDirectory, "diagnostics-exports");
        CrashReportsDirectory = Path.Combine(rootDirectory, "crash-reports");
        ConfigBackupsDirectory = Path.Combine(rootDirectory, "config-backups");
        UpdatesDirectory = Path.Combine(rootDirectory, "updates");
        SecretsDirectory = Path.Combine(rootDirectory, "secrets");
    }

    public string RootDirectory { get; }

    public string ConfigPath { get; }

    public string SnapshotsPath { get; }

    public string HistoryPath { get; }

    public string DiagnosticsLogPath { get; }

    public string DiagnosticsExportsDirectory { get; }

    public string CrashReportsDirectory { get; }

    public string ConfigBackupsDirectory { get; }

    public string UpdatesDirectory { get; }

    public string SecretsDirectory { get; }

    public static AppDataPaths CreateDefault()
    {
        return CreateDefault(
            Environment.GetEnvironmentVariable(RootOverrideEnvironmentVariable),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
    }

    public static AppDataPaths CreateDefault(
        string? rootOverride,
        string appDataRoot)
    {
        if (!string.IsNullOrWhiteSpace(rootOverride))
        {
            return new AppDataPaths(Path.GetFullPath(rootOverride));
        }

        var appData = string.IsNullOrWhiteSpace(appDataRoot)
            ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            : appDataRoot;
        return new AppDataPaths(Path.Combine(appData, "WinAiUsageBar"));
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(DiagnosticsExportsDirectory);
        Directory.CreateDirectory(CrashReportsDirectory);
        Directory.CreateDirectory(ConfigBackupsDirectory);
        Directory.CreateDirectory(UpdatesDirectory);
        Directory.CreateDirectory(SecretsDirectory);
    }
}
