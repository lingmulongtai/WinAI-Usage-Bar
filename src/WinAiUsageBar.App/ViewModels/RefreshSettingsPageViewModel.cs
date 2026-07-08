using System.Globalization;
using WinAiUsageBar.Core.Configuration;

namespace WinAiUsageBar.App.ViewModels;

public sealed class RefreshSettingsPageViewModel(AppConfig config)
{
    public string IntervalText { get; set; } = config.Refresh.Interval.ToString();

    public bool NotificationsEnabled { get; set; } = config.Notifications.IsEnabled;

    public string HistoryMaxDaysText { get; set; } = config.HistoryRetention.MaxDays.ToString(CultureInfo.InvariantCulture);

    public string HistoryMaxBytesText { get; set; } = config.HistoryRetention.MaxBytes.ToString(CultureInfo.InvariantCulture);

    public bool CheckUpdatesOnStartup { get; set; } = config.Updates.CheckOnStartup;

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

        if (InstallUpdatesAutomatically && !DownloadUpdatesAutomatically)
        {
            errors.Add("Automatic install requires automatic download.");
        }

        if (errors.Count > 0 || maxDays is null || maxBytes is null)
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

        config.Refresh.Interval = interval;
        config.Notifications.IsEnabled = NotificationsEnabled;
        config.HistoryRetention.MaxDays = clampedDays;
        config.HistoryRetention.MaxBytes = clampedBytes;
        config.Updates.CheckOnStartup = CheckUpdatesOnStartup;
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
            : updates.LastStatus;
        var latest = string.IsNullOrWhiteSpace(updates.LastLatestVersion)
            ? "n/a"
            : updates.LastLatestVersion;
        var message = string.IsNullOrWhiteSpace(updates.LastMessage)
            ? "No update status has been recorded yet."
            : updates.LastMessage;

        return string.Join(
            Environment.NewLine,
            checkedText,
            $"Status: {status}",
            $"Latest version: {latest}",
            $"Message: {message}");
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
}

public sealed record RefreshSettingsApplyResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
