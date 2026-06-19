namespace ClickMap.Models;

/// <summary>
/// User/session settings persisted alongside the regions.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Last widget position (DIPs). Null until the widget has been placed once.</summary>
    public double? WidgetLeft { get; set; }
    public double? WidgetTop { get; set; }

    /// <summary>Whether click dispatch was paused when the app last closed.</summary>
    public bool Paused { get; set; }

    /// <summary>Launch ClickMap when Windows starts (HKCU Run key).</summary>
    public bool StartWithWindows { get; set; }

    /// <summary>Click type applied to newly created regions.</summary>
    public ClickType DefaultClickType { get; set; } = ClickType.LeftClick;

    /// <summary>
    /// Click strategy: true = move the cursor to the target then click (most compatible);
    /// false = send an absolute click without moving the cursor.
    /// </summary>
    public bool MoveCursorToTarget { get; set; } = true;

    /// <summary>Briefly highlight the region when a click fires.</summary>
    public bool VisualFeedback { get; set; } = true;

    /// <summary>Play a short sound when a click fires.</summary>
    public bool SoundFeedback { get; set; }

    /// <summary>
    /// Global key that instantly toggles pause. Defaults to Ctrl+Alt+P; set to null to
    /// disable. (VK 0x50 = 'P'.)
    /// </summary>
    public KeyCombo? PanicKey { get; set; } = new KeyCombo(0x50, KeyModifiers.Control | KeyModifiers.Alt);
}
