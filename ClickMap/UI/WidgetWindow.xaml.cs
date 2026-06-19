using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using ClickMap.Models;
using ClickMap.Persistence;
using ClickMap.Services;
using static ClickMap.Interop.NativeMethods;

namespace ClickMap.UI;

/// <summary>
/// The always-on-top floating widget: lists regions, toggles pause, and drives the
/// create/edit/delete/flash loop. Closing hides to tray; <see cref="ForceClose"/> really
/// exits.
/// </summary>
public partial class WidgetWindow : Window
{
    private readonly RegionStore _store;
    private readonly ClickEngine _engine;
    private readonly AppSettings _settings;
    private readonly SettingsStore _settingsStore;
    private bool _forceClose;

    public WidgetWindow(RegionStore store, ClickEngine engine, AppSettings settings, SettingsStore settingsStore)
    {
        InitializeComponent();
        _store = store;
        _engine = engine;
        _settings = settings;
        _settingsStore = settingsStore;

        PauseToggle.IsChecked = _engine.IsPaused;
        _store.Changed += (_, _) => Dispatcher.Invoke(RefreshList);

        Loaded += OnLoaded;
        LocationChanged += (_, _) => CapturePosition();

        // Subtle idle transparency; full opacity on hover.
        Opacity = 0.9;
        MouseEnter += (_, _) => Opacity = 1.0;
        MouseLeave += (_, _) => Opacity = 0.9;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (_settings.WidgetLeft is double l && _settings.WidgetTop is double t)
        {
            Left = l;
            Top = t;
        }
        else
        {
            // Default: bottom-right of the primary work area.
            Left = SystemParameters.WorkArea.Right - Width - 20;
            Top = SystemParameters.WorkArea.Bottom - Height - 20;
        }

        RefreshList();
    }

    private void RefreshList()
    {
        Guid? selectedId = (RegionListBox.SelectedItem as Region)?.Id;
        RegionListBox.ItemsSource = _store.Regions.ToList();
        if (selectedId is Guid id)
            RegionListBox.SelectedItem = _store.Regions.FirstOrDefault(r => r.Id == id);
    }

    // ---- Window chrome ----------------------------------------------------------------

    private void HeaderBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void Hide_Click(object sender, RoutedEventArgs e) => HideToTray();

    public void HideToTray()
    {
        CapturePosition();
        PersistSettings();
        Hide();
    }

    public void ShowFromTray()
    {
        Show();
        Activate();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Closing (e.g. Alt+F4) hides to tray instead of exiting the app.
        if (!_forceClose)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }
        base.OnClosing(e);
    }

    /// <summary>Called by the app on real shutdown to actually close the window.</summary>
    public void ForceClose()
    {
        _forceClose = true;
        Close();
    }

    // ---- Pause ------------------------------------------------------------------------

    private void PauseToggle_Click(object sender, RoutedEventArgs e)
    {
        SetPaused(PauseToggle.IsChecked == true);
    }

    public void SetPaused(bool paused)
    {
        _engine.IsPaused = paused;
        _settings.Paused = paused;
        PauseToggle.IsChecked = paused;
        PersistSettings();
    }

    public bool IsPaused => _engine.IsPaused;

    // ---- Region actions ---------------------------------------------------------------

    public void AddRegion()
    {
        var result = RegionOverlay.Capture(this);
        if (result is not { } draft)
            return;

        var region = new Region
        {
            Name = $"Region {_store.Regions.Count + 1}",
            Bounds = draft.Bounds,
            Key = draft.Key,
        };
        _store.Add(region);
        RegionListBox.SelectedItem = _store.Regions.FirstOrDefault(r => r.Id == region.Id);
    }

    private void Add_Click(object sender, RoutedEventArgs e) => AddRegion();

    private void Edit_Click(object sender, RoutedEventArgs e) => EditSelected();

    private void RegionListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e) => EditSelected();

    private void EditSelected()
    {
        if (RegionListBox.SelectedItem is not Region region)
            return;

        var editor = new RegionEditorWindow(region) { Owner = this };
        if (editor.ShowDialog() != true)
            return;

        if (editor.Deleted)
            _store.Remove(region.Id);
        else
            _store.Update();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (RegionListBox.SelectedItem is not Region region)
            return;

        if (MessageBox.Show($"Delete region \"{region.Name}\"?", "ClickMap",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
            _store.Remove(region.Id);
    }

    private void Flash_Click(object sender, RoutedEventArgs e)
    {
        if (RegionListBox.SelectedItem is Region region)
            FlashRegion(region.Bounds);
    }

    /// <summary>Briefly highlights a region on screen so the user can confirm its location.</summary>
    public static void FlashRegion(ScreenRect bounds)
    {
        var flash = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            ShowInTaskbar = false,
            Topmost = true,
            ResizeMode = ResizeMode.NoResize,
            Content = new System.Windows.Controls.Border
            {
                BorderBrush = System.Windows.Media.Brushes.DeepSkyBlue,
                BorderThickness = new Thickness(4),
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(60, 61, 165, 255)),
            },
        };

        flash.SourceInitialized += (_, _) =>
        {
            IntPtr hwnd = new WindowInteropHelper(flash).Handle;
            SetWindowPos(hwnd, HWND_TOPMOST, bounds.Left, bounds.Top, bounds.Width, bounds.Height,
                SWP_SHOWWINDOW | SWP_NOACTIVATE);
        };

        flash.Show();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            flash.Close();
        };
        timer.Start();
    }

    // ---- Settings ---------------------------------------------------------------------

    private void CapturePosition()
    {
        _settings.WidgetLeft = Left;
        _settings.WidgetTop = Top;
    }

    public void PersistSettings()
    {
        CapturePosition();
        _settingsStore.Save(_settings);
    }
}
