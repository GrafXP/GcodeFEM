using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Study;
using GcodeFem.Core.Visual;
using Axis = GcodeFem.Core.Fem.Axis;

namespace GcodeFem.App;

/// <summary>A load case in the list. What each load carries in it is kept with the interfaces.</summary>
public sealed partial class LoadCaseItem(string name) : ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; } = name;
}

/// <summary>
/// An interface as the editor shows it: its name, kind and faces, and its value in the load case
/// that is selected. The value is edited as text, and kept per load case.
/// A force is fixed to the part and kept in the part's own directions, but it is shown and typed
/// in the directions of the view: those of the print as the part is turned now, Z up from the bed.
/// </summary>
public sealed partial class InterfaceItem : ObservableObject
{
    readonly Dictionary<LoadCaseItem, LoadValue> values = [];
    LoadCaseItem? shown;
    Matrix4x4 toPrint = Matrix4x4.Identity; // part frame → the print frame on screen
    bool refreshing;

    public InterfaceItem(string name, InterfaceKind kind) => (Name, Kind) = (name, kind);

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoad), nameof(IsForce), nameof(IsBoltHole), nameof(KindText), nameof(Swatch), nameof(ValueLabel), nameof(ValueHint))]
    public partial InterfaceKind Kind { get; set; }

    /// <summary>For a bolt hole: held along the hole as well.</summary>
    [ObservableProperty]
    public partial bool Axial { get; set; } = true;

    /// <summary>For a force: given as a push along the face's normal instead of as X, Y, Z.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValueLabel), nameof(ValueHint))]
    public partial bool AlongNormal { get; set; }

    /// <summary>The value in the load case shown, as typed.</summary>
    [ObservableProperty]
    public partial string ValueText { get; set; } = "";

    /// <summary>Why <see cref="ValueText"/> could not be read; the last good value stays in force.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValueError))]
    public partial string ValueError { get; set; } = "";

    [ObservableProperty]
    public partial string FacesText { get; set; } = "No faces picked yet";

    /// <summary>The model's triangles that make up the face, ascending.</summary>
    public int[] Triangles { get; private set; } = [];

    public bool IsLoad => Kind.IsLoad();
    public bool IsForce => Kind == InterfaceKind.Force;
    public bool IsBoltHole => Kind == InterfaceKind.BoltHole;
    public bool HasValueError => ValueError.Length > 0;
    public Brush Swatch => Swatches.Frozen(IsLoad ? InterfaceGlyphs.Load : InterfaceGlyphs.Mount);

    public string KindText => Kind switch
    {
        InterfaceKind.Fixed => "fixed",
        InterfaceKind.Sliding => "sliding",
        InterfaceKind.BoltHole => "bolt hole",
        InterfaceKind.Force => "force",
        InterfaceKind.Pressure => "pressure",
        _ => "bearing load",
    };

    public string ValueLabel => Kind switch
    {
        InterfaceKind.Pressure => "MPa",
        InterfaceKind.Force when AlongNormal => "Push, N",
        _ => "X, Y, Z, N",
    };

    public string ValueHint => Kind switch
    {
        InterfaceKind.Pressure => "Pressure onto the face in MPa (N/mm²). A negative value pulls.",
        InterfaceKind.Force when AlongNormal => "Newtons pushing onto the face along its normal. A negative value pulls.",
        InterfaceKind.Bearing => "Newtons along X, Y and Z as the view shows them now: Z is up from the bed. The pin presses on the side of the hole it pushes against; " +
                                 "what there is of the force along the hole is left out. " + Attached,
        _ => "Newtons along X, Y and Z as the view shows them now: Z is up from the bed. The force is spread evenly over the face. " + Attached,
    };

    const string Attached = "The force is fixed to the part: turn the part, and the force turns with it and these numbers change to match.";

    /// <summary>The value in a load case; a force in it is in the part's own directions.</summary>
    public LoadValue Value(LoadCaseItem loadCase) => values.GetValueOrDefault(loadCase) ?? new LoadValue();

    public void SetValue(LoadCaseItem loadCase, LoadValue value)
    {
        values[loadCase] = value;
        if (ReferenceEquals(loadCase, shown)) Refresh();
    }

    public void Forget(LoadCaseItem loadCase) => values.Remove(loadCase);

    /// <summary>
    /// Makes <paramref name="loadCase"/> the one whose value is shown and edited, with a force given
    /// in the print's directions for a part turned by <paramref name="partToPrint"/>.
    /// </summary>
    public void Show(LoadCaseItem? loadCase, Matrix4x4 partToPrint)
    {
        (shown, toPrint) = (loadCase, partToPrint);
        Refresh();
    }

    public void SetFaces(int[] triangles, TriangleMesh model)
    {
        Triangles = triangles;
        FacesText = FaceDescription.Of(model, triangles);
    }

    public PartInterface ToCore() => new() { Name = Name.Trim(), Kind = Kind, Triangles = Triangles, Axial = Axial };

    partial void OnKindChanged(InterfaceKind value) => Refresh();

    partial void OnAlongNormalChanged(bool value)
    {
        if (refreshing || shown is null) return;
        // Switching to a push starts from the size of the force that was there; switching back leaves the force as it was.
        var current = Value(shown);
        values[shown] = current with { NormalForce = value ? current.NormalForce ?? current.Force.Length() : null };
        Refresh();
    }

    partial void OnValueTextChanged(string value)
    {
        if (refreshing || shown is null) return;
        var current = Value(shown);
        try
        {
            values[shown] = Kind switch
            {
                InterfaceKind.Pressure => current with { Pressure = Number(value) },
                InterfaceKind.Force when AlongNormal => current with { NormalForce = Number(value) },
                InterfaceKind.Force => current with { Force = ToPart(MainViewModel.ParseTriple(value)), NormalForce = null },
                InterfaceKind.Bearing => current with { Force = ToPart(MainViewModel.ParseTriple(value)) },
                _ => current,
            };
            ValueError = "";
        }
        catch (FormatException ex)
        {
            ValueError = ex.Message;
        }
    }

    /// <summary>A force as typed, in the print's directions → in the part's own. A rotation is undone by its transpose.</summary>
    Vector3 ToPart(Vector3 inPrint) => Vector3.TransformNormal(inPrint, Matrix4x4.Transpose(toPrint));

    /// <summary>Brings the text and the tick box in line with the value held for the load case shown.</summary>
    void Refresh()
    {
        var value = shown is null ? new LoadValue() : Value(shown);
        var force = Vector3.TransformNormal(value.Force, toPrint);
        refreshing = true;
        AlongNormal = Kind == InterfaceKind.Force && value.NormalForce is not null;
        ValueText = Kind switch
        {
            InterfaceKind.Pressure => Text(value.Pressure),
            InterfaceKind.Force when value.NormalForce is { } push => Text(push),
            InterfaceKind.Force or InterfaceKind.Bearing => $"{Text(force.X)}, {Text(force.Y)}, {Text(force.Z)}",
            _ => "",
        };
        ValueError = "";
        refreshing = false;
    }

    /// <summary>Four decimals: what turning a force there and back leaves over is rounded away, and never shows as "-0".</summary>
    static string Text(float value)
    {
        var text = value.ToString("0.####", CultureInfo.InvariantCulture);
        return text == "-0" ? "0" : text;
    }

    static float Number(string text) =>
        float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : throw new FormatException($"Expected a number like 12.5 but got '{text}'.");
}

/// <summary>The part of the view model that deals with the study: interfaces, load cases, picking faces, and the study file.</summary>
public sealed partial class MainViewModel
{
    MeshTopology? topology; // of Part; built when the first face is picked

    /// <summary>The faces where the part is held or loaded. They belong to the model, whichever way it is turned.</summary>
    public ObservableCollection<InterfaceItem> Interfaces { get; } = [];

    public ObservableCollection<LoadCaseItem> LoadCases { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedInterface))]
    [NotifyCanExecuteChangedFor(nameof(RemoveInterfaceCommand), nameof(ClearFacesCommand))]
    public partial InterfaceItem? SelectedInterface { get; set; }

    public bool HasSelectedInterface => SelectedInterface is not null;

    /// <summary>The load case whose values are shown, and which Solve solves.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveLoadCaseCommand))]
    public partial LoadCaseItem? SelectedLoadCase { get; set; }

    /// <summary>While on, a click on the model adds the face under it to the selected interface, or takes it off again.</summary>
    [ObservableProperty]
    public partial bool IsPicking { get; set; }

    /// <summary>A picked face runs on across its facets for as long as neighbours differ by less than this many degrees.</summary>
    [ObservableProperty]
    public partial double PickAngle { get; set; } = 20;

    /// <summary>The model's file, full path.</summary>
    public string? ModelPath { get; private set; }

    /// <summary>The study file last opened or saved, if any.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    public partial string? StudyPath { get; set; }

    public string Title => StudyPath is null ? "GcodeFem" : $"GcodeFem - {Path.GetFileName(StudyPath)}";

    public IReadOnlyList<Choice<InterfaceKind>> Kinds { get; } =
    [
        new(InterfaceKind.Fixed, "Mount: fixed"),
        new(InterfaceKind.Sliding, "Mount: sliding (held against the face only)"),
        new(InterfaceKind.BoltHole, "Mount: bolt hole"),
        new(InterfaceKind.Force, "Load: force"),
        new(InterfaceKind.Pressure, "Load: pressure"),
        new(InterfaceKind.Bearing, "Load: bearing load of a pin in a hole"),
    ];

    public IReadOnlyList<Choice<CellColouring>> CellColourings { get; } =
    [
        new(CellColouring.Fill, "Fill"),
        new(CellColouring.Interfaces, "Interfaces, as the solver takes them"),
    ];

    [ObservableProperty]
    public partial CellColouring CellColouring { get; set; }

    partial void OnCellColouringChanged(CellColouring value) => Redraw();

    partial void OnSelectedLoadCaseChanged(LoadCaseItem? value)
    {
        foreach (var item in Interfaces) item.Show(value, Rotation);
        RedrawInterfaces();
    }

    partial void OnSelectedInterfaceChanged(InterfaceItem? value)
    {
        if (value is null) IsPicking = false;
        RedrawInterfaces();
    }

    partial void OnIsPickingChanged(bool value)
    {
        if (!value) return;
        if (SelectedInterface is not { } item || Part is null)
        {
            IsPicking = false;
            return;
        }
        IsPlacing = false; // a click means one thing at a time
        View = ViewMode.Model; // faces are picked on the model
        Status = $"Click a face of the model to add it to '{item.Name}'; click it again to take it off.";
    }

    /// <summary>While on, the next click on the model lays the face under it on the bed.</summary>
    [ObservableProperty]
    public partial bool IsPlacing { get; set; }

    partial void OnIsPlacingChanged(bool value)
    {
        if (!value) return;
        if (Part is null)
        {
            IsPlacing = false;
            return;
        }
        IsPicking = false;
        View = ViewMode.Model;
        Status = "Click the face of the model that is to lie on the bed.";
    }

    /// <summary>
    /// Turns the part so that the face a ray meets lies on the bed, by the shortest way from how
    /// it lies now. The interfaces are faces of the part and turn with it.
    /// </summary>
    void LayOnBed(TriangleMesh part, Vector3 origin, Vector3 direction)
    {
        if (MeshPicker.Pick(part.Transformed(Rotation), origin, direction) is not { } hit) return;
        var angles = Orientation.LayFlat(Rotation, FaceRegions.FlatNormal(part, topology ??= MeshTopology.Of(part), hit.Triangle));
        IsPlacing = false;
        // Through double, 35.2644 would show as 35.26440048217773.
        (RotationX, RotationY, RotationZ) = (Math.Round(angles.X, 4), Math.Round(angles.Y, 4), Math.Round(angles.Z, 4));
        Status = $"Laid that face on the bed: rotation {RotationX:0.####}, {RotationY:0.####}, {RotationZ:0.####}." + (HasPrint ? " Slice again to print it this way." : "");
    }

    // ---- interfaces ----

    [RelayCommand]
    void AddMount() => AddInterface(InterfaceKind.Fixed);

    [RelayCommand]
    void AddLoad() => AddInterface(InterfaceKind.Force);

    /// <summary>A new interface of that kind, selected and ready to have its faces picked.</summary>
    public InterfaceItem? AddInterface(InterfaceKind kind)
    {
        if (Part is null) return null;
        var stem = kind.IsLoad() ? "Load" : "Mount";
        var number = 1;
        while (Interfaces.Any(i => string.Equals(i.Name, $"{stem} {number}", StringComparison.OrdinalIgnoreCase))) number++;
        var item = Insert(new InterfaceItem($"{stem} {number}", kind));
        SelectedInterface = item;
        IsPicking = true;
        return item;
    }

    InterfaceItem Insert(InterfaceItem item)
    {
        item.Show(SelectedLoadCase, Rotation);
        item.PropertyChanged += OnInterfaceChanged;
        Interfaces.Add(item);
        return item;
    }

    [RelayCommand(CanExecute = nameof(HasSelectedInterface))]
    void RemoveInterface()
    {
        if (SelectedInterface is not { } item) return;
        var at = Interfaces.IndexOf(item);
        item.PropertyChanged -= OnInterfaceChanged;
        Interfaces.Remove(item);
        SelectedInterface = Interfaces.Count == 0 ? null : Interfaces[Math.Min(at, Interfaces.Count - 1)];
        RedrawInterfaces();
    }

    [RelayCommand(CanExecute = nameof(HasSelectedInterface))]
    void ClearFaces()
    {
        if (SelectedInterface is not { } item || Part is not { } part) return;
        item.SetFaces([], part);
        RedrawInterfaces();
    }

    void OnInterfaceChanged(object? sender, PropertyChangedEventArgs e)
    {
        // What changes the picture: the kind (its colour and marker) and the value (its arrow).
        if (e.PropertyName is nameof(InterfaceItem.Kind) or nameof(InterfaceItem.ValueText) or nameof(InterfaceItem.AlongNormal)) RedrawInterfaces();
    }

    /// <summary>
    /// A click on the model view, as a ray in the print frame. While placing, the face it meets is
    /// laid on the bed; while picking, it is added to the selected interface, or taken off if it
    /// was part of it.
    /// </summary>
    public void Pick(Vector3 origin, Vector3 direction)
    {
        if (View != ViewMode.Model || Part is not { } part) return;
        if (IsPlacing)
        {
            LayOnBed(part, origin, direction);
            return;
        }
        if (!IsPicking || SelectedInterface is not { } item) return;
        // The model is shown turned for printing; its triangles are numbered as in the part.
        if (MeshPicker.Pick(part.Transformed(Rotation), origin, direction) is not { } hit) return;

        var face = FaceRegions.Grow(part, topology ??= MeshTopology.Of(part), hit.Triangle, (float)PickAngle);
        var picked = new HashSet<int>(item.Triangles);
        var taken = picked.Contains(hit.Triangle);
        if (taken) picked.ExceptWith(face);
        else picked.UnionWith(face);
        item.SetFaces([.. picked.Order()], part);
        Status = $"{(taken ? "Took" : "Added")} {FaceDescription.Of(part, face).ToLowerInvariant()} {(taken ? "off" : "to")} '{item.Name}'.";
        RedrawInterfaces();
    }

    /// <summary>
    /// The quick load case of the command line: the part's outer face on one side of the print is
    /// fixed and the opposite one carries <paramref name="force"/> (N, in the print's directions).
    /// </summary>
    public void ClampAndPush(Axis axis, bool max, Vector3 force)
    {
        if (Part is not { } part || SelectedLoadCase is not { } loadCase) return;
        var print = part.Transformed(Rotation);
        int[] Side(bool high) => FaceRegions.OnPlane(print, LoadCase.Unit(axis), LoadCase.Component(high ? print.Bounds.Max : print.Bounds.Min, axis));

        foreach (var item in Interfaces) item.PropertyChanged -= OnInterfaceChanged;
        Interfaces.Clear();
        var clamp = Insert(new InterfaceItem("Clamp", InterfaceKind.Fixed));
        clamp.SetFaces(Side(max), part);
        var push = Insert(new InterfaceItem("Push", InterfaceKind.Force));
        push.SetFaces(Side(!max), part);
        // Forces are kept in the model's own directions.
        push.SetValue(loadCase, new LoadValue(Vector3.TransformNormal(force, Matrix4x4.Transpose(Rotation))));
        SelectedInterface = push;
        RedrawInterfaces();
    }

    // ---- load cases ----

    /// <summary>A new load case that starts with the values of the one shown, to be changed from there.</summary>
    [RelayCommand]
    void AddLoadCase()
    {
        var number = LoadCases.Count + 1;
        while (LoadCases.Any(c => string.Equals(c.Name, $"Load case {number}", StringComparison.OrdinalIgnoreCase))) number++;
        var added = new LoadCaseItem($"Load case {number}");
        if (SelectedLoadCase is { } from)
            foreach (var item in Interfaces) item.SetValue(added, item.Value(from));
        LoadCases.Add(added);
        SelectedLoadCase = added;
    }

    bool CanRemoveLoadCase() => LoadCases.Count > 1 && SelectedLoadCase is not null;

    [RelayCommand(CanExecute = nameof(CanRemoveLoadCase))]
    void RemoveLoadCase()
    {
        if (SelectedLoadCase is not { } removed || LoadCases.Count < 2) return;
        var at = LoadCases.IndexOf(removed);
        SelectedLoadCase = LoadCases[at == 0 ? 1 : at - 1];
        LoadCases.Remove(removed);
        foreach (var item in Interfaces) item.Forget(removed);
        RemoveLoadCaseCommand.NotifyCanExecuteChanged();
    }

    // ---- the study as a whole ----

    void ResetStudy()
    {
        foreach (var item in Interfaces) item.PropertyChanged -= OnInterfaceChanged;
        Interfaces.Clear();
        SelectedInterface = null;
        IsPicking = IsPlacing = false;
        LoadCases.Clear();
        LoadCases.Add(new LoadCaseItem("Load case 1"));
        SelectedLoadCase = LoadCases[0];
        topology = null;
        StudyPath = null;
    }

    /// <summary>What the window holds, as a study; the load cases come in the order of <see cref="LoadCases"/>.</summary>
    PartStudy ToStudy()
    {
        var study = new PartStudy
        {
            ModelPath = ModelPath ?? "",
            GeometryHash = Part?.GeometryHash() ?? "",
            Rotation = new Vector3((float)RotationX, (float)RotationY, (float)RotationZ),
            Process = Process,
            Filament = Filament,
            MeshLevel = Level.Value,
        };
        var items = Interfaces.Select(item => (Item: item, Core: item.ToCore())).ToList();
        study.Interfaces.AddRange(items.Select(pair => pair.Core));
        foreach (var loadCase in LoadCases)
        {
            var saved = new StudyLoadCase { Name = loadCase.Name.Trim() };
            foreach (var (item, core) in items)
                if (core.Kind.IsLoad()) saved.Loads[core] = item.Value(loadCase);
            study.LoadCases.Add(saved);
        }
        return study;
    }

    public void SaveStudy(string path)
    {
        if (Part is null || ModelPath is null) throw new InvalidOperationException("There is no model to save a study of.");
        StudyFile.Save(ToStudy(), path);
        StudyPath = path;
        Status = $"Study saved to {path}.";
    }

    /// <summary>Opens a study and its model. If the model has changed since, the interfaces keep everything but their faces.</summary>
    public void OpenStudy(string path)
    {
        var study = StudyFile.Load(path);
        LoadModel(study.ModelPath);
        var part = Part!;
        var fits = study.Fits(part);
        if (!fits) study.DropFaces();

        (RotationX, RotationY, RotationZ) = (study.Rotation.X, study.Rotation.Y, study.Rotation.Z);
        Process = study.Process ?? Process;
        Filament = study.Filament ?? Filament;
        Level = Levels.FirstOrDefault(level => level.Value == study.MeshLevel) ?? Levels[0];

        LoadCases.Clear();
        foreach (var loadCase in study.LoadCases) LoadCases.Add(new LoadCaseItem(loadCase.Name));
        SelectedLoadCase = LoadCases[0];
        foreach (var saved in study.Interfaces)
        {
            var item = new InterfaceItem(saved.Name, saved.Kind) { Axial = saved.Axial };
            item.SetFaces(saved.Triangles, part);
            for (var n = 0; n < LoadCases.Count; n++) item.SetValue(LoadCases[n], study.LoadCases[n].Of(saved));
            Insert(item);
        }
        SelectedInterface = Interfaces.FirstOrDefault();
        StudyPath = path;
        RemoveLoadCaseCommand.NotifyCanExecuteChanged();
        RedrawInterfaces();

        if (fits) Status = $"Opened {Path.GetFileName(path)}: {Interfaces.Count} interfaces, {LoadCases.Count} load cases. Slice it to solve.";
        else
        {
            Status = "The model has changed since this study was saved: pick the faces of its interfaces again.";
            Failed?.Invoke("The model has changed", $"{Path.GetFileName(study.ModelPath)} is not the model this study's faces were picked on. " +
                                                     "The interfaces and their values were kept, but their faces have to be picked again.");
        }
    }

    IReadOnlyList<InterfaceMark> CurrentMarks() =>
        [.. Interfaces.Select(item => new InterfaceMark(item.Kind, item.Triangles, SelectedLoadCase is { } shown ? item.Value(shown) : new LoadValue(), ReferenceEquals(item, SelectedInterface)))];

    /// <summary>Redraws if the view on screen shows the interfaces.</summary>
    void RedrawInterfaces()
    {
        if (View == ViewMode.Model || (View == ViewMode.Cells && CellColouring == CellColouring.Interfaces)) Redraw();
    }
}
