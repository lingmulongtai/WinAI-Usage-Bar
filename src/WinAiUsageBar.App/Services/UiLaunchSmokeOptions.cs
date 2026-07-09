namespace WinAiUsageBar.App.Services;

public sealed record UiLaunchSmokeOptions(TimeSpan HoldDuration);

public sealed record UiLaunchSmokeParseResult(
    bool IsMatch,
    bool IsValid,
    UiLaunchSmokeOptions? Options,
    string ErrorMessage)
{
    public static UiLaunchSmokeParseResult NoMatch()
    {
        return new UiLaunchSmokeParseResult(false, true, null, string.Empty);
    }

    public static UiLaunchSmokeParseResult Valid(UiLaunchSmokeOptions options)
    {
        return new UiLaunchSmokeParseResult(true, true, options, string.Empty);
    }

    public static UiLaunchSmokeParseResult Invalid(string errorMessage)
    {
        return new UiLaunchSmokeParseResult(true, false, null, errorMessage);
    }
}

public static class UiLaunchSmokeOptionsParser
{
    private const int DefaultHoldSeconds = 5;
    private const int MinimumHoldSeconds = 1;
    private const int MaximumHoldSeconds = 60;

    public static UiLaunchSmokeParseResult Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0
            || !string.Equals(args[0].Trim(), "--ui-launch-smoke", StringComparison.OrdinalIgnoreCase))
        {
            return UiLaunchSmokeParseResult.NoMatch();
        }

        int? holdSeconds = null;
        for (var index = 1; index < args.Count; index++)
        {
            var option = args[index].Trim();
            if (string.Equals(option, "--hold-seconds", StringComparison.OrdinalIgnoreCase))
            {
                if (holdSeconds is not null)
                {
                    return UiLaunchSmokeParseResult.Invalid("Duplicate --hold-seconds option.");
                }

                if (++index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
                {
                    return UiLaunchSmokeParseResult.Invalid("Missing value for --hold-seconds.");
                }

                if (!int.TryParse(args[index].Trim(), out var parsedHoldSeconds)
                    || parsedHoldSeconds < MinimumHoldSeconds
                    || parsedHoldSeconds > MaximumHoldSeconds)
                {
                    return UiLaunchSmokeParseResult.Invalid("--hold-seconds must be a whole number from 1 to 60.");
                }

                holdSeconds = parsedHoldSeconds;
                continue;
            }

            return UiLaunchSmokeParseResult.Invalid($"Unknown --ui-launch-smoke option: {option}");
        }

        return UiLaunchSmokeParseResult.Valid(
            new UiLaunchSmokeOptions(TimeSpan.FromSeconds(holdSeconds ?? DefaultHoldSeconds)));
    }
}
