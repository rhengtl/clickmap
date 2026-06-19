using System.Windows;
using ClickMap.Interop;
using ClickMap.Models;
using ClickMap.Services;

namespace ClickMap;

/// <summary>
/// Phase 1 manual harness: validates the global keyboard hook + SendInput click pipeline.
/// Press F8 anywhere and a left click is dispatched at the chosen target point.
/// </summary>
public partial class MainWindow : Window
{
    private const ushort TestKeyVk = 0x77; // F8

    private readonly HotkeyService _hotkeys = new();
    private readonly ClickService _click = new();
    private System.Windows.Point _target = new(960, 540); // sensible default; override via the button

    public MainWindow()
    {
        InitializeComponent();
        UpdateTargetText();

        _hotkeys.HotkeyPressed += OnHotkeyPressed;
        _hotkeys.HookFailed += (_, ex) => Dispatcher.Invoke(() =>
        {
            StatusText.Text = "FAILED";
            Log(ex.Message);
        });

        Loaded += (_, _) =>
        {
            _hotkeys.Start();
            StatusText.Text = _hotkeys.IsRunning ? "installed (press F8)" : "not running";
            Log($"Hook {(_hotkeys.IsRunning ? "installed" : "failed")}. Test key = F8.");
        };

        Closed += (_, _) => _hotkeys.Dispose();
    }

    private void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        // Runs on the hook thread — keep it cheap: match, then offload the click.
        if (e.Combo.Vk != TestKeyVk)
            return;

        e.Suppress = true; // swallow F8 so it doesn't reach other apps during testing

        var target = _target;
        bool move = false;
        // Read UI-bound option without blocking the hook thread on the dispatcher.
        Dispatcher.Invoke(() => move = MoveCursorCheck.IsChecked == true);
        _click.MoveCursorToTarget = move;

        Task.Run(() =>
        {
            try
            {
                _click.ClickAt(target, ClickType.LeftClick);
                Dispatcher.Invoke(() => Log($"Click @ ({target.X:0},{target.Y:0})  [{e.Combo.Display}]"));
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => Log("Click error: " + ex.Message));
            }
        });
    }

    private async void SetTargetButton_Click(object sender, RoutedEventArgs e)
    {
        SetTargetButton.IsEnabled = false;
        for (int i = 3; i >= 1; i--)
        {
            SetTargetButton.Content = $"Move mouse… capturing in {i}";
            await Task.Delay(1000);
        }

        if (NativeMethods.GetCursorPos(out var p))
        {
            _target = new System.Windows.Point(p.X, p.Y);
            UpdateTargetText();
            Log($"Target set to ({p.X},{p.Y}).");
        }

        SetTargetButton.Content = "Set target to cursor (3s)";
        SetTargetButton.IsEnabled = true;
    }

    private void UpdateTargetText() => TargetText.Text = $"({_target.X:0}, {_target.Y:0})";

    private void Log(string message)
    {
        LogBox.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
        LogBox.ScrollToEnd();
    }
}
