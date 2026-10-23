using GcodeFem.Core.Fem;

namespace GcodeFem.Core.Materials;

/// <summary>
/// What a material file can say about a filament. "XY" is measured in the plane of the layers
/// (a specimen printed lying flat), "Z" across them (printed standing up). Each has one unit
/// (<see cref="MaterialProperties.Unit"/>).
/// </summary>
public enum MaterialProperty
{
    Density,
    YoungsModulusXY,
    YoungsModulusZ,
    TensileStrengthXY,
    TensileStrengthZ,
    ElongationAtBreakXY,
    ElongationAtBreakZ,
    BendingModulusXY,
    BendingModulusZ,
    BendingStrengthXY,
    BendingStrengthZ,
    ImpactStrengthXY,
    NotchedImpactStrengthXY,
    ImpactStrengthZ,
    PoissonRatio,
    GlassTransitionTemperature,
    NozzleTemperatureMin,
    NozzleTemperatureMax,

    /// <summary>How the specimens behind the numbers above were printed: the Z values hold for these conditions.</summary>
    SpecimenNozzleTemperature,
    SpecimenBedTemperature,
    SpecimenPrintSpeed,
}

public static class MaterialProperties
{
    public static IReadOnlyList<MaterialProperty> All { get; } = Enum.GetValues<MaterialProperty>();

    public static string Unit(this MaterialProperty property) => property switch
    {
        MaterialProperty.Density => "g/cm³",
        MaterialProperty.ElongationAtBreakXY or MaterialProperty.ElongationAtBreakZ => "%",
        MaterialProperty.ImpactStrengthXY or MaterialProperty.NotchedImpactStrengthXY or MaterialProperty.ImpactStrengthZ => "kJ/m²",
        MaterialProperty.PoissonRatio => "",
        MaterialProperty.GlassTransitionTemperature or MaterialProperty.NozzleTemperatureMin or MaterialProperty.NozzleTemperatureMax
            or MaterialProperty.SpecimenNozzleTemperature or MaterialProperty.SpecimenBedTemperature => "°C",
        MaterialProperty.SpecimenPrintSpeed => "mm/s",
        _ => "MPa",
    };

    public static string Label(this MaterialProperty property) => property switch
    {
        MaterialProperty.Density => "Density",
        MaterialProperty.YoungsModulusXY => "Young's modulus, in the plane",
        MaterialProperty.YoungsModulusZ => "Young's modulus, across the layers",
        MaterialProperty.TensileStrengthXY => "Tensile strength, in the plane",
        MaterialProperty.TensileStrengthZ => "Tensile strength, across the layers",
        MaterialProperty.ElongationAtBreakXY => "Elongation at break, in the plane",
        MaterialProperty.ElongationAtBreakZ => "Elongation at break, across the layers",
        MaterialProperty.BendingModulusXY => "Bending modulus, in the plane",
        MaterialProperty.BendingModulusZ => "Bending modulus, across the layers",
        MaterialProperty.BendingStrengthXY => "Bending strength, in the plane",
        MaterialProperty.BendingStrengthZ => "Bending strength, across the layers",
        MaterialProperty.ImpactStrengthXY => "Impact strength, in the plane",
        MaterialProperty.NotchedImpactStrengthXY => "Impact strength, in the plane, notched",
        MaterialProperty.ImpactStrengthZ => "Impact strength, across the layers",
        MaterialProperty.PoissonRatio => "Poisson's ratio",
        MaterialProperty.GlassTransitionTemperature => "Glass transition temperature",
        MaterialProperty.NozzleTemperatureMin => "Nozzle temperature, lowest",
        MaterialProperty.NozzleTemperatureMax => "Nozzle temperature, highest",
        MaterialProperty.SpecimenNozzleTemperature => "Specimens: nozzle temperature",
        MaterialProperty.SpecimenBedTemperature => "Specimens: bed temperature",
        _ => "Specimens: printing speed",
    };

    /// <summary>The name in a material file: "youngsModulusXY".</summary>
    public static string Key(this MaterialProperty property)
    {
        var name = property.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    public static bool TryParse(string key, out MaterialProperty property) =>
        Enum.TryParse(key, ignoreCase: true, out property) && Enum.IsDefined(property);
}

/// <summary>
/// One number of a material and where it comes from. It is either taken from somewhere
/// (<paramref name="Source"/>) or assumed (<paramref name="Assumed"/>), never both and never
/// neither, so that measured and guessed numbers cannot mix unseen.
/// </summary>
/// <param name="Spread">The ± a data sheet gives with the value.</param>
/// <param name="Method">The test standard, as the source names it.</param>
/// <param name="Source">The id of one of the material's documents ("tds"), or a few words saying where the number is from.</param>
/// <param name="Page">The page of that document.</param>
/// <param name="Assumed">Why the number was taken without a source.</param>
public sealed record MaterialValue(double Value, double? Spread = null, string? Method = null, string? Source = null, int? Page = null, string? Assumed = null)
{
    public bool IsAssumed => !string.IsNullOrWhiteSpace(Assumed);

    /// <summary>What is wrong with it as a value of <paramref name="property"/>; null if nothing.</summary>
    public string? Problem(MaterialProperty property)
    {
        if (!double.IsFinite(Value)) return "is not a number";
        if (Spread is { } spread && !(spread >= 0)) return "has a negative ±";
        if (IsAssumed == !string.IsNullOrWhiteSpace(Source))
            return IsAssumed ? "names a source and is marked as assumed; it can only be one of the two" : "has no source and is not marked as assumed";
        return property switch
        {
            MaterialProperty.PoissonRatio when Value is < 0 or >= 0.5 => "must be at least 0 and below 0.5",
            MaterialProperty.PoissonRatio or MaterialProperty.GlassTransitionTemperature or MaterialProperty.SpecimenBedTemperature => null,
            _ when Value <= 0 => "must be above zero",
            _ => null,
        };
    }
}

/// <summary>A document that values are taken from, usually the vendor's technical data sheet.</summary>
/// <param name="Id">What the values call it: "tds".</param>
/// <param name="File">The document's file, named from the material file's folder.</param>
/// <param name="Url">Where it was downloaded.</param>
/// <param name="Sha256">Of the file, so that another version of the sheet under the same name is noticed.</param>
public sealed record MaterialDocument(string Id, string Title, string? File = null, string? Url = null, string? Sha256 = null);

/// <summary>
/// A filament as a material: stiffness, strength and the rest of what its data sheet gives, each
/// number with its source. Units: MPa, %, g/cm³, kJ/m², °C, mm/s.
/// </summary>
public sealed class Material
{
    public string Name { get; set; } = "";
    public string Vendor { get; set; } = "";

    /// <summary>Bambu Studio's filament_type: PLA, PETG, PA-CF, …</summary>
    public string Type { get; set; } = "";

    /// <summary>
    /// The filament presets this material is for. A name without a printer ("Bambu PLA Basic")
    /// covers that filament's presets for every printer ("Bambu PLA Basic @BBL X1C"); a name with
    /// one covers that preset alone.
    /// </summary>
    public List<string> Filaments { get; } = [];

    /// <summary>Also used for every filament of its <see cref="Type"/> that has no material of its own, and said so wherever it is.</summary>
    public bool StandsInForType { get; set; }

    public List<MaterialDocument> Documents { get; } = [];

    public SortedDictionary<MaterialProperty, MaterialValue> Values { get; } = [];

    /// <summary>How the specimens behind the values were printed and treated, in the source's words.</summary>
    public string SpecimenNote { get; set; } = "";

    /// <summary>The file it was read from or saved to; null for one that is in no file.</summary>
    public string? Path { get; set; }

    public MaterialValue? Of(MaterialProperty property) => Values.GetValueOrDefault(property);

    public double? Number(MaterialProperty property) => Of(property)?.Value;

    /// <summary>
    /// The material as the solver takes it for now: the same in every direction, with the stiffness
    /// measured in the plane of the layers. The lower stiffness across them comes in with the line
    /// directions (M7).
    /// </summary>
    public IsotropicMaterial Isotropic() =>
        Lacking() is { Count: > 0 } lacking
            ? throw new InvalidDataException($"The material '{Name}' cannot be solved with: it gives no {string.Join(" and no ", lacking.Select(p => p.Label()))}.")
            : new IsotropicMaterial(Values[MaterialProperty.YoungsModulusXY].Value, Values[MaterialProperty.PoissonRatio].Value);

    /// <summary>What the solver needs and the material does not give. A data sheet without them can still be kept: it says what it says.</summary>
    public IReadOnlyList<MaterialProperty> Lacking() =>
        [.. new[] { MaterialProperty.YoungsModulusXY, MaterialProperty.PoissonRatio }.Where(property => !Values.ContainsKey(property))];

    /// <summary>Everything that keeps the material from being saved, in plain words; empty if it is in order.</summary>
    public List<string> Problems()
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(Name)) problems.Add("The material has no name.");
        foreach (var twice in Documents.GroupBy(d => d.Id, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            problems.Add($"Two documents are called '{twice.Key}'.");
        foreach (var (property, value) in Values)
            if (value.Problem(property) is { } problem) problems.Add($"{property.Label()} {problem}.");
        return problems;
    }

    /// <summary>
    /// What is solved with when a filament has no material at all: the stiffness of an ordinary PLA,
    /// every number of it assumed.
    /// </summary>
    public static Material Generic()
    {
        const string why = "No material data for this filament: the value of an ordinary PLA.";
        var pla = IsotropicMaterial.Pla;
        return new Material
        {
            Name = "Generic PLA",
            Type = "PLA",
            Values =
            {
                [MaterialProperty.Density] = new MaterialValue(1.24, Assumed: why),
                [MaterialProperty.YoungsModulusXY] = new MaterialValue(pla.YoungsModulus, Assumed: why),
                [MaterialProperty.YoungsModulusZ] = new MaterialValue(pla.YoungsModulus, Assumed: why),
                [MaterialProperty.PoissonRatio] = new MaterialValue(pla.PoissonRatio, Assumed: why),
            },
        };
    }
}

/// <summary>Numbers that data sheets leave out, by filament type. Whatever is taken from here is marked as assumed.</summary>
public static class TypicalValues
{
    /// <summary>A usual Poisson's ratio for the filament type, and for whom it is usual.</summary>
    public static (double Value, string Of) PoissonRatio(string type)
    {
        var plain = type.ToUpperInvariant();
        if (plain.StartsWith("TPU", StringComparison.Ordinal) || plain.StartsWith("TPE", StringComparison.Ordinal)) return (0.48, "TPU");
        if (plain.StartsWith("PETG", StringComparison.Ordinal) || plain.StartsWith("PCTG", StringComparison.Ordinal)) return (0.38, "PETG");
        if (plain.StartsWith("PLA", StringComparison.Ordinal)) return (0.35, "PLA");
        if (plain.StartsWith("ABS", StringComparison.Ordinal) || plain.StartsWith("ASA", StringComparison.Ordinal)) return (0.35, "ABS and ASA");
        if (plain.StartsWith("PA", StringComparison.Ordinal)) return (0.39, "polyamides");
        if (plain.StartsWith("PC", StringComparison.Ordinal)) return (0.37, "polycarbonate");
        return (0.35, "rigid thermoplastics");
    }
}
