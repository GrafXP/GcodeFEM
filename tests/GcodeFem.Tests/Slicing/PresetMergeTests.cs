using System.Text.Json.Nodes;
using GcodeFem.Core.Slicing;

namespace GcodeFem.Tests.Slicing;

/// <summary>What is handed to the Bambu CLI besides the model: flattened presets, and the build plate.</summary>
public class PresetMergeTests
{
    static JsonObject Json(string text) => (JsonObject)JsonNode.Parse(text)!;

    [Fact]
    public void A_nil_in_a_users_preset_is_what_the_preset_inherits_at_that_place()
    {
        // As Bambu Studio 2 keeps "eSun PLA+", made from "Bambu PLA Basic @BBL X1C" for a printer with two nozzle variants.
        var resolved = Json("""{ "nozzle_temperature": ["220", "221"], "nozzle_temperature_initial_layer": ["220"], "filament_flow_ratio": ["0.98", "0.98"] }""");
        var own = Json("""{ "nozzle_temperature": ["230", "nil"], "nozzle_temperature_initial_layer": ["240", "nil"], "filament_cost": ["nil"], "name": "eSun PLA+" }""");

        PresetLibrary.Merge(resolved, own, inheritNil: true);

        Assert.Equal(new[] { "230", "221" }, resolved["nozzle_temperature"]!.AsArray().Select(n => n!.GetValue<string>()));
        // The inherited list is shorter: its last value stands for the rest.
        Assert.Equal(new[] { "240", "220" }, resolved["nozzle_temperature_initial_layer"]!.AsArray().Select(n => n!.GetValue<string>()));
        Assert.Equal(new[] { "0.98", "0.98" }, resolved["filament_flow_ratio"]!.AsArray().Select(n => n!.GetValue<string>()));
        // Nothing to inherit: it stays as it is.
        Assert.Equal(new[] { "nil" }, resolved["filament_cost"]!.AsArray().Select(n => n!.GetValue<string>()));
        Assert.Equal("eSun PLA+", resolved["name"]!.GetValue<string>());
    }

    [Fact]
    public void A_nil_in_a_system_preset_stays()
    {
        // There it says that the filament leaves the setting to the printer.
        var resolved = Json("""{ "filament_retraction_length": ["0.8"] }""");
        var own = Json("""{ "filament_retraction_length": ["nil", "0.4"] }""");

        PresetLibrary.Merge(resolved, own);

        Assert.Equal(new[] { "nil", "0.4" }, resolved["filament_retraction_length"]!.AsArray().Select(n => n!.GetValue<string>()));
    }

    [Theory]
    // PLA prints on the Cool Plate, which the slicer assumes: no plate is named, as before.
    [InlineData("""{ "cool_plate_temp": ["35"], "textured_plate_temp": ["55"], "hot_plate_temp": ["55"] }""", null)]
    // PETG HF does not: the textured plate is the first it prints on.
    [InlineData("""{ "cool_plate_temp": ["0"], "eng_plate_temp": ["70"], "hot_plate_temp": ["70"], "textured_plate_temp": ["70"] }""", "Textured PEI Plate")]
    [InlineData("""{ "cool_plate_temp": ["0"], "eng_plate_temp": ["100"], "hot_plate_temp": ["100"], "textured_plate_temp": ["0"] }""", "High Temp Plate")]
    [InlineData("""{ "cool_plate_temp": ["0"], "eng_plate_temp": ["90"] }""", "Engineering Plate")]
    // Nothing is known of the filament's plates: left to the slicer.
    [InlineData("""{ "nozzle_temperature": ["220"] }""", null)]
    public void A_filament_that_does_not_print_on_the_cool_plate_gets_a_plate_it_prints_on(string filament, string? plate) =>
        Assert.Equal(plate, BambuSlicer.PlateFor(Json(filament)));
}
