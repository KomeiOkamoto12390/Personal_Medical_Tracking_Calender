using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MedicalCalendar;

public partial class MainWindow : Window
{
    private const int MaxChips = 3;

    private readonly Store _store = new();
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly List<CategorySection> _sections;

    private DateTime _day;                    // day being edited (Today page, or the calendar side panel)
    private DateTime _month;                  // first day of month shown on the calendar page
    private List<DayCell> _cells = new();
    private bool _loadingDay;

    public MainWindow()
    {
        InitializeComponent();

        _store.StatusChanged += s => StatusText.Text = s;
        _store.Load();
        StatusText.Text = "Stored on this PC";
        StatusText.ToolTip = AppPaths.DataFile;
        LogoDay.Text = DateTime.Today.Day.ToString(_culture);

        _sections = Catalog.Categories.Select(def => new CategorySection(
            def,
            _store.Custom(def.Id),
            entriesChanged: OnDayEdited,
            customAdded: name => _store.AddCustom(def.Id, name),
            customRemoved: name => _store.RemoveCustom(def.Id, name))).ToList();

        // The Today page and the calendar's side panel edit the same day model.
        SectionsList.ItemsSource = _sections;
        PanelSections.ItemsSource = _sections;

        ExportPage.Init(_store);
        BuildWeekdayHeader();
        ShowDay(DateTime.Today, animate: false);
        NavDay.IsChecked = true;

        SourceInitialized += (_, _) => StyleTitleBar();
        Closing += (_, _) => _store.Flush();
    }

    // ================= Navigation =================

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (_sections is null) return; // during InitializeComponent
        DayPage.Visibility = sender == NavDay ? Visibility.Visible : Visibility.Collapsed;
        CalendarPage.Visibility = sender == NavCalendar ? Visibility.Visible : Visibility.Collapsed;
        ExportPage.Visibility = sender == NavExport ? Visibility.Visible : Visibility.Collapsed;
        foreach (var s in _sections) s.CancelEdit();

        if (sender == NavDay)
        {
            ShowDay(DateTime.Today, animate: false);
            Fade(DayContent, 0.3);
        }
        else if (sender == NavCalendar)
        {
            ClosePanel(animate: false);
            _month = FirstOfMonth(DateTime.Today);
            BuildMonth();
            Fade(CalendarPage, 0.3);
        }
        else
        {
            _store.Flush();
            ExportPage.Refresh();
            Fade(ExportPage, 0.3);
        }
        Keyboard.Focus(Root);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var focused = Keyboard.FocusedElement as DependencyObject;

        // Esc: let an open suggestion list close itself first; then cancel edits / leave the field / close the panel.
        if (e.Key == Key.Escape)
        {
            if (FindAncestor<SuggestBox>(focused) is { IsOpen: true }) return;
            bool wasEditing = _sections.Any(s => s.IsEditing);
            foreach (var s in _sections) s.CancelEdit();
            bool wasTyping = focused is TextBoxBase or ComboBox;
            Keyboard.Focus(Root);
            if (!wasEditing && !wasTyping && DayPanel.Visibility == Visibility.Visible) ClosePanel();
            e.Handled = true;
            return;
        }

        if (focused is TextBoxBase or ComboBox or ComboBoxItem or DatePicker) return; // typing — leave keys alone
        if (ExportPage.Visibility == Visibility.Visible) return;

        bool onCalendar = CalendarPage.Visibility == Visibility.Visible;
        switch (e.Key)
        {
            case Key.Left when !onCalendar:  ShowDay(_day.AddDays(-1)); break;
            case Key.Right when !onCalendar: ShowDay(_day.AddDays(1));  break;
            case Key.PageUp when onCalendar:   ShiftMonth(-1); break;
            case Key.PageDown when onCalendar: ShiftMonth(1);  break;
            case Key.T:
                if (onCalendar) ShowMonth(FirstOfMonth(DateTime.Today)); else ShowDay(DateTime.Today);
                break;
            default: return;
        }
        e.Handled = true;
    }

    // ================= The edited day (shared by Today page + side panel) =================

    private void ShowDay(DateTime date, bool animate = true)
    {
        _store.Flush();
        _day = date.Date;

        var kicker = $"{_day.ToString("dddd", _culture).ToUpper(_culture)} · {Relative(_day).ToUpper(_culture)}";
        var title = _day.ToString(_culture.DateTimeFormat.LongDatePattern.Contains("dddd")
            ? "MMMM d, yyyy" : _culture.DateTimeFormat.LongDatePattern, _culture);
        DayKicker.Text = PanelKicker.Text = kicker;
        DayTitle.Text = PanelTitle.Text = title;

        _loadingDay = true;
        var log = _store.Get(_day);
        foreach (var s in _sections)
            s.Load(log?.Entries.Where(en => en.Category == s.Def.Id) ?? Enumerable.Empty<LogEntry>());
        NotesBox.Text = PanelNotesBox.Text = log?.Notes ?? "";
        _loadingDay = false;
        UpdateNoteHints();

        if (animate) Fade(DayPanel.Visibility == Visibility.Visible ? PanelContent : DayContent, 0.4);
    }

    private void PrevDay_Click(object sender, RoutedEventArgs e) => ShowDay(_day.AddDays(-1));
    private void NextDay_Click(object sender, RoutedEventArgs e) => ShowDay(_day.AddDays(1));
    private void Today_Click(object sender, RoutedEventArgs e) => ShowDay(DateTime.Today);

    private void OnDayEdited()
    {
        if (_loadingDay) return;
        var log = _store.GetOrCreate(_day);
        log.Entries = _sections.SelectMany(s => s.Entries).ToList();
        _store.DayChanged(_day);
        RefreshCell(_day);
    }

    private void NotesBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loadingDay) { UpdateNoteHints(); return; }
        var text = ((TextBox)sender).Text;

        // Keep the other notes box (Today page / panel) in sync.
        _loadingDay = true;
        var other = sender == NotesBox ? PanelNotesBox : NotesBox;
        if (other.Text != text) other.Text = text;
        _loadingDay = false;
        UpdateNoteHints();

        _store.GetOrCreate(_day).Notes = text;
        _store.DayChanged(_day);
        RefreshCell(_day);
    }

    private void UpdateNoteHints()
    {
        NotesHint.Visibility = NotesBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        PanelNotesHint.Visibility = PanelNotesBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ================= Calendar page =================

    private void BuildWeekdayHeader()
    {
        var names = _culture.DateTimeFormat.AbbreviatedDayNames;
        int first = (int)_culture.DateTimeFormat.FirstDayOfWeek;
        WeekdayHeader.ItemsSource = Enumerable.Range(0, 7)
            .Select(i => names[(first + i) % 7].ToUpper(_culture)).ToList();
    }

    private void BuildMonth()
    {
        int first = (int)_culture.DateTimeFormat.FirstDayOfWeek;
        int offset = ((int)_month.DayOfWeek - first + 7) % 7;
        var start = _month.AddDays(-offset);
        bool panelOpen = DayPanel.Visibility == Visibility.Visible;

        _cells = Enumerable.Range(0, 42).Select(i =>
        {
            var d = start.AddDays(i);
            return new DayCell(d, d.Month == _month.Month, ChipsFor(d)) { IsSelected = panelOpen && d == _day };
        }).ToList();
        DaysGrid.ItemsSource = _cells;

        MonthRun.Text = _month.ToString("MMMM", _culture);
        YearRun.Text = _month.ToString("yyyy", _culture);
    }

    private void RefreshCell(DateTime d)
    {
        var cell = _cells.FirstOrDefault(c => c.Date == d);
        if (cell != null) cell.Preview = ChipsFor(d);
    }

    private IReadOnlyList<Chip> ChipsFor(DateTime d)
    {
        var log = _store.Get(d);
        if (log is null) return Array.Empty<Chip>();

        var chips = new List<Chip>();
        foreach (var e in log.Entries)
        {
            var def = Catalog.Find(e.Category);
            if (def is null) continue;
            chips.Add(new Chip(e.Name, def.Soft, def.Accent, def.Ink));
        }
        var firstNoteLine = log.Notes.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
        if (firstNoteLine != null) chips.Add(new Chip(firstNoteLine, Catalog.NoteSoft, Catalog.NoteAccent, Catalog.NoteInk));

        if (chips.Count <= MaxChips) return chips;
        var shown = chips.Take(MaxChips - 1).ToList();
        shown.Add(new Chip($"+{chips.Count - shown.Count} more", Brushes.Transparent, Brushes.Transparent, Catalog.NoteAccent));
        return shown;
    }

    private void ShowMonth(DateTime firstOfMonth)
    {
        int dir = firstOfMonth.CompareTo(_month);
        _month = firstOfMonth;
        BuildMonth();
        if (dir != 0) AnimateMonth(dir);
    }

    private void ShiftMonth(int months) => ShowMonth(_month.AddMonths(months));
    private void PrevMonth_Click(object sender, RoutedEventArgs e) => ShiftMonth(-1);
    private void NextMonth_Click(object sender, RoutedEventArgs e) => ShiftMonth(1);
    private void TodayMonth_Click(object sender, RoutedEventArgs e) => ShowMonth(FirstOfMonth(DateTime.Today));

    private void Day_Click(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).Tag is not DayCell cell) return;
        bool wasOpen = DayPanel.Visibility == Visibility.Visible;
        if (wasOpen && cell.Date == _day) { ClosePanel(); return; }   // click the open day again to close

        foreach (var s in _sections) s.CancelEdit();
        ShowDay(cell.Date, animate: wasOpen);
        foreach (var c in _cells) c.IsSelected = c.Date == _day;
        if (cell.Date.Month != _month.Month) ShowMonth(FirstOfMonth(cell.Date));
        if (!wasOpen) OpenPanel();
    }

    private void OpenPanel()
    {
        DayPanel.Visibility = Visibility.Visible;
        var dur = new Duration(TimeSpan.FromMilliseconds(220));
        ((TranslateTransform)DayPanel.RenderTransform).BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(60, 0, dur) { EasingFunction = Ease });
        DayPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, dur) { EasingFunction = Ease });
    }

    private void ClosePanel(bool animate = true)
    {
        foreach (var s in _sections) s.CancelEdit();
        foreach (var c in _cells) c.IsSelected = false;
        if (!animate || DayPanel.Visibility != Visibility.Visible)
        {
            DayPanel.Visibility = Visibility.Collapsed;
            return;
        }
        var fade = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(140)));
        fade.Completed += (_, _) =>
        {
            DayPanel.Visibility = Visibility.Collapsed;
            DayPanel.BeginAnimation(OpacityProperty, null);
        };
        DayPanel.BeginAnimation(OpacityProperty, fade);
    }

    private void ClosePanel_Click(object sender, RoutedEventArgs e) => ClosePanel();

    // Touchpads send many small wheel deltas; accumulate to one notch (120) per month
    // and enforce a short cooldown so a single swipe never skips several months.
    private int _wheelAccum;
    private DateTime _lastWheelShift;

    private void DaysGrid_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        _wheelAccum += e.Delta;
        if (Math.Abs(_wheelAccum) < 120) return;
        int dir = _wheelAccum > 0 ? -1 : 1;
        _wheelAccum = 0;
        if (DateTime.UtcNow - _lastWheelShift < TimeSpan.FromMilliseconds(250)) return;
        _lastWheelShift = DateTime.UtcNow;
        ShiftMonth(dir);
    }

    // ================= Helpers =================

    private static DateTime FirstOfMonth(DateTime d) => new(d.Year, d.Month, 1);

    private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d != null && d is not T)
            d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        return d as T;
    }

    private static string Relative(DateTime d)
    {
        int days = (d - DateTime.Today).Days;
        return days switch
        {
            0 => "Today",
            1 => "Tomorrow",
            -1 => "Yesterday",
            > 1 => $"In {days} days",
            _ => $"{-days} days ago",
        };
    }

    private static readonly IEasingFunction Ease = new CubicEase { EasingMode = EasingMode.EaseOut };

    private void AnimateMonth(int direction)
    {
        var dur = new Duration(TimeSpan.FromMilliseconds(220));
        var tt = (TranslateTransform)DaysGrid.RenderTransform;
        tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(direction * 24, 0, dur) { EasingFunction = Ease });
        Fade(DaysGrid, 0.2);
    }

    private static void Fade(UIElement el, double from) =>
        el.BeginAnimation(OpacityProperty,
            new DoubleAnimation(from, 1, new Duration(TimeSpan.FromMilliseconds(180))) { EasingFunction = Ease });

    // ================= Windows 11 title bar =================

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private void StyleTitleBar()
    {
        // Paint the title bar white to blend with the app (Windows 11 22000+; ignored on Windows 10).
        const int DWMWA_CAPTION_COLOR = 35;
        int white = 0x00FFFFFF; // COLORREF 0x00BBGGRR
        _ = DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, DWMWA_CAPTION_COLOR, ref white, sizeof(int));
    }
}
