namespace WinAiUsageBar.Infrastructure.Windows;

public sealed record WindowPlacement(
    double Left,
    double Top,
    double Width,
    double Height,
    bool TopMost);
