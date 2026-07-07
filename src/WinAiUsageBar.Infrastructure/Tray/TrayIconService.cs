using System.Drawing;
using System.Windows.Forms;

namespace WinAiUsageBar.Infrastructure.Tray;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon notifyIcon;
    private readonly ContextMenuStrip contextMenu;
    private bool disposed;

    public TrayIconService()
    {
        contextMenu = new ContextMenuStrip();
        AddMenuItem("Show", (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty));
        AddMenuItem("Show Widget", (_, _) => ShowWidgetRequested?.Invoke(this, EventArgs.Empty));
        AddMenuItem("Refresh Now", (_, _) => RefreshNowRequested?.Invoke(this, EventArgs.Empty));
        AddMenuItem("Settings", (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));
        contextMenu.Items.Add(new ToolStripSeparator());
        AddMenuItem("Exit", (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "WinAI Usage Bar",
            ContextMenuStrip = contextMenu,
            Visible = true
        };

        notifyIcon.MouseUp += OnMouseUp;
    }

    public event EventHandler? ShowRequested;

    public event EventHandler? ShowWidgetRequested;

    public event EventHandler? RefreshNowRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? ExitRequested;

    public void UpdateTooltip(string tooltip)
    {
        if (disposed)
        {
            return;
        }

        notifyIcon.Text = tooltip.Length <= 63
            ? tooltip
            : string.Concat(tooltip.AsSpan(0, 60), "...");
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        notifyIcon.MouseUp -= OnMouseUp;
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        contextMenu.Dispose();
    }

    private void AddMenuItem(string text, EventHandler handler)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += handler;
        contextMenu.Items.Add(item);
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ShowRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
