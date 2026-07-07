using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinAiUsageBar.App.Services;
using WinAiUsageBar.Infrastructure.Windows;

namespace WinAiUsageBar.App;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase))
        {
            Environment.ExitCode = SmokeTestRunner.RunAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            return;
        }

        using var singleInstance = new SingleInstanceGuard("Local\\WinAIUsageBar");
        if (!singleInstance.TryAcquire())
        {
            return;
        }

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(parameters =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }
}
