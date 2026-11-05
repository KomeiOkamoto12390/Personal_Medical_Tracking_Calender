using System.IO;
using System.Text;

namespace MedicalCalendar;

/// <summary>Plain spreadsheet export (opens in Excel with Japanese text intact thanks to the UTF-8 BOM).</summary>
public static class CsvReport
{
    public static void Write(string path, IReadOnlyList<ReportDay> days)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Date,Time,Category,Item,Detail");
        foreach (var d in days)
        {
            var date = d.Date.ToString("yyyy-MM-dd");
            foreach (var e in d.Entries)
                sb.AppendLine(string.Join(",", date, Q(e.Time), Q(Catalog.Find(e.Category)?.Title ?? e.Category), Q(e.Name), Q(e.Detail)));
            if (d.Notes.Length > 0)
                sb.AppendLine(string.Join(",", date, "", "Notes", "", Q(d.Notes)));
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static string Q(string s) =>
        s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
}
