using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ClickMap.Models;
using ClickMap.Persistence;
using ClickMap.Services;
using static ClickMap.Interop.NativeMethods;

namespace ClickMap.UI;

/// <summary>
/// The always-on-top floating widget: lists click targets, toggles pause, and drives the
/// create/edit/delete/flash loop. Closing hides to tray; <see cref="ForceClose"/> really
/// exits.
/// </summary>
public partial class WidgetWindow : Window
{
    private readonly TargetStore _store;
    private readonly ClickEngine _engine;
    private readonly ClickService _click;
    private readonly AppSettings _settings;
    private readonly SettingsStore _settingsStore;
    private bool _forceClose;

    public WidgetWindow(TargetStore store, ClickEngine engine, ClickService click,
        AppSettings settings, SettingsStore settingsStore)
    {
        InitializeComponent();
        _store = store;
        _engine = engine;
        _click = click;
        _settings = settings;
        _settingsStore = settingsStore;

        PauseToggle.IsChecked = _engine.IsPaused;
        _store.Changed += (_, _) => Dispatcher.Invoke(RefreshList);
        _engine.PauseChanged += (_, paused) => Dispatcher.Invoke(() => OnPauseChanged(paused));
        _engine.TargetClicked += (_, target) => Dispatcher.BeginInvoke(() => OnTargetClicked(target));

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
        Guid? selectedId = (TargetListBox.SelectedItem as ClickTarget)?.Id;
        var targets = _store.Targets.ToList();
        TargetListBox.ItemsSource = targets;
        if (selectedId is Guid id)
            TargetListBox.SelectedItem = targets.FirstOrDefault(t => t.Id == id);

        EmptyState.Visibility = targets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CountText.Text = targets.Count == 0
            ? string.Empty
            : $"{targets.Count} target{(targets.Count == 1 ? "" : "s")}";
        UpdateActionButtons();
    }

    private void TargetListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateActionButtons();

    private void UpdateActionButtons()
    {
        bool hasSelection = TargetListBox.SelectedItem is ClickTarget;
        EditButton.IsEnabled = hasSelection;
        FlashButton.IsEnabled = hasSelection;
        DeleteButton.IsEnabled = hasSelection;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (TargetListBox.SelectedItem is not ClickTarget)
            return;

        if (e.Key == Key.Delete)
        {
            Delete_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter || e.Key == Key.F2)
        {
            EditSelected();
            e.Handled = true;
        }
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

    private void PauseToggle_Click(object sender, RoutedEventArgs e) =>
        SetPaused(PauseToggle.IsChecked == true);

    public void SetPaused(bool paused) => _engine.IsPaused = paused;

    public bool IsPaused => _engine.IsPaused;

    private void OnPauseChanged(bool paused)
    {
        // Single place that reflects pause state (toggle button, tray, panic key) to UI/settings.
        PauseToggle.IsChecked = paused;
        _settings.Paused = paused;
        PersistSettings();
    }

    // ---- Click feedback ---------------------------------------------------------------

    private void OnTargetClicked(ClickTarget target)
    {
        if (_settings.VisualFeedback)
            FlashTarget(target.Target, target.Key.Display, 180);
        if (_settings.SoundFeedback)
            System.Media.SystemSounds.Asterisk.Play();
    }

    // ---- Target actions ---------------------------------------------------------------

    public void AddTarget()
    {
        var result = TargetOverlay.Capture(IsVisible ? this : null);
        if (result is not { Key: { } key } draft)
            return;

        var target = new ClickTarget
        {
            Name = $"Target {_store.Targets.Count + 1}",
            Target = draft.Target,
            Key = key,
            ClickType = _settings.DefaultClickType,
        };
        _store.Add(target);
        TargetListBox.SelectedItem = _store.Targets.FirstOrDefault(t => t.Id == target.Id);
        WarnIfConflict(target);
    }

    private void Add_Click(object sender, RoutedEventArgs e) => AddTarget();

    private void Edit_Click(object sender, RoutedEventArgs e) => EditSelected();

    private void TargetListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e) => EditSelected();

    private void EditSelected()
    {
        if (TargetListBox.SelectedItem is not ClickTarget target)
            return;

        var editor = new TargetEditorWindow(target, _store) { Owner = this };
        if (editor.ShowDialog() != true)
            return;

        if (editor.Deleted)
            _store.Remove(target.Id);
        else
        {
            _store.Update();
            WarnIfConflict(target);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (TargetListBox.SelectedItem is not ClickTarget target)
            return;

        if (MessageBox.Show($"Delete target \"{target.Name}\"?", "ClickMap",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
            _store.Remove(target.Id);
    }

    private void Flash_Click(object sender, RoutedEventArgs e)
    {
        if (TargetListBox.SelectedItem is ClickTarget target)
            FlashTarget(target.Target, target.Key.Display, 700);
    }

    private void WarnIfConflict(ClickTarget target)
    {
        if (_store.DuplicateKeys().Contains(target.Key))
            MessageBox.Show(
                $"The key {target.Key.Display} is now assigned to more than one target. " +
                "Only the first will fire — give them distinct keys.",
                "ClickMap — key conflict", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    /// <summary>Size of the flash marker in DIPs; the target point sits at its center.</summary>
    private const double FlashMarkerDip = 44;

    /// <summary>Gap between the marker and its key label, in DIPs.</summary>
    private const double FlashLabelGapDip = 6;

    /// <summary>
    /// Briefly shows a click-through crosshair marker centered on a point, with the
    /// target's key beside it, so the user can see where a target clicks and what fires it.
    /// </summary>
    public static void FlashTarget(ScreenPoint point, string? label, int milliseconds)
    {
        var accent = Color.FromArgb(255, 61, 165, 255);
        var (content, marker, pill) = BuildFlashContent(accent, label);
        var flash = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            Topmost = true,
            ResizeMode = ResizeMode.NoResize,
            IsHitTestVisible = false,
            Focusable = false,
            Content = content,
        };

        // Natural size in DIPs; converted to physical pixels once we know the monitor's DPI.
        content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double dipW = content.DesiredSize.Width;
        double dipH = content.DesiredSize.Height;

        flash.SourceInitialized += (_, _) =>
        {
            IntPtr hwnd = new WindowInteropHelper(flash).Handle;
            // Click-through, no activation, hidden from Alt-Tab.
            int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE,
                ex | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);

            // Move onto the target's monitor (still hidden) so GetDpiForWindow reports that
            // monitor's DPI, then size and show in one go.
            SetWindowPos(hwnd, HWND_TOPMOST, point.X, point.Y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
            double scale = GetDpiForWindow(hwnd) / 96.0;
            if (scale <= 0) scale = 1;

            int markerPx = (int)Math.Round(FlashMarkerDip * scale);
            int width = (int)Math.Ceiling(dipW * scale);
            int height = (int)Math.Ceiling(dipH * scale);

            // Label goes to the right of the marker unless that would run off the desktop.
            int vsRight = GetSystemMetrics(SM_XVIRTUALSCREEN) + GetSystemMetrics(SM_CXVIRTUALSCREEN);
            bool labelOnLeft = pill is not null && point.X - markerPx / 2 + width > vsRight;
            if (labelOnLeft)
            {
                Grid.SetColumn(marker, 2);
                Grid.SetColumn(pill!, 0);
            }

            int left = labelOnLeft ? point.X + markerPx / 2 - width : point.X - markerPx / 2;
            int top = point.Y - height / 2;
            SetWindowPos(hwnd, HWND_TOPMOST, left, top, width, height, SWP_SHOWWINDOW | SWP_NOACTIVATE);
        };

        flash.Show();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            flash.Close();
        };
        timer.Start();
    }

    // [marker] [gap] [key pill]; the pill is omitted when there is no label. Columns are
    // swapped at show time if the pill would run off the right edge of the desktop.
    private static (Grid Content, FrameworkElement Marker, FrameworkElement? Pill) BuildFlashContent(
        Color accent, string? label)
    {
        var stroke = new SolidColorBrush(accent);

        var marker = new Grid { Width = FlashMarkerDip, Height = FlashMarkerDip, VerticalAlignment = VerticalAlignment.Center };
        marker.Children.Add(new Ellipse
        {
            Stroke = stroke,
            StrokeThickness = 3,
            Fill = new SolidColorBrush(Color.FromArgb(60, accent.R, accent.G, accent.B)),
            Margin = new Thickness(2),
        });
        marker.Children.Add(new Rectangle { Fill = stroke, Height = 2, Width = 16, RadiusX = 1, RadiusY = 1 });
        marker.Children.Add(new Rectangle { Fill = stroke, Width = 2, Height = 16, RadiusX = 1, RadiusY = 1 });

        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(label is null ? 0 : FlashLabelGapDip) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(marker, 0);
        content.Children.Add(marker);

        Border? pill = null;
        if (label is not null)
        {
            pill = new Border
            {
                Background = stroke,
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(9, 4, 9, 4),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = label,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 13,
                    FontFamily = (FontFamily)Application.Current.FindResource("Font.Ui"),
                },
            };
            Grid.SetColumn(pill, 2);
            content.Children.Add(pill);
        }

        return (content, marker, pill);
    }

    // ---- Settings ---------------------------------------------------------------------

    private void Settings_Click(object sender, RoutedEventArgs e) => OpenSettings();

    public void OpenSettings()
    {
        var dlg = new SettingsWindow(_settings);
        if (IsVisible) dlg.Owner = this;
        if (dlg.ShowDialog() != true)
            return;

        ApplySettings();
        StartupRegistration.Set(_settings.StartWithWindows);
        PersistSettings();
        Log.Info("Settings updated.");
    }

    /// <summary>Pushes the current settings into the live services. Safe to call repeatedly.</summary>
    public void ApplySettings()
    {
        _click.MoveCursorToTarget = _settings.MoveCursorToTarget;
        _engine.PanicKey = _settings.PanicKey;
    }

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
