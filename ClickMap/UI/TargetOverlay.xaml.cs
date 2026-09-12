using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using ClickMap.Models;
using static ClickMap.Interop.NativeMethods;

namespace ClickMap.UI;

/// <summary>
/// A transparent full-virtual-desktop overlay for picking a click target: a crosshair
/// follows the cursor, one click sets the point, then (optionally) the user presses the
/// key to assign. Captured coordinates come from <c>GetCursorPos</c> (physical pixels), so
/// the stored point is correct regardless of per-monitor DPI; the drawn crosshair and
/// marker are a best-effort visual guide.
/// </summary>
public partial class TargetOverlay : Window
{
    private int _vsLeft, _vsTop, _vsWidth, _vsHeight; // virtual-screen bounds, physical px
    private readonly bool _askForKey;
    private bool _awaitingKey;
    private ScreenPoint _point;

    /// <summary>The captured point and key, or null if the user cancelled.</summary>
    public (ScreenPoint Target, KeyCombo? Key)? Result { get; private set; }

    private TargetOverlay(bool askForKey)
    {
        InitializeComponent();
        _askForKey = askForKey;
        SourceInitialized += OnSourceInitialized;
    }

    /// <summary>
    /// Shows the overlay modally and returns the picked point, plus the key the user
    /// pressed when <paramref name="askForKey"/> is true. Null if cancelled.
    /// </summary>
    public static (ScreenPoint Target, KeyCombo? Key)? Capture(Window? owner = null, bool askForKey = true)
    {
        var overlay = new TargetOverlay(askForKey);
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

        GetCursorPos(out var cur);
        UpdateCrosshair(new ScreenPoint(cur.X, cur.Y));
    }

    private double ScaleX => VisualTreeHelper.GetDpi(this).DpiScaleX;
    private double ScaleY => VisualTreeHelper.GetDpi(this).DpiScaleY;

    // physical screen px -> canvas DIP (relative to the overlay origin)
    private double ToLocalX(int physX) => (physX - _vsLeft) / ScaleX;
    private double ToLocalY(int physY) => (physY - _vsTop) / ScaleY;

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_awaitingKey) return;
        GetCursorPos(out var cur);
        UpdateCrosshair(new ScreenPoint(cur.X, cur.Y));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_awaitingKey) return;

        GetCursorPos(out var cur);
        _point = new ScreenPoint(cur.X, cur.Y);

        if (!_askForKey)
        {
            Result = (_point, null);
            Close();
            return;
        }

        // Freeze the crosshair on the chosen point and show the marker while waiting.
        _awaitingKey = true;
        UpdateCrosshair(_point);
        PlaceMarker(_point);
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
            Result = (_point, InputCapture.FromKeyEvent(e));
            Close();
            e.Handled = true;
        }
    }

    private void UpdateCrosshair(ScreenPoint p)
    {
        double x = ToLocalX(p.X);
        double y = ToLocalY(p.Y);
        double w = _vsWidth / ScaleX;
        double h = _vsHeight / ScaleY;

        CrossH.X1 = 0; CrossH.X2 = w; CrossH.Y1 = y; CrossH.Y2 = y;
        CrossV.Y1 = 0; CrossV.Y2 = h; CrossV.X1 = x; CrossV.X2 = x;

        CoordText.Text = $"{p.X}, {p.Y}";
        // Keep the tip beside the cursor, flipping to the other side near the right/bottom edge.
        CoordTip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double tipW = CoordTip.DesiredSize.Width, tipH = CoordTip.DesiredSize.Height;
        Canvas.SetLeft(CoordTip, x + 16 + tipW > w ? x - 16 - tipW : x + 16);
        Canvas.SetTop(CoordTip, y + 16 + tipH > h ? y - 16 - tipH : y + 16);
    }

    private void PlaceMarker(ScreenPoint p)
    {
        Canvas.SetLeft(Marker, ToLocalX(p.X) - Marker.Width / 2);
        Canvas.SetTop(Marker, ToLocalY(p.Y) - Marker.Height / 2);
        Marker.Visibility = Visibility.Visible;
    }
}
