namespace ClickMap.Models;

/// <summary>
/// A saved screen region with the key that triggers a click inside it. Mutable so the
/// Phase 3 editor can update it in place.
/// </summary>
public sealed class Region
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "Region";

    /// <summary>Region bounds in physical (virtual-desktop) pixels.</summary>
    public ScreenRect Bounds { get; set; }

    /// <summary>The key (with optional modifiers) that fires this region.</summary>
    public KeyCombo Key { get; set; }

    public ClickType ClickType { get; set; } = ClickType.LeftClick;

    public bool Enabled { get; set; } = true;
}
