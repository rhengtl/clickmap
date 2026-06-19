using System.Windows.Input;
using ClickMap.Models;

namespace ClickMap.UI;

/// <summary>
/// Helpers to turn a WPF key event into the app's <see cref="KeyCombo"/> for assigning a
/// key to a region in the overlay and editor.
/// </summary>
public static class InputCapture
{
    /// <summary>Builds a <see cref="KeyCombo"/> from the pressed key plus current modifiers.</summary>
    public static KeyCombo FromKeyEvent(KeyEventArgs e)
    {
        // Alt-combined keys arrive as Key.System with the real key in SystemKey.
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        ushort vk = (ushort)KeyInterop.VirtualKeyFromKey(key);

        var mods = KeyModifiers.None;
        var m = Keyboard.Modifiers;
        if (m.HasFlag(ModifierKeys.Control)) mods |= KeyModifiers.Control;
        if (m.HasFlag(ModifierKeys.Alt)) mods |= KeyModifiers.Alt;
        if (m.HasFlag(ModifierKeys.Shift)) mods |= KeyModifiers.Shift;
        if (m.HasFlag(ModifierKeys.Windows)) mods |= KeyModifiers.Win;

        return new KeyCombo(vk, mods);
    }

    /// <summary>True when the event is a bare modifier key (wait for a "real" key instead).</summary>
    public static bool IsModifierOnly(KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        return key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin;
    }
}
