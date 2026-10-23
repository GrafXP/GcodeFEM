using System.Globalization;
using System.Text.RegularExpressions;
using GcodeFem.Core.Slicing;

namespace GcodeFem.Core.Materials;

/// <summary>
/// What a data sheet gave: a material filled in as far as the sheet goes, to be looked over and
/// completed by the user before it is saved.
/// </summary>
/// <param name="Template">The vendor whose layout was recognised; null if none was, and the material is empty.</param>
/// <param name="Version">The sheet's own version, "V3.0".</param>
/// <param name="Missing">The properties the template looks for and did not find.</param>
/// <param name="Remarks">What else the user should know before trusting the result.</param>
public sealed record TdsImport(Material Material, string? Template, string Version, IReadOnlyList<MaterialProperty> Missing, IReadOnlyList<string> Remarks);

/// <summary>Turns a vendor's technical data sheet into a material. One template per vendor's layout; Bambu Lab's is the first.</summary>
public static class TdsImporter
{
    /// <summary>What the values of an imported material call the sheet.</summary>
    public const string DocumentId = "tds";

    public static TdsImport Import(string pdf) => Import(PdfText.Read(pdf), Path.GetFileNameWithoutExtension(pdf));

    /// <param name="fallbackName">The material's name if no template recognises the sheet.</param>
    public static TdsImport Import(IReadOnlyList<TextPage> pages, string fallbackName = "")
    {
        if (BambuTds.Recognises(pages)) return BambuTds.Parse(pages);
        // Any other vendor: an empty form, to be filled in by hand with the sheet alongside.
        return new TdsImport(new Material { Name = fallbackName.Replace('_', ' ').Trim() }, null, "", [], []);
    }
}

/// <summary>
/// Bambu Lab's technical data sheets: tables of "subject, testing method, data" with the values in
/// the plane of the layers (X-Y) and across them (Z) in rows of their own. There are two layouts.
/// The one in use since 2022 is headed "Bambu Filament – Technical Data Sheet V3.0"; the one of
/// the filaments new in 2026 is headed "TDS of Bambu PLA Pure", has charts beside its tables and
/// says how the specimens were printed in a note beside the table instead of in a table of its own.
/// </summary>
public static class BambuTds
{
    const RegexOptions Loose = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    const string Number = @"\d+(?:\.\d+)?";

    /// <summary>"number ± number", the ± part optional.</summary>
    const string Value = $@"(?<value>{Number})\s*(?:±\s*(?<spread>{Number}))?";

    /// <summary>What may follow a row's value: a remark in the cell, or text of a chart or a note that stands beside the table on the same line.</summary>
    const string Rest = @"(?:\s+.*)?$";

    /// <summary>The rows that are one line each: the label as the sheet prints it, and its unit as a pattern.</summary>
    static readonly (MaterialProperty Property, string Label, string Unit)[] Rows =
    [
        (MaterialProperty.Density, "Density", "g/cm[³3]"),
        (MaterialProperty.GlassTransitionTemperature, "Glass Transition Temperature", "°C"),
        (MaterialProperty.YoungsModulusXY, "Young's Modulus (X-Y)", "MPa"),
        (MaterialProperty.YoungsModulusZ, "Young's Modulus (Z)", "MPa"),
        (MaterialProperty.TensileStrengthXY, "Tensile Strength (X-Y)", "MPa"),
        (MaterialProperty.TensileStrengthZ, "Tensile Strength (Z)", "MPa"),
        (MaterialProperty.ElongationAtBreakXY, "Breaking Elongation Rate (X-Y)", "%"),
        (MaterialProperty.ElongationAtBreakZ, "Breaking Elongation Rate (Z)", "%"),
        (MaterialProperty.BendingModulusXY, "Bending Modulus (X-Y)", "MPa"),
        (MaterialProperty.BendingModulusZ, "Bending Modulus (Z)", "MPa"),
        (MaterialProperty.BendingStrengthXY, "Bending Strength (X-Y)", "MPa"),
        (MaterialProperty.BendingStrengthZ, "Bending Strength (Z)", "MPa"),
        (MaterialProperty.ImpactStrengthZ, "Impact Strength (Z)", "kJ/m[²2]"),
    ];

    static readonly (MaterialProperty Property, Regex Pattern)[] RowPatterns =
        [.. Rows.Select(row => (row.Property, new Regex($@"^{Label(row.Label)}\s*(?<method>.*?)(?<![\d.]){Value}\s*{row.Unit}{Rest}", Loose)))];

    static readonly Regex Title = new(@"Technical\s*Data\s*Sheet\s*(?<version>V\s*\d+(?:\.\d+)*)?", Loose);
    static readonly Regex NewTitle = new(@"^TDS\s*of\s*Bambu\s+(?<name>.+)$", Loose);
    static readonly Regex VersionLine = new(@"^V\s*\d+(?:\.\d+)*$", Loose);
    static readonly Regex Product = new(@"performance\s+of\s+(?:the\s+)?(?<name>.+?)\s+material", Loose);
    static readonly Regex NozzleRange = new($@"^{Label("Nozzle Temperature")}\s*(?<low>{Number})\s*[-–~]\s*(?<high>{Number})\s*°C", Loose);
    static readonly Regex Impact = new($@"{Value}\s*kJ/m[²2]\s*;?\s*(?<notched>\(\s*notched\s*\))?", Loose);
    static readonly Regex ImpactLabel = new($@"{Label("Impact Strength (X-Y)")}\s*(?<method>[^±]*?)\s*(?=$|{Number}\s*(?:±|kJ))", Loose);
    static readonly Regex AboutSpecimens = new(@"^All\s*the\s*specimens", Loose);
    static readonly Regex Bound = new(@"[<>≥≤]\s*$", RegexOptions.CultureInvariant);
    static readonly Regex Sentence = new(@"(?<=[.!?])\s+(?=[A-Z])", RegexOptions.CultureInvariant);

    /// <summary>In the layout of 2026 the label of this row stands on two lines, with the value on a line between them.</summary>
    static readonly Regex GlassLabel = new(@"^Glass\s*Transition(?!\s*Temperature)", Loose);
    static readonly Regex GlassValue = new($@"^(?<method>.*?)(?<![\d.]){Value}\s*°C{Rest}", Loose);

    static readonly (MaterialProperty Property, Regex Pattern)[] SpecimenRows =
    [
        (MaterialProperty.SpecimenNozzleTemperature, new Regex($@"^{Label("Nozzle Temperature")}\s*(?<value>{Number})\s*°C$", Loose)),
        (MaterialProperty.SpecimenBedTemperature, new Regex($@"^{Label("Bed Temperature")}\s*(?<value>{Number})\s*°C$", Loose)),
        (MaterialProperty.SpecimenPrintSpeed, new Regex($@"^{Label("Printing Speed")}\s*(?<value>{Number})\s*mm/s$", Loose)),
    ];

    /// <summary>
    /// The same three in the words of the note beside the table of the 2026 layout. The file does not
    /// give all of that note's letters (no "°", no "fi"), so only the numbers are taken from it.
    /// </summary>
    static readonly (MaterialProperty Property, Regex Pattern)[] SpecimenWords =
    [
        (MaterialProperty.SpecimenNozzleTemperature, new Regex($@"nozzle\s+temperature\s+(?:is\s+)?(?:set\s+)?(?:at|to)\s+(?<value>{Number})\s*°?\s*C\b", Loose)),
        (MaterialProperty.SpecimenBedTemperature, new Regex($@"bed\s+temperature\s+(?:is\s+)?(?:set\s+)?(?:at|to)\s+(?<value>{Number})\s*°?\s*C\b", Loose)),
        (MaterialProperty.SpecimenPrintSpeed, new Regex($@"printing\s+speed\s+(?:is\s+)?(?:set\s+)?(?:at|to)\s+(?<value>{Number})\s*mm/s", Loose)),
    ];

    static readonly Regex InfillRow = new($@"^{Label("Infill Density")}\s*(?<value>{Number})\s*%$", Loose);
    static readonly Regex InfillWords = new($@"\w*ll\w*\s+rate\s+(?:is\s+)?(?:set\s+)?(?:at|to)\s+(?<value>{Number})\s*%", Loose);

    enum Section { Other, Settings, Properties, Specimens }

    public static bool Recognises(IReadOnlyList<TextPage> pages)
    {
        if (pages.Count == 0) return false;
        var top = pages[0].Lines.Take(6).Select(Clean).ToList();
        return (top.Any(line => Regex.IsMatch(line, @"^Bambu\s*Filament$", Loose)) && top.Any(Title.IsMatch))
               || (top.Any(NewTitle.IsMatch) && top.Any(VersionLine.IsMatch));
    }

    public static TdsImport Parse(IReadOnlyList<TextPage> pages)
    {
        var material = new Material { Vendor = "Bambu Lab" };
        var remarks = new List<string>();
        var lines = pages.SelectMany(page => page.Lines.Select(line => (Page: page.Number, Text: Clean(line)))).Where(line => line.Text.Length > 0).ToList();

        var newLayout = lines.Take(6).Select(line => NewTitle.Match(line.Text)).FirstOrDefault(m => m.Success);
        string version, product;
        if (newLayout is not null)
        {
            version = lines.Take(6).Select(line => line.Text).FirstOrDefault(VersionLine.IsMatch)?.Replace(" ", "") ?? "";
            product = newLayout.Groups["name"].Value.Trim();
        }
        else
        {
            version = lines.Select(line => Title.Match(line.Text)).FirstOrDefault(m => m.Success)?.Groups["version"].Value.Replace(" ", "") ?? "";
            // The heading's words can come out of the file run together ("PETGHF"); the sentence under "Properties" has the name spaced.
            product = lines.Select(line => Product.Match(line.Text)).FirstOrDefault(m => m.Success)?.Groups["name"].Value
                      ?? lines.SkipWhile(line => !Title.IsMatch(line.Text)).Skip(1).FirstOrDefault().Text
                      ?? "";
        }
        material.Name = $"Bambu {product}".Trim();
        material.Type = product.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        material.Filaments.Add(material.Name);
        material.Documents.Add(new MaterialDocument(TdsImporter.DocumentId, $"Bambu Filament Technical Data Sheet {version}, {product}".Replace(" ,", ",")));

        var section = Section.Other;
        var infill = "";
        var note = new List<string>();
        var inNote = false;
        for (var n = 0; n < lines.Count; n++)
        {
            var (page, text) = lines[n];
            if (Heading(text) is { } entered)
            {
                section = entered;
                continue;
            }
            switch (section)
            {
                case Section.Settings when NozzleRange.Match(text) is { Success: true } range:
                    material.Values.TryAdd(MaterialProperty.NozzleTemperatureMin, new MaterialValue(Parse(range.Groups["low"]), Source: TdsImporter.DocumentId, Page: page));
                    material.Values.TryAdd(MaterialProperty.NozzleTemperatureMax, new MaterialValue(Parse(range.Groups["high"]), Source: TdsImporter.DocumentId, Page: page));
                    break;

                case Section.Properties when ImpactLabel.Match(text) is { Success: true } label:
                    // The cell can hold two values on lines of its own, above, beside or below the label: unnotched first, then notched.
                    var cell = string.Join(' ', new[] { Neighbour(lines, n - 1), text, Neighbour(lines, n + 1) }.Where(part => part.Length > 0));
                    foreach (Match value in Impact.Matches(cell))
                    {
                        var property = value.Groups["notched"].Success ? MaterialProperty.NotchedImpactStrengthXY : MaterialProperty.ImpactStrengthXY;
                        material.Values.TryAdd(property, Sourced(value, Method(label.Groups["method"].Value), page));
                    }
                    break;

                case Section.Properties when GlassLabel.IsMatch(text) && n + 1 < lines.Count && GlassValue.Match(lines[n + 1].Text) is { Success: true } glass:
                    material.Values.TryAdd(MaterialProperty.GlassTransitionTemperature, Sourced(glass, Method(glass.Groups["method"].Value), page));
                    break;

                case Section.Properties:
                    foreach (var (property, pattern) in RowPatterns)
                        if (pattern.Match(text) is { Success: true } row)
                        {
                            // "> 650 %" says the specimen did not break before that: a bound, not the value.
                            if (Bound.Match(row.Groups["method"].Value) is { Success: true } bound)
                                remarks.Add($"{property.Label()} is given as a bound only ({bound.Value.Trim()} {row.Groups["value"].Value} {property.Unit()}) and was left out.");
                            else material.Values.TryAdd(property, Sourced(row, Method(row.Groups["method"].Value), page));
                            break;
                        }
                    // The note beside the table says how the specimens were printed, in running text.
                    foreach (var (property, pattern) in SpecimenWords)
                        if (pattern.Match(text) is { Success: true } words)
                            material.Values.TryAdd(property, new MaterialValue(Parse(words.Groups["value"]), Source: TdsImporter.DocumentId, Page: page));
                    if (infill.Length == 0 && InfillWords.Match(text) is { Success: true } filled) infill = $"Infill density {filled.Groups["value"].Value} %.";
                    break;

                case Section.Specimens when inNote || text.StartsWith('*'):
                    // The footnote of the table runs until the first test's heading ("1. Tensile Testing").
                    inNote = true;
                    if (Regex.IsMatch(text, @"^\d\s*\.\s*\w+\s*Testing", Loose)) section = Section.Other;
                    else note.Add(text.TrimStart('*', ' '));
                    break;

                case Section.Specimens when InfillRow.Match(text) is { Success: true } row:
                    // The table's last row. What follows is its footnote, which not every sheet marks with a star.
                    infill = $"Infill density {row.Groups["value"].Value} %.";
                    inNote = true;
                    break;

                case Section.Specimens:
                    foreach (var (property, pattern) in SpecimenRows)
                        if (pattern.Match(text) is { Success: true } row)
                            material.Values.TryAdd(property, new MaterialValue(Parse(row.Groups["value"]), Source: TdsImporter.DocumentId, Page: page));
                    break;
            }
        }

        // Of the footnote, what says how the specimens were made and treated; the rest is advice on ovens.
        var said = Sentence.Split(string.Join(' ', note)).Where(sentence => AboutSpecimens.IsMatch(sentence)).Select(sentence => AboutSpecimens.Replace(sentence, "All the specimens")).ToList();
        material.SpecimenNote = string.Join(' ', new[] { infill }.Concat(said.Count > 0 ? said : note).Where(part => part.Length > 0));
        if (newLayout is not null)
            remarks.Add("How the specimens were printed and treated stands in a note beside the mechanical properties, and the file does not give that note's text whole. " +
                        "Only its numbers were taken; read the note in the sheet and write down what matters of it.");

        var (ratio, of) = TypicalValues.PoissonRatio(material.Type);
        material.Values[MaterialProperty.PoissonRatio] = new MaterialValue(ratio, Assumed: $"The data sheet gives none; {ratio.ToString(CultureInfo.InvariantCulture)} is usual for {of}.");

        var looked = Rows.Select(row => row.Property).Concat(SpecimenRows.Select(row => row.Property))
            .Concat([MaterialProperty.ImpactStrengthXY, MaterialProperty.NotchedImpactStrengthXY, MaterialProperty.NozzleTemperatureMin, MaterialProperty.NozzleTemperatureMax]);
        return new TdsImport(material, "Bambu Lab", version, [.. looked.Where(property => !material.Values.ContainsKey(property)).Order()], remarks);
    }

    /// <summary>
    /// Brings a material read from a sheet in line with Bambu Studio's own preset of the filament,
    /// and says what it found. The preset knows the filament's name in the slicer and its type,
    /// which is what a filament is matched by, so both are taken from there ("ASA Aero" becomes
    /// "Bambu ASA-Aero", of type ASA-AERO). It also carries one number of the data sheet, the impact
    /// strength across the layers: if that differs, the sheet is of another version than the preset,
    /// or of another filament.
    /// </summary>
    public static List<string> AlignWithStudio(Material material, PresetLibrary presets, bool takeType = true)
    {
        var notes = new List<string>();
        var preset = presets.All.FirstOrDefault(p => p.Kind == PresetKind.Filament && p.Name.EndsWith(" @base", StringComparison.Ordinal)
                                                     && MaterialLibrary.SameFilament(MaterialLibrary.Stem(p.Name), material.Name));
        if (preset is null)
        {
            notes.Add($"Bambu Studio has no filament '{material.Name}': the name and the type '{material.Type}' are as the sheet gives them.");
            return notes;
        }

        var inStudio = MaterialLibrary.Stem(preset.Name);
        if (inStudio != material.Name)
        {
            notes.Add($"Named '{inStudio}' as in Bambu Studio; the sheet says '{material.Name}'.");
            material.Filaments.RemoveAll(filament => filament == material.Name);
            material.Filaments.Insert(0, inStudio);
            // The sheet's title goes by the same name: the file may have run its words together ("Supportfor PA/PET").
            var (was, now) = (material.Name.Replace("Bambu ", ""), inStudio.Replace("Bambu ", ""));
            for (var n = 0; n < material.Documents.Count; n++)
                material.Documents[n] = material.Documents[n] with { Title = material.Documents[n].Title.Replace(was, now) };
            material.Name = inStudio;
        }
        var settings = presets.Resolve(preset.Name);
        if (takeType && PresetLibrary.First(settings, "filament_type") is { Length: > 0 } type) material.Type = type;

        // The preset's density is the slicer's own, for weighing the print, and often not the sheet's; its impact strength is the sheet's.
        const MaterialProperty property = MaterialProperty.ImpactStrengthZ;
        if (PresetLibrary.First(settings, "impact_strength_z") is { } text && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var inPreset) && inPreset > 0)
        {
            if (material.Number(property) is not { } inSheet)
                notes.Add($"Bambu Studio's preset has an impact strength across the layers of {Plain(inPreset)} {property.Unit()}; the sheet gives none.");
            else if (Math.Abs(inPreset - inSheet) <= 1e-6 * Math.Abs(inSheet))
                notes.Add($"Impact strength across the layers {Plain(inSheet)} {property.Unit()}: the same in Bambu Studio's preset.");
            else
                notes.Add($"Impact strength across the layers {Plain(inSheet)} {property.Unit()} in the sheet, {Plain(inPreset)} in Bambu Studio's preset: another version of the sheet?");
        }
        return notes;
    }

    static string Plain(double number) => number.ToString(CultureInfo.InvariantCulture);

    static Section? Heading(string text)
    {
        if (Regex.IsMatch(text, @"Recommended\s*Printing\s*Settings", Loose)) return Section.Settings;
        if (Regex.IsMatch(text, @"^(Physical|Thermal|Mechanical)\s*Properties", Loose)) return Section.Properties;
        if (Regex.IsMatch(text, @"^Other\s*Physical", Loose)) return Section.Other;
        if (Regex.IsMatch(text, @"^Specimen\s*Printing\s*Conditions", Loose)) return Section.Specimens;
        return null;
    }

    /// <summary>The line next to an impact row, if it is part of that row's cell: it begins with a value or with "(notched)".</summary>
    static string Neighbour(List<(int Page, string Text)> lines, int index) =>
        index >= 0 && index < lines.Count && Regex.IsMatch(lines[index].Text, $@"^(?:{Number}\s*±|\(\s*notched)", Loose) ? lines[index].Text : "";

    static MaterialValue Sourced(Match match, string? method, int page) =>
        new(Parse(match.Groups["value"]), match.Groups["spread"].Success ? Parse(match.Groups["spread"]) : null, method, TdsImporter.DocumentId, page);

    static double Parse(Group group) => double.Parse(group.Value, CultureInfo.InvariantCulture);

    static string? Method(string text) => text.Trim(' ', ',', ';') is { Length: > 0 } method ? method : null;

    /// <summary>A label as a pattern that also matches when the file ran its words together or used another apostrophe.</summary>
    static string Label(string label) => Regex.Escape(label).Replace(@"\ ", @"\s*").Replace("'", "['’`]");

    /// <summary>Full-width punctuation as its plain form, one space between words.</summary>
    static string Clean(string line)
    {
        var plain = line.Replace("，", ", ").Replace("；", "; ").Replace('（', '(').Replace('）', ')').Replace('＜', '<').Replace('＞', '>').Replace("：", ": ").Replace(' ', ' ').Replace("•", "")
            .Replace("℃", "°C");
        return Regex.Replace(plain, @"\s+", " ").Replace(" ,", ",").Trim();
    }
}
