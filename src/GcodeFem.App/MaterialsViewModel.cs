using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GcodeFem.Core.Materials;
using GcodeFem.Core.Slicing;

namespace GcodeFem.App;

/// <summary>A material in the list of the library.</summary>
public sealed record MaterialEntry(Material Material)
{
    public string Name => Material.Name;

    public string Note
    {
        get
        {
            var parts = new List<string>();
            if (Material.Type.Length > 0) parts.Add(Material.Type);
            if (Material.Number(MaterialProperty.YoungsModulusXY) is { } e) parts.Add($"E {e:0} MPa");
            if (Material.StandsInForType) parts.Add($"stands in for {Material.Type}");
            if (Material.Lacking().Count > 0) parts.Add("incomplete");
            return string.Join(", ", parts);
        }
    }
}

/// <summary>One property of the material in the form: its value and where the value comes from, all as typed.</summary>
public sealed partial class ValueRow(MaterialProperty property) : ObservableObject
{
    public MaterialProperty Property => property;
    public string Label => property.Label();
    public string Unit => property.Unit();

    [ObservableProperty]
    public partial string Value { get; set; } = "";

    /// <summary>The ± a data sheet gives with the value.</summary>
    [ObservableProperty]
    public partial string Spread { get; set; } = "";

    [ObservableProperty]
    public partial string Method { get; set; } = "";

    /// <summary>"tds" for the data sheet, or a few words saying where the number is from.</summary>
    [ObservableProperty]
    public partial string Source { get; set; } = "";

    [ObservableProperty]
    public partial string Page { get; set; } = "";

    /// <summary>Why the number was taken without a source; filled in instead of <see cref="Source"/>.</summary>
    [ObservableProperty]
    public partial string Assumed { get; set; } = "";

    public void Show(MaterialValue? value)
    {
        Value = value?.Value.ToString(CultureInfo.InvariantCulture) ?? "";
        Spread = value?.Spread?.ToString(CultureInfo.InvariantCulture) ?? "";
        Method = value?.Method ?? "";
        Source = value?.Source ?? "";
        Page = value?.Page?.ToString(CultureInfo.InvariantCulture) ?? "";
        Assumed = value?.Assumed ?? "";
    }

    /// <summary>The row as a value; null if no value is given, which is how a property is left out.</summary>
    public MaterialValue? Read()
    {
        if (string.IsNullOrWhiteSpace(Value)) return null;
        return new MaterialValue(
            Number(Value, "value") ?? 0,
            Number(Spread, "±"),
            Text(Method), Text(Source),
            string.IsNullOrWhiteSpace(Page) ? null : int.TryParse(Page.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var page) ? page : throw new FormatException($"{Label}: the page '{Page}' is not a number."),
            Text(Assumed));
    }

    double? Number(string text, string what) =>
        string.IsNullOrWhiteSpace(text) ? null
        : double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number
        : throw new FormatException($"{Label}: the {what} '{text}' is not a number (write 12.5, not 12,5).");

    static string? Text(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

/// <summary>
/// The materials window: the library's materials, and a form for one of them. A data sheet fills
/// the form in as far as its layout is known; the user looks the values over, completes them and
/// saves. Nothing reaches the library before that.
/// </summary>
public sealed partial class MaterialsViewModel : ObservableObject
{
    MaterialLibrary library;
    readonly PresetLibrary? presets;
    Material shown = new(); // what the form was filled from: it keeps the file and the documents
    string? sheet; // a data sheet just read in, to be copied into the library when the material is saved
    string sheetVersion = "";
    bool filling;

    public MaterialsViewModel(MaterialLibrary library, PresetLibrary? presets)
    {
        (this.library, this.presets) = (library, presets);
        foreach (var property in MaterialProperties.All)
        {
            var row = new ValueRow(property);
            row.PropertyChanged += OnEdited;
            Rows.Add(row);
        }
        Refresh(select: null);
        Message = library.Problems.Count > 0 ? string.Join("\n", library.Problems) : "";
        MessageIsError = library.Problems.Count > 0;
    }

    public string Directory => library.Directory;

    public ObservableCollection<MaterialEntry> Materials { get; } = [];

    [ObservableProperty]
    public partial MaterialEntry? Selected { get; set; }

    /// <summary>One row per property a material can have; a row without a value is a property the material does not give.</summary>
    public ObservableCollection<ValueRow> Rows { get; } = [];

    [ObservableProperty]
    public partial string Name { get; set; } = "";

    [ObservableProperty]
    public partial string Vendor { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StandInText))]
    public partial string Type { get; set; } = "";

    /// <summary>The filament presets the material is for, separated by ";".</summary>
    [ObservableProperty]
    public partial string Filaments { get; set; } = "";

    [ObservableProperty]
    public partial bool StandsInForType { get; set; }

    public string StandInText => $"Also use it for every {(Type.Trim().Length > 0 ? Type.Trim() : "…")} filament that has no material of its own";

    [ObservableProperty]
    public partial string SpecimenNote { get; set; } = "";

    /// <summary>The source document, in a line.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDocument))]
    public partial string DocumentText { get; set; } = "";

    public bool HasDocument => DocumentPath is not null;

    [ObservableProperty]
    public partial string Message { get; set; } = "";

    [ObservableProperty]
    public partial bool MessageIsError { get; set; }

    /// <summary>The form holds changes that are not saved. Until they are saved or discarded, no other material can be picked.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUnchanged))]
    [NotifyCanExecuteChangedFor(nameof(DiscardCommand))]
    public partial bool IsChanged { get; set; }

    public bool IsUnchanged => !IsChanged;

    /// <summary>The file of the data sheet the form's values point to, if it is on this disk.</summary>
    public string? DocumentPath =>
        sheet ?? shown.Documents.Select(document => MaterialLibrary.DocumentPath(shown, document)).FirstOrDefault(path => path is not null && File.Exists(path));

    partial void OnSelectedChanged(MaterialEntry? value)
    {
        if (value is not null) Show(value.Material, importedFrom: null);
    }

    partial void OnNameChanged(string value) => OnEdited();
    partial void OnVendorChanged(string value) => OnEdited();
    partial void OnTypeChanged(string value) => OnEdited();
    partial void OnFilamentsChanged(string value) => OnEdited();
    partial void OnStandsInForTypeChanged(bool value) => OnEdited();
    partial void OnSpecimenNoteChanged(string value) => OnEdited();

    void OnEdited(object? sender = null, PropertyChangedEventArgs? e = null)
    {
        if (!filling) IsChanged = true;
    }

    /// <summary>An empty form, for a filament whose sheet has to be typed in.</summary>
    [RelayCommand]
    void New()
    {
        Selected = null;
        var material = new Material { Name = "New material" };
        Show(material, importedFrom: null);
        IsChanged = true;
        Say("Give every value its source: the document and the page, or the reason it is assumed. Leave out what you do not know.");
    }

    /// <summary>Reads a data sheet into the form. The material is saved only when the user says so.</summary>
    public void Import(string pdf)
    {
        var import = TdsImporter.Import(pdf);
        var material = import.Material;
        var notes = new List<string>();
        if (import.Template is null)
        {
            // Entered by hand, with the sheet alongside: it still becomes the material's document.
            material.Documents.Add(new MaterialDocument(TdsImporter.DocumentId, material.Name));
            notes.Add("No template knows this sheet's layout: enter its values by hand, with \"tds\" and the page as the source of each. " +
                      "\"Open the data sheet\" shows it alongside.");
        }
        else
        {
            if (presets is not null) notes.AddRange(BambuTds.AlignWithStudio(material, presets));
            notes.Insert(0, $"Read as a {import.Template} data sheet{(import.Version.Length > 0 ? " " + import.Version : "")}: {material.Values.Count(v => !v.Value.IsAssumed)} values. " +
                            "Look them over against the sheet, then save.");
            if (import.Missing.Count > 0) notes.Add("Not found in the sheet: " + string.Join("; ", import.Missing.Select(p => p.Label())) + ".");
            notes.AddRange(import.Remarks);
        }
        // A newer sheet of a filament that is in the library takes that material's place.
        if (library.Find(material.Name) is { Path: { } existing } old)
        {
            material.Path = existing;
            material.StandsInForType = old.StandsInForType;
            notes.Add($"Saving replaces the material '{old.Name}' that is in the library.");
        }

        Selected = null;
        Show(material, pdf);
        sheetVersion = import.Version;
        IsChanged = true;
        Say(string.Join("\n", notes));
    }

    [RelayCommand]
    void Save()
    {
        try
        {
            var material = new Material
            {
                Name = Name.Trim(),
                Vendor = Vendor.Trim(),
                Type = Type.Trim(),
                StandsInForType = StandsInForType,
                SpecimenNote = SpecimenNote.Trim(),
                Path = shown.Path,
            };
            material.Filaments.AddRange(Filaments.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
            material.Documents.AddRange(shown.Documents);
            foreach (var row in Rows)
                if (row.Read() is { } value) material.Values[row.Property] = value;

            if (material.Problems() is { Count: > 0 } problems) throw new InvalidOperationException(string.Join("\n", problems));
            if (material.Path is null && library.Find(material.Name) is not null)
                throw new InvalidOperationException($"There is a material '{material.Name}' already. Give this one another name.");
            if (sheet is not null && material.Documents.FindIndex(d => d.Id == TdsImporter.DocumentId) is >= 0 and var index)
            {
                var copy = $"{MaterialLibrary.Slug(material.Name)}-tds{(sheetVersion.Length > 0 ? "-" + sheetVersion.ToLowerInvariant() : "")}.pdf";
                material.Documents[index] = library.Attach(sheet, TdsImporter.DocumentId, material.Documents[index].Title, material.Documents[index].Url, copy);
            }

            var path = library.Save(material);
            Refresh(material.Name);
            var gaps = material.Lacking().Count > 0 ? $" It gives no {string.Join(" and no ", material.Lacking().Select(p => p.Label()))}, so it cannot be solved with yet." : "";
            Say($"Saved to {path}.{gaps}");
        }
        catch (Exception ex) when (MainViewModel.IsExpected(ex))
        {
            Say(ex.Message, isError: true);
        }
    }

    [RelayCommand(CanExecute = nameof(IsChanged))]
    void Discard()
    {
        var back = shown.Path is { } path ? library.Materials.FirstOrDefault(m => m.Path == path)?.Name : null;
        Refresh(back);
        Say("");
    }

    /// <summary>Reads the library again and shows the material of that name, or the first one.</summary>
    void Refresh(string? select)
    {
        library = MaterialLibrary.Load(library.Directory);
        Materials.Clear();
        foreach (var material in library.Materials) Materials.Add(new MaterialEntry(material));
        var entry = Materials.FirstOrDefault(m => string.Equals(m.Name, select, StringComparison.OrdinalIgnoreCase)) ?? Materials.FirstOrDefault();
        Selected = null;
        if (entry is null) Show(new Material(), importedFrom: null);
        Selected = entry;
        IsChanged = false;
    }

    void Show(Material material, string? importedFrom)
    {
        filling = true;
        (shown, sheet, sheetVersion) = (material, importedFrom, "");
        Name = material.Name;
        Vendor = material.Vendor;
        Type = material.Type;
        Filaments = string.Join("; ", material.Filaments);
        StandsInForType = material.StandsInForType;
        SpecimenNote = material.SpecimenNote;
        foreach (var row in Rows) row.Show(material.Of(row.Property));
        DocumentText = material.Documents.FirstOrDefault() is { } document
            ? $"\"{document.Id}\" is {document.Title}{((importedFrom ?? document.File) is { } file ? $" ({Path.GetFileName(file)})" : "")}"
            : "No source document. \"Import a data sheet…\" reads one in.";
        OnPropertyChanged(nameof(HasDocument));
        filling = false;
        IsChanged = false;
    }

    void Say(string message, bool isError = false) => (Message, MessageIsError) = (message, isError);
}
