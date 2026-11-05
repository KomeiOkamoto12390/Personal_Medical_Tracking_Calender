using System.IO;
using PdfSharp.Fonts;

namespace MedicalCalendar;

/// <summary>
/// Supplies Windows fonts to PDFsharp. Latin text uses Segoe UI; if the report contains
/// Japanese/Chinese/Korean text we switch to Yu Gothic (falling back to Meiryo / MS Gothic).
/// Those ship as .ttc collections, so the first face is extracted into a standalone font.
/// </summary>
public sealed class PdfFonts : IFontResolver
{
    public const string Latin = "Segoe UI";
    public const string Cjk = "Report CJK";

    private static readonly string FontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
    private static readonly object Gate = new();
    private static bool _installed;

    public static void Install()
    {
        lock (Gate)
        {
            if (_installed) return;
            GlobalFontSettings.FontResolver = new PdfFonts();
            _installed = true;
        }
    }

    /// <summary>True when the text needs a CJK-capable font.</summary>
    public static bool NeedsCjk(string s)
    {
        foreach (var ch in s)
            if (ch >= '⺀' && ch <= '￯') return true;
        return false;
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
    {
        if (familyName == Cjk) return new FontResolverInfo(bold ? "cjk-bold" : "cjk-regular");
        return new FontResolverInfo(bold ? "segoeuib" : italic ? "segoeuii" : "segoeui");
    }

    public byte[]? GetFont(string faceName)
    {
        string[] candidates = faceName switch
        {
            "cjk-bold" => new[] { "YuGothB.ttc", "meiryob.ttc", "msgothic.ttc" },
            "cjk-regular" => new[] { "YuGothR.ttc", "meiryo.ttc", "msgothic.ttc" },
            _ => new[] { faceName + ".ttf", "segoeui.ttf", "arial.ttf" },
        };
        foreach (var file in candidates)
        {
            var path = Path.Combine(FontsDir, file);
            if (!File.Exists(path)) continue;
            var bytes = File.ReadAllBytes(path);
            return file.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase) ? ExtractFirstFace(bytes) : bytes;
        }
        return null;
    }

    /// <summary>Turns face #0 of a TrueType collection into a standalone .ttf.</summary>
    private static byte[] ExtractFirstFace(byte[] ttc)
    {
        static uint U32(byte[] b, int o) => (uint)(b[o] << 24 | b[o + 1] << 16 | b[o + 2] << 8 | b[o + 3]);
        static ushort U16(byte[] b, int o) => (ushort)(b[o] << 8 | b[o + 1]);
        static void W32(Stream s, uint v) { s.WriteByte((byte)(v >> 24)); s.WriteByte((byte)(v >> 16)); s.WriteByte((byte)(v >> 8)); s.WriteByte((byte)v); }

        if (U32(ttc, 0) != 0x74746366) return ttc; // not 'ttcf'
        int fontOffset = (int)U32(ttc, 12);
        int numTables = U16(ttc, fontOffset + 4);
        int headerLen = 12 + numTables * 16;

        using var ms = new MemoryStream();
        ms.Write(ttc, fontOffset, 12);                      // sfnt header (version, numTables, search fields)

        uint dataOffset = (uint)headerLen;
        var tables = new List<(int Src, int Len)>();
        for (int i = 0; i < numTables; i++)
        {
            int rec = fontOffset + 12 + i * 16;
            ms.Write(ttc, rec, 8);                          // tag + checksum
            int src = (int)U32(ttc, rec + 8), len = (int)U32(ttc, rec + 12);
            W32(ms, dataOffset);
            W32(ms, (uint)len);
            tables.Add((src, len));
            dataOffset += (uint)((len + 3) & ~3);
        }
        foreach (var (src, len) in tables)
        {
            ms.Write(ttc, src, len);
            for (int pad = (4 - len % 4) % 4; pad > 0; pad--) ms.WriteByte(0);
        }
        return ms.ToArray();
    }
}
