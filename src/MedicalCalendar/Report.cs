using System.Globalization;

namespace MedicalCalendar;

/// <summary>What the user picked on the Export page.</summary>
public sealed record ReportOptions(
    DateTime From,
    DateTime To,
    IReadOnlySet<string> Categories,   // category ids to include
    bool IncludeNotes,
    string PatientName);

public sealed record ReportDay(DateTime Date, IReadOnlyList<LogEntry> Entries, string Notes);

/// <summary>Per-item summary row (e.g. "Headache — 4 days, 6 times, worst: Severe").</summary>
public sealed record ReportItemSummary(
    string Name,
    int Times,
    int Days,
    DateTime First,
    DateTime Last,
    string Highest,                    // highest severity (symptoms)
    IReadOnlyList<string> Details,     // distinct doses / values
    IReadOnlyList<(DateTime Date, string Time, string Detail)> Occurrences);

public static class Report
{
    private static readonly string[] SeverityOrder = { "Mild", "Moderate", "Severe" };

    public static List<ReportDay> Collect(Store store, ReportOptions o)
    {
        var days = new List<ReportDay>();
        foreach (var (key, log) in store.Data.Days)
        {
            if (!DateTime.TryParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) continue;
            if (d < o.From.Date || d > o.To.Date) continue;

            var entries = log.Entries.Where(e => o.Categories.Contains(e.Category)).ToList();
            var notes = o.IncludeNotes ? log.Notes.Trim() : "";
            if (entries.Count == 0 && notes.Length == 0) continue;
            days.Add(new ReportDay(d, entries, notes));
        }
        days.Sort((a, b) => a.Date.CompareTo(b.Date));
        return days;
    }

    public static List<ReportItemSummary> Summarize(IEnumerable<ReportDay> days, string categoryId)
    {
        var occ = days.SelectMany(d => d.Entries.Where(e => e.Category == categoryId).Select(e => (d.Date, e)));
        return occ
            .GroupBy(x => x.e.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(g =>
            {
                var list = g.OrderBy(x => x.Date).ToList();
                var highest = list.Select(x => Array.IndexOf(SeverityOrder, x.e.Detail)).DefaultIfEmpty(-1).Max();
                return new ReportItemSummary(
                    Name: list[0].e.Name,
                    Times: list.Count,
                    Days: list.Select(x => x.Date).Distinct().Count(),
                    First: list[0].Date,
                    Last: list[^1].Date,
                    Highest: highest >= 0 ? SeverityOrder[highest] : "",
                    Details: list.Select(x => x.e.Detail).Where(s => s.Length > 0).Distinct(StringComparer.CurrentCultureIgnoreCase).ToList(),
                    Occurrences: list.Select(x => (x.Date, x.e.Time, x.e.Detail)).ToList());
            })
            .OrderByDescending(s => s.Times).ThenBy(s => s.Name, StringComparer.CurrentCulture)
            .ToList();
    }

    public static string DefaultFileName(ReportOptions o, string ext) =>
        $"Health log {o.From:yyyy-MM-dd} to {o.To:yyyy-MM-dd}.{ext}";
}
