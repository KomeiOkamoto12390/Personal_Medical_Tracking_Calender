namespace MedicalCalendar.Tests;

public class CategorySectionTests
{
    private sealed class Harness
    {
        public int EntriesChanged;
        public readonly List<string> Added = new(), Removed = new();
        public CategorySection Section { get; }

        public Harness(string categoryId, params string[] custom)
        {
            Section = new CategorySection(Catalog.Find(categoryId)!, custom,
                entriesChanged: () => EntriesChanged++,
                customAdded: Added.Add,
                customRemoved: Removed.Add);
        }

        public void Add(string name, string detail = "", string time = "9:00 AM")
        {
            Section.NewName = name;
            Section.NewDetail = detail;
            Section.NewTime = time;
            Section.AddCommand.Execute(null);
        }
    }

    [Fact]
    public void Known_item_typed_in_any_case_uses_the_catalog_spelling()
    {
        var h = new Harness("medication");

        h.Add("ibuprofen", "200 mg");

        Assert.Equal("Ibuprofen", Assert.Single(h.Section.Entries).Name);
        Assert.Empty(h.Added);
        Assert.Equal(1, h.EntriesChanged);
    }

    [Fact]
    public void Unknown_item_is_logged_and_remembered_under_my_items()
    {
        var h = new Harness("symptom");

        h.Add("  Ringing in ears  ", "Mild");

        Assert.Equal("Ringing in ears", Assert.Single(h.Section.Entries).Name);
        Assert.Equal(new[] { "Ringing in ears" }, h.Added);
        Assert.Equal(new CatalogItem("Ringing in ears", Catalog.CustomGroup), h.Section.Items[0]);
    }

    [Fact]
    public void Blank_name_adds_nothing()
    {
        var h = new Harness("medication");

        h.Add("   ");

        Assert.Empty(h.Section.Entries);
        Assert.Equal(0, h.EntriesChanged);
    }

    [Fact]
    public void Entries_are_kept_in_time_order()
    {
        using var _ = new CultureScope("en-US");
        var h = new Harness("medication");

        h.Add("Vitamin D", time: "9:00 PM");
        h.Add("Ibuprofen", time: "8:00 AM");
        h.Add("Iron", time: "1:15 PM");

        Assert.Equal(new[] { "Ibuprofen", "Iron", "Vitamin D" }, h.Section.Entries.Select(e => e.Name));
    }

    [Fact]
    public void Inputs_reset_after_adding_and_symptoms_default_to_mild()
    {
        var h = new Harness("symptom");

        h.Add("Headache", "Severe");

        Assert.Equal("", h.Section.NewName);
        Assert.Equal("Mild", h.Section.NewDetail);
    }

    [Fact]
    public void Editing_replaces_the_entry_instead_of_adding_one()
    {
        using var _ = new CultureScope("en-US");
        var h = new Harness("medication");
        h.Add("Ibuprofen", "200 mg", "8:00 AM");
        var original = h.Section.Entries[0];

        h.Section.EditCommand.Execute(original);
        Assert.True(h.Section.IsEditing);
        Assert.Equal("Save", h.Section.CommitLabel);
        h.Section.NewDetail = "400 mg";
        h.Section.AddCommand.Execute(null);

        var e = Assert.Single(h.Section.Entries);
        Assert.Equal("400 mg", e.Detail);
        Assert.False(h.Section.IsEditing);
    }

    [Fact]
    public void Cancelling_an_edit_leaves_the_entry_unchanged()
    {
        var h = new Harness("medication");
        h.Add("Ibuprofen", "200 mg");

        h.Section.EditCommand.Execute(h.Section.Entries[0]);
        h.Section.NewDetail = "999 mg";
        h.Section.CancelEdit();

        Assert.Equal("200 mg", h.Section.Entries[0].Detail);
        Assert.Equal("", h.Section.NewName);
    }

    [Fact]
    public void Removing_the_entry_being_edited_cancels_the_edit()
    {
        var h = new Harness("medication");
        h.Add("Ibuprofen");
        var entry = h.Section.Entries[0];
        h.Section.EditCommand.Execute(entry);

        h.Section.RemoveCommand.Execute(entry);

        Assert.Empty(h.Section.Entries);
        Assert.False(h.Section.IsEditing);
    }

    [Fact]
    public void Only_my_items_can_be_removed_from_the_list()
    {
        var h = new Harness("medication", "Pill A");
        var builtIn = h.Section.Items.First(i => i.Group != Catalog.CustomGroup);
        var custom = h.Section.Items.First(i => i.Group == Catalog.CustomGroup);

        h.Section.RemoveCustomCommand.Execute(builtIn);
        h.Section.RemoveCustomCommand.Execute(custom);

        Assert.Contains(builtIn, h.Section.Items);
        Assert.DoesNotContain(custom, h.Section.Items);
        Assert.Equal(new[] { "Pill A" }, h.Removed);
    }
}
