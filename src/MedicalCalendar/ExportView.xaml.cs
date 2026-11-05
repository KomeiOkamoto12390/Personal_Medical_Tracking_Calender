using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace MedicalCalendar;

/// <summary>Export page: pick a period + categories, then save a PDF (or CSV).</summary>
public partial class ExportView : UserControl
{
    private const string NotesId = "notes";
    private Store? _store;
    private readonly Dictionary<string, CheckBox> _checks = new();
    private bool _applyingPreset;
    private string? _lastFile;

    public ExportView()
    {
        InitializeComponent();
        BuildCategoryChecks();
    }

    public void Init(Store store)
    {
        _store = store;
        PatientBox.Text = store.Data.PatientName;
        DefaultPreset.IsChecked = true;
    }

    /// <summary>Call when the page is shown, so counts reflect the latest log.</summary>
    public void Refresh()
    {
        if (CurrentPresetTag() is { } tag and not "custom") ApplyPreset(tag); // e.g. "last 30 days" moves with today
        UpdateSummary();
    }

    // ---------- Building ----------

    private void BuildCategoryChecks()
    {
        foreach (var def in Catalog.Categories) AddCheck(def.Id, def.Title, def.Accent);
        AddCheck(NotesId, "Notes", Catalog.NoteAccent);
    }

    private void AddCheck(string id, string title, System.Windows.Media.Brush dot)
    {
        var label = new StackPanel { Orientation = Orientation.Horizontal };
        label.Children.Add(new Ellipse { Width = 9, Height = 9, Fill = dot, VerticalAlignment = VerticalAlignment.Center });
        label.Children.Add(new TextBlock { Text = title, Margin = new Thickness(8, 0, 0, 0), FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
        var cb = new CheckBox { Content = label, IsChecked = true, Margin = new Thickness(0, 0, 22, 6), MinWidth = 0 };
        System.Windows.Automation.AutomationProperties.SetName(cb, title);
        cb.Checked += (_, _) => UpdateSummary();
        cb.Unchecked += (_, _) => UpdateSummary();
        _checks[id] = cb;
        CategoryChecks.Children.Add(cb);
    }

    // ---------- Period ----------

    private string? CurrentPresetTag() =>
        LogicalTreeHelper.GetChildren((DependencyObject)DefaultPreset.Parent).OfType<RadioButton>()
            .FirstOrDefault(r => r.IsChecked == true)?.Tag as string;

    private void Preset_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tag } && tag != "custom") ApplyPreset(tag);
        UpdateSummary();
    }

    private void ApplyPreset(string tag)
    {
        var today = DateTime.Today;
        DateTime from = tag switch
        {
            "7d" => today.AddDays(-6),
            "30d" => today.AddDays(-29),
            "3m" => today.AddMonths(-3).AddDays(1),
            "6m" => today.AddMonths(-6).AddDays(1),
            "year" => new DateTime(today.Year, 1, 1),
            "all" => EarliestLoggedDay() ?? today,
            _ => FromPicker.SelectedDate ?? today,
        };
        _applyingPreset = true;
        FromPicker.SelectedDate = from;
        ToPicker.SelectedDate = today;
        _applyingPreset = false;
    }

    private DateTime? EarliestLoggedDay()
    {
        if (_store is null || _store.Data.Days.Count == 0) return null;
        var first = _store.Data.Days.Keys.Min();
        return DateTime.TryParseExact(first, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }

    private void Range_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (!_applyingPreset && CustomPreset != null) CustomPreset.IsChecked = true; // user picked a date by hand
        UpdateSummary();
    }

    // ---------- Categories ----------

    private void SelectAll_Click(object sender, RoutedEventArgs e) { foreach (var c in _checks.Values) c.IsChecked = true; }
    private void SelectNone_Click(object sender, RoutedEventArgs e) { foreach (var c in _checks.Values) c.IsChecked = false; }

    private void PatientBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_store is null || _store.Data.PatientName == PatientBox.Text) return;
        _store.Data.PatientName = PatientBox.Text;
        _store.MarkDirty();
    }

    // ---------- Summary ----------

    private ReportOptions? CurrentOptions(out string? problem)
    {
        problem = null;
        if (FromPicker.SelectedDate is not { } from || ToPicker.SelectedDate is not { } to) { problem = "Choose a start and end date."; return null; }
        if (from > to) { problem = "The start date is after the end date."; return null; }
        var cats = _checks.Where(kv => kv.Key != NotesId && kv.Value.IsChecked == true).Select(kv => kv.Key).ToHashSet();
        bool notes = _checks[NotesId].IsChecked == true;
        if (cats.Count == 0 && !notes) { problem = "Pick at least one thing to include."; return null; }
        return new ReportOptions(from.Date, to.Date, cats, notes, PatientBox.Text.Trim());
    }

    private void UpdateSummary()
    {
        if (_store is null || SummaryText is null) return;
        var o = CurrentOptions(out var problem);
        PdfBtn.IsEnabled = CsvBtn.IsEnabled = o != null;
        if (o is null)
        {
            SummaryText.Text = problem;
            BreakdownText.Text = "";
            return;
        }

        var days = Report.Collect(_store, o);
        int entries = days.Sum(d => d.Entries.Count);
        SummaryText.Text = days.Count == 0
            ? "Nothing logged in this selection"
            : $"{days.Count} {(days.Count == 1 ? "day" : "days")} · {entries} {(entries == 1 ? "entry" : "entries")}";

        var parts = Catalog.Categories.Where(c => o.Categories.Contains(c.Id))
            .Select(c => $"{c.Title} {days.Sum(d => d.Entries.Count(e => e.Category == c.Id))}").ToList();
        if (o.IncludeNotes) parts.Add($"Notes {days.Count(d => d.Notes.Length > 0)}");
        BreakdownText.Text = $"{o.From:yyyy-MM-dd} – {o.To:yyyy-MM-dd}   ·   " + string.Join("  ·  ", parts);
    }

    // ---------- Export ----------

    private void ExportPdf_Click(object sender, RoutedEventArgs e) => Export("pdf", "PDF document (*.pdf)|*.pdf",
        (path, o, days) => PdfReport.Write(path, o, days));

    private void ExportCsv_Click(object sender, RoutedEventArgs e) => Export("csv", "CSV spreadsheet (*.csv)|*.csv",
        (path, _, days) => CsvReport.Write(path, days));

    private void Export(string ext, string filter, Action<string, ReportOptions, IReadOnlyList<ReportDay>> write)
    {
        if (_store is null || CurrentOptions(out _) is not { } o) return;
        _store.Flush();

        var dlg = new SaveFileDialog
        {
            FileName = Report.DefaultFileName(o, ext),
            Filter = filter,
            DefaultExt = ext,
            AddExtension = true,
            OverwritePrompt = true,
            InitialDirectory = DownloadsFolder(),
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;

        try
        {
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            write(dlg.FileName, o, Report.Collect(_store, o));
            _lastFile = dlg.FileName;
            ResultText.Text = $"✓ Saved {System.IO.Path.GetFileName(dlg.FileName)}";
            ResultBar.Visibility = Visibility.Visible;
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show(Window.GetWindow(this),
                "Windows blocked saving to that folder.\n\nIf \"Controlled folder access\" is turned on in Windows Security, " +
                "protected folders like Documents and Desktop are off-limits to new apps. Try saving to Downloads instead.",
                "Couldn't save", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this), $"Couldn't create the file:\n{ex.Message}", "Export failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private static string DownloadsFolder()
    {
        // Test mode (custom data folder): keep exports next to the test data.
        if (Environment.GetEnvironmentVariable("MEDCAL_DATA_DIR") is { Length: > 0 }) return AppPaths.DataDir;
        var p = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        return Directory.Exists(p) ? p : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        if (_lastFile != null && File.Exists(_lastFile))
            Process.Start(new ProcessStartInfo(_lastFile) { UseShellExecute = true });
    }

    private void ShowInFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_lastFile != null && File.Exists(_lastFile))
            Process.Start("explorer.exe", $"/select,\"{_lastFile}\"");
    }
}
