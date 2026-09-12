using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using ClickMap.Interop;
using ClickMap.Models;
using static ClickMap.Interop.NativeMethods;

namespace ClickMap.Services;

/// <summary>
/// Issues synthetic mouse clicks at a point in physical screen (virtual-desktop) pixels,
/// using the configured <see cref="ClickStrategy"/>.
/// </summary>
public sealed class ClickService
{
    public ClickStrategy Strategy { get; set; } = ClickStrategy.RestoreCursor;

    /// <summary>Performs <paramref name="clickType"/> at the given screen point.</summary>
    public void ClickAt(Point screenPoint, ClickType clickType = ClickType.LeftClick)
        => ClickAt((int)Math.Round(screenPoint.X), (int)Math.Round(screenPoint.Y), clickType);

    public void ClickAt(int x, int y, ClickType clickType = ClickType.LeftClick)
    {
        switch (Strategy)
        {
            case ClickStrategy.DirectToWindow:
                PostToWindow(x, y, clickType);
                break;
            case ClickStrategy.MoveCursor:
                SendInputClick(x, y, clickType, restoreCursor: false);
                break;
            default:
                SendInputClick(x, y, clickType, restoreCursor: true);
                break;
        }
    }

    // ---- SendInput strategies ---------------------------------------------------------

    /// <summary>
    /// Moves the cursor to the target and clicks via <c>SendInput</c>. With
    /// <paramref name="restoreCursor"/> the move back to the original position is part of
    /// the same batch, so no other input can interleave and the cursor is only away for
    /// as long as the batch takes to process.
    /// </summary>
    private static void SendInputClick(int x, int y, ClickType clickType, bool restoreCursor)
    {
        GetCursorPos(out var origin);

        var (nx, ny) = ToAbsolute(x, y);
        var inputs = new List<INPUT>(6);

        // Only MOUSEEVENTF_MOVE repositions the cursor; ABSOLUTE alone just changes how
        // dx/dy are interpreted, so a click can never be "aimed" without a move.
        inputs.Add(MouseInput(nx, ny, MouseEventF.Move | MouseEventF.Absolute | MouseEventF.VirtualDesk));

        switch (clickType)
        {
            case ClickType.LeftClick:
                AddButton(inputs, MouseEventF.LeftDown, MouseEventF.LeftUp);
                break;
            case ClickType.RightClick:
                AddButton(inputs, MouseEventF.RightDown, MouseEventF.RightUp);
                break;
            case ClickType.MiddleClick:
                AddButton(inputs, MouseEventF.MiddleDown, MouseEventF.MiddleUp);
                break;
            case ClickType.DoubleClick:
                AddButton(inputs, MouseEventF.LeftDown, MouseEventF.LeftUp);
                AddButton(inputs, MouseEventF.LeftDown, MouseEventF.LeftUp);
                break;
        }

        if (restoreCursor)
        {
            var (ox, oy) = ToAbsolute(origin.X, origin.Y);
            inputs.Add(MouseInput(ox, oy, MouseEventF.Move | MouseEventF.Absolute | MouseEventF.VirtualDesk));
        }

        var array = inputs.ToArray();
        uint sent = SendInput((uint)array.Length, array, Marshal.SizeOf<INPUT>());
        if (sent != array.Length)
            throw new InvalidOperationException(
                $"SendInput sent {sent}/{array.Length} events (Win32 error {Marshal.GetLastWin32Error()}).");

        // The 0..65535 mapping can land a pixel off on large desktops; snap back exactly.
        if (restoreCursor && GetCursorPos(out var now) && (now.X != origin.X || now.Y != origin.Y))
            SetCursorPos(origin.X, origin.Y);
    }

    private static void AddButton(List<INPUT> inputs, MouseEventF down, MouseEventF up)
    {
        inputs.Add(MouseInput(0, 0, down));
        inputs.Add(MouseInput(0, 0, up));
    }

    private static INPUT MouseInput(int dx, int dy, MouseEventF flags) => new()
    {
        type = INPUT_MOUSE,
        u = new INPUTUNION
        {
            mi = new MOUSEINPUT
            {
                dx = dx,
                dy = dy,
                mouseData = 0,
                dwFlags = (uint)flags,
                time = 0,
                dwExtraInfo = IntPtr.Zero,
            },
        },
    };

    /// <summary>
    /// Maps a physical-pixel screen point to the 0..65535 absolute range across the whole
    /// virtual desktop, as required by <c>MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK</c>.
    /// </summary>
    internal static (int nx, int ny) ToAbsolute(int x, int y)
    {
        int left = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int top = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int height = GetSystemMetrics(SM_CYVIRTUALSCREEN);

        // Guard against degenerate metrics (e.g. width of 1) to avoid divide-by-zero.
        int denomX = Math.Max(width - 1, 1);
        int denomY = Math.Max(height - 1, 1);

        int nx = (int)Math.Round((x - left) * 65535.0 / denomX);
        int ny = (int)Math.Round((y - top) * 65535.0 / denomY);
        return (nx, ny);
    }

    // ---- Direct-to-window strategy ----------------------------------------------------

    /// <summary>
    /// Posts the button messages straight to the window under the point, with the point
    /// in the message's client coordinates. Nothing goes through the input pipeline, so
    /// the cursor is untouched. A preceding <c>WM_MOUSEMOVE</c> lets frameworks that
    /// track hover (WPF, Chromium) update their hit state before the button arrives.
    /// </summary>
    private static void PostToWindow(int x, int y, ClickType clickType)
    {
        var screen = new POINT { X = x, Y = y };
        IntPtr hwnd = WindowFromPoint(screen);
        if (hwnd == IntPtr.Zero)
            throw new InvalidOperationException($"No window at ({x}, {y}).");

        var client = screen;
        ScreenToClient(hwnd, ref client);
        IntPtr lParam = MakeLParam(client.X, client.Y);

        Post(hwnd, WM_MOUSEMOVE, IntPtr.Zero, lParam);

        switch (clickType)
        {
            case ClickType.LeftClick:
                Post(hwnd, WM_LBUTTONDOWN, MK_LBUTTON, lParam);
                Post(hwnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
                break;
            case ClickType.RightClick:
                Post(hwnd, WM_RBUTTONDOWN, MK_RBUTTON, lParam);
                Post(hwnd, WM_RBUTTONUP, IntPtr.Zero, lParam);
                break;
            case ClickType.MiddleClick:
                Post(hwnd, WM_MBUTTONDOWN, MK_MBUTTON, lParam);
                Post(hwnd, WM_MBUTTONUP, IntPtr.Zero, lParam);
                break;
            case ClickType.DoubleClick:
                // Windows turns the second down of a double-click into WM_LBUTTONDBLCLK
                // for CS_DBLCLKS windows; post the same sequence an app would see.
                Post(hwnd, WM_LBUTTONDOWN, MK_LBUTTON, lParam);
                Post(hwnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
                Post(hwnd, WM_LBUTTONDBLCLK, MK_LBUTTON, lParam);
                Post(hwnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
                break;
        }
    }

    private static void Post(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (PostMessage(hwnd, msg, wParam, lParam))
            return;

        int err = Marshal.GetLastWin32Error();
        string hint = err == 5 // ERROR_ACCESS_DENIED: UIPI blocks messages to elevated windows
            ? " The target window is running elevated; use another click strategy or run ClickMap as administrator."
            : string.Empty;
        throw new Win32Exception(err, $"PostMessage(0x{msg:X}) to window 0x{hwnd:X} failed.{hint}");
    }

    private static void Post(IntPtr hwnd, uint msg, int wParam, IntPtr lParam) =>
        Post(hwnd, msg, new IntPtr(wParam), lParam);
}
