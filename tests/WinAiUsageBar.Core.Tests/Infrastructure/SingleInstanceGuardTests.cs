using WinAiUsageBar.Infrastructure.Windows;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class SingleInstanceGuardTests
{
    [Fact]
    public void TryAcquire_AllowsOnlyOneGuardForTheSameName()
    {
        var name = UniqueName();
        using var first = new SingleInstanceGuard(name);
        using var second = new SingleInstanceGuard(name);

        Assert.True(first.TryAcquire());
        Assert.False(second.TryAcquire());
    }

    [Fact]
    public void Dispose_ReleasesTheGuard()
    {
        var name = UniqueName();
        using (var first = new SingleInstanceGuard(name))
        {
            Assert.True(first.TryAcquire());
        }

        using var second = new SingleInstanceGuard(name);

        Assert.True(second.TryAcquire());
    }

    private static string UniqueName()
    {
        return $"Local\\WinAIUsageBarTests-{Guid.NewGuid():N}";
    }
}
