using System.Numerics;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Geometry;

namespace GcodeFem.Tests.Fem;

/// <summary>Runs only where native/build.cmd has produced AmgclBridge.dll.</summary>
public sealed class AmgclFactAttribute : FactAttribute
{
    public AmgclFactAttribute()
    {
        if (!AmgclSolver.IsAvailable) Skip = "AmgclBridge.dll not built (native\\build.cmd).";
    }
}

public class SolidBlockTests
{
    const float Pitch = 0.42f, Layer = 0.2f;
    static readonly IsotropicMaterial Material = new(2500, 0.35);

    static bool Near(float a, float b) => MathF.Abs(a - b) < 1e-4f;

    [Fact]
    public void Uniform_tension_is_reproduced_exactly()
    {
        // Patch test: symmetry supports on three faces, uniform traction on the +X face.
        var grid = CellGrid.Solid(6, 4, 5, Pitch, Layer);
        float lx = 6 * Pitch, ly = 4 * Pitch, lz = 5 * Layer;
        const float stress = 10;
        var loadCase = new LoadCase(
            [
                new Fixture(p => Near(p.X, 0), FixX: true, FixY: false, FixZ: false),
                new Fixture(p => Near(p.Y, 0), FixX: false, FixY: true, FixZ: false),
                new Fixture(p => Near(p.Z, 0), FixX: false, FixY: false, FixZ: true),
            ],
            [new SurfaceLoad((c, n) => Near(c.X, lx) && n.X > 0.5f, new Vector3(stress * ly * lz, 0, 0))]);

        var result = StaticAnalysis.Run(grid, Material, loadCase, new PcgSolver(), new AnalysisOptions(Tolerance: 1e-12));

        var strain = stress / Material.YoungsModulus;
        for (var n = 0; n < result.Mesh.NodeCount; n++)
        {
            var p = result.Mesh.NodePosition(n);
            var u = result.Displacement(n);
            Assert.Equal(strain * p.X, u.X, 7);
            Assert.Equal(-Material.PoissonRatio * strain * p.Y, u.Y, 7);
            Assert.Equal(-Material.PoissonRatio * strain * p.Z, u.Z, 7);
        }
        Assert.All(result.VonMises, s => Assert.Equal(stress, s, 3));
    }

    [Fact]
    public void Cantilever_matches_beam_theory()
    {
        // 25.2 × 3.36 × 3.2 mm beam, clamped at x = 0, 1 N down at the tip.
        int nx = 60, ny = 8, nz = 16;
        var grid = CellGrid.Solid(nx, ny, nz, Pitch, Layer);
        double length = nx * Pitch, width = ny * Pitch, height = nz * Layer;
        var bounds = new Box3(Vector3.Zero, new Vector3((float)length, (float)width, (float)height));
        var loadCase = LoadCase.ClampAndPush(bounds, Axis.X, fixMax: false, new Vector3(0, 0, -1));

        var result = StaticAnalysis.Run(grid, Material, loadCase, LinearSolvers.Best());

        // Timoshenko: bending + shear (Cowper's shear coefficient for a rectangle).
        var inertia = width * height * height * height / 12;
        var kappa = 10 * (1 + Material.PoissonRatio) / (12 + 11 * Material.PoissonRatio);
        var expected = length * length * length / (3 * Material.YoungsModulus * inertia)
                       + length / (kappa * Material.ShearModulus * width * height);
        var tip = Enumerable.Range(0, result.Mesh.NodeCount)
            .Where(n => Near(result.Mesh.NodePosition(n).X, (float)length))
            .Average(n => -result.Displacement(n).Z);

        Assert.True(result.Solve.Converged);
        Assert.InRange(tip / expected, 0.97, 1.03);
        Assert.Equal(-1, result.AppliedForce().Z, 4);
    }

    [AmgclFact]
    public void Amgcl_and_pcg_agree()
    {
        var grid = CellGrid.Solid(30, 4, 8, Pitch, Layer);
        var bounds = new Box3(Vector3.Zero, new Vector3(30 * Pitch, 4 * Pitch, 8 * Layer));
        var loadCase = LoadCase.ClampAndPush(bounds, Axis.X, fixMax: false, new Vector3(0, 0.3f, -1));

        var amg = StaticAnalysis.Run(grid, Material, loadCase, new AmgclSolver());
        var pcg = StaticAnalysis.Run(grid, Material, loadCase, new PcgSolver());

        Assert.True(amg.Solve.Converged);
        Assert.True(pcg.Solve.Converged);
        Assert.True(amg.Solve.Iterations < pcg.Solve.Iterations / 5, $"AMG {amg.Solve.Iterations} vs PCG {pcg.Solve.Iterations} iterations");
        for (var i = 0; i < amg.Displacements.Length; i++)
            Assert.Equal(pcg.Displacements[i], amg.Displacements[i], 6);
    }

    [AmgclFact]
    public void Gauss_seidel_takes_over_when_ilu_runs_out_of_iterations()
    {
        var grid = CellGrid.Solid(60, 8, 16, Pitch, Layer);
        var bounds = new Box3(Vector3.Zero, new Vector3(60 * Pitch, 8 * Pitch, 16 * Layer));
        var loadCase = LoadCase.ClampAndPush(bounds, Axis.X, fixMax: false, new Vector3(0, 0, -1));

        var patient = StaticAnalysis.Run(grid, Material, loadCase, new AmgclSolver());
        var impatient = StaticAnalysis.Run(grid, Material, loadCase, new AmgclSolver(iluIterationLimit: 3));

        Assert.DoesNotContain("stalled", patient.Solve.Solver);
        Assert.Contains("GaussSeidel", impatient.Solve.Solver);
        Assert.Contains("stalled", impatient.Solve.Solver);
        Assert.True(impatient.Solve.Converged);
        Assert.True(impatient.Solve.Iterations > patient.Solve.Iterations);
        for (var i = 0; i < patient.Displacements.Length; i++)
            Assert.Equal(patient.Displacements[i], impatient.Displacements[i], 6);
    }

    [AmgclFact]
    public void A_solve_that_cannot_fit_in_memory_is_refused_instead_of_crashing()
    {
        // Only the size matters for the check: 2 billion unknowns need terabytes.
        var system = new LinearSystem
        {
            Size = 2_000_000_001,
            RowPointers = [0],
            Columns = [],
            Values = [],
            RightHandSide = [],
            Coordinates = [],
            Fixed = [],
        };

        var error = Assert.Throws<InvalidOperationException>(() => new AmgclSolver().Solve(system, [], 1e-8, 100));

        Assert.Contains("Not enough memory", error.Message);
        var (physical, commit) = MemoryStatus.Available();
        Assert.True(physical > 0 && commit > 0);
    }

    [Fact]
    public void Drops_islands_that_do_not_reach_a_fixture()
    {
        var grid = CellGrid.Solid(10, 2, 2, Pitch, Layer);
        for (var k = 0; k < 2; k++)
        for (var j = 0; j < 2; j++)
            grid.Deposit(5, j, k, -grid.CellVolume(k)); // empty column x = 5 splits the bar in two

        var mesh = FemMesh.Build(grid, 0.05f, [new Fixture(p => Near(p.X, 0))]);

        Assert.Equal(5 * 2 * 2, mesh.Cells.Length);
        Assert.Equal(4 * 2 * 2, mesh.DroppedCells);
    }
}
