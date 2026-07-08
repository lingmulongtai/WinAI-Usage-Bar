using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Infrastructure.Security;

namespace WinAiUsageBar.Infrastructure.Notifications;

public interface IAppNotificationService : IAsyncDisposable
{
    Task NotifyAsync(UsageSnapshot snapshot, CancellationToken cancellationToken);
}

public interface ILocalNotificationTransport : IDisposable
{
    void Register();

    void Show(LocalNotificationPayload payload);

    void Unregister();
}

public sealed record LocalNotificationPayload(
    string Title,
    string Body,
    string ProviderId,
    string Reason);

public sealed class NoOpAppNotificationService : IAppNotificationService
{
    public Task NotifyAsync(UsageSnapshot snapshot, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

public sealed class WindowsAppNotificationService(
    ILocalNotificationTransport transport) : IAppNotificationService
{
    private bool isRegistered;
    private bool isUnsupported;

    public Task NotifyAsync(UsageSnapshot snapshot, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var payload = LocalNotificationPayloadFactory.Create(snapshot);
        if (payload is null || isUnsupported)
        {
            return Task.CompletedTask;
        }

        try
        {
            EnsureRegistered();
            if (!isUnsupported)
            {
                transport.Show(payload);
            }
        }
        catch
        {
            isUnsupported = true;
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            if (isRegistered)
            {
                transport.Unregister();
            }
        }
        catch
        {
        }

        transport.Dispose();
        return ValueTask.CompletedTask;
    }

    private void EnsureRegistered()
    {
        if (isRegistered || isUnsupported)
        {
            return;
        }

        try
        {
            transport.Register();
            isRegistered = true;
        }
        catch
        {
            isUnsupported = true;
        }
    }
}

public static class LocalNotificationPayloadFactory
{
    public static LocalNotificationPayload? Create(UsageSnapshot snapshot)
    {
        if (snapshot.Health == ProviderHealth.AuthRequired)
        {
            return new LocalNotificationPayload(
                $"{Safe(snapshot.DisplayName)} authentication required",
                Safe(snapshot.ErrorMessage ?? "Sign in again or switch this provider to Manual mode."),
                snapshot.ProviderId.ToString(),
                "auth-required");
        }

        if (snapshot.PrimaryWindow?.RemainingPercent is double remaining && remaining < 20)
        {
            return new LocalNotificationPayload(
                $"{Safe(snapshot.DisplayName)} usage is low",
                $"{remaining:0.#}% remaining. {Safe(snapshot.PrimaryWindow.ResetDescription ?? "Check the provider for reset details.")}",
                snapshot.ProviderId.ToString(),
                remaining < 10 ? "quota-critical" : "quota-low");
        }

        return null;
    }

    private static string Safe(string? value)
    {
        return DiagnosticRedactor.RedactForDisplay(value).Trim();
    }
}

public sealed class WindowsAppNotificationTransport : ILocalNotificationTransport
{
    public void Register()
    {
        AppNotificationManager.Default.Register();
    }

    public void Show(LocalNotificationPayload payload)
    {
        var notification = new AppNotificationBuilder()
            .AddArgument("provider", payload.ProviderId)
            .AddArgument("reason", payload.Reason)
            .AddText(payload.Title)
            .AddText(payload.Body)
            .BuildNotification();

        AppNotificationManager.Default.Show(notification);
    }

    public void Unregister()
    {
        AppNotificationManager.Default.Unregister();
    }

    public void Dispose()
    {
    }
}
