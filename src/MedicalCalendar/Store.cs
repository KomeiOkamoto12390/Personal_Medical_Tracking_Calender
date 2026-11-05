using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;

namespace MedicalCalendar;

public sealed class LogEntry
{
    public string Category { get; set; } = "";
    public string Name { get; set; } = "";
    public string Detail { get; set; } = "";
    public string Time { get; set; } = "";
}

public sealed class DayLog
{
    public List<LogEntry> Entries { get; set; } = new();
    public string Notes { get; set; } = "";

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsEmpty => Entries.Count == 0 && string.IsNullOrWhiteSpace(Notes);
}

public sealed class AppData
{
    public int Version { get; set; } = 2;
    public Dictionary<string, DayLog> Days { get; set; } = new();
    /// <summary>Category id → names the user added themselves.</summary>
    public Dictionary<string, List<string>> CustomItems { get; set; } = new();
    /// <summary>Optional name printed on exported reports.</summary>
    public string PatientName { get; set; } = "";
}

/// <summary>Loads/saves everything to one JSON file with debounced, atomic writes.</summary>
public sealed class Store
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };

    public AppData Data { get; private set; } = new();
    public event Action<string>? StatusChanged;

    /// <summary>Shows a warning to the user. Replaceable so tests don't block on a message box.</summary>
    public Action<string> Warn { get; set; } = message =>
        System.Windows.MessageBox.Show(message, "Medical Calendar",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);

    public Store()
    {
        _timer.Tick += (_, _) => { _timer.Stop(); SaveNow(); };
    }

    public static string Key(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public DayLog? Get(DateTime d) => Data.Days.TryGetValue(Key(d), out var log) ? log : null;

    public DayLog GetOrCreate(DateTime d)
    {
        var k = Key(d);
        if (!Data.Days.TryGetValue(k, out var log)) Data.Days[k] = log = new DayLog();
        return log;
    }

    public IReadOnlyList<string> Custom(string categoryId) =>
        Data.CustomItems.TryGetValue(categoryId, out var list) ? list : Array.Empty<string>();

    public void AddCustom(string categoryId, string name)
    {
        if (!Data.CustomItems.TryGetValue(categoryId, out var list))
            Data.CustomItems[categoryId] = list = new List<string>();
        if (!list.Contains(name, StringComparer.OrdinalIgnoreCase)) list.Insert(0, name);
        MarkDirty();
    }

    public void RemoveCustom(string categoryId, string name)
    {
        if (Data.CustomItems.TryGetValue(categoryId, out var list))
        {
            list.RemoveAll(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
            if (list.Count == 0) Data.CustomItems.Remove(categoryId);
        }
        MarkDirty();
    }

    /// <summary>Call after changing a day; drops it if empty and schedules a save.</summary>
    public void DayChanged(DateTime d)
    {
        var k = Key(d);
        if (Data.Days.TryGetValue(k, out var log) && log.IsEmpty) Data.Days.Remove(k);
        MarkDirty();
    }

    public void MarkDirty()
    {
        StatusChanged?.Invoke("Saving…");
        _timer.Stop();
        _timer.Start();
    }

    public void Flush()
    {
        if (_timer.IsEnabled) { _timer.Stop(); SaveNow(); }
    }

    private const int BackupsToKeep = 30;

    public void Load()
    {
        AppPaths.EnsureCreated();
        try
        {
            if (File.Exists(AppPaths.DataFile))
            {
                Data = ReadFile(AppPaths.DataFile);
                BackupDaily();
            }
            else if (File.Exists(AppPaths.LegacyNotesFile))
            {
                // v1 stored { "yyyy-MM-dd": "note text" } — carry the text over as day notes.
                var old = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(AppPaths.LegacyNotesFile)) ?? new();
                foreach (var (k, v) in old) Data.Days[k] = new DayLog { Notes = v };
                SaveNow();
                File.Move(AppPaths.LegacyNotesFile, AppPaths.LegacyNotesFile + ".v1.bak", overwrite: true);
            }
        }
        catch (Exception ex)
        {
            RecoverFromUnreadableFile(ex);
        }
    }

    private static AppData ReadFile(string path)
    {
        var data = JsonSerializer.Deserialize<AppData>(File.ReadAllText(path), Json) ?? new();
        // Tolerate hand-edited files with nulls.
        data.Days ??= new();
        data.CustomItems ??= new();
        data.PatientName ??= "";
        foreach (var log in data.Days.Values)
        {
            log.Entries ??= new();
            log.Notes ??= "";
        }
        return data;
    }

    /// <summary>
    /// The data file couldn't be read. Never overwrite it: set it aside, then fall back to the
    /// newest readable daily backup (or start empty) and tell the user where everything is.
    /// </summary>
    private void RecoverFromUnreadableFile(Exception error)
    {
        string? keptAt = null;
        try
        {
            if (File.Exists(AppPaths.DataFile))
            {
                keptAt = Path.Combine(AppPaths.DataDir, $"data.unreadable-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                File.Copy(AppPaths.DataFile, keptAt);
            }
        }
        catch { /* best effort */ }

        string restored = "";
        foreach (var backup in BackupFiles())
        {
            try
            {
                Data = ReadFile(backup);
                restored = $"\n\nRestored from the backup {Path.GetFileName(backup)}.";
                break;
            }
            catch { /* try the next older one */ }
        }
        if (restored.Length == 0) Data = new AppData();

        Log.Write($"Data file unreadable: {error}");
        Warn(
            $"Your saved data could not be read ({error.Message})." +
            (keptAt != null ? $"\n\nThe original file was kept unchanged at:\n{keptAt}" : "") +
            (restored.Length > 0 ? restored : "\n\nNo readable backup was found, so the app is starting empty."));
    }

    /// <summary>Keeps one copy of the data file per day (newest 30) in the backups folder.</summary>
    private static void BackupDaily()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.BackupDir);
            var today = Path.Combine(AppPaths.BackupDir, $"data-{DateTime.Today:yyyy-MM-dd}.json");
            if (!File.Exists(today)) File.Copy(AppPaths.DataFile, today);
            foreach (var old in BackupFiles().Skip(BackupsToKeep)) File.Delete(old);
        }
        catch (Exception ex)
        {
            Log.Write($"Backup failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Extra safety copy taken right before an update is applied (kept outside the daily rotation).
    /// Updates never touch the data folder, but a new version might migrate the file format.
    /// </summary>
    public void BackupBeforeUpdate(string fromVersion, string toVersion)
    {
        Flush();
        try
        {
            if (!File.Exists(AppPaths.DataFile)) return;
            Directory.CreateDirectory(AppPaths.BackupDir);
            File.Copy(AppPaths.DataFile,
                Path.Combine(AppPaths.BackupDir, $"before-update-{fromVersion}-to-{toVersion}-{DateTime.Now:yyyyMMdd-HHmmss}.json"));
        }
        catch (Exception ex)
        {
            Log.Write($"Pre-update backup failed: {ex.Message}");
        }
    }

    private static IEnumerable<string> BackupFiles() =>
        Directory.Exists(AppPaths.BackupDir)
            ? Directory.GetFiles(AppPaths.BackupDir, "data-*.json").OrderByDescending(f => f, StringComparer.Ordinal)
            : Enumerable.Empty<string>();

    private void SaveNow()
    {
        try
        {
            // Write to a temp file then swap, so a crash mid-write never corrupts data.
            var tmp = AppPaths.DataFile + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Data, Json));
            File.Move(tmp, AppPaths.DataFile, overwrite: true);
            StatusChanged?.Invoke($"Saved {DateTime.Now:t} · stored on this PC");
        }
        catch (Exception ex)
        {
            Log.Write($"Save failed: {ex}");
            StatusChanged?.Invoke($"Couldn't save: {ex.Message}");
        }
    }
}
