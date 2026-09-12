using System.Text.Json.Serialization;

namespace ClickMap.Models;

/// <summary>
/// A saved click target: a screen point plus the key that clicks it. Mutable so the
/// editor can update it in place.
/// </summary>
public sealed class ClickTarget
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "Target";

    /// <summary>Where the click lands, in physical (virtual-desktop) pixels.</summary>
    public ScreenPoint Target { get; set; }

    /// <summary>The key (with optional modifiers) that fires this target.</summary>
    public KeyCombo Key { get; set; }

    public ClickType ClickType { get; set; } = ClickType.LeftClick;

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Read-only migration shim for the pre-1.1 file format, which stored a rectangle and
    /// clicked its center. Assigning it sets <see cref="Target"/> to that center; it is
    /// never written back.
    /// </summary>
    [JsonPropertyName("Bounds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LegacyBounds? Bounds
    {
        get => null;
        set
        {
            if (value is { } b)
                Target = new ScreenPoint(b.Left + b.Width / 2, b.Top + b.Height / 2);
        }
    }

    public readonly record struct LegacyBounds(int Left, int Top, int Width, int Height);
}
