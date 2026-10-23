using GcodeFem.Core.Materials;

namespace GcodeFem.Tests.Materials;

public sealed class MaterialFileTests : IDisposable
{
    readonly string folder = Path.Combine(Path.GetTempPath(), "GcodeFem.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }

    static Material Sample()
    {
        var material = new Material
        {
            Name = "Example PLA",
            Vendor = "Example",
            Type = "PLA",
            StandsInForType = true,
            SpecimenNote = "Infill density 100 %. All the specimens were annealed and dried at 55 °C for 8 h before testing.",
            Values =
            {
                [MaterialProperty.Density] = new MaterialValue(1.24, Method: "ISO 1183", Source: "tds", Page: 2),
                [MaterialProperty.YoungsModulusXY] = new MaterialValue(2580, 220, "ISO 527, GB/T 1040", "tds", 2),
                [MaterialProperty.YoungsModulusZ] = new MaterialValue(2060, 170, "ISO 527, GB/T 1040", "tds", 2),
                [MaterialProperty.TensileStrengthZ] = new MaterialValue(31, 3, "ISO 527", "own tensile test, 2026-11"),
                [MaterialProperty.PoissonRatio] = new MaterialValue(0.35, Assumed: "The data sheet gives none; 0.35 is usual for PLA."),
                [MaterialProperty.GlassTransitionTemperature] = new MaterialValue(60, Method: "DSC, 10 °C/min", Source: "tds", Page: 2),
            },
        };
        material.Filaments.AddRange(["Example PLA", "Example PLA Silk @BBL X1C"]);
        material.Documents.Add(new MaterialDocument("tds", "Example Technical Data Sheet V3.0", "tds/example-pla-tds-v3.0.pdf", "https://example.com/tds.pdf", "0123abcd"));
        return material;
    }

    [Fact]
    public void A_material_comes_back_as_it_was_saved()
    {
        var path = Path.Combine(folder, "example-pla.material.json");

        MaterialFile.Save(Sample(), path);
        var material = MaterialFile.Load(path);

        Assert.Equal(("Example PLA", "Example", "PLA", true), (material.Name, material.Vendor, material.Type, material.StandsInForType));
        Assert.Equal(new[] { "Example PLA", "Example PLA Silk @BBL X1C" }, material.Filaments);
        Assert.Equal(Sample().Documents, material.Documents);
        Assert.Equal(Sample().SpecimenNote, material.SpecimenNote);
        Assert.Equal(Sample().Values, material.Values);
        Assert.Equal(path, material.Path);
    }

    [Fact]
    public void The_file_says_the_unit_and_the_source_of_every_value_in_plain_text()
    {
        var path = Path.Combine(folder, "example-pla.material.json");

        MaterialFile.Save(Sample(), path);
        var text = File.ReadAllText(path);

        Assert.Contains("\"youngsModulusXY\": {", text);
        Assert.Contains("\"unit\": \"MPa\"", text);
        Assert.Contains("\"unit\": \"g/cm³\"", text);
        Assert.Contains("\"method\": \"DSC, 10 °C/min\"", text);
        Assert.Contains("\"source\": \"tds\"", text);
        Assert.Contains("\"page\": 2", text);
        Assert.Contains("\"assumed\": \"The data sheet gives none; 0.35 is usual for PLA.\"", text);
        Assert.Contains("\"file\": \"tds/example-pla-tds-v3.0.pdf\"", text);
    }

    [Fact]
    public void The_solver_gets_the_stiffness_in_the_plane_of_the_layers()
    {
        var isotropic = Sample().Isotropic();

        Assert.Equal((2580, 0.35), (isotropic.YoungsModulus, isotropic.PoissonRatio));
    }

    [Fact]
    public void A_material_without_stiffness_can_be_kept_but_not_solved_with()
    {
        var material = Sample();
        material.Values.Remove(MaterialProperty.YoungsModulusXY);
        var path = Path.Combine(folder, "support.material.json");

        MaterialFile.Save(material, path);

        Assert.Equal(new[] { MaterialProperty.YoungsModulusXY }, MaterialFile.Load(path).Lacking());
        Assert.Contains("no Young's modulus, in the plane", Assert.Throws<InvalidDataException>(() => MaterialFile.Load(path).Isotropic()).Message);
    }

    [Theory]
    [InlineData("""{ "value": 2500, "unit": "MPa" }""", "has no source and is not marked as assumed")]
    [InlineData("""{ "value": 2500, "unit": "MPa", "source": "tds", "assumed": "a guess" }""", "names a source and is marked as assumed")]
    [InlineData("""{ "value": 2.5, "unit": "GPa", "source": "tds" }""", "in 'GPa'; it is in 'MPa'")]
    [InlineData("""{ "value": -2500, "unit": "MPa", "source": "tds" }""", "must be above zero")]
    [InlineData("""{ "value": 2500, "spread": -1, "source": "tds" }""", "negative ±")]
    public void A_value_that_does_not_say_where_it_is_from_or_what_it_is_in_is_refused(string value, string complaint)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "bad.material.json");
        File.WriteAllText(path, $$"""{ "format": 1, "name": "Bad", "properties": { "youngsModulusXY": {{value}} } }""");

        Assert.Contains(complaint, Assert.Throws<InvalidDataException>(() => MaterialFile.Load(path)).Message);
    }

    [Fact]
    public void A_file_of_another_kind_is_refused()
    {
        Directory.CreateDirectory(folder);
        var notJson = Path.Combine(folder, "a.material.json");
        File.WriteAllText(notJson, "PLA");
        var newer = Path.Combine(folder, "b.material.json");
        File.WriteAllText(newer, """{ "format": 99, "name": "x" }""");
        var unknown = Path.Combine(folder, "c.material.json");
        File.WriteAllText(unknown, """{ "format": 1, "name": "x", "properties": { "hardness": { "value": 95, "source": "tds" } } }""");

        Assert.Throws<InvalidDataException>(() => MaterialFile.Load(notJson));
        Assert.Contains("format 99", Assert.Throws<InvalidDataException>(() => MaterialFile.Load(newer)).Message);
        Assert.Contains("'hardness'", Assert.Throws<InvalidDataException>(() => MaterialFile.Load(unknown)).Message);
    }

    [Fact]
    public void A_material_that_is_not_in_order_is_not_saved()
    {
        var material = Sample();
        material.Name = " ";
        material.Values[MaterialProperty.PoissonRatio] = new MaterialValue(0.5, Assumed: "incompressible");

        var error = Assert.Throws<InvalidOperationException>(() => MaterialFile.Save(material, Path.Combine(folder, "x.material.json")));

        Assert.Contains("no name", error.Message);
        Assert.Contains("Poisson's ratio must be at least 0 and below 0.5", error.Message);
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void The_generic_material_is_all_assumed()
    {
        var generic = Material.Generic();

        Assert.All(generic.Values.Values, value => Assert.True(value.IsAssumed));
        Assert.Empty(generic.Problems());
        Assert.Equal((2500, 0.35), (generic.Isotropic().YoungsModulus, generic.Isotropic().PoissonRatio));
    }
}
