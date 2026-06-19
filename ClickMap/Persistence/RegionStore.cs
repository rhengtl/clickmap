using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClickMap.Models;

namespace ClickMap.Persistence;

/// <summary>
/// Loads and saves regions as JSON under <c>%APPDATA%\ClickMap\</c>, maintains an O(1)
/// key-&gt;region index for the hook to dispatch against, and raises <see cref="Changed"/>
/// whenever the set changes. Writes are atomic (temp file + replace) to avoid corruption.
/// </summary>
public sealed class RegionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _dir;
    private readonly string _path;
    private readonly List<Region> _regions = new();
    private Dictionary<KeyCombo, Region> _byKey = new();

    public RegionStore(string? directory = null)
    {
        _dir = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClickMap");
        _path = Path.Combine(_dir, "regions.json");
    }

    /// <summary>Raised after any change to the in-memory set (load, add, update, remove).</summary>
    public event EventHandler? Changed;

    /// <summary>Surfaces non-fatal load problems (e.g. a corrupt file that was quarantined).</summary>
    public event EventHandler<string>? LoadWarning;

    public string FilePath => _path;
    public string Directory => _dir;

    public IReadOnlyList<Region> Regions => _regions;

    /// <summary>
    /// Loads regions from disk. A missing file is treated as an empty set; a corrupt file
    /// is quarantined (renamed to <c>.corrupt</c>) so the app still starts cleanly.
    /// </summary>
    public void Load()
    {
        _regions.Clear();

        if (File.Exists(_path))
        {
            try
            {
                string json = File.ReadAllText(_path);
                var loaded = JsonSerializer.Deserialize<List<Region>>(json, JsonOptions);
                if (loaded is not null)
                    _regions.AddRange(loaded.Where(IsUsable));
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                QuarantineCorruptFile(ex);
            }
        }

        RebuildIndex();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Adds a region, persists, and notifies.</summary>
    public void Add(Region region)
    {
        _regions.Add(region);
        SaveAndNotify();
    }

    /// <summary>Removes the region with the given id, persists, and notifies.</summary>
    public bool Remove(Guid id)
    {
        int removed = _regions.RemoveAll(r => r.Id == id);
        if (removed == 0) return false;
        SaveAndNotify();
        return true;
    }

    /// <summary>
    /// Call after mutating a region obtained from <see cref="Regions"/> to persist the
    /// change, rebuild the key index, and notify listeners.
    /// </summary>
    public void Update() => SaveAndNotify();

    /// <summary>O(1) lookup used by the dispatch path on each key-down.</summary>
    public bool TryGetByKey(KeyCombo key, out Region? region) => _byKey.TryGetValue(key, out region);

    /// <summary>Keys assigned to more than one region (surfaced as conflicts in a later phase).</summary>
    public IReadOnlyList<KeyCombo> DuplicateKeys() =>
        _regions.GroupBy(r => r.Key).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

    private void SaveAndNotify()
    {
        RebuildIndex();
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Save()
    {
        System.IO.Directory.CreateDirectory(_dir);
        string json = JsonSerializer.Serialize(_regions, JsonOptions);

        // Atomic write: write to a temp file, then replace the target so a crash mid-write
        // can never leave a half-written regions.json.
        string tmp = _path + ".tmp";
        File.WriteAllText(tmp, json);
        if (File.Exists(_path))
            File.Replace(tmp, _path, null);
        else
            File.Move(tmp, _path);
    }

    private void RebuildIndex()
    {
        // First assignment of a key wins; duplicates are reported via DuplicateKeys().
        var index = new Dictionary<KeyCombo, Region>();
        foreach (var r in _regions)
        {
            if (r.Enabled)
                index.TryAdd(r.Key, r);
        }
        _byKey = index;
    }

    private static bool IsUsable(Region r) => r.Bounds.IsValid;

    private void QuarantineCorruptFile(Exception ex)
    {
        try
        {
            string backup = _path + ".corrupt";
            if (File.Exists(backup)) File.Delete(backup);
            File.Move(_path, backup);
            LoadWarning?.Invoke(this,
                $"regions.json was unreadable ({ex.Message}); moved to {Path.GetFileName(backup)} and started empty.");
        }
        catch
        {
            LoadWarning?.Invoke(this, $"regions.json was unreadable ({ex.Message}); started empty.");
        }
    }
}
