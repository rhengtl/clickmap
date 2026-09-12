using System.Text.Json.Serialization;

namespace ClickMap.Models;

/// <summary>
/// User/session settings persisted alongside the click targets.
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

    /// <summary>Click type applied to newly created targets.</summary>
    public ClickType DefaultClickType { get; set; } = ClickType.LeftClick;

    /// <summary>How clicks are delivered; see <see cref="Models.ClickStrategy"/>.</summary>
    public ClickStrategy ClickStrategy { get; set; } = ClickStrategy.RestoreCursor;

    /// <summary>
    /// Migration shim for the pre-1.1 boolean. <c>true</c> maps to <see cref="ClickStrategy.MoveCursor"/>
    /// (what it did); <c>false</c> maps to <see cref="ClickStrategy.RestoreCursor"/> (what it
    /// was meant to do). Never written back.
    /// </summary>
    [JsonPropertyName("MoveCursorToTarget")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? MoveCursorToTarget
    {
        get => null;
        set
        {
            if (value is { } move)
                ClickStrategy = move ? ClickStrategy.MoveCursor : ClickStrategy.RestoreCursor;
        }
    }

    /// <summary>Briefly mark the target point when a click fires.</summary>
    public bool VisualFeedback { get; set; } = true;

    /// <summary>Play a short sound when a click fires.</summary>
    public bool SoundFeedback { get; set; }

    /// <summary>
    /// Global key that instantly toggles pause. Defaults to Ctrl+Alt+P; set to null to
    /// disable. (VK 0x50 = 'P'.)
    /// </summary>
    public KeyCombo? PanicKey { get; set; } = new KeyCombo(0x50, KeyModifiers.Control | KeyModifiers.Alt);
}
