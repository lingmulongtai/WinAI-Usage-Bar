using System.ComponentModel;
using WinAiUsageBar.Infrastructure.Process;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class CliEnvironmentServiceTests
{
    [Fact]
    public async Task GetReportAsync_ReportsMissingCommand()
    {
        var service = new CliEnvironmentService(
            pathResolver: (_, _) => Task.FromResult<IReadOnlyList<string>>([]),
            startupRunner: (_, _) => Task.FromResult(new CliCommandStartupResult(true, 0, "should not run")));

        var report = await service.GetReportAsync(
            [new CliCommandCheck("codex", "--version")],
            CancellationToken.None);

        var status = Assert.Single(report.Commands);
        Assert.Equal("codex", status.CommandName);
        Assert.False(status.IsFound);
        Assert.Null(status.CanStart);
        Assert.Equal("Not found on PATH.", status.StatusMessage);
    }

    [Fact]
    public async Task GetReportAsync_ReportsSuccessfulStartup()
    {
        var service = new CliEnvironmentService(
            pathResolver: (_, _) => Task.FromResult<IReadOnlyList<string>>([@"C:\Tools\git.exe"]),
            startupRunner: (_, _) => Task.FromResult(new CliCommandStartupResult(true, 0, "git version 2.50.0\nhttps://example.test/release")));

        var report = await service.GetReportAsync(
            [new CliCommandCheck("git", "--version")],
            CancellationToken.None);

        var status = Assert.Single(report.Commands);
        Assert.True(status.IsFound);
        Assert.True(status.CanStart);
        Assert.Equal(0, status.ExitCode);
        Assert.Equal(@"C:\Tools\git.exe", Assert.Single(status.Paths));
        Assert.Equal(@"C:\Tools\git.exe", status.LaunchTarget);
        Assert.False(status.UsesCommandProcessor);
        Assert.Equal("git version 2.50.0", status.StatusMessage);
    }

    [Fact]
    public async Task GetReportAsync_ReportsCommandShimLaunchMetadata()
    {
        var service = new CliEnvironmentService(
            pathResolver: (_, _) => Task.FromResult<IReadOnlyList<string>>([@"C:\Users\me\AppData\Roaming\npm\codex.cmd"]),
            startupRunner: (_, _) => Task.FromResult(new CliCommandStartupResult(true, 0, "codex 1.2.3")));

        var report = await service.GetReportAsync(
            [new CliCommandCheck("codex", "--version")],
            CancellationToken.None);

        var status = Assert.Single(report.Commands);
        Assert.True(status.IsFound);
        Assert.True(status.CanStart);
        Assert.Equal(@"C:\Users\me\AppData\Roaming\npm\codex.cmd", status.LaunchTarget);
        Assert.True(status.UsesCommandProcessor);
    }

    [Fact]
    public async Task GetReportAsync_UsesConfiguredOverrideWithoutPathResolution()
    {
        var pathResolverCalled = false;
        CliCommandCheck? checkedCommand = null;
        var service = new CliEnvironmentService(
            pathResolver: (_, _) =>
            {
                pathResolverCalled = true;
                return Task.FromResult<IReadOnlyList<string>>([@"C:\WindowsApps\codex.exe"]);
            },
            startupRunner: (command, _) =>
            {
                checkedCommand = command;
                return Task.FromResult(new CliCommandStartupResult(true, 0, "codex 1.2.3"));
            });

        var report = await service.GetReportAsync(
            [new CliCommandCheck("codex", "--version", @" C:\Tools\codex.cmd ")],
            CancellationToken.None);

        var status = Assert.Single(report.Commands);
        Assert.False(pathResolverCalled);
        Assert.NotNull(checkedCommand);
        Assert.Equal(@" C:\Tools\codex.cmd ", checkedCommand.CommandOverride);
        Assert.True(status.IsFound);
        Assert.True(status.CanStart);
        Assert.Equal(@"C:\Tools\codex.cmd", Assert.Single(status.Paths));
        Assert.Equal(@"C:\Tools\codex.cmd", status.LaunchTarget);
        Assert.True(status.UsesCommandProcessor);
        Assert.True(status.UsesConfiguredOverride);
    }

    [Fact]
    public async Task GetReportAsync_RedactsConfiguredOverrideInStatus()
    {
        var service = new CliEnvironmentService(
            pathResolver: (_, _) => Task.FromResult<IReadOnlyList<string>>([]),
            startupRunner: (_, _) => Task.FromResult(new CliCommandStartupResult(true, 0, "codex 1.2.3")),
            startupTimeout: TimeSpan.FromSeconds(1));

        var report = await service.GetReportAsync(
            [new CliCommandCheck("codex", "--version", @"C:\Tools\token=sample-secret-value\codex.exe")],
            CancellationToken.None);

        var status = Assert.Single(report.Commands);
        Assert.True(status.UsesConfiguredOverride);
        Assert.Contains("[REDACTED]", Assert.Single(status.Paths), StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", status.LaunchTarget, StringComparison.Ordinal);
        Assert.DoesNotContain("sample-secret-value", status.Paths[0], StringComparison.Ordinal);
        Assert.DoesNotContain("sample-secret-value", status.LaunchTarget, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetReportAsync_ReportsWindowsAppsAccessDeniedStartup()
    {
        var paths = new[]
        {
            @"C:\Program Files\WindowsApps\OpenAI.Codex_26.623.19656.0_x64__2p2nqsd0c76g0\app\resources\codex",
            @"C:\Program Files\WindowsApps\OpenAI.Codex_26.623.19656.0_x64__2p2nqsd0c76g0\app\resources\codex.exe"
        };
        var service = new CliEnvironmentService(
            pathResolver: (_, _) => Task.FromResult<IReadOnlyList<string>>(paths),
            startupRunner: (_, _) => throw new Win32Exception(5, "Access is denied."),
            startupTimeout: TimeSpan.FromSeconds(1));

        var report = await service.GetReportAsync(
            [new CliCommandCheck("codex", "--version")],
            CancellationToken.None);

        var status = Assert.Single(report.Commands);
        Assert.True(status.IsFound);
        Assert.False(status.CanStart);
        Assert.False(status.TimedOut);
        Assert.Null(status.ExitCode);
        Assert.Equal(paths, status.Paths);
        Assert.Equal(paths[1], status.LaunchTarget);
        Assert.Contains("Access is denied", status.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetReportAsync_ReportsFailedStartupWithRedaction()
    {
        var service = new CliEnvironmentService(
            pathResolver: (_, _) => Task.FromResult<IReadOnlyList<string>>([@"C:\Tools\codex.exe"]),
            startupRunner: (_, _) => Task.FromResult(new CliCommandStartupResult(false, 1, "token=sample-secret-value")),
            startupTimeout: TimeSpan.FromSeconds(1));

        var report = await service.GetReportAsync(
            [new CliCommandCheck("codex", "--version")],
            CancellationToken.None);

        var status = Assert.Single(report.Commands);
        Assert.True(status.IsFound);
        Assert.False(status.CanStart);
        Assert.Equal(1, status.ExitCode);
        Assert.Contains("[REDACTED]", status.StatusMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("sample-secret-value", status.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetReportAsync_ReportsStartupTimeout()
    {
        var service = new CliEnvironmentService(
            pathResolver: (_, _) => Task.FromResult<IReadOnlyList<string>>([@"C:\Tools\claude.exe"]),
            startupRunner: async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new CliCommandStartupResult(true, 0, "should not finish");
            },
            startupTimeout: TimeSpan.FromMilliseconds(10));

        var report = await service.GetReportAsync(
            [new CliCommandCheck("claude", "--version")],
            CancellationToken.None);

        var status = Assert.Single(report.Commands);
        Assert.True(status.IsFound);
        Assert.False(status.CanStart);
        Assert.True(status.TimedOut);
        Assert.Equal("Startup check timed out.", status.StatusMessage);
    }
}
