using System.Numerics;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Gcode;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Slicing;

namespace GcodeFem.Tests.Fem;

public class OctreeTests
{
    const float Pitch = 0.42f, Layer = 0.2f;
    static readonly IsotropicMaterial Material = new(2500, 0.35);
    static readonly Fixture Bed = new(p => p.Z < 1e-3f);

    static bool Near(float a, float b) => MathF.Abs(a - b) < 1e-4f;

    /// <summary>A full block whose first layer is thicker than the rest, as on most prints.</summary>
    static CellGrid UnevenBlock(int sizeX, int sizeY, int sizeZ)
    {
        var z = new float[sizeZ + 1];
        for (var k = 1; k <= sizeZ; k++) z[k] = z[k - 1] + (k == 1 ? 0.28f : Layer);
        var grid = new CellGrid(Vector2.Zero, Pitch, z, sizeX, sizeY);
        for (var k = 0; k < sizeZ; k++)
        for (var j = 0; j < sizeY; j++)
        for (var i = 0; i < sizeX; i++)
            grid.Deposit(i, j, k, grid.CellVolume(k));
        return grid;
    }

    static bool Touch(OctreeLeaf a, OctreeLeaf b) =>
        a.I <= b.I + b.Size && b.I <= a.I + a.Size && a.J <= b.J + b.Size && b.J <= a.J + a.Size && a.K <= b.K + b.Size && b.K <= a.K + a.Size;

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    public void A_full_coarse_element_is_the_exact_brick(int level)
    {
        // Summing the cells' stiffness through trilinear interpolation integrates the big brick exactly,
        // uneven layers included.
        var size = 1 << level;
        var grid = UnevenBlock(size, size, size);
        var forest = new OctreeForest(FemMesh.Build(grid, 0.05f, [Bed]), Material, level);

        var leaf = Assert.Single(forest.Leaves);
        var (matrix, scale) = forest.Stiffness(leaf);
        var expected = HexElement.Stiffness(size * (double)Pitch, size * (double)Pitch, grid.ZBoundaries[size], Material.Constitutive());

        Assert.Equal(1, scale);
        var tolerance = 1e-5 * expected.Max(); // layer heights are stored as floats
        for (var n = 0; n < expected.Length; n++) Assert.InRange(matrix[n], expected[n] - tolerance, expected[n] + tolerance);
    }

    [Fact]
    public void A_half_empty_coarse_element_is_softer_and_still_free_of_rigid_body_forces()
    {
        var grid = CellGrid.Solid(8, 8, 8, Pitch, Layer);
        for (var k = 0; k < 8; k++)
        for (var j = 0; j < 8; j++)
        for (var i = 4; i < 8; i++)
            grid.Deposit(i, j, k, -grid.CellVolume(k));
        var forest = new OctreeForest(FemMesh.Build(grid, 0.05f, [Bed]), Material, 3);

        var (matrix, _) = forest.Stiffness(Assert.Single(forest.Leaves));
        var full = HexElement.Stiffness(8 * (double)Pitch, 8 * (double)Pitch, 8 * (double)Layer, Material.Constitutive());

        Assert.InRange(matrix[0] / full[0], 0.05, 0.95);
        // Rotation about z, u = (−y, x, 0), at the element's corners.
        var u = new double[24];
        for (var a = 0; a < 8; a++) (u[3 * a], u[3 * a + 1]) = (-HexElement.CornerY[a] * 8 * Pitch, HexElement.CornerX[a] * 8 * Pitch);
        for (var r = 0; r < 24; r++)
        {
            double force = 0;
            for (var c = 0; c < 24; c++) force += matrix[r * 24 + c] * u[c];
            Assert.Equal(0, force, 6);
        }
    }

    [Fact]
    public void Refining_one_corner_keeps_neighbours_within_one_level()
    {
        var forest = new OctreeForest(FemMesh.Build(CellGrid.Solid(32, 32, 32, Pitch, Layer), 0.05f, [Bed]), Material, 4);
        Assert.Equal(8, forest.LeafCount);

        while (forest.TryFindLeaf(17, 15, 16, out var leaf) && leaf.Level > 0) forest.Refine([leaf]);

        var leaves = forest.Leaves.ToArray();
        Assert.Contains(leaves, l => l.Level == 0);
        Assert.Contains(leaves, l => l.Level == 3);
        Assert.Equal(32 * 32 * 32, leaves.Sum(l => l.Size * l.Size * l.Size)); // still fills the block exactly
        foreach (var a in leaves)
        foreach (var b in leaves)
            if (Touch(a, b)) Assert.InRange(Math.Abs(a.Level - b.Level), 0, 1);

        var octree = OctreeMesh.Build(forest); // throws if a hanging node had nothing solid to hang on
        Assert.True(octree.HangingCount > 0);
    }

    [Fact]
    public void Empty_children_get_no_element()
    {
        var grid = CellGrid.Solid(8, 8, 8, Pitch, Layer);
        for (var k = 4; k < 8; k++)
        for (var j = 0; j < 8; j++)
        for (var i = 0; i < 8; i++)
            grid.Deposit(i, j, k, -grid.CellVolume(k));
        var forest = new OctreeForest(FemMesh.Build(grid, 0.05f, [Bed]), Material, 3);

        forest.Refine(forest.Leaves.ToArray());

        Assert.Equal(4, forest.LeafCount);
        Assert.All(forest.Leaves, leaf => Assert.Equal((2, 0), (leaf.Level, leaf.K)));
    }

    [Fact]
    public void Uniform_tension_passes_through_hanging_nodes_exactly()
    {
        // The patch test of SolidBlockTests on a mesh with three element sizes and uneven layers.
        var grid = UnevenBlock(16, 16, 16);
        float lx = 16 * Pitch, ly = 16 * Pitch, lz = grid.ZBoundaries[16];
        const float stress = 10;
        var loadCase = new LoadCase(
            [
                new Fixture(p => Near(p.X, 0), FixX: true, FixY: false, FixZ: false),
                new Fixture(p => Near(p.Y, 0), FixX: false, FixY: true, FixZ: false),
                new Fixture(p => Near(p.Z, 0), FixX: false, FixY: false, FixZ: true),
            ],
            [new SurfaceLoad((c, n) => Near(c.X, lx) && n.X > 0.5f, new Vector3(stress * ly * lz, 0, 0))]);
        var mesh = FemMesh.Build(grid, 0.05f, loadCase.Fixtures);
        var forest = new OctreeForest(mesh, Material, 3);
        for (var pass = 0; pass < 2; pass++)
        {
            // One corner on the loaded face and one where the three supports meet.
            Assert.True(forest.TryFindLeaf(13, 13, 13, out var loaded));
            Assert.True(forest.TryFindLeaf(1, 1, 1, out var held));
            forest.Refine([loaded, held]);
        }
        var octree = OctreeMesh.Build(forest);
        Assert.Equal([1, 2, 3], octree.Leaves.Select(l => l.Level).Distinct().Order());
        Assert.True(octree.HangingCount > 0);

        var system = octree.Assemble(octree.RestrictLoads(Assembler.Loads(mesh, loadCase)), octree.RestrictFixtures(Assembler.FixedDofs(mesh, loadCase)));
        var unknowns = new double[system.Size];
        Assert.True(new PcgSolver().Solve(system, unknowns, 1e-12, 50_000).Converged);
        var u = octree.NodeDisplacements(unknowns);

        var strain = stress / Material.YoungsModulus;
        for (var n = 0; n < octree.NodeCount; n++)
        {
            var (i, j, k) = octree.NodeGrid(n);
            Assert.Equal(strain * forest.X(i), u[3 * n], 7);
            Assert.Equal(-Material.PoissonRatio * strain * forest.Y(j), u[3 * n + 1], 7);
            Assert.Equal(-Material.PoissonRatio * strain * forest.Z(k), u[3 * n + 2], 7);
        }
        Assert.All(octree.CellStresses(u, Material).VonMises, s => Assert.Equal(stress, s, 3));
    }

    [Fact]
    public void At_bead_level_the_octree_is_the_reference_system()
    {
        // A block with a notch, so the matrix pattern is not the same everywhere.
        var grid = UnevenBlock(12, 6, 7);
        for (var k = 3; k < 7; k++)
        for (var j = 0; j < 6; j++)
            grid.Deposit(5, j, k, -grid.CellVolume(k));
        var bounds = new Box3(Vector3.Zero, new Vector3(12 * Pitch, 6 * Pitch, grid.ZBoundaries[7]));
        var loadCase = LoadCase.ClampAndPush(bounds, Axis.X, fixMax: false, new Vector3(0, 0.2f, -1));
        var mesh = FemMesh.Build(grid, 0.05f, loadCase.Fixtures);

        var reference = Assembler.Assemble(mesh, Material, loadCase);
        var octree = OctreeMesh.Build(new OctreeForest(mesh, Material, 0));
        var system = octree.Assemble(octree.RestrictLoads(Assembler.Loads(mesh, loadCase)), octree.RestrictFixtures(Assembler.FixedDofs(mesh, loadCase)));

        Assert.Equal(0, octree.HangingCount);
        Assert.Equal(reference.RowPointers, system.RowPointers);
        Assert.Equal(reference.Columns, system.Columns);
        Assert.Equal(reference.Fixed, system.Fixed);
        for (var n = 0; n < reference.Values.Length; n++) Assert.Equal(reference.Values[n], system.Values[n], 8);
        for (var n = 0; n < reference.Size; n++) Assert.Equal(reference.RightHandSide[n], system.RightHandSide[n], 10);
    }

    [Fact]
    public void Coarse_meshes_are_too_stiff_and_loosen_with_every_pass()
    {
        // 25 × 3.4 × 3.2 mm cantilever. The octree's fields are a subset of the bead cells' fields,
        // so its compliance f·u can only grow towards the reference as it refines.
        var grid = CellGrid.Solid(60, 8, 16, Pitch, Layer);
        var bounds = new Box3(Vector3.Zero, new Vector3(60 * Pitch, 8 * Pitch, 16 * Layer));
        var loadCase = LoadCase.ClampAndPush(bounds, Axis.X, fixMax: false, new Vector3(0, 0, -1));
        var mesh = FemMesh.Build(grid, 0.05f, loadCase.Fixtures);

        var reference = StaticAnalysis.Run(mesh, Material, loadCase, LinearSolvers.Best());
        var adaptive = AdaptiveAnalysis.Run(mesh, Material, loadCase, LinearSolvers.Best(), new AdaptiveOptions(RootLevel: 3));

        var exact = reference.System.RightHandSide.Zip(reference.Displacements, (f, u) => f * u).Sum();
        Assert.True(adaptive.Passes.Count >= 3);
        Assert.InRange(adaptive.Passes[0].Compliance / exact, 0.3, 0.95);
        for (var p = 1; p < adaptive.Passes.Count; p++)
            Assert.True(adaptive.Passes[p].Compliance >= adaptive.Passes[p - 1].Compliance * (1 - 1e-6), $"pass {p} got stiffer");
        Assert.True(adaptive.Final.Compliance <= exact * (1 + 1e-6));
    }

    [Fact]
    public void Kept_pass_fields_trace_the_refinement()
    {
        var grid = CellGrid.Solid(60, 8, 16, Pitch, Layer);
        var bounds = new Box3(Vector3.Zero, new Vector3(60 * Pitch, 8 * Pitch, 16 * Layer));
        var loadCase = LoadCase.ClampAndPush(bounds, Axis.X, fixMax: false, new Vector3(0, 0, -1));
        var mesh = FemMesh.Build(grid, 0.05f, loadCase.Fixtures);

        var adaptive = AdaptiveAnalysis.Run(mesh, Material, loadCase, LinearSolvers.Best(), new AdaptiveOptions(RootLevel: 3, KeepPassFields: true));

        Assert.All(adaptive.Passes[0].CellLevels!, level => Assert.Equal(3, level));
        for (var p = 1; p < adaptive.Passes.Count; p++)
        for (var e = 0; e < mesh.Cells.Length; e++)
            Assert.True(adaptive.Passes[p].CellLevels![e] <= adaptive.Passes[p - 1].CellLevels![e], $"cell {e} got coarser in pass {p}");
        Assert.Equal(adaptive.Octree.CellLevels(), adaptive.Final.CellLevels);
        Assert.Same(adaptive.VonMises, adaptive.Final.VonMises);
        Assert.All(adaptive.Passes, pass => Assert.Equal(pass.MaxVonMises, pass.VonMises!.Max()));
    }

    [AmgclFact]
    public void Adaptive_cantilever_finds_the_reference_peak_with_fewer_unknowns()
    {
        var grid = CellGrid.Solid(160, 16, 32, Pitch, Layer);
        var bounds = new Box3(Vector3.Zero, new Vector3(160 * Pitch, 16 * Pitch, 32 * Layer));
        var loadCase = LoadCase.ClampAndPush(bounds, Axis.X, fixMax: false, new Vector3(0, 0, -5));
        var mesh = FemMesh.Build(grid, 0.05f, loadCase.Fixtures);

        var reference = StaticAnalysis.Run(mesh, Material, loadCase, new AmgclSolver());
        var adaptive = AdaptiveAnalysis.Run(mesh, Material, loadCase, new AmgclSolver(), new AdaptiveOptions(RootLevel: 3));

        var peak = reference.VonMises.Max();
        Assert.All(adaptive.Passes, pass => Assert.True(pass.Solve.Converged));
        Assert.InRange(adaptive.Final.MaxVonMises / peak, 0.95, 1.05);
        Assert.True(adaptive.Final.Dofs < 0.4 * reference.Dofs, $"{adaptive.Final.Dofs} of {reference.Dofs} DOF");
        Assert.Equal(0, adaptive.Final.PeakLevel);
    }

    [AmgclFact]
    public void Adaptive_solve_of_the_printed_cube_matches_bead_resolution()
    {
        // The sliced cube, clamped on the bed and pushed sideways at the top: walls and infill in bending.
        var toolpath = GcodeParser.Parse(TestPaths.Sample("cube20_X1C_PLA.gcode"));
        var report = SlicerReport.Read(TestPaths.Sample("cube20_X1C_PLA.result.json"));
        var model = Stl.Read(TestPaths.Sample("cube20.stl")).Bounds;
        var grid = Voxelizer.Voxelize(toolpath, Placement.FromSlice(Matrix4x4.Identity, model, report.ObjectBounds), model);
        var loadCase = LoadCase.ClampAndPush(grid.OccupiedBounds(0.05f), Axis.Z, fixMax: false, new Vector3(50, 0, 0));
        var mesh = FemMesh.Build(grid, 0.05f, loadCase.Fixtures);

        var reference = StaticAnalysis.Run(mesh, IsotropicMaterial.Pla, loadCase, new AmgclSolver());
        var adaptive = AdaptiveAnalysis.Run(mesh, IsotropicMaterial.Pla, loadCase, new AmgclSolver(), new AdaptiveOptions(RootLevel: 3));

        var peak = reference.VonMises.Max();
        Assert.True(reference.Solve.Converged);
        Assert.All(adaptive.Passes, pass => Assert.True(pass.Solve.Converged));
        Assert.InRange(adaptive.Passes[0].MaxVonMises / peak, 0.3, 0.9); // 8³-cell elements read far too low
        Assert.InRange(adaptive.Final.MaxVonMises / peak, 0.95, 1.05);
        Assert.True(adaptive.Final.Dofs < 0.5 * reference.Dofs, $"{adaptive.Final.Dofs} of {reference.Dofs} DOF");
    }

    [Fact]
    public void Bigger_parts_and_smaller_machines_get_coarser_roots()
    {
        var mesh = FemMesh.Build(CellGrid.Solid(64, 64, 64, Pitch, Layer), 0.05f, [Bed]);

        Assert.Equal(3 * 9 * 9 * 9, OctreeAdvisor.CoarseDofs(mesh, 3));
        Assert.Equal(3, OctreeAdvisor.SuggestRootLevel(mesh, 1_000_000));
        Assert.Equal(4, OctreeAdvisor.SuggestRootLevel(mesh, 20_000));
        Assert.Equal(5, OctreeAdvisor.SuggestRootLevel(mesh, 1_000));
    }

    [Fact]
    public void Small_parts_are_solved_at_bead_resolution_without_an_octree()
    {
        var grid = CellGrid.Solid(30, 4, 8, Pitch, Layer);
        var bounds = new Box3(Vector3.Zero, new Vector3(30 * Pitch, 4 * Pitch, 8 * Layer));
        var loadCase = LoadCase.ClampAndPush(bounds, Axis.X, fixMax: false, new Vector3(0, 0, -1));
        var mesh = FemMesh.Build(grid, 0.05f, loadCase.Fixtures);

        var reference = StaticAnalysis.Run(mesh, Material, loadCase, LinearSolvers.Best());
        var adaptive = AdaptiveAnalysis.Run(mesh, Material, loadCase, LinearSolvers.Best());

        Assert.Equal(0, adaptive.RootLevel);
        Assert.Single(adaptive.Passes);
        Assert.Equal(reference.Dofs, adaptive.Final.Dofs);
        for (var e = 0; e < reference.VonMises.Length; e++) Assert.Equal(reference.VonMises[e], adaptive.VonMises[e], 4);
        for (var n = 0; n < mesh.NodeCount; n++) Assert.Equal(reference.Displacement(n).Z, adaptive.Displacement(n).Z, 6);
    }
}
