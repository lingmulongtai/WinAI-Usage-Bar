using WinAiUsageBar.Core.Configuration;

namespace WinAiUsageBar.Core.Tests.Core;

public sealed class ManualUsageInputValidatorTests
{
    [Fact]
    public void Parse_ReturnsErrorForInvalidPercentText()
    {
        var result = ManualUsageInputValidator.Parse(
            new ManualUsageSettings(),
            new ManualUsageInput("abc", "", "", "", "", "", "", "", ""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Used % must be a number", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_ClampsOutOfRangePercentAndReportsWarning()
    {
        var result = ManualUsageInputValidator.Parse(
            new ManualUsageSettings(),
            new ManualUsageInput("120", "", "", "", "", "", "", "", ""));

        Assert.True(result.IsValid);
        Assert.Equal(100, result.Settings.UsedPercent);
        Assert.Contains(result.Warnings, warning => warning.Contains("clamped", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_ReturnsErrorWhenUsedAndRemainingDoNotAddToOneHundred()
    {
        var result = ManualUsageInputValidator.Parse(
            new ManualUsageSettings(),
            new ManualUsageInput("80", "80", "", "", "", "", "", "", ""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("add up to 100", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_ReturnsErrorsForInvalidResetAndNegativeMoney()
    {
        var result = ManualUsageInputValidator.Parse(
            new ManualUsageSettings(),
            new ManualUsageInput("", "", "tomorrow-ish", "", "-1", "", "-2", "", ""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Reset datetime", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("Credits cannot be negative", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("Month cost cannot be negative", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_ReturnsErrorsForInvalidCurrencyAndTokens()
    {
        var result = ManualUsageInputValidator.Parse(
            new ManualUsageSettings(),
            new ManualUsageInput("", "", "", "", "", "this-unit-name-is-too-long", "", "12.5", ""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Currency/unit", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("Tokens last 31 days", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_ParsesValidManualFields()
    {
        var current = new ManualUsageSettings
        {
            ResetDescription = "daily",
            Currency = "JPY",
            TokensLast31Days = 123
        };

        var result = ManualUsageInputValidator.Parse(
            current,
            new ManualUsageInput(
                "33.333",
                "",
                "2026-07-08T12:00:00Z",
                "  weekly  ",
                "1234.567",
                " JPY ",
                "89.994",
                "12345",
                "  my note  "));

        Assert.True(result.IsValid);
        Assert.Equal(33.33, result.Settings.UsedPercent);
        Assert.Equal(new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero), result.Settings.ResetsAt);
        Assert.Equal(1234.57m, result.Settings.CreditBalance);
        Assert.Equal(89.99m, result.Settings.MonthToDateCost);
        Assert.Equal(12345, result.Settings.TokensLast31Days);
        Assert.Equal("my note", result.Settings.Notes);
        Assert.Equal("weekly", result.Settings.ResetDescription);
        Assert.Equal("JPY", result.Settings.Currency);
    }
}
