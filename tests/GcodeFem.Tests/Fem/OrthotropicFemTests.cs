using System.Numerics;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Gcode;
using GcodeFem.Core.Geometry;

namespace GcodeFem.Tests.Fem;

/// <summary>The solver with cells that are stiffer along their lines and across them than across the layers.</summary>
public class OrthotropicFemTests
{
    const float Pitch = 0.42f, Layer = 0.2f;
    static readonly OrthotropicMaterial Printed = new(3000, 2000, 1500, 0.35);

    static Deposition Lines(Vector2 direction) => new(Vector2.Normalize(direction), 0, 220, 1, FeatureClass.SolidInfill);

    static bool Near(float a, float b) => MathF.Abs(a - b) < 1e-4f;

    [Theory]
    [InlineData(Axis.X)]
    [InlineData(Axis.Y)]
    [InlineData(Axis.Z)]
    public void Uniform_tension_along_each_direction_is_reproduced_exactly(Axis axis)
    {
        // The patch test of SolidBlockTests, lines along X: E1 along X, E2 along Y, E3 along Z.
        var grid = CellGrid.Solid(6, 4, 5, Pitch, Layer, Lines(Vector2.UnitX));
        var size = new Vector3(6 * Pitch, 4 * Pitch, 5 * Layer);
        const float stress = 10;
        var along = LoadCase.Unit(axis);
        var section = size.X * size.Y * size.Z / LoadCase.Component(size, axis);
        var loadCase = new LoadCase(
            [
                new Fixture(p => Near(p.X, 0), FixX: true, FixY: false, FixZ: false),
                new Fixture(p => Near(p.Y, 0), FixX: false, FixY: true, FixZ: false),
                new Fixture(p => Near(p.Z, 0), FixX: false, FixY: false, FixZ: true),
            ],
            [new SurfaceLoad((c, n) => Near(LoadCase.Component(c, axis), LoadCase.Component(size, axis)) && Vector3.Dot(n, along) > 0.5f, stress * section * along)]);

        var result = StaticAnalysis.Run(grid, Printed, loadCase, new PcgSolver(), new AnalysisOptions(Tolerance: 1e-12));

        double[] moduli = [Printed.AlongLine, Printed.AcrossLine, Printed.AcrossLayers];
        var pulled = (int)axis;
        for (var n = 0; n < result.Mesh.NodeCount; n++)
        {
            var p = result.Mesh.NodePosition(n);
            var u = result.Displacement(n);
            for (var d = 0; d < 3; d++)
            {
                // Huber: the strain across is −ν σ / √(E_pulled E_across).
                var strain = d == pulled ? stress / moduli[d] : -Printed.PoissonRatio * stress / Math.Sqrt(moduli[pulled] * moduli[d]);
                Assert.Equal(strain * LoadCase.Component(p, (Axis)d), LoadCase.Component(u, (Axis)d), 1e-9);
            }
        }
        Assert.All(result.VonMises, s => Assert.Equal(stress, s, 3));
    }

    [Fact]
    public void A_full_coarse_element_of_slanted_lines_is_the_exact_brick()
    {
        // Galerkin summing integrates any one stiffness exactly, turned or not.
        var direction = new Vector2(MathF.Cos(MathF.PI / 6), MathF.Sin(MathF.PI / 6));
        var grid = CellGrid.Solid(8, 8, 8, Pitch, Layer, Lines(direction));
        var forest = new OctreeForest(FemMesh.Build(grid, 0.05f, [new Fixture(p => p.Z < 1e-3f)]), Printed, 3);

        var (matrix, scale) = forest.Stiffness(Assert.Single(forest.Leaves));

        var expected = HexElement.Stiffness(8 * (double)Pitch, 8 * (double)Pitch, 8 * (double)Layer, Printed.Constitutive(Math.PI / 6));
        Assert.Equal(1, scale);
        var tolerance = 1e-5 * expected.Max();
        for (var n = 0; n < expected.Length; n++) Assert.InRange(matrix[n], expected[n] - tolerance, expected[n] + tolerance);
    }

    [Fact]
    public void At_bead_level_the_octree_assembles_the_same_cells_the_same_way()
    {
        // Lines along X in even layers, along Y in odd ones, and one layer crossed. Layers of 0.25 mm,
        // whose boundaries are exact in floats, so that all cells have one height.
        var grid = new CellGrid(Vector2.Zero, Pitch, [.. Enumerable.Range(0, 7).Select(k => k * 0.25f)], 10, 5);
        for (var k = 0; k < 6; k++)
        for (var j = 0; j < 5; j++)
        for (var i = 0; i < 10; i++)
        {
            var volume = grid.CellVolume(k);
            grid.Deposit(i, j, k, volume, Lines(k % 2 == 0 ? Vector2.UnitX : Vector2.UnitY));
            if (k == 3) grid.Deposit(i, j, k, volume, Lines(Vector2.UnitX));
        }
        var bounds = new Box3(Vector3.Zero, new Vector3(10 * Pitch, 5 * Pitch, 6 * 0.25f));
        var loadCase = LoadCase.ClampAndPush(bounds, Axis.X, fixMax: false, new Vector3(0, 0.3f, -1));
        var mesh = FemMesh.Build(grid, 0.05f, loadCase.Fixtures);

        var reference = Assembler.Assemble(mesh, Printed, loadCase);
        var octree = OctreeMesh.Build(new OctreeForest(mesh, Printed, 0));
        var system = octree.Assemble(octree.RestrictLoads(Assembler.Loads(mesh, loadCase)), octree.RestrictFixtures(Assembler.FixedDofs(mesh, loadCase)));

        Assert.Equal(3, octree.Forest.Cells.KindCount); // along X, along Y, crossed
        Assert.Equal(reference.Columns, system.Columns);
        for (var n = 0; n < reference.Values.Length; n++) Assert.Equal(reference.Values[n], system.Values[n], 8);
    }

    /// <summary>Force over section over the strain of the middle half of a bar clamped at its low end and pulled at the other.</summary>
    static double Modulus(CellGrid grid, OrthotropicMaterial material, Axis axis)
    {
        var bounds = grid.OccupiedBounds(0.05f);
        const float force = 10;
        var result = StaticAnalysis.Run(grid, material, LoadCase.ClampAndPush(bounds, axis, fixMax: false, force * LoadCase.Unit(axis)), new PcgSolver(), new AnalysisOptions(Tolerance: 1e-10));
        var (low, length) = (LoadCase.Component(bounds.Min, axis), LoadCase.Component(bounds.Size, axis));
        double Shift(float at) => Enumerable.Range(0, result.Mesh.NodeCount)
            .Where(n => Near(LoadCase.Component(result.Mesh.NodePosition(n), axis), at)).Average(n => LoadCase.Component(result.Displacement(n), axis));
        var (from, to) = (low + length / 4, low + 3 * length / 4);
        var section = bounds.Size.X * bounds.Size.Y * bounds.Size.Z / length;
        return force / section / ((Shift(to) - Shift(from)) / (to - from));
    }

    [Fact]
    public void A_bar_standing_up_is_softer_than_one_lying_flat_by_the_materials_ratio()
    {
        // 60 cells long, 4 × 4 across, cells as tall as they are wide; the lines along X in every layer.
        var lines = Lines(Vector2.UnitX);
        var flat = CellGrid.Solid(60, 4, 4, Pitch, Pitch, lines);
        var upright = CellGrid.Solid(4, 4, 60, Pitch, Pitch, lines);
        var material = new OrthotropicMaterial(2580, 2580, 2060, 0.35);

        var ratio = Modulus(flat, material, Axis.X) / Modulus(upright, material, Axis.Z);

        Assert.Equal(2580.0 / 2060, ratio, 2);
        Assert.InRange(Modulus(flat, material, Axis.X) / 2580, 0.99, 1.01);
    }

    [Fact]
    public void Lines_across_a_flat_bar_give_it_the_stiffness_across_the_lines()
    {
        var along = CellGrid.Solid(60, 4, 4, Pitch, Pitch, Lines(Vector2.UnitX));
        var across = CellGrid.Solid(60, 4, 4, Pitch, Pitch, Lines(Vector2.UnitY));

        Assert.InRange(Modulus(along, Printed, Axis.X) / Printed.AlongLine, 0.99, 1.01);
        Assert.InRange(Modulus(across, Printed, Axis.X) / Printed.AcrossLine, 0.99, 1.01);
    }
}
