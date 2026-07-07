namespace WinAiUsageBar.Infrastructure.Storage;

public sealed class AppDataPaths
{
    public AppDataPaths(string rootDirectory)
    {
        RootDirectory = rootDirectory;
        ConfigPath = Path.Combine(rootDirectory, "config.json");
        SnapshotsPath = Path.Combine(rootDirectory, "snapshots.json");
        HistoryPath = Path.Combine(rootDirectory, "history.ndjson");
        DiagnosticsLogPath = Path.Combine(rootDirectory, "diagnostics.log");
        DiagnosticsExportsDirectory = Path.Combine(rootDirectory, "diagnostics-exports");
        SecretsDirectory = Path.Combine(rootDirectory, "secrets");
    }

    public string RootDirectory { get; }

    public string ConfigPath { get; }

    public string SnapshotsPath { get; }

    public string HistoryPath { get; }

    public string DiagnosticsLogPath { get; }

    public string DiagnosticsExportsDirectory { get; }

    public string SecretsDirectory { get; }

    public static AppDataPaths CreateDefault()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return new AppDataPaths(Path.Combine(appData, "WinAiUsageBar"));
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(DiagnosticsExportsDirectory);
        Directory.CreateDirectory(SecretsDirectory);
    }
}
