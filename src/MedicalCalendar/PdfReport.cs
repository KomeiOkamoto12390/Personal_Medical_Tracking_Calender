using System.Globalization;
using System.Text;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using MediaColor = System.Windows.Media.Color;
using SolidBrush = System.Windows.Media.SolidColorBrush;

namespace MedicalCalendar;

/// <summary>Builds an A4 report for a doctor: summary per category, then the day-by-day log.</summary>
public static class PdfReport
{
    private static readonly CultureInfo Ui = CultureInfo.CurrentCulture;
    private static readonly Color Ink = new(0x1D, 0x1D, 0x1F);
    private static readonly Color Subtle = new(0x6E, 0x6E, 0x73);
    private static readonly Color Rule = new(0xDD, 0xDD, 0xE2);
    private static readonly Color HeadFill = new(0xF2, 0xF2, 0xF5);
    private const double ContentCm = 17.4; // A4 width 21 cm − 2 × 1.8 cm margins

    public static void Write(string path, ReportOptions o, IReadOnlyList<ReportDay> days)
    {
        PdfFonts.Install();
        var doc = Build(o, days);
        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(path);
    }

    private static Document Build(ReportOptions o, IReadOnlyList<ReportDay> days)
    {
        var doc = new Document();
        doc.Info.Title = "Health log";
        doc.Info.Author = o.PatientName;

        var font = PdfFonts.NeedsCjk(AllText(o, days)) ? PdfFonts.Cjk : PdfFonts.Latin;
        var normal = doc.Styles[StyleNames.Normal]!;
        normal.Font.Name = font;
        normal.Font.Size = 9.5;
        normal.Font.Color = Ink;

        var h2 = doc.Styles[StyleNames.Heading2]!;
        h2.Font.Size = 12.5;
        h2.Font.Bold = true;
        h2.ParagraphFormat.SpaceBefore = Unit.FromPoint(16);
        h2.ParagraphFormat.SpaceAfter = Unit.FromPoint(6);
        h2.ParagraphFormat.KeepWithNext = true;

        var sec = doc.AddSection();
        sec.PageSetup = doc.DefaultPageSetup.Clone();
        sec.PageSetup.PageFormat = PageFormat.A4;
        sec.PageSetup.LeftMargin = sec.PageSetup.RightMargin = Unit.FromCentimeter(1.8);
        sec.PageSetup.TopMargin = Unit.FromCentimeter(1.8);
        sec.PageSetup.BottomMargin = Unit.FromCentimeter(2.0);

        // Footer: "Health log · Name                     Page 1 of 3"
        var foot = sec.Footers.Primary.AddParagraph();
        foot.Format.Font.Size = 8;
        foot.Format.Font.Color = Subtle;
        foot.Format.TabStops.AddTabStop(Unit.FromCentimeter(ContentCm), TabAlignment.Right);
        foot.AddText(o.PatientName.Length > 0 ? $"Health log · {o.PatientName}" : "Health log");
        foot.AddTab();
        foot.AddText("Page ");
        foot.AddPageField();
        foot.AddText(" of ");
        foot.AddNumPagesField();

        // ----- Title block -----
        var title = sec.AddParagraph("Health log");
        title.Format.Font.Size = 22;
        title.Format.Font.Bold = true;
        title.Format.SpaceAfter = Unit.FromPoint(8);

        var info = NewTable(sec, 3.2, ContentCm - 3.2);
        info.Borders.Visible = false;
        void Info(string k, string v)
        {
            var r = info.AddRow();
            r.Cells[0].AddParagraph(k).Format.Font.Color = Subtle;
            r.Cells[1].AddParagraph(v);
        }
        if (o.PatientName.Length > 0) Info("Patient", o.PatientName);
        Info("Period", $"{FmtDate(o.From)} – {FmtDate(o.To)}  ({(o.To.Date - o.From.Date).Days + 1} days)");
        Info("Includes", string.Join(", ", Catalog.Categories.Where(c => o.Categories.Contains(c.Id)).Select(c => c.Title)
                                              .Concat(o.IncludeNotes ? new[] { "Notes" } : Array.Empty<string>())));
        Info("Logged", $"{days.Count} days with entries · {days.Sum(d => d.Entries.Count)} entries");
        Info("Generated", DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));

        if (days.Count == 0)
        {
            var p = sec.AddParagraph("Nothing was logged for the selected period and categories.");
            p.Format.SpaceBefore = Unit.FromPoint(18);
            p.Format.Font.Color = Subtle;
            return doc;
        }

        // ----- Summary per category -----
        foreach (var def in Catalog.Categories.Where(c => o.Categories.Contains(c.Id)))
        {
            var summary = Report.Summarize(days, def.Id);
            CategoryHeading(sec, def, "summary");
            if (summary.Count == 0)
            {
                sec.AddParagraph("Nothing logged in this period.").Format.Font.Color = Subtle;
                continue;
            }

            switch (def.Id)
            {
                case "symptom":
                {
                    var t = NewTable(sec, 4.8, 1.4, 1.4, 2.4, 3.7, 3.7);
                    Header(t, "Symptom", "Times", "Days", "Worst", "First", "Last");
                    foreach (var s in summary)
                        Row(t, s.Name, s.Times.ToString(Ui), s.Days.ToString(Ui), s.Highest, FmtDate(s.First), FmtDate(s.Last));
                    break;
                }
                case "medication":
                {
                    var t = NewTable(sec, 5.2, 1.4, 1.4, 5.7, 3.7);
                    Header(t, "Medication", "Times", "Days", "Doses", "Last taken");
                    foreach (var s in summary)
                        Row(t, s.Name, s.Times.ToString(Ui), s.Days.ToString(Ui), string.Join(", ", s.Details), FmtDate(s.Last));
                    break;
                }
                case "vital":
                {
                    // One row per reading so trends are easy to scan; name shown once per measurement.
                    var t = NewTable(sec, 5.2, 3.7, 2.4, ContentCm - 11.3);
                    Header(t, "Measurement", "Date", "Time", "Value");
                    foreach (var s in summary)
                    {
                        bool first = true;
                        foreach (var x in s.Occurrences)
                        {
                            var r = Row(t, first ? $"{s.Name}  ({s.Times})" : "", FmtDate(x.Date), x.Time,
                                        x.Detail.Length > 0 ? x.Detail : "—");
                            if (first) { r.Cells[0].Format.Font.Bold = true; DayRule(r); }
                            first = false;
                        }
                    }
                    break;
                }
                default: // appointments and anything else: list every occurrence
                {
                    var t = NewTable(sec, 3.6, 2.2, 5.8, ContentCm - 11.6);
                    Header(t, "Date", "Time", def.Id == "appointment" ? "Appointment" : "Item", "Details");
                    foreach (var x in summary.SelectMany(s => s.Occurrences.Select(oc => (s.Name, oc))).OrderBy(x => x.oc.Date))
                        Row(t, FmtDate(x.oc.Date), x.oc.Time, x.Name, x.oc.Detail);
                    break;
                }
            }
        }

        // ----- Day by day -----
        var h = sec.AddParagraph("Daily log", StyleNames.Heading2);
        h.Format.SpaceBefore = Unit.FromPoint(22);

        var log = NewTable(sec, 3.6, 2.0, 2.4, 4.8, ContentCm - 12.8);
        Header(log, "Date", "Time", "Category", "Item", "Detail");
        foreach (var d in days)
        {
            bool first = true;
            foreach (var e in d.Entries.OrderBy(e => SortKey(e.Time)))
            {
                var def = Catalog.Find(e.Category);
                var r = Row(log, first ? FmtDate(d.Date) : "", e.Time, ShortTitle(e.Category), e.Name, e.Detail);
                if (def != null) r.Cells[2].Format.Font.Color = ToPdf(def.Accent);
                if (first) { DayRule(r); r.Cells[0].Format.Font.Bold = true; }
                first = false;
            }
            if (d.Notes.Length > 0)
            {
                var r = Row(log, first ? FmtDate(d.Date) : "", "", "Notes", d.Notes, "");
                r.Cells[3].MergeRight = 1;
                r.Cells[2].Format.Font.Color = Subtle;
                if (first) { DayRule(r); r.Cells[0].Format.Font.Bold = true; }
            }
        }
        return doc;
    }

    // ---------- helpers ----------

    private static void CategoryHeading(Section sec, CategoryDef def, string _)
    {
        var p = sec.AddParagraph("", StyleNames.Heading2);
        p.AddFormattedText("■ ").Color = ToPdf(def.Accent);
        p.AddText(def.Title);
    }

    private static Table NewTable(Section sec, params double[] widthsCm)
    {
        var t = sec.AddTable();
        t.Borders.Width = 0;
        t.LeftPadding = Unit.FromPoint(4);
        t.RightPadding = Unit.FromPoint(4);
        t.TopPadding = Unit.FromPoint(3);
        t.BottomPadding = Unit.FromPoint(3);
        foreach (var w in widthsCm) t.AddColumn(Unit.FromCentimeter(w));
        return t;
    }

    private static void Header(Table t, params string[] titles)
    {
        var r = t.AddRow();
        r.HeadingFormat = true;          // repeats on each page
        r.Shading.Color = HeadFill;
        r.Format.Font.Bold = true;
        r.Format.Font.Size = 8.5;
        r.Format.Font.Color = Subtle;
        for (int i = 0; i < titles.Length; i++) r.Cells[i].AddParagraph(titles[i]);
    }

    private static Row Row(Table t, params string[] values)
    {
        var r = t.AddRow();
        r.Borders.Bottom.Width = 0.4;
        r.Borders.Bottom.Color = Rule;
        for (int i = 0; i < values.Length; i++) r.Cells[i].AddParagraph(values[i] ?? "");
        return r;
    }

    private static void DayRule(Row r)
    {
        r.Borders.Top.Width = 0.9;
        r.Borders.Top.Color = new Color(0xB8, 0xB8, 0xBE);
    }

    private static string ShortTitle(string categoryId) => categoryId switch
    {
        "medication" => "Medication",
        "symptom" => "Symptom",
        "vital" => "Vital",
        "appointment" => "Appointment",
        _ => Catalog.Find(categoryId)?.Title ?? categoryId,
    };

    private static string FmtDate(DateTime d) =>
        $"{d:yyyy-MM-dd} ({d.ToString("ddd", Ui)})";

    private static TimeSpan SortKey(string time) =>
        DateTime.TryParse(time, Ui, DateTimeStyles.NoCurrentDateDefault, out var dt) ? dt.TimeOfDay : TimeSpan.MaxValue;

    private static Color ToPdf(System.Windows.Media.Brush b)
    {
        var c = b is SolidBrush s ? s.Color : MediaColor.FromRgb(0, 0, 0);
        return new Color(c.R, c.G, c.B);
    }

    private static string AllText(ReportOptions o, IReadOnlyList<ReportDay> days)
    {
        var sb = new StringBuilder(o.PatientName).Append(DateTime.Today.ToString("ddd", Ui));
        foreach (var d in days)
        {
            sb.Append(d.Notes);
            foreach (var e in d.Entries) sb.Append(e.Name).Append(e.Detail).Append(e.Time);
        }
        return sb.ToString();
    }
}
