using System.IO;

namespace MedicalCalendar;

/// <summary>
/// Where the app keeps its data. %LOCALAPPDATA% is writable without admin rights
/// and is NOT covered by Defender "Controlled Folder Access" (which blocks
/// unknown apps from writing to Documents/Desktop/Pictures).
/// </summary>
public static class AppPaths
{
    public static string DataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MedicalCalendar");

    public static string DataFile => Path.Combine(DataDir, "entries.json");

    public static void EnsureCreated() => Directory.CreateDirectory(DataDir);
}
