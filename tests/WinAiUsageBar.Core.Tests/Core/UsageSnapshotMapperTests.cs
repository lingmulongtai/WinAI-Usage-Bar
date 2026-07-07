using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Core.Providers;

namespace WinAiUsageBar.Core.Tests.Core;

public sealed class UsageSnapshotMapperTests
{
    [Fact]
    public void FromManual_ComputesRemainingPercentAndHealth()
    {
        var descriptor = ProviderDescriptors.Get(ProviderId.Codex);
        var manual = new ManualUsageSettings
        {
            UsedPercent = 85,
            CreditBalance = 12.5m,
            Currency = "USD",
            Notes = "Manual note"
        };

        var snapshot = UsageSnapshotMapper.FromManual(
            descriptor,
            manual,
            new DateTimeOffset(2026, 7, 8, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(15, snapshot.PrimaryWindow?.RemainingPercent);
        Assert.Equal(ProviderHealth.Warning, snapshot.Health);
        Assert.Equal("Manual note", snapshot.StatusMessage);
        Assert.Equal(12.5m, snapshot.Credits?.Balance);
    }

    [Fact]
    public void NormalizePercent_ClampsInvalidValues()
    {
        Assert.Equal(100, UsageSnapshotMapper.NormalizePercent(140));
        Assert.Equal(0, UsageSnapshotMapper.NormalizePercent(-1));
        Assert.Null(UsageSnapshotMapper.NormalizePercent(double.NaN));
    }
}
