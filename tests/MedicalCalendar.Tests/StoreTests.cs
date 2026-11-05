using System.IO;

namespace MedicalCalendar.Tests;

public class StoreTests
{
    private const string ValidJson =
        """{"Version":2,"Days":{"2026-11-01":{"Entries":[{"Category":"symptom","Name":"Headache","Detail":"Mild","Time":"9:00 AM"}],"Notes":"from backup"}},"CustomItems":{},"PatientName":""}""";

    private static readonly DateTime Day = new(2026, 11, 5);

    [Fact]
    public void Load_without_a_data_file_starts_empty()
    {
        using var dir = new TempDataDir();
        var store = Make.Store();

        store.Load();

        Assert.Empty(store.Data.Days);
        Assert.False(File.Exists(AppPaths.DataFile));
    }

    [Fact]
    public void Saved_entries_notes_and_custom_items_survive_a_reload()
    {
        using var dir = new TempDataDir();
        var store = Make.Store();
        store.Load();
        var log = store.GetOrCreate(Day);
        log.Entries.Add(Make.Entry("medication", "Ibuprofen", "200 mg", "8:00 AM"));
        log.Notes = "Slept badly";
        store.AddCustom("symptom", "Ringing in ears");
        store.Data.PatientName = "Test Patient";
        store.DayChanged(Day);
        store.Flush();

        var reloaded = Make.Store();
        reloaded.Load();

        var day = reloaded.Get(Day);
        Assert.NotNull(day);
        var e = Assert.Single(day.Entries);
        Assert.Equal(("medication", "Ibuprofen", "200 mg", "8:00 AM"), (e.Category, e.Name, e.Detail, e.Time));
        Assert.Equal("Slept badly", day.Notes);
        Assert.Equal(new[] { "Ringing in ears" }, reloaded.Custom("symptom"));
        Assert.Equal("Test Patient", reloaded.Data.PatientName);
    }

    [Fact]
    public void A_day_with_no_entries_and_no_notes_is_removed()
    {
        using var dir = new TempDataDir();
        var store = Make.Store();
        store.GetOrCreate(Day).Notes = "   ";

        store.DayChanged(Day);

        Assert.Null(store.Get(Day));
    }

    [Fact]
    public void Save_does_not_leave_a_temp_file_behind()
    {
        using var dir = new TempDataDir();
        var store = Make.Store();
        store.GetOrCreate(Day).Notes = "x";
        store.DayChanged(Day);
        store.Flush();

        Assert.True(File.Exists(AppPaths.DataFile));
        Assert.False(File.Exists(AppPaths.DataFile + ".tmp"));
    }

    [Fact]
    public void Unreadable_file_is_kept_unchanged_and_newest_backup_is_restored()
    {
        using var dir = new TempDataDir();
        const string broken = """{"Version":2,"Days":{ broken""";
        File.WriteAllText(AppPaths.DataFile, broken);
        Directory.CreateDirectory(AppPaths.BackupDir);
        File.WriteAllText(Path.Combine(AppPaths.BackupDir, "data-2026-10-01.json"), ValidJson.Replace("from backup", "older"));
        File.WriteAllText(Path.Combine(AppPaths.BackupDir, "data-2026-11-04.json"), ValidJson);
        var warnings = new List<string>();
        var store = Make.Store(warnings);

        store.Load();

        Assert.Equal("from backup", store.Get(new DateTime(2026, 11, 1))?.Notes);
        var kept = Assert.Single(Directory.GetFiles(dir.Path, "data.unreadable-*.json"));
        Assert.Equal(broken, File.ReadAllText(kept));
        Assert.Equal(broken, File.ReadAllText(AppPaths.DataFile)); // not overwritten on load
        var warning = Assert.Single(warnings);
        Assert.Contains("data-2026-11-04.json", warning);
    }

    [Fact]
    public void Unreadable_backups_are_skipped_in_favour_of_an_older_good_one()
    {
        using var dir = new TempDataDir();
        File.WriteAllText(AppPaths.DataFile, "not json");
        Directory.CreateDirectory(AppPaths.BackupDir);
        File.WriteAllText(Path.Combine(AppPaths.BackupDir, "data-2026-10-01.json"), ValidJson);
        File.WriteAllText(Path.Combine(AppPaths.BackupDir, "data-2026-11-04.json"), "also not json");
        var store = Make.Store();

        store.Load();

        Assert.Equal("from backup", store.Get(new DateTime(2026, 11, 1))?.Notes);
    }

    [Fact]
    public void Unreadable_file_without_backups_starts_empty_and_warns()
    {
        using var dir = new TempDataDir();
        File.WriteAllText(AppPaths.DataFile, "not json");
        var warnings = new List<string>();
        var store = Make.Store(warnings);

        store.Load();

        Assert.Empty(store.Data.Days);
        Assert.Contains("starting empty", Assert.Single(warnings));
    }

    [Fact]
    public void Load_makes_one_backup_per_day_and_keeps_the_newest_30()
    {
        using var dir = new TempDataDir();
        File.WriteAllText(AppPaths.DataFile, ValidJson);
        Directory.CreateDirectory(AppPaths.BackupDir);
        for (int i = 0; i < 35; i++)
            File.WriteAllText(Path.Combine(AppPaths.BackupDir, $"data-{new DateTime(2025, 1, 1).AddDays(i):yyyy-MM-dd}.json"), "{}");

        Make.Store().Load();
        Make.Store().Load(); // second launch the same day must not add another copy

        var backups = Directory.GetFiles(AppPaths.BackupDir, "data-*.json").Select(Path.GetFileName).ToList();
        Assert.Equal(30, backups.Count);
        Assert.Contains($"data-{DateTime.Today:yyyy-MM-dd}.json", backups);
    }

    [Fact]
    public void Prototype_notes_file_is_migrated_and_renamed()
    {
        using var dir = new TempDataDir();
        File.WriteAllText(AppPaths.LegacyNotesFile, """{"2026-11-01":"old note"}""");
        var store = Make.Store();

        store.Load();

        Assert.Equal("old note", store.Get(new DateTime(2026, 11, 1))?.Notes);
        Assert.True(File.Exists(AppPaths.DataFile));
        Assert.False(File.Exists(AppPaths.LegacyNotesFile));
        Assert.True(File.Exists(AppPaths.LegacyNotesFile + ".v1.bak"));
    }

    [Fact]
    public void Nulls_in_a_hand_edited_file_are_tolerated()
    {
        using var dir = new TempDataDir();
        File.WriteAllText(AppPaths.DataFile,
            """{"Version":2,"Days":{"2026-11-01":{"Entries":null,"Notes":null}},"CustomItems":null,"PatientName":null}""");
        var warnings = new List<string>();
        var store = Make.Store(warnings);

        store.Load();

        Assert.Empty(warnings);
        var day = store.Get(new DateTime(2026, 11, 1));
        Assert.NotNull(day);
        Assert.Empty(day.Entries);
        Assert.Equal("", day.Notes);
        Assert.Empty(store.Custom("symptom"));
        Assert.Equal("", store.Data.PatientName);
    }

    [Fact]
    public void Custom_items_are_unique_ignoring_case_and_newest_first()
    {
        using var dir = new TempDataDir();
        var store = Make.Store();

        store.AddCustom("medication", "Pill A");
        store.AddCustom("medication", "pill a");
        store.AddCustom("medication", "Pill B");

        Assert.Equal(new[] { "Pill B", "Pill A" }, store.Custom("medication"));
    }

    [Fact]
    public void Removing_the_last_custom_item_removes_the_category()
    {
        using var dir = new TempDataDir();
        var store = Make.Store();
        store.AddCustom("medication", "Pill A");

        store.RemoveCustom("medication", "PILL A");

        Assert.False(store.Data.CustomItems.ContainsKey("medication"));
    }
}
