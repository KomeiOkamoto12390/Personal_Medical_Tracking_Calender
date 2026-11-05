using System.IO;

namespace MedicalCalendar;

/// <summary>
/// Where the app keeps its data. %LOCALAPPDATA% is writable without admin rights
/// and is NOT covered by Defender "Controlled Folder Access" (which blocks
/// unknown apps from writing to Documents/Desktop/Pictures).
/// </summary>
public static class AppPaths
{
    /// <summary>Set MEDCAL_DATA_DIR to use a different folder (handy for testing).
    /// Tests may also reassign it directly.</summary>
    public static string DataDir { get; internal set; } =
        Environment.GetEnvironmentVariable("MEDCAL_DATA_DIR") is { Length: > 0 } custom
            ? custom
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MedicalCalendar");

    public static string DataFile => Path.Combine(DataDir, "data.json");

    /// <summary>One copy of data.json per day, newest 30 kept.</summary>
    public static string BackupDir => Path.Combine(DataDir, "backups");

    public static string LogFile => Path.Combine(DataDir, "error.log");

    /// <summary>v1 format (plain notes per day); migrated on first launch.</summary>
    public static string LegacyNotesFile => Path.Combine(DataDir, "entries.json");

    public static void EnsureCreated() => Directory.CreateDirectory(DataDir);
}

/// <summary>Tiny append-only error log next to the data (never contains health data).</summary>
public static class Log
{
    private const long MaxBytes = 512 * 1024;

    public static void Write(string message)
    {
        try
        {
            AppPaths.EnsureCreated();
            var info = new FileInfo(AppPaths.LogFile);
            if (info.Exists && info.Length > MaxBytes) File.Move(AppPaths.LogFile, AppPaths.LogFile + ".old", overwrite: true);
            File.AppendAllText(AppPaths.LogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch { /* logging must never crash the app */ }
    }
}
