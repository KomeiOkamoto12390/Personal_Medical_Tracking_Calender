using System.IO;
using System.Text;

namespace MedicalCalendar.Tests;

public class ReportTests
{
    private static readonly HashSet<string> AllCategories = Catalog.Categories.Select(c => c.Id).ToHashSet();

    private static Store StoreWith(params (string Date, LogEntry[] Entries, string Notes)[] days)
    {
        var store = Make.Store();
        foreach (var (date, entries, notes) in days)
        {
            var log = store.GetOrCreate(DateTime.Parse(date, System.Globalization.CultureInfo.InvariantCulture));
            log.Entries.AddRange(entries);
            log.Notes = notes;
        }
        return store;
    }

    private static ReportOptions Options(string from, string to, IEnumerable<string>? categories = null, bool notes = true) =>
        new(DateTime.Parse(from), DateTime.Parse(to), (categories ?? AllCategories).ToHashSet(), notes, "");

    [Fact]
    public void Collect_includes_both_end_dates_and_sorts_by_date()
    {
        var store = StoreWith(
            ("2026-11-10", new[] { Make.Entry("symptom", "Fever") }, ""),
            ("2026-10-31", new[] { Make.Entry("symptom", "Too early") }, ""),
            ("2026-11-01", new[] { Make.Entry("symptom", "Headache") }, ""),
            ("2026-11-11", new[] { Make.Entry("symptom", "Too late") }, ""));

        var days = Report.Collect(store, Options("2026-11-01", "2026-11-10"));

        Assert.Equal(new[] { new DateTime(2026, 11, 1), new DateTime(2026, 11, 10) }, days.Select(d => d.Date));
    }

    [Fact]
    public void Collect_keeps_only_chosen_categories_and_drops_days_left_empty()
    {
        var store = StoreWith(
            ("2026-11-01", new[] { Make.Entry("symptom", "Headache"), Make.Entry("medication", "Ibuprofen") }, ""),
            ("2026-11-02", new[] { Make.Entry("medication", "Ibuprofen") }, ""));

        var days = Report.Collect(store, Options("2026-11-01", "2026-11-30", new[] { "symptom" }, notes: false));

        var day = Assert.Single(days);
        Assert.Equal("Headache", Assert.Single(day.Entries).Name);
    }

    [Fact]
    public void Notes_only_days_appear_only_when_notes_are_included()
    {
        var store = StoreWith(("2026-11-03", Array.Empty<LogEntry>(), "Felt dizzy"));

        Assert.Single(Report.Collect(store, Options("2026-11-01", "2026-11-30", notes: true)));
        Assert.Empty(Report.Collect(store, Options("2026-11-01", "2026-11-30", notes: false)));
    }

    [Fact]
    public void Summary_counts_times_days_worst_severity_and_distinct_details()
    {
        var store = StoreWith(
            ("2026-11-01", new[] { Make.Entry("symptom", "Headache", "Mild"), Make.Entry("symptom", "headache", "Severe") }, ""),
            ("2026-11-03", new[] { Make.Entry("symptom", "Headache", "Moderate") }, ""),
            ("2026-11-02", new[] { Make.Entry("symptom", "Nausea", "Mild") }, ""));
        var days = Report.Collect(store, Options("2026-11-01", "2026-11-30"));

        var summary = Report.Summarize(days, "symptom");

        Assert.Equal(new[] { "Headache", "Nausea" }, summary.Select(s => s.Name)); // most frequent first
        var headache = summary[0];
        Assert.Equal(3, headache.Times);
        Assert.Equal(2, headache.Days);
        Assert.Equal("Severe", headache.Highest);
        Assert.Equal(new DateTime(2026, 11, 1), headache.First);
        Assert.Equal(new DateTime(2026, 11, 3), headache.Last);
        Assert.Equal(new[] { "Mild", "Severe", "Moderate" }, headache.Details);
    }

    [Fact]
    public void Summary_has_no_worst_severity_for_free_text_details()
    {
        var store = StoreWith(("2026-11-01", new[] { Make.Entry("medication", "Ibuprofen", "200 mg") }, ""));
        var days = Report.Collect(store, Options("2026-11-01", "2026-11-30"));

        Assert.Equal("", Report.Summarize(days, "medication").Single().Highest);
    }

    [Fact]
    public void Default_file_name_contains_the_period()
    {
        Assert.Equal("Health log 2026-10-01 to 2026-11-05.pdf",
            Report.DefaultFileName(Options("2026-10-01", "2026-11-05"), "pdf"));
    }

    [Fact]
    public void Csv_quotes_commas_quotes_and_newlines_and_writes_a_bom_for_excel()
    {
        using var dir = new TempDataDir();
        var store = StoreWith(("2026-11-01",
            new[] { Make.Entry("medication", "Pain relief, extra", "1 \"tablet\"", "8:00 AM") }, "Line one\nLine two"));
        var path = dir.File("out.csv");

        CsvReport.Write(path, Report.Collect(store, Options("2026-11-01", "2026-11-30")));

        var bytes = File.ReadAllBytes(path);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        var text = Encoding.UTF8.GetString(bytes[3..]);
        Assert.StartsWith("Date,Time,Category,Item,Detail", text);
        Assert.Contains("2026-11-01,8:00 AM,Medications,\"Pain relief, extra\",\"1 \"\"tablet\"\"\"", text);
        Assert.Contains("2026-11-01,,Notes,,\"Line one\nLine two\"", text);
    }
}
