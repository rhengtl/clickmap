using System.Threading;
using System.Windows;
using System.Windows.Threading;
using ClickMap.Models;
using ClickMap.Persistence;
using ClickMap.Services;
using ClickMap.UI;

namespace ClickMap;

/// <summary>
/// Application entry point. Enforces single-instance, wires the services, widget, and tray
/// together, logs unhandled failures, and keeps running in the tray until the user exits.
/// </summary>
public partial class App : Application
{
    private const string MutexName = "ClickMap.SingleInstance";
    private const string ShowEventName = "ClickMap.ShowWidget";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _showReg;

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

        // Single instance: a second launch signals the first to show, then exits.
        _mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch { /* first instance gone */ }
            Shutdown();
            return;
        }

        // Closing the widget hides it to tray; the app lives until Exit is chosen.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _store = new RegionStore();
        _settingsStore = new SettingsStore();
        _settings = _settingsStore.Load();

        Log.Init(_store.Directory);
        Log.Info("ClickMap starting.");
        WireExceptionLogging();

        _store.LoadWarning += (_, msg) => Log.Warn(msg);
        _store.Load();

        var click = new ClickService { MoveCursorToTarget = _settings.MoveCursorToTarget };
        _hotkeys = new HotkeyService();
        _engine = new ClickEngine(_hotkeys, _store, click)
        {
            IsPaused = _settings.Paused,
            PanicKey = _settings.PanicKey,
        };

        _hotkeys.HookFailed += (_, ex) => Dispatcher.Invoke(() =>
        {
            Log.Error("Keyboard hook failed", ex);
            MessageBox.Show(ex.Message, "ClickMap — keyboard hook failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
        });

        _hotkeys.Start();

        _widget = new WidgetWindow(_store, _engine, click, _settings, _settingsStore);
        _widget.ApplySettings();

        // Keep the Run-key entry in sync with the stored preference (refreshes the exe path).
        StartupRegistration.Set(_settings.StartWithWindows);

        _tray = new TrayIcon(_widget, ExitApp);
        _widget.Show();

        // Listen for a second instance asking us to surface the widget.
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _showReg = ThreadPool.RegisterWaitForSingleObject(_showEvent,
            (_, _) => Dispatcher.Invoke(() => _widget?.ShowFromTray()), null, Timeout.Infinite, false);

        Log.Info($"Started; {_store.Regions.Count} region(s), hook {(_hotkeys.IsRunning ? "up" : "down")}.");
    }

    private void WireExceptionLogging()
    {
        DispatcherUnhandledException += (_, args) =>
            Log.Error("Unhandled UI exception", args.Exception);

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Error("Unhandled exception", args.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };
    }

    private void ExitApp()
    {
        Log.Info("Exit requested.");
        _widget?.PersistSettings();
        _tray?.Dispose();
        _engine?.Dispose();
        _hotkeys?.Dispose();
        _widget?.ForceClose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showReg?.Unregister(null);
        _showEvent?.Dispose();
        _mutex?.Dispose();
        Log.Info("ClickMap exited.");
        base.OnExit(e);
    }
}
