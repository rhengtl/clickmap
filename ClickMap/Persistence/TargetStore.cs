using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClickMap.Models;

namespace ClickMap.Persistence;

/// <summary>
/// Loads and saves click targets as JSON under <c>%APPDATA%\ClickMap\</c>, maintains an
/// O(1) key-&gt;target index for the hook to dispatch against, and raises
/// <see cref="Changed"/> whenever the set changes. Writes are atomic (temp file + replace)
/// to avoid corruption.
/// </summary>
public sealed class TargetStore
{
    private const string FileName = "targets.json";

    /// <summary>Pre-1.1 file name; read once and renamed after a successful migration.</summary>
    private const string LegacyFileName = "regions.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _dir;
    private readonly string _path;
    private readonly List<ClickTarget> _targets = new();
    private Dictionary<KeyCombo, ClickTarget> _byKey = new();

    public TargetStore(string? directory = null)
    {
        _dir = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClickMap");
        _path = Path.Combine(_dir, FileName);
    }

    /// <summary>Raised after any change to the in-memory set (load, add, update, remove).</summary>
    public event EventHandler? Changed;

    /// <summary>Surfaces non-fatal load problems (e.g. a corrupt file that was quarantined).</summary>
    public event EventHandler<string>? LoadWarning;

    public string FilePath => _path;
    public string Directory => _dir;

    public IReadOnlyList<ClickTarget> Targets => _targets;

    /// <summary>
    /// Loads targets from disk. A missing file is treated as an empty set; a corrupt file
    /// is quarantined (renamed to <c>.corrupt</c>) so the app still starts cleanly. If only
    /// the pre-1.1 <c>regions.json</c> exists it is read instead, converted (each rectangle
    /// becomes its center point), saved as <c>targets.json</c>, and set aside as
    /// <c>regions.json.migrated</c>.
    /// </summary>
    public void Load()
    {
        _targets.Clear();

        string legacyPath = Path.Combine(_dir, LegacyFileName);
        bool migrating = !File.Exists(_path) && File.Exists(legacyPath);
        string source = migrating ? legacyPath : _path;

        if (File.Exists(source))
        {
            try
            {
                string json = File.ReadAllText(source);
                var loaded = JsonSerializer.Deserialize<List<ClickTarget>>(json, JsonOptions);
                if (loaded is not null)
                    _targets.AddRange(loaded);
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                QuarantineCorruptFile(source, ex);
                migrating = false;
            }
        }

        RebuildIndex();

        if (migrating)
            FinishMigration(legacyPath);

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Adds a target, persists, and notifies.</summary>
    public void Add(ClickTarget target)
    {
        _targets.Add(target);
        SaveAndNotify();
    }

    /// <summary>Removes the target with the given id, persists, and notifies.</summary>
    public bool Remove(Guid id)
    {
        int removed = _targets.RemoveAll(t => t.Id == id);
        if (removed == 0) return false;
        SaveAndNotify();
        return true;
    }

    /// <summary>Removes every target whose id is listed, with a single save and notification.</summary>
    public int RemoveMany(IEnumerable<Guid> ids)
    {
        var set = ids.ToHashSet();
        int removed = _targets.RemoveAll(t => set.Contains(t.Id));
        if (removed > 0) SaveAndNotify();
        return removed;
    }

    /// <summary>
    /// Call after mutating a target obtained from <see cref="Targets"/> to persist the
    /// change, rebuild the key index, and notify listeners.
    /// </summary>
    public void Update() => SaveAndNotify();

    /// <summary>O(1) lookup used by the dispatch path on each key-down.</summary>
    public bool TryGetByKey(KeyCombo key, out ClickTarget? target) => _byKey.TryGetValue(key, out target);

    /// <summary>Keys assigned to more than one target (surfaced as conflicts in the UI).</summary>
    public IReadOnlyList<KeyCombo> DuplicateKeys() =>
        _targets.GroupBy(t => t.Key).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

    private void SaveAndNotify()
    {
        RebuildIndex();
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Save()
    {
        System.IO.Directory.CreateDirectory(_dir);
        string json = JsonSerializer.Serialize(_targets, JsonOptions);

        // Atomic write: write to a temp file, then replace the target so a crash mid-write
        // can never leave a half-written targets.json.
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
        var index = new Dictionary<KeyCombo, ClickTarget>();
        foreach (var t in _targets)
        {
            if (t.Enabled)
                index.TryAdd(t.Key, t);
        }
        _byKey = index;
    }

    private void FinishMigration(string legacyPath)
    {
        try
        {
            Save();
            string migrated = legacyPath + ".migrated";
            if (File.Exists(migrated)) File.Delete(migrated);
            File.Move(legacyPath, migrated);
            LoadWarning?.Invoke(this,
                $"Migrated {_targets.Count} region(s) from {LegacyFileName} to {FileName}; " +
                $"the old file was kept as {Path.GetFileName(migrated)}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not fatal: the in-memory set is fine and the next Save() will retry.
            LoadWarning?.Invoke(this, $"Loaded {LegacyFileName} but could not finish migrating it: {ex.Message}");
        }
    }

    private void QuarantineCorruptFile(string path, Exception ex)
    {
        string name = Path.GetFileName(path);
        try
        {
            string backup = path + ".corrupt";
            if (File.Exists(backup)) File.Delete(backup);
            File.Move(path, backup);
            LoadWarning?.Invoke(this,
                $"{name} was unreadable ({ex.Message}); moved to {Path.GetFileName(backup)} and started empty.");
        }
        catch
        {
            LoadWarning?.Invoke(this, $"{name} was unreadable ({ex.Message}); started empty.");
        }
    }
}
