using System.Windows;
using ClickMap.Models;
using ClickMap.Persistence;
using ClickMap.Services;
using ClickMap.UI;

namespace ClickMap;

/// <summary>
/// Application entry point. Wires the services, widget, and tray icon together and keeps
/// running in the tray until the user explicitly exits.
/// </summary>
public partial class App : Application
{
    private HotkeyService? _hotkeys;
    private ClickEngine? _engine;
    private RegionStore? _store;
    private SettingsStore? _settingsStore;
    private AppSettings? _settings;
    private WidgetWindow? _widget;
    private TrayIcon? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Closing the widget hides it to tray; the app lives until Exit is chosen.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _store = new RegionStore();
        _settingsStore = new SettingsStore();
        _settings = _settingsStore.Load();
        _store.Load();

        var click = new ClickService();
        _hotkeys = new HotkeyService();
        _engine = new ClickEngine(_hotkeys, _store, click) { IsPaused = _settings.Paused };

        _hotkeys.HookFailed += (_, ex) => Dispatcher.Invoke(() =>
            MessageBox.Show(ex.Message, "ClickMap — keyboard hook failed",
                MessageBoxButton.OK, MessageBoxImage.Error));

        _hotkeys.Start();

        _widget = new WidgetWindow(_store, _engine, _settings, _settingsStore);
        _tray = new TrayIcon(_widget, ExitApp);
        _widget.Show();
    }

    private void ExitApp()
    {
        _widget?.PersistSettings();
        _tray?.Dispose();
        _engine?.Dispose();
        _hotkeys?.Dispose();
        _widget?.ForceClose();
        Shutdown();
    }
}
