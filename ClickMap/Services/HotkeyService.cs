using System.Runtime.InteropServices;
using ClickMap.Models;
using static ClickMap.Interop.NativeMethods;

namespace ClickMap.Services;

/// <summary>
/// Raised for every non-modifier key-down seen system-wide. Set <see cref="Suppress"/> to
/// stop the key from reaching other applications.
/// </summary>
public sealed class HotkeyPressedEventArgs : EventArgs
{
    public HotkeyPressedEventArgs(KeyCombo combo) => Combo = combo;

    public KeyCombo Combo { get; }

    /// <summary>When set true by a handler, the key is swallowed (not passed on).</summary>
    public bool Suppress { get; set; }
}

/// <summary>
/// Installs a global low-level keyboard hook (<c>WH_KEYBOARD_LL</c>) on a dedicated thread
/// with its own message pump, and raises <see cref="HotkeyPressed"/> for each key-down.
/// <para>
/// The hook callback runs on the hook thread and must stay cheap. Handlers should only do
/// an O(1) lookup and offload any real work (the actual click) to another thread —
/// blocking here stalls all keyboard input system-wide.
/// </para>
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private readonly LowLevelKeyboardProc _proc;     // kept alive to prevent GC of the callback
    private Thread? _thread;
    private IntPtr _hookId = IntPtr.Zero;
    private uint _threadId;
    private readonly ManualResetEventSlim _ready = new(false);
    private volatile bool _running;

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    /// <summary>Surfaces a fatal hook-thread error (e.g. install failure) to the app.</summary>
    public event EventHandler<Exception>? HookFailed;

    public HotkeyService() => _proc = HookCallback;

    public bool IsRunning => _running;

    public void Start()
    {
        if (_running) return;

        _thread = new Thread(ThreadProc)
        {
            IsBackground = true,
            Name = "ClickMap.HotkeyHook",
        };
        _thread.Start();

        // Wait until the hook is installed (or failed) so callers know the state on return.
        _ready.Wait();
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;

        if (_threadId != 0)
            PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);

        _thread?.Join(TimeSpan.FromSeconds(2));
        _thread = null;
        _threadId = 0;
    }

    private void ThreadProc()
    {
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
        if (_hookId == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            _ready.Set();
            HookFailed?.Invoke(this, new InvalidOperationException(
                $"Failed to install keyboard hook (Win32 error {err})."));
            return;
        }

        _threadId = GetCurrentThreadId();
        _running = true;
        _ready.Set();

        // Pump messages so the low-level hook keeps firing; exits on WM_QUIT from Stop().
        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
            {
                var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                ushort vk = (ushort)data.vkCode;

                if (!IsModifierKey(vk))
                {
                    var combo = new KeyCombo(vk, CurrentModifiers());
                    var args = new HotkeyPressedEventArgs(combo);
                    HotkeyPressed?.Invoke(this, args);
                    if (args.Suppress)
                        return 1; // swallow the key
                }
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static KeyModifiers CurrentModifiers()
    {
        var m = KeyModifiers.None;
        if (IsDown(VK_CONTROL)) m |= KeyModifiers.Control;
        if (IsDown(VK_MENU)) m |= KeyModifiers.Alt;
        if (IsDown(VK_SHIFT)) m |= KeyModifiers.Shift;
        if (IsDown(VK_LWIN) || IsDown(VK_RWIN)) m |= KeyModifiers.Win;
        return m;
    }

    // GetAsyncKeyState: high-order bit set (negative short) means the key is down.
    private static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    private static bool IsModifierKey(ushort vk) => vk switch
    {
        VK_SHIFT or VK_CONTROL or VK_MENU or VK_LWIN or VK_RWIN => true,
        >= 0xA0 and <= 0xA5 => true, // L/R Shift, Ctrl, Alt
        _ => false,
    };

    public void Dispose()
    {
        Stop();
        _ready.Dispose();
    }
}
