using System.Diagnostics;
using System.Numerics;

namespace GcodeFem.Core.Fem;

/// <param name="MinFill">Cells with less plastic than this fraction are left out (a sliver of a neighbouring bead).</param>
public sealed record AnalysisOptions(float MinFill = 0.05f, double Tolerance = 1e-8, int MaxIterations = 50_000);

public sealed class FemResult
{
    public required FemMesh Mesh { get; init; }
    public required LinearSystem System { get; init; }

    /// <summary>ux, uy, uz per node (mm), print frame.</summary>
    public required double[] Displacements { get; init; }

    /// <summary>σxx, σyy, σzz, τxy, τyz, τzx per cell at its centre (MPa), print frame.</summary>
    public required float[] Stress { get; init; }

    public required float[] VonMises { get; init; }
    public required SolveStatistics Solve { get; init; }
    public required TimeSpan MeshTime { get; init; }
    public required TimeSpan AssemblyTime { get; init; }
    public required TimeSpan StressTime { get; init; }

    public int Dofs => System.Size;

    public Vector3 Displacement(int node) =>
        new((float)Displacements[3 * node], (float)Displacements[3 * node + 1], (float)Displacements[3 * node + 2]);

    public double MaxDisplacement()
    {
        double max = 0;
        for (var n = 0; n < Mesh.NodeCount; n++) max = Math.Max(max, Displacement(n).Length());
        return max;
    }

    /// <summary>Sum of applied nodal loads; equals minus the support reactions.</summary>
    public Vector3 AppliedForce()
    {
        var f = Vector3.Zero;
        for (var n = 0; n < Mesh.NodeCount; n++)
            f += new Vector3((float)System.RightHandSide[3 * n], (float)System.RightHandSide[3 * n + 1], (float)System.RightHandSide[3 * n + 2]);
        return f;
    }
}

/// <summary>Linear static analysis on bead cells: mesh → assemble → solve → cell stresses.</summary>
public static class StaticAnalysis
{
    public static FemResult Run(CellGrid grid, IsotropicMaterial material, LoadCase loadCase, ILinearSolver solver, AnalysisOptions? options = null)
    {
        options ??= new AnalysisOptions();
        var watch = Stopwatch.StartNew();
        var mesh = FemMesh.Build(grid, options.MinFill, loadCase.Fixtures);
        return Run(mesh, material, loadCase, solver, options, watch.Elapsed);
    }

    /// <summary>Solves on a mesh that is already built (with the load case's fixtures).</summary>
    public static FemResult Run(FemMesh mesh, IsotropicMaterial material, LoadCase loadCase, ILinearSolver solver, AnalysisOptions? options = null, TimeSpan meshTime = default)
    {
        options ??= new AnalysisOptions();
        var watch = Stopwatch.StartNew();
        var system = Assembler.Assemble(mesh, material, loadCase);
        var assemblyTime = watch.Elapsed;

        var u = new double[system.Size];
        var statistics = solver.Solve(system, u, options.Tolerance, options.MaxIterations);

        watch.Restart();
        var (stress, vonMises) = CellStresses(mesh, material, u);
        return new FemResult
        {
            Mesh = mesh,
            System = system,
            Displacements = u,
            Stress = stress,
            VonMises = vonMises,
            Solve = statistics,
            MeshTime = meshTime,
            AssemblyTime = assemblyTime,
            StressTime = watch.Elapsed,
        };
    }

    /// <summary>Stress at each cell centre: σ = E φ C₁ B u.</summary>
    static (float[] Stress, float[] VonMises) CellStresses(FemMesh mesh, IsotropicMaterial material, double[] u)
    {
        var unit = new IsotropicMaterial(1, material.PoissonRatio).Constitutive();
        var cells = mesh.Cells.Length;
        var stress = new float[6 * cells];
        var vonMises = new float[cells];
        var strainMatrices = new Dictionary<float, double[]>();
        foreach (var h in mesh.Cells.Select(c => mesh.Grid.CellHeight(c.K)).Distinct())
        {
            var b = new double[6 * HexElement.Dofs];
            HexElement.StrainMatrix(mesh.Grid.Pitch, mesh.Grid.Pitch, h, 0, 0, 0, b);
            strainMatrices[h] = b;
        }

        Parallel.For(0, cells, e =>
        {
            var b = strainMatrices[mesh.Grid.CellHeight(mesh.Cells[e].K)];
            Span<double> strain = stackalloc double[6];
            for (var r = 0; r < 6; r++)
            {
                double sum = 0;
                for (var a = 0; a < 8; a++)
                {
                    var node = mesh.CellNodes[e * 8 + a];
                    for (var d = 0; d < 3; d++) sum += b[r * 24 + 3 * a + d] * u[3 * node + d];
                }
                strain[r] = sum;
            }
            var scale = material.YoungsModulus * mesh.Fill[e];
            Span<double> s = stackalloc double[6];
            for (var r = 0; r < 6; r++)
            {
                double sum = 0;
                for (var m = 0; m < 6; m++) sum += unit[r * 6 + m] * strain[m];
                s[r] = sum * scale;
                stress[6 * e + r] = (float)s[r];
            }
            vonMises[e] = (float)Math.Sqrt(0.5 * ((s[0] - s[1]) * (s[0] - s[1]) + (s[1] - s[2]) * (s[1] - s[2]) + (s[2] - s[0]) * (s[2] - s[0]))
                                           + 3 * (s[3] * s[3] + s[4] * s[4] + s[5] * s[5]));
        });
        return (stress, vonMises);
    }
}
