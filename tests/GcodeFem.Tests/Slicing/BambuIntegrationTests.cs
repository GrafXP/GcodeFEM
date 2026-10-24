using System.Numerics;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Gcode;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Materials;
using GcodeFem.Core.Slicing;
using GcodeFem.Core.Study;

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

    /// <summary>
    /// The check of M8: the bracket of the samples, bolted down and pulled sideways at its tip. Lying flat,
    /// it bends in the plane of its layers; standing on edge, its upright leg bends across them and breaks
    /// at the layer line where it meets the base, at about half the load.
    /// </summary>
    [BambuFact]
    public async Task The_bracket_on_edge_breaks_at_a_layer_line_at_half_the_load()
    {
        var study = StudyFile.Load(TestPaths.Sample("bracket40_holes.study.json"));
        var model = Stl.Read(study.ModelPath);
        var slicer = new BambuSlicer(BambuInstallation.Locate(), SystemPresets(), BambuSlicer.DefaultCacheDirectory);
        var materials = MaterialLibrary.Load(TestPaths.Materials);

        var results = new List<(CellFailure Failure, FemMesh Mesh)>();
        foreach (var turned in new[] { Vector3.Zero, new Vector3(90, 0, 0) })
        {
            var rotation = Orientation.FromEulerDegrees(turned.X, turned.Y, turned.Z);
            var slice = await slicer.SliceAsync(new SliceRequest(model, rotation, Machine, study.Process!, study.Filament!));
            var toolpath = GcodeParser.Parse(slice.GcodePath);
            var grid = Voxelizer.Voxelize(toolpath, slice.Placement, slice.PrintMesh.Bounds, PrintClock.Calibrated(toolpath, slice.Report.FeatureSeconds));
            var loadCase = InterfaceMapper.Map(slice.PrintMesh, slice.Placement.Rotation, grid.Pitch, study.Interfaces, study.LoadCases[0]);
            var mesh = FemMesh.Build(grid, 0.05f, loadCase);
            var material = materials.Match(toolpath).Material;
            var bonds = new BondModel().Cells(grid, mesh.Cells, material.Bonding(toolpath), 0.05f);
            var cells = new CellStrength(mesh, material.Strength(out _)!, bonds.Factor);
            var adaptive = AdaptiveAnalysis.Run(mesh, material.Stiffness(), loadCase, LinearSolvers.Best(), new AdaptiveOptions(MinFill: 0.05f), strength: cells);
            results.Add((adaptive.Failure!, mesh));
        }

        var ((flat, _), (onEdge, edgeMesh)) = (results[0], results[1]);
        Assert.NotEqual(FailureMode.LayerSeparation, flat.Mode[flat.Weakest]);
        Assert.Equal(FailureMode.LayerSeparation, onEdge.Mode[onEdge.Weakest]);
        Assert.Equal(onEdge.WeakestLayerLine, onEdge.Weakest);
        Assert.InRange(onEdge.SafetyFactor / flat.SafetyFactor, 0.4, 0.7); // measured: 0.870 / 1.583 = 0.55
        Assert.Equal(51, edgeMesh.Cells[onEdge.Weakest].K + 1); // where the upright leg meets the base, 10 mm up
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
