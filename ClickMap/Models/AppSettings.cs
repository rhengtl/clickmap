namespace ClickMap.Models;

/// <summary>
/// Lightweight user/session settings persisted alongside the regions. Grows in Phase 4
/// (startup-with-Windows, click strategy, feedback toggles, panic key).
/// </summary>
public sealed class AppSettings
{
    /// <summary>Last widget position (DIPs). Null until the widget has been placed once.</summary>
    public double? WidgetLeft { get; set; }
    public double? WidgetTop { get; set; }

    /// <summary>Whether click dispatch was paused when the app last closed.</summary>
    public bool Paused { get; set; }
}
