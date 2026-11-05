using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;

namespace MedicalCalendar;

/// <summary>One card on the daily log page (Medications, Symptoms, …).</summary>
public sealed class CategorySection : ObservableObject
{
    private readonly Action _entriesChanged;
    private readonly Action<string> _customAdded;
    private readonly Action<string> _customRemoved;
    private LogEntry? _editing;

    public CategorySection(CategoryDef def, IEnumerable<string> customItems,
                           Action entriesChanged, Action<string> customAdded, Action<string> customRemoved)
    {
        Def = def;
        _entriesChanged = entriesChanged;
        _customAdded = customAdded;
        _customRemoved = customRemoved;

        // The user's own items come first so "My items" is the top group in the list.
        foreach (var c in customItems) Items.Add(new CatalogItem(c, Catalog.CustomGroup));
        foreach (var g in def.Groups)
            foreach (var i in g.Items) Items.Add(new CatalogItem(i, g.Name));



        Entries.CollectionChanged += (_, _) =>
        {
            Raise(nameof(IsEmpty)); Raise(nameof(HasEntries)); Raise(nameof(CountText));
        };

        AddCommand = new RelayCommand(_ => Commit());
        RemoveCommand = new RelayCommand(p =>
        {
            if (p is not LogEntry e) return;
            if (e == _editing) CancelEdit();
            if (Entries.Remove(e)) _entriesChanged();
        });
        EditCommand = new RelayCommand(p => { if (p is LogEntry e) BeginEdit(e); });
        CancelEditCommand = new RelayCommand(_ => CancelEdit());
        RemoveCustomCommand = new RelayCommand(p =>
        {
            if (p is CatalogItem { Group: Catalog.CustomGroup } item && Items.Remove(item))
                _customRemoved(item.Name);
        });
        ResetInputs();
    }

    public CategoryDef Def { get; }
    public ObservableCollection<CatalogItem> Items { get; } = new();
    public ObservableCollection<LogEntry> Entries { get; } = new();

    public ICommand AddCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand RemoveCustomCommand { get; }

    public bool IsEmpty => Entries.Count == 0;
    public bool HasEntries => Entries.Count > 0;
    public string CountText => Entries.Count == 0 ? "" : Entries.Count.ToString(CultureInfo.CurrentCulture);

    // ----- edit state -----
    public bool IsEditing => _editing != null;
    public string EditingText => _editing is null ? "" : $"Editing “{_editing.Name}” — change the fields, then Save.";
    public string CommitLabel => IsEditing ? "Save" : "Add";
    public string CommitGlyph => IsEditing ? "" : "";   // Segoe Fluent Icons: checkmark / plus

    // ----- input fields -----
    private string _newName = "";
    public string NewName { get => _newName; set => Set(ref _newName, value ?? ""); }

    private string _newDetail = "";
    public string NewDetail
    {
        get => _newDetail;
        set { if (Set(ref _newDetail, value ?? "")) Raise(nameof(ShowDetailHint)); }
    }
    public bool ShowDetailHint => NewDetail.Length == 0 && !Def.HasOptions;

    private string _newTime = "";
    public string NewTime { get => _newTime; set => Set(ref _newTime, value ?? ""); }

    public void Load(IEnumerable<LogEntry> entries)
    {
        SetEditing(null);
        Entries.Clear();
        foreach (var e in entries) Entries.Add(e);
        ResetInputs();
    }

    public void CancelEdit()
    {
        if (!IsEditing) return;
        SetEditing(null);
        ResetInputs();
    }

    private void BeginEdit(LogEntry e)
    {
        SetEditing(e);
        NewName = e.Name;
        NewDetail = e.Detail;
        NewTime = e.Time;
    }

    private void SetEditing(LogEntry? e)
    {
        _editing = e;
        Raise(nameof(IsEditing)); Raise(nameof(EditingText));
        Raise(nameof(CommitLabel)); Raise(nameof(CommitGlyph));
    }

    private void ResetInputs()
    {
        NewName = "";
        NewDetail = Def.DetailOptions?.FirstOrDefault() ?? "";
        NewTime = DateTime.Now.ToString("t", CultureInfo.CurrentCulture);
    }

    /// <summary>Add a new entry, or save the one being edited.</summary>
    private void Commit()
    {
        var name = NewName.Trim();
        if (name.Length == 0) return;

        var known = Items.FirstOrDefault(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));
        if (known is null)
        {
            Items.Insert(0, new CatalogItem(name, Catalog.CustomGroup)); // remember it for next time
            _customAdded(name);
        }
        else name = known.Name;

        if (_editing != null) Entries.Remove(_editing);
        var entry = new LogEntry { Category = Def.Id, Name = name, Detail = NewDetail.Trim(), Time = NewTime.Trim() };

        // Keep the list ordered by time of day.
        var t = SortKey(entry.Time);
        int idx = 0;
        while (idx < Entries.Count && SortKey(Entries[idx].Time) <= t) idx++;
        Entries.Insert(idx, entry);

        SetEditing(null);
        _entriesChanged();
        ResetInputs();
    }

    private static TimeSpan SortKey(string time) =>
        DateTime.TryParse(time, CultureInfo.CurrentCulture, DateTimeStyles.NoCurrentDateDefault, out var dt)
            ? dt.TimeOfDay : TimeSpan.MaxValue;
}
