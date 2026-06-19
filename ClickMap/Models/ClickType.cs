namespace ClickMap.Models;

/// <summary>
/// The mouse action performed when a region's key is pressed. v1 defaults to
/// <see cref="LeftClick"/>; the others are wired now so per-region config in a later
/// phase is purely additive.
/// </summary>
public enum ClickType
{
    LeftClick,
    RightClick,
    MiddleClick,
    DoubleClick,
}
