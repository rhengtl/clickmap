using ClickMap.Models;
using ClickMap.Persistence;

namespace ClickMap.Services;

/// <summary>
/// Connects the three pieces of the runtime: a global key-down from
/// <see cref="HotkeyService"/> is matched against a <see cref="TargetStore"/> and, on a
/// hit, a click is dispatched at the target's point via <see cref="ClickService"/>.
/// <para>
/// The match runs on the hook thread and is O(1); the click itself is offloaded so the
/// keyboard hook never blocks.
/// </para>
/// </summary>
public sealed class ClickEngine : IDisposable
{
    private readonly HotkeyService _hotkeys;
    private readonly TargetStore _store;
    private readonly ClickService _click;
    private bool _paused;

    public ClickEngine(HotkeyService hotkeys, TargetStore store, ClickService click)
    {
        _hotkeys = hotkeys;
        _store = store;
        _click = click;
        _hotkeys.HotkeyPressed += OnHotkeyPressed;
    }

    /// <summary>When true, matched keys are ignored (and passed through to other apps).</summary>
    public bool IsPaused
    {
        get => _paused;
        set
        {
            if (_paused == value) return;
            _paused = value;
            PauseChanged?.Invoke(this, _paused);
        }
    }

    /// <summary>Global key that instantly toggles <see cref="IsPaused"/>; null disables it.</summary>
    public KeyCombo? PanicKey { get; set; }

    /// <summary>Raised whenever <see cref="IsPaused"/> changes (including via the panic key).</summary>
    public event EventHandler<bool>? PauseChanged;

    /// <summary>Raised after a click is dispatched for a target (for UI feedback/logging).</summary>
    public event EventHandler<ClickTarget>? TargetClicked;

    private void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        // Panic key is checked first so it works even while paused (to resume).
        if (PanicKey is { } panic && e.Combo == panic)
        {
            e.Suppress = true;
            IsPaused = !IsPaused;
            Log.Info($"Panic key {panic.Display} -> {(IsPaused ? "paused" : "resumed")}");
            return;
        }

        if (_paused)
            return;

        if (!_store.TryGetByKey(e.Combo, out var target) || target is null)
            return;

        e.Suppress = true; // the key is "ours" — don't pass it to the focused app

        int x = target.Target.X;
        int y = target.Target.Y;
        var clickType = target.ClickType;

        Task.Run(() =>
        {
            try
            {
                _click.ClickAt(x, y, clickType);
                TargetClicked?.Invoke(this, target);
            }
            catch (Exception ex)
            {
                // A single failed SendInput must not tear down the engine.
                Log.Error($"Click dispatch failed for target '{target.Name}'", ex);
            }
        });
    }

    public void Dispose() => _hotkeys.HotkeyPressed -= OnHotkeyPressed;
}
