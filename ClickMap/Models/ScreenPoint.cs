namespace ClickMap.Models;

/// <summary>
/// A point on screen in physical (virtual-desktop) pixels. Stored instead of WPF's
/// <c>Point</c> so persistence stays simple and free of UI coupling.
/// </summary>
public readonly record struct ScreenPoint(int X, int Y)
{
    public override string ToString() => $"{X}, {Y}";
}
