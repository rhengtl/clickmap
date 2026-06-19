using System.IO;
using System.Text.Json;
using ClickMap.Models;

namespace ClickMap.Persistence;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> to <c>%APPDATA%\ClickMap\settings.json</c>
/// using the same atomic-write / fail-soft approach as <see cref="RegionStore"/>.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _dir;
    private readonly string _path;

    public SettingsStore(string? directory = null)
    {
        _dir = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClickMap");
        _path = Path.Combine(_dir, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), JsonOptions);
                if (s is not null) return s;
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // Corrupt settings should never block startup; fall back to defaults.
        }

        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(_dir);
        string json = JsonSerializer.Serialize(settings, JsonOptions);

        string tmp = _path + ".tmp";
        File.WriteAllText(tmp, json);
        if (File.Exists(_path))
            File.Replace(tmp, _path, null);
        else
            File.Move(tmp, _path);
    }
}
