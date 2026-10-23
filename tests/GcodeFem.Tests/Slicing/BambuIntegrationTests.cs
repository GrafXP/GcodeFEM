using System.Numerics;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Gcode;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Slicing;

namespace GcodeFem.Tests.Slicing;

/// <summary>Runs only where Bambu Studio is installed; uses system presets so results don't depend on the account.</summary>
public sealed class BambuFactAttribute : FactAttribute
{
    public BambuFactAttribute()
    {
        if (BambuInstallation.TryLocate() is null) Skip = "Bambu Studio is not installed.";
    }
}

public class BambuIntegrationTests
{
    const string Machine = "Bambu Lab X1 Carbon 0.4 nozzle";
    const string Process = "0.20mm Standard @BBL X1C";
    const string Filament = "Bambu PLA Basic @BBL X1C";

    static PresetLibrary SystemPresets() => PresetLibrary.Load(BambuInstallation.Locate(), userPresetFolder: null);

    [BambuFact]
    public void Resolve_follows_inherits()
    {
        var filament = SystemPresets().Resolve(Filament);

        Assert.Equal("220", PresetLibrary.First(filament, "nozzle_temperature"));
        Assert.Equal("PLA", PresetLibrary.First(filament, "filament_type"));
        Assert.Equal("1.26", PresetLibrary.First(filament, "filament_density"));
        Assert.False(filament.ContainsKey("inherits"));
    }

    [BambuFact]
    public void Resolve_pulls_in_included_gcode_templates()
    {
        var machine = SystemPresets().Resolve(Machine);

        Assert.StartsWith(";===== machine: X1", PresetLibrary.First(machine, "machine_start_gcode"));
        Assert.False(machine.ContainsKey("include"));
    }

    [BambuFact]
    public void Lists_only_filaments_for_the_chosen_printer()
    {
        var names = SystemPresets().CompatibleWith(Machine, PresetKind.Filament).Select(p => p.Name).ToList();

        Assert.Contains(Filament, names);
        Assert.DoesNotContain(names, n => n.Contains("@BBL A1M", StringComparison.Ordinal));
    }

    /// <summary>
    /// The check of M7: a bar printed lying flat is stiffer along its length than one standing up, by the
    /// data sheet's ratio of its modulus in the plane of the layers to that across them. Each bar is
    /// solved twice, with the material and with its in-plane modulus every way; the second takes out
    /// what the slicer's lines and the cells alone make of the two bars.
    /// </summary>
    [BambuFact]
    public async Task A_bar_printed_upright_is_softer_by_the_sheets_ratio()
    {
        var bambu = BambuInstallation.Locate();
        var slicer = new BambuSlicer(bambu, SystemPresets(), BambuSlicer.DefaultCacheDirectory);
        var material = new OrthotropicMaterial(2580, 2580, 2060, 0.35); // Bambu PLA Basic
        var isotropic = new OrthotropicMaterial(2580, 2580, 2580, 0.35);
        var bar = MeshFactory.Box(Vector3.Zero, new Vector3(60, 4, 1.6f));

        var moduli = new List<(double Printed, double Isotropic)>();
        foreach (var (turned, axis) in new[] { (Vector3.Zero, Axis.X), (new Vector3(0, -90, 0), Axis.Z) })
        {
            var result = await slicer.SliceAsync(new SliceRequest(bar, Orientation.FromEulerDegrees(turned.X, turned.Y, turned.Z), Machine, Process, Filament));
            var toolpath = GcodeParser.Parse(result.GcodePath);
            var grid = Voxelizer.Voxelize(toolpath, result.Placement, result.PrintMesh.Bounds, PrintClock.Calibrated(toolpath, result.Report.FeatureSeconds));
            var loadCase = LoadCase.ClampAndPush(grid.OccupiedBounds(0.05f), axis, fixMax: false, 20 * LoadCase.Unit(axis));
            var mesh = FemMesh.Build(grid, 0.05f, loadCase);
            moduli.Add((Strain(StaticAnalysis.Run(mesh, material, loadCase, LinearSolvers.Best()), axis), Strain(StaticAnalysis.Run(mesh, isotropic, loadCase, LinearSolvers.Best()), axis)));
        }

        // Same bar, same force: the moduli stand as the strains do, the other way round.
        var (flat, upright) = (moduli[0], moduli[1]);
        var printed = upright.Printed / flat.Printed;
        var geometry = upright.Isotropic / flat.Isotropic;
        Assert.Equal(2580.0 / 2060, printed / geometry, 2);
        Assert.InRange(printed, 0.9 * 2580 / 2060, 1.1 * 2580 / 2060); // measured: 1.325, the cells of the upright bar being 6 % softer

        static double Strain(FemResult fem, Axis axis)
        {
            var mesh = fem.Mesh;
            var planes = Enumerable.Range(0, mesh.NodeCount).Select(n => LoadCase.Component(mesh.NodePosition(n), axis)).Distinct().Order().ToArray();
            var (from, to) = (planes[planes.Length / 4], planes[3 * planes.Length / 4]);
            double Shift(float plane) => Enumerable.Range(0, mesh.NodeCount).Where(n => LoadCase.Component(mesh.NodePosition(n), axis) == plane)
                .Average(n => LoadCase.Component(fem.Displacement(n), axis));
            return (Shift(to) - Shift(from)) / (to - from);
        }
    }

    [BambuFact]
    public async Task Slices_a_rotated_part_and_maps_it_back()
    {
        var cache = Path.Combine(Path.GetTempPath(), $"gcodefem-test-{Guid.NewGuid():N}");
        try
        {
            var bambu = BambuInstallation.Locate();
            var slicer = new BambuSlicer(bambu, SystemPresets(), cache);
            var box = MeshFactory.Box(new Vector3(5, 7, 2), new Vector3(35, 17, 7)); // 30 x 10 x 5
            var request = new SliceRequest(box, Orientation.FromEulerDegrees(90, 0, 0), Machine, Process, Filament);

            var result = await slicer.SliceAsync(request);
            var toolpath = GcodeParser.Parse(result.GcodePath);

            Assert.False(result.FromCache);
            Assert.Equal(new Vector3(30, 5, 10), result.PrintMesh.Bounds.Size);
            Assert.Equal(50, toolpath.Layers.Length); // 10 mm tall at 0.2 mm
            Assert.Equal((220f, 220f), ToolpathStatistics.PartRange(toolpath, s => s.NozzleTemperature));

            var beads = toolpath.PartBounds()!.Value;
            var low = result.Placement.BedToPrint(beads.Min) - result.PrintMesh.Bounds.Min;
            var high = result.PrintMesh.Bounds.Max - result.Placement.BedToPrint(beads.Max);
            foreach (var inset in new[] { low.X, low.Y, high.X, high.Y })
                Assert.Equal(0.21f, inset, 2);

            Assert.True((await slicer.SliceAsync(request)).FromCache);
        }
        finally
        {
            if (Directory.Exists(cache)) Directory.Delete(cache, recursive: true);
        }
    }
}
