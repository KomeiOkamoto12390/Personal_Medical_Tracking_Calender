using Velopack;

namespace MedicalCalendar;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Must run first: handles Velopack's install / update / uninstall hooks and exits early when
        // the installer calls the app for one of them. Does nothing when the app isn't installed.
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
