using System.Globalization;
using System.IO;

// Tests share static state (AppPaths.DataDir, CurrentCulture), so run them one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace MedicalCalendar.Tests;

/// <summary>Points the app at a fresh temporary data folder for the duration of a test.</summary>
public sealed class TempDataDir : IDisposable
{
    private readonly string _previous;
    public string Path { get; }

    public TempDataDir()
    {
        _previous = AppPaths.DataDir;
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "medcal-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
        AppPaths.DataDir = Path;
    }

    public string File(string relative) => System.IO.Path.Combine(Path, relative);

    public void Dispose()
    {
        AppPaths.DataDir = _previous;
        try { Directory.Delete(Path, recursive: true); } catch { /* best effort */ }
    }
}

/// <summary>Runs a test under a fixed culture so time parsing and formatting are predictable.</summary>
public sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _culture, _ui;

    public CultureScope(string name)
    {
        _culture = CultureInfo.CurrentCulture;
        _ui = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo(name);
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _ui;
    }
}

public static class Make
{
    public static LogEntry Entry(string category, string name, string detail = "", string time = "9:00 AM") =>
        new() { Category = category, Name = name, Detail = detail, Time = time };

    /// <summary>A store with no data file, whose warnings are captured instead of shown.</summary>
    public static Store Store(List<string>? warnings = null)
    {
        var s = new Store();
        s.Warn = m => warnings?.Add(m);
        return s;
    }
}
