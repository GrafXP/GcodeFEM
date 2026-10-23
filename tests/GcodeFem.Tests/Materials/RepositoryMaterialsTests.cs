using System.Security.Cryptography;
using GcodeFem.Core.Gcode;
using GcodeFem.Core.Materials;
using GcodeFem.Core.Slicing;
using GcodeFem.Tests.Slicing;

namespace GcodeFem.Tests.Materials;

/// <summary>The material library that comes with the repository: Bambu Lab's filaments, each from its technical data sheet.</summary>
public class RepositoryMaterialsTests
{
    static readonly MaterialLibrary Library = MaterialLibrary.Load(TestPaths.Materials);

    [Fact]
    public void The_library_reads_without_complaint()
    {
        Assert.Empty(Library.Problems);
        Assert.True(Library.Materials.Count >= 45, $"{Library.Materials.Count} materials");
        Assert.All(Library.Materials, material => Assert.Equal("Bambu Lab", material.Vendor));
    }

    /// <summary>
    /// Reads every sheet again and compares: no number in a material file was typed, changed or
    /// attributed by hand, and the sheet in the repository is the one the file was made from.
    /// </summary>
    [Fact]
    public void Every_material_is_what_the_importer_reads_from_its_data_sheet()
    {
        var differences = new List<string>();
        foreach (var material in Library.Materials)
        {
            var sheet = Assert.Single(material.Documents);
            var path = MaterialLibrary.DocumentPath(material, sheet)!;
            Assert.True(File.Exists(path), $"{material.Name}: {path} is missing");
            if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant() != sheet.Sha256)
                differences.Add($"{material.Name}: {sheet.File} is not the file its hash was taken of");
            Assert.StartsWith("https://", sheet.Url);

            var read = TdsImporter.Import(path).Material;
            foreach (var property in MaterialProperties.All)
                if (!Equals(material.Of(property), read.Of(property)))
                    differences.Add($"{material.Name}, {property.Label()}: the file has {material.Of(property)?.ToString() ?? "nothing"}, the sheet gives {read.Of(property)?.ToString() ?? "nothing"}");
            if (material.SpecimenNote != read.SpecimenNote) differences.Add($"{material.Name}: the note on the specimens differs");
        }
        Assert.Empty(differences);
    }

    [Fact]
    public void Every_number_names_the_sheet_and_the_page_it_is_from_or_is_marked_as_assumed()
    {
        foreach (var material in Library.Materials)
        {
            var pages = PdfText.Read(MaterialLibrary.DocumentPath(material, material.Documents[0])!).Count;
            foreach (var (property, value) in material.Values)
            {
                Assert.Null(value.Problem(property));
                if (value.IsAssumed) Assert.Equal(MaterialProperty.PoissonRatio, property); // the one number the sheets leave out
                else
                {
                    Assert.Equal(TdsImporter.DocumentId, value.Source);
                    Assert.InRange(value.Page ?? 0, 1, pages);
                }
            }
        }
    }

    /// <summary>The two materials the plan asked for, with the numbers read off their sheets by eye.</summary>
    [Fact]
    public void PLA_Basic_and_PETG_HF_carry_the_numbers_of_their_sheets()
    {
        var pla = Library.Find("Bambu PLA Basic")!;
        var petg = Library.Find("Bambu PETG HF")!;

        Assert.Equal("Bambu Filament Technical Data Sheet V3.0, PLA Basic", pla.Documents[0].Title);
        Assert.Equal(new MaterialValue(2580, 220, "ISO 527, GB/T 1040", "tds", 2), pla.Of(MaterialProperty.YoungsModulusXY));
        Assert.Equal(new MaterialValue(2060, 170, "ISO 527, GB/T 1040", "tds", 2), pla.Of(MaterialProperty.YoungsModulusZ));
        Assert.Equal(new MaterialValue(35, 4, "ISO 527, GB/T 1040", "tds", 2), pla.Of(MaterialProperty.TensileStrengthXY));
        Assert.Equal(new MaterialValue(31, 3, "ISO 527, GB/T 1040", "tds", 2), pla.Of(MaterialProperty.TensileStrengthZ));
        Assert.Equal(new MaterialValue(1.24, null, "ISO 1183", "tds", 2), pla.Of(MaterialProperty.Density));
        Assert.Equal((190d, 230d, 220d), (pla.Number(MaterialProperty.NozzleTemperatureMin), pla.Number(MaterialProperty.NozzleTemperatureMax), pla.Number(MaterialProperty.SpecimenNozzleTemperature)));
        Assert.Equal((26.6, 7.9, 13.8), (pla.Number(MaterialProperty.ImpactStrengthXY), pla.Number(MaterialProperty.NotchedImpactStrengthXY), pla.Number(MaterialProperty.ImpactStrengthZ)));

        Assert.Equal("Bambu Filament Technical Data Sheet V1.0, PETG HF", petg.Documents[0].Title);
        Assert.Equal(new MaterialValue(1810, 190, "ISO 527, GB/T 1040", "tds", 2), petg.Of(MaterialProperty.YoungsModulusXY));
        Assert.Equal(new MaterialValue(1540, 130, "ISO 527, GB/T 1040", "tds", 2), petg.Of(MaterialProperty.YoungsModulusZ));
        Assert.Equal(new MaterialValue(34, 4, "ISO 527, GB/T 1040", "tds", 2), petg.Of(MaterialProperty.TensileStrengthXY));
        Assert.Equal(new MaterialValue(23, 4, "ISO 527, GB/T 1040", "tds", 2), petg.Of(MaterialProperty.TensileStrengthZ));
        Assert.Equal((31.5, 6.2, 10.6), (petg.Number(MaterialProperty.ImpactStrengthXY), petg.Number(MaterialProperty.NotchedImpactStrengthXY), petg.Number(MaterialProperty.ImpactStrengthZ)));
        Assert.Equal((2580, 0.35, 1810, 0.38), (pla.Isotropic().YoungsModulus, pla.Isotropic().PoissonRatio, petg.Isotropic().YoungsModulus, petg.Isotropic().PoissonRatio));
    }

    [Fact]
    public void The_material_follows_from_the_filament_named_in_the_gcode()
    {
        var toolpath = GcodeParser.Parse(TestPaths.Sample("cube20_X1C_PLA.gcode"));

        var match = Library.Match(toolpath);

        Assert.Equal(("Bambu PLA Basic", MatchKind.ByName, "Bambu PLA Basic @BBL X1C", "PLA"), (match.Material.Name, match.Kind, match.Filament, match.Type));
        Assert.Equal("Bambu PLA Basic, from Bambu Filament Technical Data Sheet V3.0, PLA Basic", match.Describe());
    }

    [Theory]
    [InlineData("eSun PLA+", "PLA", "Bambu PLA Basic")]
    [InlineData("Prusament PETG", "PETG", "Bambu PETG HF")]
    [InlineData("eSun ABS+", "ABS", "Bambu ABS")]
    [InlineData("eSun TPU 95A", "TPU", "Bambu TPU 95A HF")]
    [InlineData("eSun ePA-CF", "PA-CF", "Bambu PAHT-CF")]
    public void Other_vendors_filaments_get_the_stand_in_of_their_type(string filament, string type, string standIn)
    {
        var match = Library.Match(filament, type);

        Assert.Equal((standIn, MatchKind.ByType), (match.Material.Name, match.Kind));
    }

    [Fact]
    public void Support_filaments_are_kept_as_far_as_their_sheets_go_and_cannot_be_solved_with()
    {
        var support = Library.Find("Bambu Support For PLA")!;

        Assert.NotNull(support.Number(MaterialProperty.Density));
        Assert.Contains(MaterialProperty.YoungsModulusXY, support.Lacking());
        Assert.False(support.StandsInForType);
    }

    /// <summary>A filament is matched by its name and type in Bambu Studio, so the materials must go by exactly those.</summary>
    [BambuFact]
    public void Every_material_goes_by_the_name_and_type_of_its_filament_in_Bambu_Studio()
    {
        var presets = PresetLibrary.Load(BambuInstallation.Locate(), userPresetFolder: null);
        var filaments = presets.All.Where(p => p.Kind == PresetKind.Filament && p.Name.EndsWith(" @base", StringComparison.Ordinal)).ToDictionary(p => MaterialLibrary.Stem(p.Name));

        foreach (var material in Library.Materials)
        {
            Assert.True(filaments.TryGetValue(material.Name, out var preset), $"Bambu Studio has no filament '{material.Name}'");
            Assert.Equal(PresetLibrary.First(presets.Resolve(preset!.Name), "filament_type"), material.Type);
            Assert.Equal(MatchKind.ByName, Library.Match(material.Name + " @BBL X1C", material.Type).Kind);
        }
    }
}
