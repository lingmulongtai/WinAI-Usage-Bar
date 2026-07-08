using WinAiUsageBar.Core.Models;
using WinAiUsageBar.Infrastructure.Notifications;

namespace WinAiUsageBar.Core.Tests.Infrastructure;

public sealed class NotificationServiceTests
{
    [Fact]
    public void LocalNotificationPayloadFactory_CreatesAuthFailurePayload()
    {
        var payload = LocalNotificationPayloadFactory.Create(Snapshot(
            ProviderHealth.AuthRequired,
            remainingPercent: null,
            errorMessage: "Sign in again."));

        Assert.NotNull(payload);
        Assert.Equal("auth-required", payload.Reason);
        Assert.Contains("authentication required", payload.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Sign in again.", payload.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalNotificationPayloadFactory_RedactsAuthFailurePayload()
    {
        var secret = "gh" + "p_" + new string('a', 8);
        var payload = LocalNotificationPayloadFactory.Create(Snapshot(
            ProviderHealth.AuthRequired,
            remainingPercent: null,
            errorMessage: "authorization: bearer " + secret));

        Assert.NotNull(payload);
        Assert.Contains("[REDACTED]", payload.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, payload.Body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(19.9, "quota-low")]
    [InlineData(9.9, "quota-critical")]
    public void LocalNotificationPayloadFactory_CreatesLowQuotaPayload(double remainingPercent, string reason)
    {
        var payload = LocalNotificationPayloadFactory.Create(Snapshot(
            ProviderHealth.Warning,
            remainingPercent,
            errorMessage: null));

        Assert.NotNull(payload);
        Assert.Equal(reason, payload.Reason);
        Assert.Contains($"{remainingPercent:0.#}% remaining", payload.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalNotificationPayloadFactory_RedactsLowQuotaResetDescription()
    {
        var secret = "gh" + "p_" + new string('b', 8);
        var payload = LocalNotificationPayloadFactory.Create(Snapshot(
            ProviderHealth.Warning,
            remainingPercent: 12,
            errorMessage: null,
            resetDescription: "reset token=" + secret));

        Assert.NotNull(payload);
        Assert.Equal("quota-low", payload.Reason);
        Assert.Contains("[REDACTED]", payload.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, payload.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalNotificationPayloadFactory_IgnoresHealthySnapshots()
    {
        var payload = LocalNotificationPayloadFactory.Create(Snapshot(
            ProviderHealth.Ok,
            remainingPercent: 55,
            errorMessage: null));

        Assert.Null(payload);
    }

    [Fact]
    public async Task WindowsAppNotificationService_RegistersAndShowsPayload()
    {
        var transport = new FakeNotificationTransport();
        var service = new WindowsAppNotificationService(transport);

        await service.NotifyAsync(Snapshot(ProviderHealth.Warning, 12, null), CancellationToken.None);
        await service.DisposeAsync();

        Assert.Equal(1, transport.RegisterCount);
        Assert.Single(transport.Payloads);
        Assert.Equal(1, transport.UnregisterCount);
        Assert.True(transport.Disposed);
    }

    [Fact]
    public async Task WindowsAppNotificationService_FallsBackWhenTransportThrows()
    {
        var transport = new FakeNotificationTransport { ThrowOnRegister = true };
        var service = new WindowsAppNotificationService(transport);

        await service.NotifyAsync(Snapshot(ProviderHealth.AuthRequired, null, "auth"), CancellationToken.None);
        await service.NotifyAsync(Snapshot(ProviderHealth.AuthRequired, null, "auth"), CancellationToken.None);

        Assert.Equal(1, transport.RegisterCount);
        Assert.Empty(transport.Payloads);
    }

    private static UsageSnapshot Snapshot(
        ProviderHealth health,
        double? remainingPercent,
        string? errorMessage,
        string? resetDescription = null)
    {
        var window = remainingPercent is null
            ? null
            : new UsageWindow(
                "Test",
                100 - remainingPercent.Value,
                remainingPercent,
                ResetsAt: null,
                resetDescription ?? "reset later",
                "%",
                Used: null,
                Limit: null);

        return new UsageSnapshot(
            ProviderId.Codex,
            "Codex",
            health,
            Identity: null,
            window,
            SecondaryWindow: null,
            Credits: null,
            DataSourceKind.Manual,
            DateTimeOffset.Now,
            StatusMessage: null,
            errorMessage);
    }

    private sealed class FakeNotificationTransport : ILocalNotificationTransport
    {
        public int RegisterCount { get; private set; }

        public int UnregisterCount { get; private set; }

        public bool Disposed { get; private set; }

        public bool ThrowOnRegister { get; set; }

        public List<LocalNotificationPayload> Payloads { get; } = [];

        public void Register()
        {
            RegisterCount++;
            if (ThrowOnRegister)
            {
                throw new InvalidOperationException("notifications unsupported");
            }
        }

        public void Show(LocalNotificationPayload payload)
        {
            Payloads.Add(payload);
        }

        public void Unregister()
        {
            UnregisterCount++;
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
