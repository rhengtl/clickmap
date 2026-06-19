using System.Windows;
using System.Windows.Input;
using ClickMap.Models;

namespace ClickMap.UI;

/// <summary>
/// Modal editor for a single region: rename, reassign the key, change the click type,
/// toggle enabled, or delete. Applies changes to the passed <see cref="Region"/> on OK.
/// </summary>
public partial class RegionEditorWindow : Window
{
    private readonly Region _region;
    private KeyCombo _key;
    private bool _capturingKey;

    /// <summary>True when the user chose to delete the region (DialogResult is also true).</summary>
    public bool Deleted { get; private set; }

    public RegionEditorWindow(Region region)
    {
        InitializeComponent();
        _region = region;
        _key = region.Key;

        NameBox.Text = region.Name;
        ClickTypeBox.ItemsSource = Enum.GetValues<ClickType>();
        ClickTypeBox.SelectedItem = region.ClickType;
        EnabledCheck.IsChecked = region.Enabled;
        KeyButton.Content = _key.Display;
    }

    private void KeyButton_Click(object sender, RoutedEventArgs e)
    {
        _capturingKey = true;
        KeyButton.Content = "press a key…";
        KeyButton.Focus();
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
            e.Handled = true;
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        _region.Name = string.IsNullOrWhiteSpace(NameBox.Text) ? "Region" : NameBox.Text.Trim();
        _region.Key = _key;
        _region.ClickType = (ClickType)(ClickTypeBox.SelectedItem ?? ClickType.LeftClick);
        _region.Enabled = EnabledCheck.IsChecked == true;
        DialogResult = true;
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show($"Delete region \"{_region.Name}\"?", "ClickMap",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
            return;

        Deleted = true;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
