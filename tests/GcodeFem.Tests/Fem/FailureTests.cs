using System.Numerics;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Gcode;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Materials;

namespace GcodeFem.Tests.Fem;

/// <summary>The failure check: each way a printed cell can give way, in the frame of its lines.</summary>
public class FailureTests
{
    const float Pitch = 0.42f, Layer = 0.2f;

    /// <summary>Along the lines 40 MPa, across them 20, across the layers 25, in compression 80.</summary>
    static readonly PrintedStrength Strength = new(40, 20, 25, 80, []);

    static Deposition Lines(Vector2 direction) => new(Vector2.Normalize(direction), 0, 220, 1, FeatureClass.SolidInfill);

    static Vector2 At(double degrees) => new((float)Math.Cos(degrees * Math.PI / 180), (float)Math.Sin(degrees * Math.PI / 180));

    /// <summary>A single cell of lines running that way, <paramref name="fill"/> full, bonded <paramref name="bond"/> to the layer below.</summary>
    static CellStrength OneCell(Vector2 direction, float fill = 1, float bond = 1, PrintedStrength? strength = null)
    {
        var grid = new CellGrid(Vector2.Zero, Pitch, [0, Layer], 1, 1);
        grid.Deposit(0, 0, 0, fill * grid.CellVolume(0), Lines(direction));
        var mesh = FemMesh.Build(grid, 0.05f, [new Fixture(p => p.Z < 1e-3f)]);
        return new CellStrength(mesh, strength ?? Strength, [bond]);
    }

    /// <summary>The failure index of each mode, and Tsai–Wu's safety factor, for σxx, σyy, σzz, τxy, τyz, τzx.</summary>
    static (double[] Modes, double TsaiWu) Check(CellStrength cell, params float[] stress)
    {
        var modes = new double[FailureModes.Count];
        var tsaiWu = cell.Evaluate(0, stress, modes);
        return (modes, tsaiWu);
    }

    static double Of(double[] modes, FailureMode mode) => modes[(int)mode];

    [Fact]
    public void Pulled_along_its_lines_a_cell_breaks_its_lines_at_the_strength_along_them()
    {
        var (modes, tsaiWu) = Check(OneCell(Vector2.UnitX), 20, 0, 0, 0, 0, 0);

        Assert.Equal(0.5, Of(modes, FailureMode.LineBreak), 6);
        Assert.Equal(0, Of(modes, FailureMode.BeadSeparation), 9);
        Assert.Equal(0, Of(modes, FailureMode.LayerSeparation), 9);
        Assert.Equal(0, Of(modes, FailureMode.Crushing), 9);
        Assert.Equal(2, tsaiWu, 6); // uniaxial: Tsai-Wu breaks at the tensile strength too
    }

    [Fact]
    public void Pulled_across_its_lines_the_beads_come_apart()
    {
        var (modes, _) = Check(OneCell(Vector2.UnitY), 10, 0, 0, 0, 0, 0);

        Assert.Equal(0.5, Of(modes, FailureMode.BeadSeparation), 6);
        Assert.Equal(0, Of(modes, FailureMode.LineBreak), 6);
    }

    [Fact]
    public void Across_the_layers_the_strength_is_the_sheets_times_the_bond()
    {
        var (bonded, _) = Check(OneCell(Vector2.UnitX), 0, 0, 10, 0, 0, 0);
        var (weak, _) = Check(OneCell(Vector2.UnitX, bond: 0.5f), 0, 0, 10, 0, 0, 0);

        Assert.Equal(10.0 / 25, Of(bonded, FailureMode.LayerSeparation), 6);
        Assert.Equal(10.0 / 12.5, Of(weak, FailureMode.LayerSeparation), 6);
    }

    [Fact]
    public void A_layer_line_slides_under_shear_at_its_strength_over_root_three()
    {
        // τzx along lines that run along X: the layer line and the cross-section of the line both take it; the layer line is weaker.
        var (modes, _) = Check(OneCell(Vector2.UnitX), 0, 0, 0, 0, 0, 5);

        Assert.Equal(5 * Math.Sqrt(3) / 25, Of(modes, FailureMode.LayerSeparation), 6);
        Assert.True(Of(modes, FailureMode.LineBreak) < Of(modes, FailureMode.LayerSeparation));
    }

    [Fact]
    public void Pressed_together_a_cell_is_crushed_at_the_compressive_strength_and_no_layer_line_opens()
    {
        var (modes, tsaiWu) = Check(OneCell(Vector2.UnitX), 0, 0, -40, 0, 0, 0);

        Assert.Equal(0.5, Of(modes, FailureMode.Crushing), 6);
        Assert.Equal(0, Of(modes, FailureMode.LayerSeparation), 9);
        Assert.Equal(2, tsaiWu, 6);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(45)]
    [InlineData(60)]
    [InlineData(75)]
    [InlineData(90)]
    public void A_plastic_as_strong_every_way_breaks_at_its_strength_at_any_angle_to_its_lines(double degrees)
    {
        // Pulled along X with lines at an angle: every plane takes its shear, so no angle holds more than the strength.
        var same = new PrintedStrength(30, 30, 30, 30, []);
        var (modes, tsaiWu) = Check(OneCell(At(degrees), strength: same), 30, 0, 0, 0, 0, 0);

        Assert.InRange(modes.Max(), 1, 1.07);
        Assert.Equal(1, tsaiWu, 6); // and Tsai-Wu is von Mises for it
    }

    [Fact]
    public void Turning_the_lines_turns_the_stress_into_their_frame()
    {
        // Lines at 90° pulled along Y is the same as lines along X pulled along X.
        var (turned, _) = Check(OneCell(Vector2.UnitY), 0, 20, 0, 0, 0, 0);

        Assert.Equal(0.5, Of(turned, FailureMode.LineBreak), 5);
        Assert.Equal(0, Of(turned, FailureMode.BeadSeparation), 5);
    }

    [Fact]
    public void Where_lines_cross_the_strengths_along_and_across_them_are_mixed()
    {
        // Plastic that says nothing of its lines spreads fully: half along, half across, 30 MPa both ways.
        var grid = CellGrid.Solid(1, 1, 1, Pitch, Layer);
        var mesh = FemMesh.Build(grid, 0.05f, [new Fixture(p => p.Z < 1e-3f)]);
        var (alongX, _) = Check(new CellStrength(mesh, Strength), 15, 0, 0, 0, 0, 0);
        var (alongY, _) = Check(new CellStrength(mesh, Strength), 0, 15, 0, 0, 0, 0);

        Assert.Equal(0.5, alongX.Max(), 5);
        Assert.Equal(0.5, alongY.Max(), 5);
    }

    [Fact]
    public void A_cell_half_full_carries_its_stress_in_half_the_plastic_but_a_sliver_is_taken_as_half_full()
    {
        var (half, _) = Check(OneCell(Vector2.UnitX, fill: 0.5f), 10, 0, 0, 0, 0, 0);
        var (sliver, _) = Check(OneCell(Vector2.UnitX, fill: 0.1f), 10, 0, 0, 0, 0, 0);

        Assert.Equal(20.0 / 40, Of(half, FailureMode.LineBreak), 5);
        Assert.Equal(10 / CellStrength.FillFloor / 40, Of(sliver, FailureMode.LineBreak), 5);
    }

    /// <summary>A bar of lines along X, flat (60 cells long in X) or standing (60 in Z), clamped at its low end and pulled along its length.</summary>
    static (CellFailure Failure, FemMesh Mesh) Pull(bool standing, float force, float bond = 1)
    {
        var grid = standing ? CellGrid.Solid(4, 4, 60, Pitch, Pitch, Lines(Vector2.UnitX)) : CellGrid.Solid(60, 4, 4, Pitch, Pitch, Lines(Vector2.UnitX));
        var axis = standing ? Axis.Z : Axis.X;
        var loadCase = LoadCase.ClampAndPush(grid.OccupiedBounds(0.05f), axis, fixMax: false, force * LoadCase.Unit(axis));
        var result = StaticAnalysis.Run(grid, new OrthotropicMaterial(2580, 2580, 2060, 0.35), loadCase, new PcgSolver(), new AnalysisOptions(Tolerance: 1e-10));
        var cells = new CellStrength(result.Mesh, Strength, [.. Enumerable.Repeat(bond, result.Mesh.Cells.Length)]);
        return (cells.Check(result.Stress), result.Mesh);
    }

    /// <summary>The highest failure index of the cells in the middle half of the bar, away from the clamp and the load.</summary>
    static (double Index, FailureMode Mode) Middle(CellFailure failure, FemMesh mesh, bool standing)
    {
        var cells = Enumerable.Range(0, mesh.Cells.Length).Where(e => (standing ? mesh.Cells[e].K : mesh.Cells[e].I) is >= 15 and < 45);
        var worst = cells.MaxBy(e => failure.Index[e]);
        return (failure.Index[worst], failure.Mode[worst]);
    }

    [Fact]
    public void A_bar_lying_flat_breaks_its_lines_and_one_standing_up_its_layer_lines()
    {
        // A section of 4 × 4 cells, 1.68 mm on a side: 10 N is 3.54 MPa.
        const float force = 10;
        var stress = force / (4 * Pitch * 4 * Pitch);
        var (flat, flatMesh) = Pull(standing: false, force);
        var (standing, standingMesh) = Pull(standing: true, force, bond: 0.8f);

        var (flatIndex, flatMode) = Middle(flat, flatMesh, standing: false);
        var (standingIndex, standingMode) = Middle(standing, standingMesh, standing: true);

        Assert.Equal(FailureMode.LineBreak, flatMode);
        Assert.InRange(flatIndex / (stress / 40), 0.99, 1.01);
        Assert.Equal(FailureMode.LayerSeparation, standingMode);
        Assert.InRange(standingIndex / (stress / (25 * 0.8)), 0.99, 1.01);
    }

    [Fact]
    public void The_safety_factor_is_the_factor_on_the_load()
    {
        var (once, _) = Pull(standing: true, 10);
        var (twice, _) = Pull(standing: true, 20);

        Assert.Equal(once.SafetyFactor / 2, twice.SafetyFactor, 6);
        Assert.Equal(once.Weakest, twice.Weakest);
    }

    [Fact]
    public void The_octree_refines_where_the_part_is_nearest_to_breaking_and_finds_its_safety_factor()
    {
        // A cantilever bent down, the layer lines of its lower half bonded half as well: the octree's safety
        // factor is the full solve's, and the weakest cell ends up an element of its own.
        var grid = CellGrid.Solid(60, 8, 16, Pitch, Layer, Lines(Vector2.UnitX));
        var bounds = grid.OccupiedBounds(0.05f);
        var loadCase = LoadCase.ClampAndPush(bounds, Axis.X, fixMax: false, new Vector3(0, 0, -1));
        var mesh = FemMesh.Build(grid, 0.05f, loadCase.Fixtures);
        var bonds = mesh.Cells.Select(c => c.K < 8 ? 0.5f : 1f).ToArray();
        var material = new OrthotropicMaterial(2580, 2580, 2060, 0.35);
        var cells = new CellStrength(mesh, Strength, bonds);

        var full = cells.Check(StaticAnalysis.Run(mesh, material, loadCase, LinearSolvers.Best()).Stress);
        var adaptive = AdaptiveAnalysis.Run(mesh, material, loadCase, LinearSolvers.Best(), new AdaptiveOptions(RootLevel: 3, KeepPassFields: true), strength: cells);

        Assert.NotNull(adaptive.Failure);
        Assert.InRange(full.SafetyFactor / adaptive.Failure.SafetyFactor, 0.95, 1.05);
        Assert.Equal(0, adaptive.Octree.Leaves[adaptive.Octree.CellLeaf[adaptive.Failure.Weakest]].Level);
        Assert.Equal(adaptive.Failure.Weakest, adaptive.Final.PeakCell);
        Assert.All(adaptive.Passes, pass => Assert.Equal(1 / pass.FailureIndex!.Max(), pass.SafetyFactor, 5));
    }

    [Fact]
    public void The_strengths_come_from_the_data_sheet_and_say_what_is_assumed()
    {
        var material = new Material
        {
            Name = "PLA",
            Values =
            {
                [MaterialProperty.TensileStrengthXY] = new MaterialValue(35, Source: "tds"),
                [MaterialProperty.TensileStrengthZ] = new MaterialValue(31, Source: "tds"),
                [MaterialProperty.BendingStrengthXY] = new MaterialValue(76, Source: "tds"),
            },
        };

        var strength = material.Strength(out var lacking)!;

        Assert.Null(lacking);
        Assert.Equal((35, 31, 31, 76), (strength.AlongLine, strength.AcrossLine, strength.AcrossLayers, strength.Compression));
        Assert.Equal(31 / Math.Sqrt(3), strength.LayerShear, 9);
        Assert.Contains(strength.Assumed, a => a.StartsWith("bead to bead", StringComparison.Ordinal));
        Assert.Contains(strength.Assumed, a => a.StartsWith("in compression", StringComparison.Ordinal));
        Assert.Contains(strength.Assumed, a => a.StartsWith("in shear", StringComparison.Ordinal));
    }

    [Fact]
    public void Without_a_strength_across_the_layers_the_one_in_the_plane_is_taken_and_called_optimistic()
    {
        var material = new Material { Name = "X", Values = { [MaterialProperty.TensileStrengthXY] = new MaterialValue(40, Source: "tds") } };

        var strength = material.Strength(out _)!;

        Assert.Equal((40, 40, 40), (strength.AcrossLayers, strength.AcrossLine, strength.Compression));
        Assert.Contains(strength.Assumed, a => a.Contains("optimistic", StringComparison.Ordinal));
    }

    [Fact]
    public void Without_a_tensile_strength_there_is_no_safety_factor()
    {
        Assert.Null(Material.Generic().Strength(out var lacking));
        Assert.Contains("tensile strength", lacking);
    }
}
