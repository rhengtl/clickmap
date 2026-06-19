using System.Runtime.InteropServices;
using System.Windows;
using ClickMap.Interop;
using ClickMap.Models;
using static ClickMap.Interop.NativeMethods;

namespace ClickMap.Services;

/// <summary>
/// Issues synthetic mouse clicks via <c>SendInput</c> at a point in physical screen
/// (virtual-desktop) pixels.
/// </summary>
public sealed class ClickService
{
    /// <summary>
    /// Default strategy: move the cursor to the target, then press/release. Most
    /// compatible with apps that track real cursor position. A future option can send an
    /// absolute click without leaving the cursor parked at the target.
    /// </summary>
    public bool MoveCursorToTarget { get; set; } = true;

    /// <summary>Performs <paramref name="clickType"/> at the given screen point.</summary>
    public void ClickAt(Point screenPoint, ClickType clickType = ClickType.LeftClick)
        => ClickAt((int)Math.Round(screenPoint.X), (int)Math.Round(screenPoint.Y), clickType);

    public void ClickAt(int x, int y, ClickType clickType = ClickType.LeftClick)
    {
        var (nx, ny) = ToAbsolute(x, y);
        var inputs = new List<INPUT>(6);

        if (MoveCursorToTarget)
            inputs.Add(MouseInput(nx, ny, MouseEventF.Move | MouseEventF.Absolute | MouseEventF.VirtualDesk));

        switch (clickType)
        {
            case ClickType.LeftClick:
                AddButton(inputs, nx, ny, MouseEventF.LeftDown, MouseEventF.LeftUp);
                break;
            case ClickType.RightClick:
                AddButton(inputs, nx, ny, MouseEventF.RightDown, MouseEventF.RightUp);
                break;
            case ClickType.MiddleClick:
                AddButton(inputs, nx, ny, MouseEventF.MiddleDown, MouseEventF.MiddleUp);
                break;
            case ClickType.DoubleClick:
                AddButton(inputs, nx, ny, MouseEventF.LeftDown, MouseEventF.LeftUp);
                AddButton(inputs, nx, ny, MouseEventF.LeftDown, MouseEventF.LeftUp);
                break;
        }

        var array = inputs.ToArray();
        uint sent = SendInput((uint)array.Length, array, Marshal.SizeOf<INPUT>());
        if (sent != array.Length)
            throw new InvalidOperationException(
                $"SendInput sent {sent}/{array.Length} events (Win32 error {Marshal.GetLastWin32Error()}).");
    }

    private static void AddButton(List<INPUT> inputs, int nx, int ny, MouseEventF down, MouseEventF up)
    {
        // Carry ABSOLUTE coords on the button events too so the click lands precisely even
        // if the cursor isn't moved first.
        inputs.Add(MouseInput(nx, ny, down | MouseEventF.Absolute | MouseEventF.VirtualDesk));
        inputs.Add(MouseInput(nx, ny, up | MouseEventF.Absolute | MouseEventF.VirtualDesk));
    }

    private static INPUT MouseInput(int nx, int ny, MouseEventF flags) => new()
    {
        type = INPUT_MOUSE,
        u = new INPUTUNION
        {
            mi = new MOUSEINPUT
            {
                dx = nx,
                dy = ny,
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
}
