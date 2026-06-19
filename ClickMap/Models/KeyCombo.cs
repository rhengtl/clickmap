namespace ClickMap.Models;

[Flags]
public enum KeyModifiers
{
    None = 0,
    Control = 1,
    Alt = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>
/// A keyboard trigger: a virtual-key code plus the modifiers that must be held. Acts as
/// the dictionary key the hook uses to find the region to click, so it is an immutable
/// value type with value equality.
/// </summary>
public readonly record struct KeyCombo(ushort Vk, KeyModifiers Modifiers = KeyModifiers.None)
{
    /// <summary>A human-friendly label, e.g. <c>"Ctrl+Shift+F8"</c>.</summary>
    public string Display
    {
        get
        {
            var parts = new List<string>(4);
            if (Modifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
            if (Modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
            if (Modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
            if (Modifiers.HasFlag(KeyModifiers.Win)) parts.Add("Win");
            parts.Add(KeyName(Vk));
            return string.Join("+", parts);
        }
    }

    public override string ToString() => Display;

    private static string KeyName(ushort vk)
    {
        // Common readable names; falls back to the raw VK for anything uncommon.
        return vk switch
        {
            >= 0x41 and <= 0x5A => ((char)vk).ToString(),          // A-Z
            >= 0x30 and <= 0x39 => ((char)vk).ToString(),          // 0-9
            >= 0x70 and <= 0x87 => "F" + (vk - 0x6F),              // F1-F24
            0x20 => "Space",
            0x0D => "Enter",
            0x1B => "Esc",
            0x09 => "Tab",
            _ => "VK_" + vk.ToString("X2"),
        };
    }
}
