using WinAiUsageBar.Infrastructure.Process;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class CliCommandLaunchPlannerTests
{
    [Fact]
    public void Create_UsesResolvedExecutableDirectly()
    {
        var plan = CliCommandLaunchPlanner.Create("codex", [@"C:\Tools\codex.exe"]);

        var startInfo = plan.CreateStartInfo(["app-server"], redirectStandardInput: true);

        Assert.False(plan.UsesCommandProcessor);
        Assert.Equal(@"C:\Tools\codex.exe", startInfo.FileName);
        Assert.Equal("app-server", Assert.Single(startInfo.ArgumentList));
        Assert.True(startInfo.RedirectStandardInput);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.False(startInfo.UseShellExecute);
    }

    [Fact]
    public void Create_RoutesCommandShimsThroughCommandProcessor()
    {
        var plan = CliCommandLaunchPlanner.Create("codex", [@"C:\Users\me\AppData\Roaming\npm\codex.cmd"]);

        var startInfo = plan.CreateStartInfo(["app-server"]);

        Assert.True(plan.UsesCommandProcessor);
        Assert.EndsWith("cmd.exe", startInfo.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("/d", startInfo.ArgumentList[0]);
        Assert.Equal("/c", startInfo.ArgumentList[1]);
        Assert.Equal(
            "call \"C:\\Users\\me\\AppData\\Roaming\\npm\\codex.cmd\" \"app-server\"",
            startInfo.ArgumentList[2]);
        Assert.False(startInfo.UseShellExecute);
    }

    [Fact]
    public void Create_PrefersLaunchablePathWhenWhereReturnsExtensionlessAliasFirst()
    {
        var plan = CliCommandLaunchPlanner.Create(
            "codex",
            [
                @"C:\Program Files\WindowsApps\OpenAI.Codex\app\resources\codex",
                @"C:\Program Files\WindowsApps\OpenAI.Codex\app\resources\codex.exe"
            ]);

        var startInfo = plan.CreateStartInfo(["app-server"]);

        Assert.False(plan.UsesCommandProcessor);
        Assert.Equal(@"C:\Program Files\WindowsApps\OpenAI.Codex\app\resources\codex.exe", startInfo.FileName);
    }

    [Fact]
    public void Create_FallsBackToCommandNameWhenProbeHasNoPath()
    {
        var plan = CliCommandLaunchPlanner.Create("codex", []);

        var startInfo = plan.CreateStartInfo(["app-server"]);

        Assert.False(plan.UsesCommandProcessor);
        Assert.Equal("codex", startInfo.FileName);
        Assert.Equal("app-server", Assert.Single(startInfo.ArgumentList));
    }
}
