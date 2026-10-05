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
public sealed unsafe partial class AmgclSolver(AmgclSmoother smoother = AmgclSmoother.Ilu0) : ILinearSolver
{
    const string Library = "AmgclBridge";

    static readonly Lazy<bool> Available = new(() =>
    {
        if (!NativeLibrary.TryLoad(Library, typeof(AmgclSolver).Assembly, DllImportSearchPath.AssemblyDirectory, out var handle))
            return false;
        NativeLibrary.Free(handle);
        return true;
    });

    public static bool IsAvailable => Available.Value;

    public string Name => $"AMGCL (SA-AMG/{smoother} + CG, rigid-body modes)";

    public SolveStatistics Solve(LinearSystem system, double[] x, double tolerance, int maxIterations)
    {
        if (!IsAvailable) throw new DllNotFoundException($"{Library}.dll not found; run native\\build.cmd.");

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
        {
            status = SolveElasticity(system.Size, ptr, col, val, coords, rhs, u, tolerance, maxIterations, (int)smoother,
                &iterations, &residual, &setupSeconds, &solveSeconds, message, error.Length);
        }
        if (status != 0)
            throw new InvalidOperationException("AMGCL failed: " + Encoding.UTF8.GetString(error).TrimEnd('\0'));

        return new SolveStatistics(Name, iterations, residual, residual <= tolerance,
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

/// <summary>AMG smoother; values match the bridge's relaxation argument.</summary>
public enum AmgclSmoother { Ilu0 = 0, Spai0 = 1, Chebyshev = 2 }

public static class LinearSolvers
{
    /// <summary>AMGCL when its DLL is present, otherwise the C# PCG.</summary>
    public static ILinearSolver Best() => AmgclSolver.IsAvailable ? new AmgclSolver() : new PcgSolver();

    /// <summary>auto, pcg, amg (ILU0 smoother), amg-spai0, amg-chebyshev.</summary>
    public static ILinearSolver ByName(string name) => name.ToLowerInvariant() switch
    {
        "amg" or "amgcl" or "amg-ilu0" => new AmgclSolver(AmgclSmoother.Ilu0),
        "amg-spai0" => new AmgclSolver(AmgclSmoother.Spai0),
        "amg-chebyshev" => new AmgclSolver(AmgclSmoother.Chebyshev),
        "pcg" => new PcgSolver(),
        "auto" => Best(),
        _ => throw new ArgumentException($"Unknown solver '{name}' (use auto, pcg, amg, amg-spai0 or amg-chebyshev)."),
    };
}
