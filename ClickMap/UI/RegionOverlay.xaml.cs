using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using ClickMap.Models;
using static ClickMap.Interop.NativeMethods;

namespace ClickMap.UI;

/// <summary>
/// A transparent full-virtual-desktop overlay for defining a region: the user drags a
/// rectangle, then presses the key to assign. Captured coordinates come from
/// <c>GetCursorPos</c> (physical pixels), so the stored bounds are correct regardless of
/// per-monitor DPI; the drawn rectangle is a best-effort visual guide.
/// </summary>
public partial class RegionOverlay : Window
{
    private int _vsLeft, _vsTop, _vsWidth, _vsHeight; // virtual-screen bounds, physical px
    private bool _dragging;
    private bool _awaitingKey;
    private POINT _startPhys;
    private ScreenRect _bounds;

    /// <summary>The captured region + key, or null if the user cancelled.</summary>
    public (ScreenRect Bounds, KeyCombo Key)? Result { get; private set; }

    public RegionOverlay()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
    }

    /// <summary>Shows the overlay modally and returns the captured region, or null.</summary>
    public static (ScreenRect Bounds, KeyCombo Key)? Capture(Window? owner = null)
    {
        var overlay = new RegionOverlay();
        if (owner is not null) overlay.Owner = owner;
        overlay.ShowDialog();
        return overlay.Result;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _vsLeft = GetSystemMetrics(SM_XVIRTUALSCREEN);
        _vsTop = GetSystemMetrics(SM_YVIRTUALSCREEN);
        _vsWidth = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        _vsHeight = GetSystemMetrics(SM_CYVIRTUALSCREEN);

        // Cover the whole virtual desktop in physical pixels, bypassing WPF DIP layout.
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        SetWindowPos(hwnd, HWND_TOPMOST, _vsLeft, _vsTop, _vsWidth, _vsHeight, SWP_SHOWWINDOW);

        Activate();
        Focus();
    }

    private double ScaleX => VisualTreeHelper.GetDpi(this).DpiScaleX;
    private double ScaleY => VisualTreeHelper.GetDpi(this).DpiScaleY;

    // physical screen px -> canvas DIP (relative to the overlay origin)
    private double ToLocalX(int physX) => (physX - _vsLeft) / ScaleX;
    private double ToLocalY(int physY) => (physY - _vsTop) / ScaleY;

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (_awaitingKey) return;

        GetCursorPos(out _startPhys);
        _dragging = true;
        CaptureMouse();
        Selection.Visibility = Visibility.Visible;
        SizeTip.Visibility = Visibility.Visible;
        UpdateSelection(_startPhys);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging) return;
        GetCursorPos(out var cur);
        UpdateSelection(cur);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_dragging) return;

        _dragging = false;
        ReleaseMouseCapture();
        GetCursorPos(out var end);
        _bounds = RectFrom(_startPhys, end);

        if (!_bounds.IsValid)
        {
            // Treat a click / zero-size drag as "start over".
            Selection.Visibility = Visibility.Collapsed;
            SizeTip.Visibility = Visibility.Collapsed;
            return;
        }

        _awaitingKey = true;
        InstructionText.Text = "Press the key to assign";
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (e.Key == Key.Escape)
        {
            Result = null;
            Close();
            e.Handled = true;
            return;
        }

        if (_awaitingKey && !InputCapture.IsModifierOnly(e))
        {
            Result = (_bounds, InputCapture.FromKeyEvent(e));
            Close();
            e.Handled = true;
        }
    }

    private void UpdateSelection(POINT current)
    {
        var r = RectFrom(_startPhys, current);

        double left = ToLocalX(r.Left);
        double top = ToLocalY(r.Top);
        double w = r.Width / ScaleX;
        double h = r.Height / ScaleY;

        Canvas.SetLeft(Selection, left);
        Canvas.SetTop(Selection, top);
        Selection.Width = w;
        Selection.Height = h;

        SizeText.Text = $"{r.Width} × {r.Height}  @ ({r.Left}, {r.Top})";
        Canvas.SetLeft(SizeTip, left);
        Canvas.SetTop(SizeTip, Math.Max(0, top - 24));
    }

    private static ScreenRect RectFrom(POINT a, POINT b)
    {
        int left = Math.Min(a.X, b.X);
        int top = Math.Min(a.Y, b.Y);
        int width = Math.Abs(a.X - b.X);
        int height = Math.Abs(a.Y - b.Y);
        return new ScreenRect(left, top, width, height);
    }
}
