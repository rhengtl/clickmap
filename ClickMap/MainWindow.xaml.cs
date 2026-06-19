using System.Diagnostics;
using System.IO;
using System.Windows;
using ClickMap.Models;
using ClickMap.Persistence;
using ClickMap.Services;

namespace ClickMap;

/// <summary>
/// Phase 2 manual harness: loads regions from <c>%APPDATA%\ClickMap\regions.json</c> and
/// dispatches clicks when their assigned keys are pressed. Validates the
/// HotkeyService → RegionStore → ClickService pipeline end to end.
/// </summary>
public partial class MainWindow : Window
{
    private readonly HotkeyService _hotkeys = new();
    private readonly ClickService _click = new();
    private readonly RegionStore _store = new();
    private readonly ClickEngine _engine;

    public MainWindow()
    {
        InitializeComponent();

        _engine = new ClickEngine(_hotkeys, _store, _click);
        _engine.RegionClicked += (_, region) => Dispatcher.Invoke(() =>
            Log($"Click @ ({region.Bounds.CenterX},{region.Bounds.CenterY})  [{region.Key.Display}]  {region.Name}"));

        _store.Changed += (_, _) => Dispatcher.Invoke(RefreshRegionList);
        _store.LoadWarning += (_, msg) => Dispatcher.Invoke(() => Log("WARN: " + msg));

        _hotkeys.HookFailed += (_, ex) => Dispatcher.Invoke(() =>
        {
            StatusText.Text = "FAILED";
            Log(ex.Message);
        });

        PathText.Text = _store.FilePath;

        Loaded += (_, _) =>
        {
            _store.Load();
            _hotkeys.Start();
            StatusText.Text = _hotkeys.IsRunning ? "installed" : "not running";
            Log($"Hook {(_hotkeys.IsRunning ? "installed" : "failed")}; {_store.Regions.Count} region(s) loaded.");
            WarnDuplicates();
        };

        Closed += (_, _) =>
        {
            _engine.Dispose();
            _hotkeys.Dispose();
        };
    }

    private void RefreshRegionList()
    {
        RegionList.ItemsSource = _store.Regions
            .Select(r => new RegionRow(r.Name, r.Key, r.ClickType,
                $"{r.Bounds.CenterX}, {r.Bounds.CenterY}", r.Enabled))
            .ToList();
    }

    private void WarnDuplicates()
    {
        foreach (var key in _store.DuplicateKeys())
            Log($"WARN: key {key.Display} is assigned to multiple regions (first wins).");
    }

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        _store.Load();
        Log($"Reloaded; {_store.Regions.Count} region(s).");
        WarnDuplicates();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_store.Directory);
        Process.Start(new ProcessStartInfo("explorer.exe", _store.Directory) { UseShellExecute = true });
    }

    private void WriteSample_Click(object sender, RoutedEventArgs e)
    {
        // Two sample regions so the pipeline can be tested without the (Phase 3) UI.
        _store.Add(new Region
        {
            Name = "Center box",
            Bounds = new ScreenRect(860, 440, 200, 200), // center ~ (960, 540)
            Key = new KeyCombo(0x77), // F8
            ClickType = ClickType.LeftClick,
        });
        _store.Add(new Region
        {
            Name = "Top-left box",
            Bounds = new ScreenRect(100, 100, 160, 100), // center (180, 150)
            Key = new KeyCombo(0x78), // F9
            ClickType = ClickType.LeftClick,
        });
        Log($"Wrote sample regions to {_store.FilePath}.");
    }

    private void PauseCheck_Changed(object sender, RoutedEventArgs e)
    {
        _engine.IsPaused = PauseCheck.IsChecked == true;
        Log(_engine.IsPaused ? "Dispatch paused." : "Dispatch resumed.");
    }

    private void Log(string message)
    {
        LogBox.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
        LogBox.ScrollToEnd();
    }

    private sealed record RegionRow(string Name, KeyCombo Key, ClickType ClickType, string CenterLabel, bool Enabled);
}
