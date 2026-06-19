using System.Drawing;
using ClickMap.UI;
using WinForms = System.Windows.Forms;

namespace ClickMap.Services;

/// <summary>
/// Owns the system-tray icon and its menu. Left-/double-click toggles the widget;
/// the context menu offers show/hide, pause, add region, and exit.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly WinForms.NotifyIcon _icon;
    private readonly WidgetWindow _widget;
    private readonly Icon _generatedIcon;

    public TrayIcon(WidgetWindow widget, Action onExit)
    {
        _widget = widget;
        _generatedIcon = BuildIcon();

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Show / hide", null, (_, _) => ToggleWidget());

        var pauseItem = new WinForms.ToolStripMenuItem("Pause", null, (_, _) =>
            _widget.SetPaused(!_widget.IsPaused));
        menu.Items.Add(pauseItem);

        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Add region…", null, (_, _) =>
        {
            _widget.ShowFromTray();
            _widget.AddRegion();
        });
        menu.Items.Add("Settings…", null, (_, _) => _widget.OpenSettings());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => onExit());

        // Reflect current pause state each time the menu opens.
        menu.Opening += (_, _) => pauseItem.Checked = _widget.IsPaused;

        _icon = new WinForms.NotifyIcon
        {
            Text = "ClickMap",
            Icon = _generatedIcon,
            Visible = true,
            ContextMenuStrip = menu,
        };
        _icon.DoubleClick += (_, _) => ToggleWidget();
    }

    private void ToggleWidget()
    {
        if (_widget.IsVisible)
            _widget.HideToTray();
        else
            _widget.ShowFromTray();
    }

    // Placeholder icon (a blue dot) until a real asset is added in Phase 5.
    private static Icon BuildIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var fill = new SolidBrush(Color.FromArgb(61, 165, 255));
            g.FillEllipse(fill, 3, 3, 26, 26);
            using var ring = new Pen(Color.White, 2);
            g.DrawEllipse(ring, 3, 3, 26, 26);
        }

        // Clone into a managed Icon so we can free the native HICON immediately.
        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using var tmp = Icon.FromHandle(hIcon);
            return (Icon)tmp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "DestroyIcon", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _generatedIcon.Dispose();
    }
}
