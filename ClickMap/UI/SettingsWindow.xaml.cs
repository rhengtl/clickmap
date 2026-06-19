using System.Windows;
using System.Windows.Input;
using ClickMap.Models;
using ClickMap.Services;

namespace ClickMap.UI;

/// <summary>
/// Edits <see cref="AppSettings"/>. Changes are written back to the passed instance only
/// when the user clicks OK; the caller then applies them to the live services.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private KeyCombo? _panicKey;
    private bool _capturingPanic;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        _panicKey = settings.PanicKey;

        // Reflect actual registry state so it can't drift from the stored flag.
        StartupCheck.IsChecked = StartupRegistration.IsEnabled();
        ClickTypeBox.ItemsSource = Enum.GetValues<ClickType>();
        ClickTypeBox.SelectedItem = settings.DefaultClickType;
        MoveCursorCheck.IsChecked = settings.MoveCursorToTarget;
        VisualFeedbackCheck.IsChecked = settings.VisualFeedback;
        SoundFeedbackCheck.IsChecked = settings.SoundFeedback;
        UpdatePanicLabel();
    }

    private void PanicKeyButton_Click(object sender, RoutedEventArgs e)
    {
        _capturingPanic = true;
        PanicKeyButton.Content = "press a key…";
        PanicKeyButton.Focus();
    }

    private void ClearPanic_Click(object sender, RoutedEventArgs e)
    {
        _panicKey = null;
        _capturingPanic = false;
        UpdatePanicLabel();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (!_capturingPanic) return;

        if (e.Key == Key.Escape)
        {
            _capturingPanic = false;
            UpdatePanicLabel();
            e.Handled = true;
            return;
        }

        if (!InputCapture.IsModifierOnly(e))
        {
            _panicKey = InputCapture.FromKeyEvent(e);
            _capturingPanic = false;
            UpdatePanicLabel();
            e.Handled = true;
        }
    }

    private void UpdatePanicLabel() => PanicKeyButton.Content = _panicKey?.Display ?? "(none)";

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        _settings.StartWithWindows = StartupCheck.IsChecked == true;
        _settings.DefaultClickType = (ClickType)(ClickTypeBox.SelectedItem ?? ClickType.LeftClick);
        _settings.MoveCursorToTarget = MoveCursorCheck.IsChecked == true;
        _settings.VisualFeedback = VisualFeedbackCheck.IsChecked == true;
        _settings.SoundFeedback = SoundFeedbackCheck.IsChecked == true;
        _settings.PanicKey = _panicKey;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
