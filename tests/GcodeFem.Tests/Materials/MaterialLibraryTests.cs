using GcodeFem.Core.Materials;

namespace GcodeFem.Tests.Materials;

public sealed class MaterialLibraryTests : IDisposable
{
    readonly string folder = Path.Combine(Path.GetTempPath(), "GcodeFem.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }

    static Material Make(string name, string type, double youngsModulus, bool standsIn = false, params string[] filaments)
    {
        var material = new Material
        {
            Name = name,
            Type = type,
            StandsInForType = standsIn,
            Values =
            {
                [MaterialProperty.YoungsModulusXY] = new MaterialValue(youngsModulus, Source: "tds", Page: 2),
                [MaterialProperty.PoissonRatio] = new MaterialValue(0.35, Assumed: "usual"),
            },
        };
        material.Filaments.AddRange(filaments.Length > 0 ? filaments : [name]);
        return material;
    }

    MaterialLibrary Library(params Material[] materials)
    {
        foreach (var material in materials) MaterialFile.Save(material, Path.Combine(folder, MaterialLibrary.Slug(material.Name) + MaterialFile.Extension));
        return MaterialLibrary.Load(folder);
    }

    [Theory]
    [InlineData("Bambu PLA Basic @BBL X1C")]
    [InlineData("Bambu PLA Basic @BBL A1M 0.2 nozzle")]
    [InlineData("bambu pla basic")]
    public void A_filament_gets_the_material_that_names_it_on_whatever_printer(string preset)
    {
        var library = Library(Make("Bambu PLA Basic", "PLA", 2580, standsIn: true), Make("Bambu PLA Matte", "PLA", 1960));

        var match = library.Match(preset, "PLA");

        Assert.Equal(("Bambu PLA Basic", MatchKind.ByName, false), (match.Material.Name, match.Kind, match.IsStandIn));
    }

    [Fact]
    public void A_preset_named_in_full_goes_before_the_filament_it_is_a_preset_of()
    {
        var library = Library(Make("Bambu PLA Basic", "PLA", 2580), Make("My hot PLA", "PLA", 2400, filaments: "Bambu PLA Basic @BBL A1"));

        Assert.Equal("My hot PLA", library.Match("Bambu PLA Basic @BBL A1", "PLA").Material.Name);
        Assert.Equal("Bambu PLA Basic", library.Match("Bambu PLA Basic @BBL X1C", "PLA").Material.Name);
    }

    [Fact]
    public void A_filament_without_a_material_gets_the_one_that_stands_in_for_its_type_and_says_so()
    {
        var library = Library(Make("Bambu PLA Basic", "PLA", 2580, standsIn: true), Make("Bambu PLA Matte", "PLA", 1960), Make("Bambu PETG HF", "PETG", 1810, standsIn: true));

        var match = library.Match("eSun PLA+", "PLA");

        Assert.Equal(("Bambu PLA Basic", MatchKind.ByType, true), (match.Material.Name, match.Kind, match.IsStandIn));
        Assert.Contains("No material data for 'eSun PLA+'", match.Describe());
        Assert.Contains("Bambu PLA Basic stands in", match.Describe());
        Assert.Equal("Bambu PETG HF", library.Match("Prusament PETG", "PETG").Material.Name);
    }

    [Fact]
    public void A_filament_of_a_type_nothing_stands_in_for_gets_the_generic_material_and_a_warning()
    {
        var library = Library(Make("Bambu PLA Basic", "PLA", 2580, standsIn: true), Make("Bambu PLA-CF", "PLA-CF", 2790));

        var unknownType = library.Match("Fancy PEEK", "PEEK");
        var noStandIn = library.Match("greentec pro carbon", "PLA-CF");

        Assert.Equal((MatchKind.None, MatchKind.None), (unknownType.Kind, noStandIn.Kind));
        Assert.Equal(2500, unknownType.Material.Stiffness().AlongLine);
        Assert.Contains("generic stiffness of 2500 MPa", unknownType.Describe());
        Assert.Contains("'Fancy PEEK' (PEEK)", unknownType.Describe());
    }

    [Fact]
    public void Names_are_compared_by_their_letters_and_digits()
    {
        var library = Library(Make("Bambu ASA Aero", "ASA-AERO", 2010), Make("Bambu PLA Silk", "PLA", 1830), Make("Bambu PLA Silk+", "PLA", 2130), Make("Bambu Support for PLA", "PLA", 1));

        Assert.Equal("Bambu ASA Aero", library.Match("Bambu ASA-Aero @BBL X1C", "ASA-AERO").Material.Name);
        Assert.Equal("Bambu PLA Silk", library.Match("Bambu PLA Silk @BBL X1C", "PLA").Material.Name);
        Assert.Equal("Bambu PLA Silk+", library.Match("Bambu PLA Silk+ @BBL X1C", "PLA").Material.Name);
        Assert.Equal("Bambu Support for PLA", library.Match("Bambu Support For PLA @BBL X1C", "PLA").Material.Name);
        Assert.Equal(MatchKind.None, library.Match("Bambu PLA Silky", "PLA").Kind);
    }

    [Fact]
    public void Materials_that_get_in_each_others_way_are_reported()
    {
        var library = Library(
            Make("A PLA", "PLA", 2500, standsIn: true), Make("B PLA", "PLA", 2600, standsIn: true),
            Make("C PETG", "PETG", 1800, filaments: "eSun PETG"), Make("D PETG", "PETG", 1900, filaments: "esun  petg"));

        Assert.Equal(2, library.Problems.Count);
        Assert.Contains(library.Problems, problem => problem.Contains("C PETG and D PETG each name the filament 'eSun PETG'; C PETG is used."));
        Assert.Contains(library.Problems, problem => problem.Contains("A PLA and B PLA each stand in for PLA; A PLA is used."));
        Assert.Equal("A PLA", library.Match("anything", "PLA").Material.Name);
    }

    [Fact]
    public void A_file_that_cannot_be_read_is_reported_and_the_others_are_read()
    {
        Library(Make("Good", "PLA", 2500));
        File.WriteAllText(Path.Combine(folder, "bad.material.json"), "{ not json");

        var library = MaterialLibrary.Load(folder);

        Assert.Equal(new[] { "Good" }, library.Materials.Select(m => m.Name));
        Assert.Contains("bad.material.json", Assert.Single(library.Problems));
    }

    [Fact]
    public void A_folder_that_is_not_there_is_an_empty_library()
    {
        var library = MaterialLibrary.Load(Path.Combine(folder, "nowhere"));

        Assert.Empty(library.Materials);
        Assert.Equal(MatchKind.None, library.Match("Bambu PLA Basic", "PLA").Kind);
    }

    [Fact]
    public void A_new_material_is_saved_under_its_name_and_never_over_another()
    {
        var library = Library(Make("Bambu PLA Basic", "PLA", 2580));

        var path = library.Save(Make("eSun PLA+", "PLA", 2300));
        var clash = Assert.Throws<InvalidOperationException>(() => library.Save(Make("Bambu PLA Basic", "PLA", 1)));

        Assert.Equal(Path.Combine(folder, "esun-pla-plus.material.json"), path);
        Assert.Contains("already", clash.Message);
        Assert.Equal(new[] { "Bambu PLA Basic", "eSun PLA+" }, library.Materials.Select(m => m.Name));
        Assert.Equal(2580, MaterialLibrary.Load(folder).Find("bambu pla basic")!.Number(MaterialProperty.YoungsModulusXY));
    }

    [Fact]
    public void A_saved_material_is_saved_again_to_its_own_file()
    {
        var library = Library(Make("Bambu PLA Basic", "PLA", 2580));
        var material = library.Find("Bambu PLA Basic")!;
        material.Name = "Bambu PLA Basic, measured";
        material.Values[MaterialProperty.YoungsModulusXY] = new MaterialValue(2650, Source: "own tensile test");

        library.Save(material);

        var again = Assert.Single(MaterialLibrary.Load(folder).Materials);
        Assert.Equal(("Bambu PLA Basic, measured", 2650d), (again.Name, again.Number(MaterialProperty.YoungsModulusXY)));
        Assert.Equal("bambu-pla-basic.material.json", Path.GetFileName(again.Path));
    }

    [Fact]
    public void A_source_document_is_copied_into_the_library_once()
    {
        Directory.CreateDirectory(folder);
        var sheet = Path.Combine(folder, "download.pdf");
        File.WriteAllText(sheet, "a data sheet");
        var other = Path.Combine(folder, "other.pdf");
        File.WriteAllText(other, "another data sheet");
        var library = MaterialLibrary.Load(Path.Combine(folder, "materials"));

        var document = library.Attach(sheet, "tds", "Example sheet V1.0", "https://example.com/a.pdf", "example-tds-v1.0.pdf");
        var again = library.Attach(sheet, "tds", "Example sheet V1.0", "https://example.com/a.pdf", "example-tds-v1.0.pdf");
        var clash = Assert.Throws<InvalidOperationException>(() => library.Attach(other, "tds", "Example sheet V1.0", null, "example-tds-v1.0.pdf"));

        Assert.Equal(("tds", "tds/example-tds-v1.0.pdf", "https://example.com/a.pdf"), (document.Id, document.File, document.Url));
        Assert.Equal(64, document.Sha256!.Length);
        Assert.Equal(document, again);
        Assert.Equal("a data sheet", File.ReadAllText(Path.Combine(folder, "materials", "tds", "example-tds-v1.0.pdf")));
        Assert.Contains("another document", clash.Message);
    }

    [Theory]
    [InlineData("Bambu PLA Basic @BBL X1C 0.2 nozzle", "Bambu PLA Basic")]
    [InlineData("Bambu PA-CF @BBL X1C - Copy", "Bambu PA-CF")]
    [InlineData("eSun PLA+", "eSun PLA+")]
    public void The_filament_of_a_preset_is_its_name_without_the_printer(string preset, string filament) =>
        Assert.Equal(filament, MaterialLibrary.Stem(preset));

    [Theory]
    [InlineData("Bambu PLA Basic", "bambu-pla-basic")]
    [InlineData("Bambu PLA Silk+", "bambu-pla-silk-plus")]
    [InlineData("Bambu Support For PA/PET", "bambu-support-for-pa-pet")]
    [InlineData("  ", "material")]
    public void A_material_file_is_named_after_its_material(string name, string slug) =>
        Assert.Equal(slug, MaterialLibrary.Slug(name));
}
