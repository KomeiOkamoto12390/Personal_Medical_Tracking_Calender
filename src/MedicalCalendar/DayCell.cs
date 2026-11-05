using System.Windows.Media;

namespace MedicalCalendar;

/// <summary>A small coloured label shown inside a calendar day square.</summary>
public sealed record Chip(string Text, Brush Bg, Brush Bar, Brush Ink);

/// <summary>One square in the month grid.</summary>
public sealed class DayCell : ObservableObject
{
    public DayCell(DateTime date, bool isCurrentMonth, IReadOnlyList<Chip> preview)
    {
        Date = date;
        IsCurrentMonth = isCurrentMonth;
        Preview = preview;
    }

    public DateTime Date { get; }
    public int DayNumber => Date.Day;
    public bool IsCurrentMonth { get; }
    public bool IsToday => Date == DateTime.Today;
    public bool IsWeekend => Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    private IReadOnlyList<Chip> _preview = Array.Empty<Chip>();
    public IReadOnlyList<Chip> Preview { get => _preview; set => Set(ref _preview, value); }

    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }
}
