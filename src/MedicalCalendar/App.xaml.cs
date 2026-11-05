using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace MedicalCalendar;

public partial class App : Application
{
    // One instance per data folder: two windows saving the same file would overwrite each other.
    private static Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        var name = @"Local\MedicalCalendar-" + StableHash(AppPaths.DataDir);
        _singleInstance = new Mutex(initiallyOwned: true, name, out bool isFirst);
        if (!isFirst)
        {
            BringExistingWindowToFront();
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnUiException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log.Write($"Fatal: {args.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, args) => { Log.Write($"Task: {args.Exception}"); args.SetObserved(); };

        base.OnStartup(e);
        MainWindow = new MainWindow();
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private static void OnUiException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Keep the app alive: data is already saved continuously, so most errors are recoverable.
        Log.Write($"UI error: {e.Exception}");
        MessageBox.Show(
            $"Something went wrong:\n{e.Exception.Message}\n\nYour data has been saved. Details were written to:\n{AppPaths.LogFile}",
            "Medical Calendar", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }

    private static string StableHash(string s)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (var ch in s.ToUpperInvariant()) h = (h ^ ch) * 16777619;
            return h.ToString("x8");
        }
    }

    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private static void BringExistingWindowToFront()
    {
        var me = Process.GetCurrentProcess();
        foreach (var p in Process.GetProcessesByName(me.ProcessName))
        {
            if (p.Id == me.Id || p.MainWindowHandle == IntPtr.Zero) continue;
            ShowWindow(p.MainWindowHandle, 9 /* SW_RESTORE */);
            SetForegroundWindow(p.MainWindowHandle);
            break;
        }
    }
}
