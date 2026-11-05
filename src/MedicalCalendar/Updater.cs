using System.IO;
using System.Reflection;
using Velopack;
using Velopack.Sources;

namespace MedicalCalendar;

/// <summary>
/// Checks for, downloads and applies updates via Velopack. Only active when the app was installed
/// with Setup.exe; a plain copy of the exe (or a dev build) never updates itself.
///
/// Updates only replace program files under %LOCALAPPDATA%\KomeiOkamoto.MedicalCalendar.
/// User data lives in a different folder (AppPaths.DataDir) and is never touched.
/// </summary>
public sealed class Updater
{
    /// <summary>Where releases are published (GitHub Releases of this repository).</summary>
    public const string ReleasesUrl = "https://github.com/KomeiOkamoto12390/Personal_Medical_Tracking_Calender";

    private readonly UpdateManager _manager;
    private VelopackAsset? _downloaded;

    public Updater()
    {
        _manager = new UpdateManager(CreateSource());
    }

    /// <summary>
    /// MEDCAL_UPDATE_SOURCE overrides the release location (a local folder or an http(s) URL),
    /// e.g. to test updates from artifacts\releases.
    /// </summary>
    private static IUpdateSource CreateSource()
    {
        var custom = Environment.GetEnvironmentVariable("MEDCAL_UPDATE_SOURCE");
        if (string.IsNullOrWhiteSpace(custom)) return new GithubSource(ReleasesUrl, accessToken: null, prerelease: false);
        return custom.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? new SimpleWebSource(custom)
            : new SimpleFileSource(new DirectoryInfo(custom));
    }

    public bool IsInstalled => _manager.IsInstalled;

    /// <summary>"1.0.0" — the installed version, or the build version when not installed.</summary>
    public string CurrentVersion =>
        _manager.CurrentVersion?.ToString()
        ?? Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "?";

    public bool HasDownloadedUpdate => _downloaded != null;

    /// <summary>Checks for a newer version and downloads it. Returns its version, or null if none / not installed / offline.</summary>
    public async Task<string?> CheckAndDownloadAsync()
    {
        if (!_manager.IsInstalled) return null;
        try
        {
            var info = await _manager.CheckForUpdatesAsync();
            if (info is null) return null;
            await _manager.DownloadUpdatesAsync(info);
            _downloaded = info.TargetFullRelease;
            return _downloaded.Version.ToString();
        }
        catch (Exception ex)
        {
            // Offline, GitHub unreachable, rate-limited… never bother the user; try again next launch.
            Log.Write($"Update check failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>Installs the downloaded update now and restarts the app.</summary>
    public void ApplyAndRestart()
    {
        if (_downloaded != null) _manager.ApplyUpdatesAndRestart(_downloaded);
    }

    /// <summary>If an update was downloaded but not applied, install it quietly after the app closes.</summary>
    public void ApplyOnExit()
    {
        if (_downloaded is null) return;
        try { _manager.WaitExitThenApplyUpdates(_downloaded, silent: true, restart: false); }
        catch (Exception ex) { Log.Write($"Apply-on-exit failed: {ex.Message}"); }
    }
}
