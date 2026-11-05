using System.Windows.Media;

namespace MedicalCalendar;

/// <summary>A named group inside a category's dropdown (e.g. "Pain &amp; fever").</summary>
public sealed record CatalogGroup(string Name, params string[] Items);

/// <summary>One choosable item in a dropdown.</summary>
public sealed record CatalogItem(string Name, string Group);

/// <summary>A kind of thing you can log: medication, symptom, vital, appointment.</summary>
public sealed class CategoryDef
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string NameHint { get; init; }
    public required string DetailHint { get; init; }
    public required string EmptyText { get; init; }
    public string[]? DetailOptions { get; init; }
    public required CatalogGroup[] Groups { get; init; }

    public required Brush Accent { get; init; }  // dot / chip bar
    public required Brush Soft { get; init; }    // chip background
    public required Brush Ink { get; init; }     // chip text

    public bool HasOptions => DetailOptions is { Length: > 0 };
    public bool HasNoOptions => !HasOptions;
}

public static class Catalog
{
    /// <summary>Group name for items the user typed in themselves.</summary>
    public const string CustomGroup = "My items";

    public static Brush B(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    public static readonly Brush NoteSoft = B("#F0F0F2"), NoteInk = B("#48484A"), NoteAccent = B("#8E8E93");

    public static readonly CategoryDef[] Categories =
    {
        new()
        {
            Id = "medication", Title = "Medications",
            NameHint = "Choose or type a medication…", DetailHint = "Dose (e.g. 200 mg)",
            EmptyText = "No medications logged.",
            Accent = B("#8E5CF7"), Soft = B("#F1EBFE"), Ink = B("#5B2DB8"),
            Groups = new CatalogGroup[]
            {
                new("Pain & fever", "Acetaminophen (Paracetamol)", "Ibuprofen", "Naproxen", "Aspirin", "Loxoprofen"),
                new("Allergy", "Cetirizine", "Loratadine", "Fexofenadine", "Diphenhydramine", "Steroid nasal spray", "Antihistamine eye drops"),
                new("Stomach & digestion", "Omeprazole", "Famotidine", "Antacid", "Loperamide", "Laxative", "Probiotic", "Anti-nausea"),
                new("Cold & cough", "Cough syrup", "Decongestant", "Throat lozenge", "Expectorant"),
                new("Respiratory", "Rescue inhaler", "Preventer inhaler"),
                new("Sleep", "Melatonin"),
                new("Prescription", "Antibiotic", "Insulin", "Blood pressure medication", "Antidepressant", "Birth control"),
                new("Vitamins & supplements", "Multivitamin", "Vitamin D", "Vitamin B12", "Vitamin C", "Iron", "Magnesium", "Calcium", "Omega-3", "Zinc"),
            },
        },
        new()
        {
            Id = "symptom", Title = "Symptoms",
            NameHint = "Choose or type a symptom…", DetailHint = "Severity",
            EmptyText = "No symptoms logged.",
            DetailOptions = new[] { "Mild", "Moderate", "Severe" },
            Accent = B("#F2672A"), Soft = B("#FDEEE6"), Ink = B("#A4400F"),
            Groups = new CatalogGroup[]
            {
                new("General", "Fatigue", "Fever", "Chills", "Dizziness", "Sweating", "Loss of appetite"),
                new("Pain", "Headache", "Migraine", "Back pain", "Neck pain", "Joint pain", "Muscle ache", "Chest pain", "Abdominal pain", "Menstrual cramps"),
                new("Respiratory", "Cough", "Sore throat", "Runny nose", "Congestion", "Sneezing", "Shortness of breath", "Wheezing"),
                new("Digestive", "Nausea", "Vomiting", "Diarrhea", "Constipation", "Bloating", "Heartburn"),
                new("Mental & sleep", "Anxiety", "Low mood", "Irritability", "Brain fog", "Insomnia", "Poor sleep"),
                new("Skin & eyes", "Rash", "Itching", "Hives", "Dry skin", "Itchy eyes"),
            },
        },
        new()
        {
            Id = "vital", Title = "Vitals & measurements",
            NameHint = "Choose or type a measurement…", DetailHint = "Value (e.g. 120/80)",
            EmptyText = "No measurements logged.",
            Accent = B("#22A06B"), Soft = B("#E3F5EC"), Ink = B("#12704A"),
            Groups = new CatalogGroup[]
            {
                new("Vitals", "Blood pressure", "Heart rate", "Temperature", "Oxygen saturation (SpO2)", "Respiratory rate"),
                new("Body", "Weight", "Blood glucose", "Waist"),
                new("Lifestyle", "Sleep (hours)", "Water (glasses)", "Steps", "Exercise (minutes)", "Caffeine", "Alcohol"),
            },
        },
        new()
        {
            Id = "appointment", Title = "Appointments",
            NameHint = "Choose or type an appointment…", DetailHint = "Where / who",
            EmptyText = "No appointments.",
            Accent = B("#3478F6"), Soft = B("#E8F0FE"), Ink = B("#1A4FAF"),
            Groups = new CatalogGroup[]
            {
                new("Visits", "GP / family doctor", "Specialist", "Dentist", "Eye doctor", "Dermatologist", "Therapist", "Physiotherapy"),
                new("Tests & procedures", "Blood test", "X-ray", "MRI / CT scan", "Vaccination", "Health check-up"),
                new("Other", "Pharmacy pickup", "Telehealth call"),
            },
        },
    };

    public static CategoryDef? Find(string id) => Categories.FirstOrDefault(c => c.Id == id);
}
