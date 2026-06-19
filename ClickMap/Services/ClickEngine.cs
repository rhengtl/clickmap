using ClickMap.Models;
using ClickMap.Persistence;

namespace ClickMap.Services;

/// <summary>
/// Connects the three pieces of the runtime: a global key-down from
/// <see cref="HotkeyService"/> is matched against a <see cref="RegionStore"/> and, on a
/// hit, a click is dispatched at the region's center via <see cref="ClickService"/>.
/// <para>
/// The match runs on the hook thread and is O(1); the click itself is offloaded so the
/// keyboard hook never blocks.
/// </para>
/// </summary>
public sealed class ClickEngine : IDisposable
{
    private readonly HotkeyService _hotkeys;
    private readonly RegionStore _store;
    private readonly ClickService _click;

    public ClickEngine(HotkeyService hotkeys, RegionStore store, ClickService click)
    {
        _hotkeys = hotkeys;
        _store = store;
        _click = click;
        _hotkeys.HotkeyPressed += OnHotkeyPressed;
    }

    /// <summary>When true, matched keys are ignored (and passed through to other apps).</summary>
    public bool IsPaused { get; set; }

    /// <summary>Raised after a click is dispatched for a region (for UI feedback/logging).</summary>
    public event EventHandler<Region>? RegionClicked;

    private void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        if (IsPaused)
            return;

        if (!_store.TryGetByKey(e.Combo, out var region) || region is null)
            return;

        e.Suppress = true; // the key is "ours" — don't pass it to the focused app

        int x = region.Bounds.CenterX;
        int y = region.Bounds.CenterY;
        var clickType = region.ClickType;

        Task.Run(() =>
        {
            try
            {
                _click.ClickAt(x, y, clickType);
                RegionClicked?.Invoke(this, region);
            }
            catch
            {
                // A single failed SendInput must not tear down the engine; ignore and
                // wait for the next key. (Surfaced via logging in a later phase.)
            }
        });
    }

    public void Dispose() => _hotkeys.HotkeyPressed -= OnHotkeyPressed;
}
