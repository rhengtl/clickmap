using System.Text.Json.Serialization;

namespace ClickMap.Models;

/// <summary>
/// A screen region in physical (virtual-desktop) pixels. Stored instead of WPF's
/// <c>Rect</c> so persistence stays simple and free of UI coupling.
/// </summary>
public readonly record struct ScreenRect(int Left, int Top, int Width, int Height)
{
    [JsonIgnore]
    public int Right => Left + Width;

    [JsonIgnore]
    public int Bottom => Top + Height;

    /// <summary>X of the region center (default click target), in physical pixels.</summary>
    [JsonIgnore]
    public int CenterX => Left + Width / 2;

    /// <summary>Y of the region center (default click target), in physical pixels.</summary>
    [JsonIgnore]
    public int CenterY => Top + Height / 2;

    [JsonIgnore]
    public bool IsValid => Width > 0 && Height > 0;

    public bool Contains(int x, int y) => x >= Left && x < Right && y >= Top && y < Bottom;
}
