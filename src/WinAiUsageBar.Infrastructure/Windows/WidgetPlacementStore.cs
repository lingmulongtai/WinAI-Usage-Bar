using WinAiUsageBar.Core.Configuration;
using WinAiUsageBar.Infrastructure.Storage;

namespace WinAiUsageBar.Infrastructure.Windows;

public sealed class WidgetPlacementStore(IAppConfigStore configStore)
{
    public async Task<WindowPlacement> LoadAsync(CancellationToken cancellationToken)
    {
        var config = await configStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        return new WindowPlacement(
            config.Widget.Left,
            config.Widget.Top,
            config.Widget.Width,
            config.Widget.Height,
            config.Widget.TopMost);
    }

    public async Task SaveAsync(WindowPlacement placement, CancellationToken cancellationToken)
    {
        var config = await configStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        config.Widget.Left = placement.Left;
        config.Widget.Top = placement.Top;
        config.Widget.Width = Math.Max(280, placement.Width);
        config.Widget.Height = Math.Max(160, placement.Height);
        config.Widget.TopMost = placement.TopMost;
        await configStore.SaveAsync(config, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveBoundsAsync(WindowPlacement placement, CancellationToken cancellationToken)
    {
        var config = await configStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        config.Widget.Left = placement.Left;
        config.Widget.Top = placement.Top;
        config.Widget.Width = Math.Max(280, placement.Width);
        config.Widget.Height = Math.Max(160, placement.Height);
        await configStore.SaveAsync(config, cancellationToken).ConfigureAwait(false);
    }
}
