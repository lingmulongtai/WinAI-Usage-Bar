using WinAiUsageBar.Core.Configuration;

namespace WinAiUsageBar.Infrastructure.Scheduling;

public static class RefreshIntervalMapper
{
    public static TimeSpan? ToTimeSpan(RefreshIntervalKind interval)
    {
        return interval switch
        {
            RefreshIntervalKind.Manual => null,
            RefreshIntervalKind.OneMinute => TimeSpan.FromMinutes(1),
            RefreshIntervalKind.TwoMinutes => TimeSpan.FromMinutes(2),
            RefreshIntervalKind.FiveMinutes => TimeSpan.FromMinutes(5),
            RefreshIntervalKind.FifteenMinutes => TimeSpan.FromMinutes(15),
            _ => TimeSpan.FromMinutes(5)
        };
    }
}
