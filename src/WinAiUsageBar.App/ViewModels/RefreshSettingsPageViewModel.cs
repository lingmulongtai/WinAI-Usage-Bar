using System.Globalization;
using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Infrastructure.Security;

namespace WinAiUsageBar.App.ViewModels;

public sealed class RefreshSettingsPageViewModel(AppConfig config)
{
    public string IntervalText { get; set; } = config.Refresh.Interval.ToString();

    public bool NotificationsEnabled { get; set; } = config.Notifications.IsEnabled;

    public string HistoryMaxDaysText { get; set; } = config.HistoryRetention.MaxDays.ToString(CultureInfo.InvariantCulture);

    public string HistoryMaxBytesText { get; set; } = config.HistoryRetention.MaxBytes.ToString(CultureInfo.InvariantCulture);

    public bool CheckUpdatesOnStartup { get; set; } = config.Updates.CheckOnStartup;

    public string UpdateCheckIntervalHoursText { get; set; } =
        config.Updates.MinimumCheckIntervalHours.ToString(CultureInfo.InvariantCulture);

    public bool DownloadUpdatesAutomatically { get; set; } = config.Updates.DownloadAutomatically;

    public bool InstallUpdatesAutomatically { get; set; } = config.Updates.InstallAutomatically;

    public string UpdateStatusText { get; } = FormatUpdateStatus(config.Updates);

    public RefreshSettingsApplyResult TryApply()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (!Enum.TryParse<RefreshIntervalKind>(IntervalText, out var interval))
        {
            errors.Add("Refresh interval is invalid.");
        }

        var maxDays = ParseInt(HistoryMaxDaysText, "History max days", errors);
        var maxBytes = ParseLong(HistoryMaxBytesText, "History max bytes", errors);
        var updateIntervalHours = ParseInt(UpdateCheckIntervalHoursText, "Startup update interval hours", errors);

        if (InstallUpdatesAutomatically && !DownloadUpdatesAutomatically)
        {
            errors.Add("Automatic install requires automatic download.");
        }

        if (errors.Count > 0 || maxDays is null || maxBytes is null || updateIntervalHours is null)
        {
            return new RefreshSettingsApplyResult(IsValid: false, errors, warnings);
        }

        var clampedDays = Math.Clamp(
            maxDays.Value,
            HistoryRetentionSettings.MinDays,
            HistoryRetentionSettings.MaxDaysLimit);
        var clampedBytes = Math.Clamp(
            maxBytes.Value,
            HistoryRetentionSettings.MinBytes,
            HistoryRetentionSettings.MaxBytesLimit);

        if (clampedDays != maxDays.Value)
        {
            warnings.Add($"History max days was clamped to {clampedDays}.");
        }

        if (clampedBytes != maxBytes.Value)
        {
            warnings.Add($"History max bytes was clamped to {clampedBytes}.");
        }

        var clampedUpdateIntervalHours = Math.Clamp(
            updateIntervalHours.Value,
            UpdateSettings.MinCheckIntervalHours,
            UpdateSettings.MaxCheckIntervalHours);
        if (clampedUpdateIntervalHours != updateIntervalHours.Value)
        {
            warnings.Add($"Startup update interval hours was clamped to {clampedUpdateIntervalHours}.");
        }

        config.Refresh.Interval = interval;
        config.Notifications.IsEnabled = NotificationsEnabled;
        config.HistoryRetention.MaxDays = clampedDays;
        config.HistoryRetention.MaxBytes = clampedBytes;
        config.Updates.CheckOnStartup = CheckUpdatesOnStartup;
        config.Updates.MinimumCheckIntervalHours = clampedUpdateIntervalHours;
        config.Updates.DownloadAutomatically = DownloadUpdatesAutomatically;
        config.Updates.InstallAutomatically = InstallUpdatesAutomatically;

        return new RefreshSettingsApplyResult(IsValid: true, errors, warnings);
    }

    private static string FormatUpdateStatus(UpdateSettings updates)
    {
        var checkedText = updates.LastCheckedAt is null
            ? "Last checked: never"
            : $"Last checked: {updates.LastCheckedAt:yyyy-MM-dd HH:mm:ss zzz}";
        var status = string.IsNullOrWhiteSpace(updates.LastStatus)
            ? "Unknown"
            : SafeValue(updates.LastStatus);
        var latest = string.IsNullOrWhiteSpace(updates.LastLatestVersion)
            ? "n/a"
            : SafeValue(updates.LastLatestVersion);
        var current = string.IsNullOrWhiteSpace(updates.LastCurrentVersion)
            ? null
            : SafeValue(updates.LastCurrentVersion);
        var message = string.IsNullOrWhiteSpace(updates.LastMessage)
            ? "No update status has been recorded yet."
            : SafeValue(updates.LastMessage);
        var interval = updates.MinimumCheckIntervalHours <= 0
            ? "Startup interval: every startup"
            : $"Startup interval: at most every {updates.MinimumCheckIntervalHours} hour(s)";
        var launchedVersion = string.IsNullOrWhiteSpace(updates.LastInstallLaunchedVersion)
            ? "Last launched install: n/a"
            : $"Last launched install: {SafeValue(updates.LastInstallLaunchedVersion)}";

        var lines = new List<string>
        {
            checkedText,
            interval,
            $"Status: {status}",
            $"Latest version: {latest}"
        };

        if (current is not null)
        {
            lines.Add($"Current version: {current}");
        }

        lines.Add(launchedVersion);

        if (!string.IsNullOrWhiteSpace(updates.LastPackagePath))
        {
            lines.Add($"Package path: {SafeValue(updates.LastPackagePath)}");
        }

        if (!string.IsNullOrWhiteSpace(updates.LastInstallScriptPath))
        {
            lines.Add($"Install script: {SafeValue(updates.LastInstallScriptPath)}");
        }

        lines.Add($"Message: {message}");

        return string.Join(
            Environment.NewLine,
            lines);
    }

    private static int? ParseInt(string text, string label, ICollection<string> errors)
    {
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        errors.Add($"{label} must be a whole number.");
        return null;
    }

    private static long? ParseLong(string text, string label, ICollection<string> errors)
    {
        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        errors.Add($"{label} must be a whole number.");
        return null;
    }

    private static string SafeValue(string value)
    {
        return DiagnosticRedactor.RedactForDisplay(value);
    }
}

public sealed record RefreshSettingsApplyResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
