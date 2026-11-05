using System.IO;
using System.Text;

namespace MedicalCalendar.Tests;

public class PdfTests
{
    private static ReportOptions AllFor(DateTime from, DateTime to) =>
        new(from, to, Catalog.Categories.Select(c => c.Id).ToHashSet(), true, "Test Patient");

    [Theory]
    [InlineData("Ibuprofen", false)]
    [InlineData("ロキソニン", true)]
    [InlineData("頭痛", true)]
    [InlineData("두통", true)]
    public void Detects_text_that_needs_a_CJK_font(string text, bool expected) =>
        Assert.Equal(expected, PdfFonts.NeedsCjk(text));

    [Theory]
    [InlineData("cjk-regular")]
    [InlineData("cjk-bold")]
    [InlineData("segoeui")]
    public void Font_resolver_returns_a_standalone_TrueType_font(string face)
    {
        var bytes = new PdfFonts().GetFont(face);

        Assert.NotNull(bytes);
        // A single sfnt starts with version 0x00010000 or 'OTTO' — never the 'ttcf' collection header.
        var tag = Encoding.ASCII.GetString(bytes, 0, 4);
        Assert.True(tag == "OTTO" || (bytes[0] == 0 && bytes[1] == 1 && bytes[2] == 0 && bytes[3] == 0), $"unexpected header '{tag}'");
    }

    [Fact]
    public void Writes_a_pdf_with_latin_and_japanese_text()
    {
        using var dir = new TempDataDir();
        var store = Make.Store();
        var log = store.GetOrCreate(new DateTime(2026, 11, 1));
        log.Entries.Add(Make.Entry("medication", "ロキソニン", "60 mg"));
        log.Entries.Add(Make.Entry("vital", "Blood pressure", "118/76"));
        log.Notes = "頭痛がした";
        var o = AllFor(new DateTime(2026, 11, 1), new DateTime(2026, 11, 30));
        var path = dir.File("report.pdf");

        PdfReport.Write(path, o, Report.Collect(store, o));

        var head = File.ReadAllBytes(path)[..5];
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(head));
    }

    [Fact]
    public void Writes_a_pdf_when_nothing_was_logged()
    {
        using var dir = new TempDataDir();
        var o = AllFor(new DateTime(2026, 11, 1), new DateTime(2026, 11, 30));
        var path = dir.File("empty.pdf");

        PdfReport.Write(path, o, Array.Empty<ReportDay>());

        Assert.True(new FileInfo(path).Length > 0);
    }
}
