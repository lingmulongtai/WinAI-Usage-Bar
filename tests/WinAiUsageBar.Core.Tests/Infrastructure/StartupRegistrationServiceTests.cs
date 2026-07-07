using WinAiUsageBar.Infrastructure.Windows;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class StartupRegistrationServiceTests
{
    [Fact]
    public async Task SetEnabledAsync_WritesQuotedProcessPath()
    {
        var runKey = new FakeStartupRunKey();
        var service = new RunKeyStartupRegistrationService(
            runKey,
            () => @"C:\Apps\WinAI Usage Bar\WinAiUsageBar.App.exe");

        await service.SetEnabledAsync(isEnabled: true, CancellationToken.None);
        var status = await service.GetStatusAsync(CancellationToken.None);

        Assert.True(status.IsSupported);
        Assert.True(status.IsEnabled);
        Assert.Equal("\"C:\\Apps\\WinAI Usage Bar\\WinAiUsageBar.App.exe\"", runKey.Value);
    }

    [Fact]
    public async Task SetEnabledAsync_RemovesRunKeyValueWhenDisabled()
    {
        var runKey = new FakeStartupRunKey
        {
            Value = "\"existing.exe\""
        };
        var service = new RunKeyStartupRegistrationService(runKey, () => "next.exe");

        await service.SetEnabledAsync(isEnabled: false, CancellationToken.None);
        var status = await service.GetStatusAsync(CancellationToken.None);

        Assert.False(status.IsEnabled);
        Assert.Null(runKey.Value);
    }

    [Fact]
    public async Task SetEnabledAsync_ThrowsWhenProcessPathCannotBeResolved()
    {
        var runKey = new FakeStartupRunKey();
        var service = new RunKeyStartupRegistrationService(runKey, () => null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SetEnabledAsync(isEnabled: true, CancellationToken.None));
        var status = await service.GetStatusAsync(CancellationToken.None);

        Assert.Contains("executable path", exception.Message, StringComparison.Ordinal);
        Assert.False(status.IsSupported);
        Assert.False(status.IsEnabled);
    }

    private sealed class FakeStartupRunKey : IStartupRunKey
    {
        public string? Value { get; set; }

        public string? GetStringValue(string name)
        {
            Assert.Equal(RunKeyStartupRegistrationService.ValueName, name);
            return Value;
        }

        public void SetStringValue(string name, string value)
        {
            Assert.Equal(RunKeyStartupRegistrationService.ValueName, name);
            Value = value;
        }

        public void DeleteValue(string name)
        {
            Assert.Equal(RunKeyStartupRegistrationService.ValueName, name);
            Value = null;
        }
    }
}
