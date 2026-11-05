using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace MedicalCalendar;

public partial class MainWindow : Window
{
    private Dictionary<string, string> _notes = new();
    private readonly DispatcherTimer _saveTimer;
    private bool _loading;

    public MainWindow()
    {
        InitializeComponent();
        AppPaths.EnsureCreated();
        Load();

        // Debounced save: writes at most once every 500 ms of idle typing,
        // so the UI never stalls on disk I/O per keystroke.
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); Save(); };

        DataPathText.Text = $"Data: {AppPaths.DataFile}";
        Cal.SelectedDate = DateTime.Today;
        Closing += (_, _) => { if (_saveTimer.IsEnabled) Save(); };
    }

    private static string Key(DateTime d) => d.ToString("yyyy-MM-dd");

    private void Cal_SelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        var date = Cal.SelectedDate ?? DateTime.Today;
        SelectedDateText.Text = date.ToString("dddd, MMMM d, yyyy");
        _loading = true;
        NoteBox.Text = _notes.TryGetValue(Key(date), out var n) ? n : "";
        _loading = false;
        Mouse.Capture(null); // Calendar holds mouse capture; release so the textbox gets the next click.
    }

    private void NoteBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        var key = Key(Cal.SelectedDate ?? DateTime.Today);
        if (string.IsNullOrWhiteSpace(NoteBox.Text)) _notes.Remove(key);
        else _notes[key] = NoteBox.Text;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(AppPaths.DataFile))
                _notes = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    File.ReadAllText(AppPaths.DataFile)) ?? new();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not read saved data:\n{ex.Message}", "Medical Calendar");
        }
    }

    private void Save()
    {
        // Write to a temp file then swap, so a crash mid-write never corrupts data.
        var tmp = AppPaths.DataFile + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_notes, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, AppPaths.DataFile, overwrite: true);
    }
}
