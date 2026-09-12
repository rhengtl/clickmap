using System.Windows;
using System.Windows.Input;
using ClickMap.Models;
using ClickMap.Persistence;

namespace ClickMap.UI;

/// <summary>
/// Modal editor for a single click target: rename, reassign the key, re-pick the point,
/// change the click type, toggle enabled, or delete. Applies changes to the passed
/// <see cref="ClickTarget"/> on OK.
/// </summary>
public partial class TargetEditorWindow : Window
{
    private readonly ClickTarget _target;
    private readonly TargetStore? _store;
    private KeyCombo _key;
    private ScreenPoint _point;
    private bool _capturingKey;

    /// <summary>True when the user chose to delete the target (DialogResult is also true).</summary>
    public bool Deleted { get; private set; }

    public TargetEditorWindow(ClickTarget target, TargetStore? store = null)
    {
        InitializeComponent();
        _target = target;
        _store = store;
        _key = target.Key;
        _point = target.Target;

        NameBox.Text = target.Name;
        ClickTypeBox.ItemsSource = Enum.GetValues<ClickType>();
        ClickTypeBox.SelectedItem = target.ClickType;
        EnabledCheck.IsChecked = target.Enabled;
        KeyButton.Content = _key.Display;
        PointText.Text = _point.ToString();
        UpdateConflictWarning();
    }

    /// <summary>Shows a warning when another target already uses the chosen key.</summary>
    private void UpdateConflictWarning()
    {
        bool conflict = _store is not null
            && _store.Targets.Any(t => t.Id != _target.Id && t.Key == _key);

        ConflictWarning.Text = conflict
            ? $"Another target already uses {_key.Display}. Only one will fire."
            : string.Empty;
        ConflictWarning.Visibility = conflict ? Visibility.Visible : Visibility.Collapsed;
    }

    private void KeyButton_Click(object sender, RoutedEventArgs e)
    {
        _capturingKey = true;
        KeyButton.Content = "press a key…";
        KeyButton.Focus();
    }

    private void PickButton_Click(object sender, RoutedEventArgs e)
    {
        _capturingKey = false;
        KeyButton.Content = _key.Display;

        if (TargetOverlay.Capture(this, askForKey: false) is { } picked)
        {
            _point = picked.Target;
            PointText.Text = _point.ToString();
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (!_capturingKey) return;

        if (e.Key == Key.Escape)
        {
            _capturingKey = false;
            KeyButton.Content = _key.Display;
            e.Handled = true;
            return;
        }

        if (!InputCapture.IsModifierOnly(e))
        {
            _key = InputCapture.FromKeyEvent(e);
            _capturingKey = false;
            KeyButton.Content = _key.Display;
            UpdateConflictWarning();
            e.Handled = true;
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        _target.Name = string.IsNullOrWhiteSpace(NameBox.Text) ? "Target" : NameBox.Text.Trim();
        _target.Key = _key;
        _target.Target = _point;
        _target.ClickType = (ClickType)(ClickTypeBox.SelectedItem ?? ClickType.LeftClick);
        _target.Enabled = EnabledCheck.IsChecked == true;
        DialogResult = true;
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show($"Delete target \"{_target.Name}\"?", "ClickMap",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
            return;

        Deleted = true;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}
