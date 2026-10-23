using GcodeFem.Core.Materials;

namespace GcodeFem.Tests.Materials;

/// <summary>The template on lines as PdfText gives them, with the quirks seen in Bambu Lab's files.</summary>
public class BambuTdsTests
{
    static TextPage Page(int number, params string[] lines) => new(number, lines);

    /// <summary>The layout in use since 2022, from a file whose headings came out with their words run together.</summary>
    static readonly TextPage[] Sheet =
    [
        Page(1,
            "Bambu Filament", "TechnicalDataSheetV3.0", "PLAExample", "• BasicInfo",
            "• Recommended Printing Settings", "Subjects Data",
            "Nozzle Temperature 190 - 230 °C", "Bed Temperature 35 - 45 °C", "Printing Speed < 300 mm/s"),
        Page(2,
            "Max Bridging Length 30 mm",
            "• Properties",
            "Bambu Lab has tested the differing aspects in the performance of PLA Example material,",
            "including physical, mechanical, and chemical properties. Typical values are listed as followed:",
            "PhysicalProperties", "Subjects Testing Methods Data",
            "Density ISO 1183 1.21 g/cm³ (filament)",
            "Melt Index 210 °C, 2.16 kg 42.4 ± 3.5 g/10 min",
            "Glass Transition Temperature DSC, 10 °C/min 57 °C",
            "Heat Deflection Temperature ISO 75 1.8 MPa 54 °C",
            "Mechanical Properties（Dry state）", "Subjects Testing Methods Data",
            "Young's Modulus (X-Y) ISO 527, GB/T 1040 2580 ± 220 MPa",
            "Young’s Modulus (Z) ISO 527，GB/T 1040 2060 ± 170 MPa",
            "Tensile Strength (X-Y) ISO 527, GB/T 1040 35 ± 4 MPa",
            "Tensile Strength (Z) ISO 527, GB/T 1040 31 ± 3 MPa",
            "Breaking Elongation Rate (X-Y) ISO 527, GB/T 1040 12.2 ± 1.8%",
            "Breaking Elongation Rate (Z) ISO 527, GB/T 1040 > 350%",
            "Bending Modulus (X-Y) ISO 178, GB/T 9341 2750 ± 160 MPa",
            "Bending Modulus (Z) ISO 178, GB/T 9341 N / A",
            "Bending Strength (X-Y) ISO 178, GB/T 9341 76 ± 5 MPa",
            "Bending Strength (Z) ISO 178, GB/T 9341 59 ± 6 MPa",
            "26.6 ± 2.8 kJ/m²；",
            "Impact Strength (X-Y) ISO 179, GB/T 1043 7.9 ± 1.2 kJ/m²",
            "(notched)",
            "Impact Strength (Z) ISO 179, GB/T 1043 13.8 ± 0.9 kJ/m²"),
        Page(3,
            "OtherPhysicaland Chemical Properties", "Subjects Data",
            "Density ISO 1183 9.99 g/cm³",
            "• Specimen Test", "Specimen Printing Conditions", "Subjects Data",
            "Nozzle Temperature 220 °C", "Bed Temperature 35 °C", "Printing Speed 200 mm/s", "Infill Density 100%",
            "*All the specimens were annealed and dried at 55 °C for 8 h before testing. And the",
            "suggested annealing temperature of models is 50 to 60 °C. When drying the filament",
            "a kitchen oven is not compatible.",
            "1. Tensile Testing",
            "Nozzle Temperature 999 °C"),
    ];

    [Fact]
    public void The_sheet_is_recognised_and_named_by_the_sentence_that_has_its_name_spaced()
    {
        var import = TdsImporter.Import(Sheet);

        Assert.Equal(("Bambu Lab", "V3.0"), (import.Template, import.Version));
        Assert.Equal(("Bambu PLA Example", "Bambu Lab", "PLA"), (import.Material.Name, import.Material.Vendor, import.Material.Type));
        Assert.Equal(new[] { "Bambu PLA Example" }, import.Material.Filaments);
        Assert.Equal(new MaterialDocument("tds", "Bambu Filament Technical Data Sheet V3.0, PLA Example"), Assert.Single(import.Material.Documents));
    }

    [Fact]
    public void Every_row_becomes_a_value_with_its_spread_its_method_and_its_page()
    {
        var material = TdsImporter.Import(Sheet).Material;

        Assert.Equal(new MaterialValue(1.21, null, "ISO 1183", "tds", 2), material.Of(MaterialProperty.Density));
        Assert.Equal(new MaterialValue(57, null, "DSC, 10 °C/min", "tds", 2), material.Of(MaterialProperty.GlassTransitionTemperature));
        Assert.Equal(new MaterialValue(2580, 220, "ISO 527, GB/T 1040", "tds", 2), material.Of(MaterialProperty.YoungsModulusXY));
        Assert.Equal(new MaterialValue(2060, 170, "ISO 527, GB/T 1040", "tds", 2), material.Of(MaterialProperty.YoungsModulusZ));
        Assert.Equal(new MaterialValue(35, 4, "ISO 527, GB/T 1040", "tds", 2), material.Of(MaterialProperty.TensileStrengthXY));
        Assert.Equal(new MaterialValue(31, 3, "ISO 527, GB/T 1040", "tds", 2), material.Of(MaterialProperty.TensileStrengthZ));
        Assert.Equal(new MaterialValue(12.2, 1.8, "ISO 527, GB/T 1040", "tds", 2), material.Of(MaterialProperty.ElongationAtBreakXY));
        Assert.Equal(new MaterialValue(2750, 160, "ISO 178, GB/T 9341", "tds", 2), material.Of(MaterialProperty.BendingModulusXY));
        Assert.Equal(new MaterialValue(59, 6, "ISO 178, GB/T 9341", "tds", 2), material.Of(MaterialProperty.BendingStrengthZ));
        Assert.Equal(new MaterialValue(13.8, 0.9, "ISO 179, GB/T 1043", "tds", 2), material.Of(MaterialProperty.ImpactStrengthZ));
    }

    [Fact]
    public void The_impact_cell_gives_the_plain_and_the_notched_value()
    {
        var material = TdsImporter.Import(Sheet).Material;

        Assert.Equal(new MaterialValue(26.6, 2.8, "ISO 179, GB/T 1043", "tds", 2), material.Of(MaterialProperty.ImpactStrengthXY));
        Assert.Equal(new MaterialValue(7.9, 1.2, "ISO 179, GB/T 1043", "tds", 2), material.Of(MaterialProperty.NotchedImpactStrengthXY));
    }

    [Theory]
    // The label between the two values, as PETG HF has it.
    [InlineData("31.5 ± 2.2 kJ/m²;|Impact Strength (X-Y) ISO 179, GB/T 1043|6.2 ± 1.8 kJ/m² (notched)", 31.5, 6.2)]
    // One value without a spread, as the TPUs have it.
    [InlineData("Impact Strength (X-Y) ISO 179, GB/T 1043 124.3 kJ/m²", 124.3, null)]
    // One value, with the text of a note beside the table on its line, as the layout of 2026 has it.
    [InlineData("Impact Strength (X-Y) ISO 179, GB/T 1043 32.6 ± 4.1 kJ/m² When drying consumables, use 1 oven", 32.6, null)]
    public void The_impact_cell_is_read_in_its_other_arrangements(string rows, double plain, double? notched)
    {
        var pages = new[] { Sheet[0], Page(2, ["Mechanical Properties", "Bending Strength (Z) ISO 178, GB/T 9341 48 ± 4 MPa", .. rows.Split('|'), "Impact Strength (Z) ISO 179, GB/T 1043 10.6 ± 1.2 kJ/m²"]) };

        var material = TdsImporter.Import(pages).Material;

        Assert.Equal(plain, material.Number(MaterialProperty.ImpactStrengthXY));
        Assert.Equal(notched, material.Number(MaterialProperty.NotchedImpactStrengthXY));
        Assert.Equal(48, material.Number(MaterialProperty.BendingStrengthZ));
        Assert.Equal(10.6, material.Number(MaterialProperty.ImpactStrengthZ));
    }

    [Fact]
    public void What_the_sheet_does_not_give_as_a_value_is_left_out_and_named()
    {
        var import = TdsImporter.Import(Sheet);

        Assert.Equal(new[] { MaterialProperty.ElongationAtBreakZ, MaterialProperty.BendingModulusZ }, import.Missing);
        Assert.Contains("Elongation at break, across the layers is given as a bound only (> 350 %)", Assert.Single(import.Remarks));
    }

    [Fact]
    public void The_nozzle_range_comes_from_the_settings_and_the_specimens_temperature_from_their_own_table()
    {
        var material = TdsImporter.Import(Sheet).Material;

        Assert.Equal(new MaterialValue(190, Source: "tds", Page: 1), material.Of(MaterialProperty.NozzleTemperatureMin));
        Assert.Equal(new MaterialValue(230, Source: "tds", Page: 1), material.Of(MaterialProperty.NozzleTemperatureMax));
        Assert.Equal(new MaterialValue(220, Source: "tds", Page: 3), material.Of(MaterialProperty.SpecimenNozzleTemperature));
        Assert.Equal(new MaterialValue(35, Source: "tds", Page: 3), material.Of(MaterialProperty.SpecimenBedTemperature));
        Assert.Equal(new MaterialValue(200, Source: "tds", Page: 3), material.Of(MaterialProperty.SpecimenPrintSpeed));
    }

    [Fact]
    public void Of_the_footnote_only_what_says_how_the_specimens_were_treated_is_kept()
    {
        var material = TdsImporter.Import(Sheet).Material;

        Assert.Equal("Infill density 100 %. All the specimens were annealed and dried at 55 °C for 8 h before testing.", material.SpecimenNote);
    }

    [Fact]
    public void The_footnote_is_found_without_its_star_and_with_its_first_words_run_together()
    {
        var pages = new[]
        {
            Sheet[0],
            Page(3,
                "Specimen Printing Conditions", "Subjects Data", "Nozzle Temperature 230 °C", "Infill Density 100%",
                "Allthe specimens were printed at the following settings: Nozzle Temperature = 230 °C, Printing",
                "Speed = 130 mm/s, Bed Temperature = 35 °C, Infill Density= 100%. All the specimens were",
                "dried at 70 °C for 12 h before testing. It's not recommended to anneal prints of",
                "TPU, or prints can deform obviously.",
                "1.TensileTesting"),
        };

        var material = TdsImporter.Import(pages).Material;

        Assert.Equal("Infill density 100 %. All the specimens were printed at the following settings: Nozzle Temperature = 230 °C, Printing Speed = 130 mm/s, " +
                     "Bed Temperature = 35 °C, Infill Density= 100%. All the specimens were dried at 70 °C for 12 h before testing.", material.SpecimenNote);
        Assert.Equal(230, material.Number(MaterialProperty.SpecimenNozzleTemperature));
    }

    [Fact]
    public void A_row_outside_the_tables_of_properties_is_not_a_property()
    {
        var material = TdsImporter.Import(Sheet).Material;

        // "Density … 9.99" stands under "Other properties", "Nozzle Temperature 999" after the footnote of the specimens.
        Assert.Equal(1.21, material.Number(MaterialProperty.Density));
        Assert.Equal(220, material.Number(MaterialProperty.SpecimenNozzleTemperature));
    }

    [Theory]
    [InlineData("PLA Example", 0.35, "PLA")]
    [InlineData("PETG HF", 0.38, "PETG")]
    [InlineData("TPU 95A", 0.48, "TPU")]
    [InlineData("PA6-CF", 0.39, "polyamides")]
    [InlineData("PVA", 0.35, "rigid thermoplastics")]
    public void Poissons_ratio_is_assumed_by_the_filament_type_and_marked_so(string product, double ratio, string usualFor)
    {
        var pages = new[] { Sheet[0], Page(2, $"aspects in the performance of {product} material,") };

        var value = TdsImporter.Import(pages).Material.Of(MaterialProperty.PoissonRatio)!;

        Assert.Equal(ratio, value.Value);
        Assert.True(value.IsAssumed);
        Assert.Null(value.Source);
        Assert.Contains($"usual for {usualFor}", value.Assumed);
    }

    /// <summary>The layout of the filaments new in 2026: another heading, labels on two lines, and a note beside the table.</summary>
    static readonly TextPage[] NewSheet =
    [
        Page(1,
            "TDS of Bambu PETG Example", "V1.0", "PETG Example",
            "Recommended Printing Settings", "Subjects Data",
            "Nozzle Temperature 230 - 260 °C", "Bed Temperature 65 - 75 °C"),
        Page(2,
            "Physical Properties", "Subjects Testing Methods Data Moisture absorption humidity",
            "Density ISO 1183 1.37 g/cm³ 70",
            "Thermal Properties", "Subjects Testing Methods Data Deformation（mm） 0.45 MPa 1.8 MPa",
            "Melting", "DSC, 10 °C/min 160 °C 0.35", "Temperature",
            "Glass Transition 0.25", "DSC, 10 °C/min 71 °C", "Temperature",
            "Heat Deflection", "ISO 75 1.8 MPa 71.2 °C", "Temperature Temperature（℃）"),
        Page(3,
            "Mechanical Properties", "Subjects Testing Methods Data",
            "Young's Modulus (X-Y) ISO 527, GB/T 1040 3349 ± 130 MPa",
            "Young's Modulus (Z) ISO 527, GB/T 1040 3174 ± 50 MPa",
            "Note The consumables used for testing are PETG Mae iolet, which were",
            "euipment is the Tuozhu H2 model, using a 0.4 mm hardened steel nozzle, with",
            "the nozzle temperature set at 245C, the hot bed temperature at 70C, the",
            "printing speed at 221 mm/s, and the ll rate at 100%. I f annealing treatment is",
            "Bending Strength (X-Y) ISO 178, GB/T 9341 65 ± 2.5 MPa",
            "Impact Strength (X-Y) ISO 179, GB/T 1043 32.6 ± 4.1 kJ/m² When drying consumables or annealing models",
            "Impact Strength (Z) ISO 179, GB/T 1043 10.0 ± 1.5 kJ/m² heat source to avoid local overheating. o not use a kitchen",
            "Other Physical and Chemical Properties"),
    ];

    [Fact]
    public void The_layout_of_2026_is_read_as_well()
    {
        var import = TdsImporter.Import(NewSheet);
        var material = import.Material;

        Assert.Equal(("Bambu Lab", "V1.0", "Bambu PETG Example", "PETG"), (import.Template, import.Version, material.Name, material.Type));
        Assert.Equal(new MaterialValue(1.37, null, "ISO 1183", "tds", 2), material.Of(MaterialProperty.Density));
        Assert.Equal(new MaterialValue(71, null, "DSC, 10 °C/min", "tds", 2), material.Of(MaterialProperty.GlassTransitionTemperature));
        Assert.Equal(new MaterialValue(3349, 130, "ISO 527, GB/T 1040", "tds", 3), material.Of(MaterialProperty.YoungsModulusXY));
        Assert.Equal(new MaterialValue(65, 2.5, "ISO 178, GB/T 9341", "tds", 3), material.Of(MaterialProperty.BendingStrengthXY));
        Assert.Equal(new MaterialValue(32.6, 4.1, "ISO 179, GB/T 1043", "tds", 3), material.Of(MaterialProperty.ImpactStrengthXY));
        Assert.Equal(new MaterialValue(10.0, 1.5, "ISO 179, GB/T 1043", "tds", 3), material.Of(MaterialProperty.ImpactStrengthZ));
        Assert.Equal((230d, 260d), (material.Number(MaterialProperty.NozzleTemperatureMin), material.Number(MaterialProperty.NozzleTemperatureMax)));
    }

    [Fact]
    public void In_the_layout_of_2026_the_specimens_conditions_come_from_the_note_and_the_rest_of_it_is_left_to_the_user()
    {
        var import = TdsImporter.Import(NewSheet);
        var material = import.Material;

        Assert.Equal(new MaterialValue(245, Source: "tds", Page: 3), material.Of(MaterialProperty.SpecimenNozzleTemperature));
        Assert.Equal(new MaterialValue(70, Source: "tds", Page: 3), material.Of(MaterialProperty.SpecimenBedTemperature));
        Assert.Equal(new MaterialValue(221, Source: "tds", Page: 3), material.Of(MaterialProperty.SpecimenPrintSpeed));
        Assert.Equal("Infill density 100 %.", material.SpecimenNote);
        Assert.Contains("does not give that note's text whole", Assert.Single(import.Remarks));
    }

    [Fact]
    public void A_sheet_of_another_vendor_leaves_an_empty_form()
    {
        var pages = new[] { Page(1, "Prusament PETG", "Technical Data Sheet", "Tensile Modulus 1.9 ± 0.1 GPa") };

        var import = TdsImporter.Import(pages, "Prusament_PETG_TDS");

        Assert.Null(import.Template);
        Assert.Equal("Prusament PETG TDS", import.Material.Name);
        Assert.Empty(import.Material.Values);
    }

    [Fact]
    public void Words_on_one_baseline_make_a_line_whatever_order_the_file_has_them_in()
    {
        var lines = PdfText.Lines(
        [
            new PlacedWord("2580", 400, 365.9, 11), new PlacedWord("Modulus", 150, 365.9, 11), new PlacedWord("Young's", 100, 366.0, 11), new PlacedWord("MPa", 430, 365.9, 11),
            new PlacedWord("Tensile", 100, 349.3, 11), new PlacedWord("35", 400, 349.2, 11),
            new PlacedWord("Density", 100, 577.6, 11),
        ]);

        Assert.Equal(new[] { "Density", "Young's Modulus 2580 MPa", "Tensile 35" }, lines);
    }

    [Fact]
    public void How_far_apart_two_lines_are_is_judged_by_the_size_of_their_letters()
    {
        // A cell that wraps: its lines are 7 apart in letters of 11, and stay two lines.
        var wrapped = PdfText.Lines([new PlacedWord("Blast Drying Oven", 300, 307.5, 11), new PlacedWord("Drying Settings", 100, 300.2, 11)]);
        // A label and its value set 6 off each other in letters of 34: one line.
        var offset = PdfText.Lines([new PlacedWord("Nozzle Temperature", 100, 1625.1, 34), new PlacedWord("190 - 230 °C", 900, 1619.1, 34)]);

        Assert.Equal(new[] { "Blast Drying Oven", "Drying Settings" }, wrapped);
        Assert.Equal(new[] { "Nozzle Temperature 190 - 230 °C" }, offset);
    }
}
