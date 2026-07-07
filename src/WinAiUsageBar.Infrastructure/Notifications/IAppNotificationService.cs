using WinAiUsageBar.Core.Models;

namespace WinAiUsageBar.Infrastructure.Notifications;

public interface IAppNotificationService
{
    Task NotifyAsync(UsageSnapshot snapshot, CancellationToken cancellationToken);
}

public sealed class NoOpAppNotificationService : IAppNotificationService
{
    public Task NotifyAsync(UsageSnapshot snapshot, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
