using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace GcodeFem.Core.Fem;

/// <param name="Residual">Relative residual |f − K u| / |f| reported by the solver.</param>
public sealed record SolveStatistics(string Solver, int Iterations, double Residual, bool Converged, TimeSpan Setup, TimeSpan Solve);

public interface ILinearSolver
{
    string Name { get; }

    /// <summary>Solves the system; <paramref name="x"/> holds the start guess and receives the solution.</summary>
    SolveStatistics Solve(LinearSystem system, double[] x, double tolerance, int maxIterations);
}

/// <summary>Conjugate gradients with a Jacobi (diagonal) preconditioner. No dependencies; slow on big or slender parts.</summary>
public sealed class PcgSolver : ILinearSolver
{
    public string Name => "C# PCG (Jacobi)";

    public SolveStatistics Solve(LinearSystem system, double[] x, double tolerance, int maxIterations)
    {
        var watch = Stopwatch.StartNew();
        var n = system.Size;
        var b = system.RightHandSide;
        var inverseDiagonal = system.Diagonal().Select(d => d > 0 ? 1 / d : 1).ToArray();
        var setup = watch.Elapsed;

        var r = new double[n];
        var z = new double[n];
        var p = new double[n];
        var q = new double[n];

        var bNorm = Math.Sqrt(Dot(b, b));
        if (bNorm == 0)
        {
            Array.Clear(x);
            return new SolveStatistics(Name, 0, 0, true, setup, watch.Elapsed - setup);
        }

        system.Multiply(x, q);
        for (var i = 0; i < n; i++)
        {
            r[i] = b[i] - q[i];
            z[i] = r[i] * inverseDiagonal[i];
            p[i] = z[i];
        }
        var rz = Dot(r, z);
        var residual = Math.Sqrt(Dot(r, r)) / bNorm;
        var iteration = 0;
        while (residual > tolerance && iteration < maxIterations)
        {
            system.Multiply(p, q);
            var alpha = rz / Dot(p, q);
            for (var i = 0; i < n; i++)
            {
                x[i] += alpha * p[i];
                r[i] -= alpha * q[i];
                z[i] = r[i] * inverseDiagonal[i];
            }
            var rzNext = Dot(r, z);
            var beta = rzNext / rz;
            rz = rzNext;
            for (var i = 0; i < n; i++) p[i] = z[i] + beta * p[i];
            residual = Math.Sqrt(Dot(r, r)) / bNorm;
            iteration++;
        }

        return new SolveStatistics(Name, iteration, residual, residual <= tolerance, setup, watch.Elapsed - setup);
    }

    static double Dot(double[] a, double[] b)
    {
        double sum = 0;
        for (var i = 0; i < a.Length; i++) sum += a[i] * b[i];
        return sum;
    }
}

/// <summary>
/// AMGCL through native/AmgclBridge: CG on 3×3 blocks with smoothed-aggregation AMG built from the
/// rigid-body modes. Needs AmgclBridge.dll next to the app (built by native/build.cmd).
/// </summary>
/// <param name="iluIterationLimit">
/// For Auto: ILU(0) converges in 20–70 iterations where it works, but it has no guarantee (it stalled
/// on a 2.4 M DOF printed bracket), so after this many Auto gives up on it and carries on with
/// Gauss-Seidel, which always smooths a positive definite matrix.
/// </param>
public sealed unsafe partial class AmgclSolver(AmgclSmoother smoother = AmgclSmoother.Auto, int iluIterationLimit = 150) : ILinearSolver
{
    const string Library = "AmgclBridge";

    /// <summary>One native solve at a time: each already uses every core through OpenMP.</summary>
    static readonly Lock Native = new();

    static readonly Lazy<bool> Available = new(() =>
    {
        if (!NativeLibrary.TryLoad(Library, typeof(AmgclSolver).Assembly, DllImportSearchPath.AssemblyDirectory, out var handle))
            return false;
        NativeLibrary.Free(handle);
        return true;
    });

    public static bool IsAvailable => Available.Value;

    public string Name => Describe(smoother == AmgclSmoother.Auto ? AmgclSmoother.Ilu0 : smoother);

    public SolveStatistics Solve(LinearSystem system, double[] x, double tolerance, int maxIterations)
    {
        if (!IsAvailable) throw new DllNotFoundException($"{Library}.dll not found; run native\\build.cmd.");
        if (smoother != AmgclSmoother.Auto) return Solve(system, x, tolerance, maxIterations, smoother);
        // Short of memory, the leaner smoother that fits beats the faster one that does not.
        if (!Fits(system, AmgclSmoother.Ilu0)) return Solve(system, x, tolerance, maxIterations, AmgclSmoother.GaussSeidel);

        var guess = (double[])x.Clone();
        var first = Solve(system, x, tolerance, Math.Min(maxIterations, iluIterationLimit), AmgclSmoother.Ilu0);
        if (first.Converged || maxIterations <= iluIterationLimit) return first;

        if (!(first.Residual < 1)) guess.CopyTo(x, 0); // ILU made it worse: back to the caller's start guess
        var second = Solve(system, x, tolerance, maxIterations - first.Iterations, AmgclSmoother.GaussSeidel);
        return second with
        {
            Solver = $"{second.Solver}, after ILU(0) stalled",
            Iterations = first.Iterations + second.Iterations,
            Setup = first.Setup + second.Setup,
            Solve = first.Solve + second.Solve,
        };
    }

    static string Describe(AmgclSmoother smoother) => $"AMGCL (SA-AMG/{smoother} + CG, rigid-body modes)";

    /// <summary>
    /// Native memory a solve allocates on top of the matrix it is given, per unknown: measured peaks
    /// of 5.0 KB (double-precision ILU(0)) and 3.6 KB (single-precision smoothers) less the 0.8 KB matrix.
    /// </summary>
    static long BytesNeeded(LinearSystem system, AmgclSmoother relaxation) =>
        system.Size * (relaxation == AmgclSmoother.Ilu0 ? 4200L : 2800L);

    /// <summary>
    /// With a quarter to spare, in RAM as well as in commit: other programs keep allocating while a
    /// solve runs, and a solve that has to page is slower than not starting it.
    /// </summary>
    static bool Fits(LinearSystem system, AmgclSmoother relaxation) => 1.25 * BytesNeeded(system, relaxation) <= Usable();

    static long Usable()
    {
        var (physical, commit) = MemoryStatus.Available();
        return Math.Min(physical, commit);
    }

    static SolveStatistics Solve(LinearSystem system, double[] x, double tolerance, int maxIterations, AmgclSmoother relaxation)
    {
        // Running out of commit inside the native solver kills the process, so refuse up front.
        if (!Fits(system, relaxation))
            throw new InvalidOperationException(
                $"Not enough memory for {system.Size:N0} unknowns: the solver needs about {BytesNeeded(system, relaxation) / 1e9:0.0} GB " +
                $"plus a quarter in reserve, and {Usable() / 1e9:0.0} GB is free. Close other programs or use the octree solve.");

        var error = new byte[1024];
        int status, iterations;
        double residual, setupSeconds, solveSeconds;
        fixed (int* ptr = system.RowPointers)
        fixed (int* col = system.Columns)
        fixed (double* val = system.Values)
        fixed (double* coords = system.Coordinates)
        fixed (double* rhs = system.RightHandSide)
        fixed (double* u = x)
        fixed (byte* message = error)
        lock (Native)
        {
            status = SolveElasticity(system.Size, ptr, col, val, coords, rhs, u, tolerance, maxIterations, (int)relaxation,
                &iterations, &residual, &setupSeconds, &solveSeconds, message, error.Length);
        }
        if (status != 0)
            throw new InvalidOperationException("AMGCL failed: " + Encoding.UTF8.GetString(error).TrimEnd('\0'));

        return new SolveStatistics(Describe(relaxation), iterations, residual, residual <= tolerance,
            TimeSpan.FromSeconds(setupSeconds), TimeSpan.FromSeconds(solveSeconds));
    }

    [LibraryImport(Library, EntryPoint = "amgcl_bridge_solve_elasticity")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]
    private static partial int SolveElasticity(
        int n, int* ptr, int* col, double* val, double* coords, double* rhs, double* x,
        double tolerance, int maxIterations, int relaxation,
        int* iterations, double* residual, double* setupSeconds, double* solveSeconds,
        byte* error, int errorCapacity);
}

/// <summary>
/// AMG smoother; values from 0 up match the bridge's relaxation argument. Auto is ILU(0) with
/// Gauss-Seidel as its fallback. ILU(0) runs the preconditioner in double precision and the others
/// in single precision. Ilu0Single saves about a quarter of the memory, but CG then stalls on
/// printed parts in bending (measured: residual stuck at 2e-4 on the L-bracket).
/// </summary>
public enum AmgclSmoother { Auto = -1, Ilu0Single = 0, Spai0 = 1, Chebyshev = 2, GaussSeidel = 3, Ilu0 = 4, Ilu1 = 5 }

public static class LinearSolvers
{
    /// <summary>AMGCL when its DLL is present, otherwise the C# PCG.</summary>
    public static ILinearSolver Best() => AmgclSolver.IsAvailable ? new AmgclSolver() : new PcgSolver();

    /// <summary>auto, pcg, amg (ILU0, else Gauss-Seidel), amg-ilu0, amg-ilu0-single, amg-ilu1, amg-gs, amg-spai0, amg-chebyshev.</summary>
    public static ILinearSolver ByName(string name) => name.ToLowerInvariant() switch
    {
        "amg" or "amgcl" => new AmgclSolver(),
        "amg-ilu0" => new AmgclSolver(AmgclSmoother.Ilu0),
        "amg-spai0" => new AmgclSolver(AmgclSmoother.Spai0),
        "amg-chebyshev" => new AmgclSolver(AmgclSmoother.Chebyshev),
        "amg-gs" => new AmgclSolver(AmgclSmoother.GaussSeidel),
        "amg-ilu0-single" => new AmgclSolver(AmgclSmoother.Ilu0Single),
        "amg-ilu1" => new AmgclSolver(AmgclSmoother.Ilu1),
        "pcg" => new PcgSolver(),
        "auto" => Best(),
        _ => throw new ArgumentException($"Unknown solver '{name}' (use auto, pcg, amg, amg-ilu0, amg-ilu0-single, amg-ilu1, amg-gs, amg-spai0 or amg-chebyshev)."),
    };
}
