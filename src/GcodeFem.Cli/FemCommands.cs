using System.Diagnostics;
using System.Numerics;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Geometry;

namespace GcodeFem.Cli;

static class FemCommands
{
    /// <summary>Slice → voxelize → clamp one bbox face, load the opposite one → solve, with a timing breakdown.</summary>
    public static async Task<int> Solve(CommandLine commandLine)
    {
        var (result, toolpath) = await SliceCommands.SliceAndParse(commandLine);
        var material = new IsotropicMaterial(commandLine.Number("E", IsotropicMaterial.Pla.YoungsModulus), commandLine.Number("nu", IsotropicMaterial.Pla.PoissonRatio));
        var (axis, fixMax) = ParseFace(commandLine.Option("fix") ?? "zmin");
        var force = Text.Triple(commandLine.Option("force") ?? "0,0,-100", "force");
        var solver = LinearSolvers.ByName(commandLine.Option("solver") ?? "auto");
        var options = new AnalysisOptions(MinFill: (float)commandLine.Number("min-fill", 0.05));

        var watch = Stopwatch.StartNew();
        var grid = Voxelizer.Voxelize(toolpath, result.Placement, result.PrintMesh.Bounds);
        var voxelTime = watch.Elapsed;

        var bounds = result.PrintMesh.Bounds;
        var fem = StaticAnalysis.Run(grid, material, LoadCase.ClampAndPush(bounds, axis, fixMax, force), solver, options);

        Console.WriteLine();
        Console.WriteLine($"load case    clamp {commandLine.Option("fix") ?? "zmin"} face, {Text.Format(force)} N on the opposite face; E {material.YoungsModulus:0} MPa, nu {material.PoissonRatio}");
        PrintAnalysis(grid, fem, voxelTime);

        var loaded = LoadCase.Component(fixMax ? bounds.Min : bounds.Max, axis);
        var face = Enumerable.Range(0, fem.Mesh.NodeCount)
            .Where(n => MathF.Abs(LoadCase.Component(fem.Mesh.NodePosition(n), axis) - loaded) < 1e-3f)
            .Select(fem.Displacement).ToList();
        if (face.Count > 0)
        {
            var mean = face.Aggregate(Vector3.Zero, (a, b) => a + b) / face.Count;
            Console.WriteLine($"  loaded face mean displacement {Text.Format(mean)} mm");
        }
        return 0;
    }

    /// <summary>Solid cantilevers of growing size: DOF vs time vs memory, tip deflection vs Timoshenko.</summary>
    public static int Bench(CommandLine commandLine)
    {
        var scales = (commandLine.Option("scales") ?? "2,4,6,8").Split(',').Select(int.Parse).ToArray();
        var which = commandLine.Option("solver") ?? "both";
        var solvers = which == "both"
            ? (AmgclSolver.IsAvailable ? new ILinearSolver[] { new AmgclSolver(), new PcgSolver() } : [new PcgSolver()])
            : [LinearSolvers.ByName(which)];
        var pcgMaxDofs = (int)commandLine.Number("pcg-max-dofs", 300_000);
        var material = IsotropicMaterial.Pla;
        const float pitch = 0.42f, layer = 0.2f;

        Console.WriteLine($"cantilever benchmark: 25s x 4s x 8s bead cells ({pitch} x {pitch} x {layer} mm), clamped at x = 0, 1 N down at the tip, {Environment.ProcessorCount} threads");
        Console.WriteLine($"{"scale",5} {"cells",9} {"DOF",10} {"nnz M",7} {"matrix",8} {"mesh+asm s",11} {"setup s",8} {"solve s",8} {"iters",6} {"tip/theory",10} {"peak RAM",9}  solver");
        foreach (var scale in scales)
        {
            int nx = 25 * scale, ny = 4 * scale, nz = 8 * scale;
            var grid = CellGrid.Solid(nx, ny, nz, pitch, layer);
            var bounds = new Box3(Vector3.Zero, new Vector3(nx * pitch, ny * pitch, nz * layer));
            var loadCase = LoadCase.ClampAndPush(bounds, Axis.X, fixMax: false, new Vector3(0, 0, -1));
            foreach (var solver in solvers)
            {
                if (solver is PcgSolver && 3L * (nx + 1) * (ny + 1) * (nz + 1) > pcgMaxDofs)
                {
                    Console.WriteLine($"{scale,5} {"",9} {"",10} {"",7} {"",8} {"",11} {"",8} {"",8} {"",6} {"",10} {"",9}  {solver.Name}: skipped (> --pcg-max-dofs)");
                    continue;
                }
                GC.Collect();
                GC.WaitForPendingFinalizers();
                var fem = StaticAnalysis.Run(grid, material, loadCase, solver);
                var tip = Enumerable.Range(0, fem.Mesh.NodeCount)
                    .Where(n => MathF.Abs(fem.Mesh.NodePosition(n).X - bounds.Max.X) < 1e-3f)
                    .Average(n => -fem.Displacement(n).Z);
                var theory = Timoshenko(material, bounds.Size.X, bounds.Size.Y, bounds.Size.Z);
                Console.WriteLine($"{scale,5} {fem.Mesh.Cells.Length,9:N0} {fem.Dofs,10:N0} {fem.System.NonZeros / 1e6,7:0.0} {Text.Megabytes(fem.System.Bytes),8} " +
                                  $"{(fem.MeshTime + fem.AssemblyTime).TotalSeconds,11:0.00} {fem.Solve.Setup.TotalSeconds,8:0.00} {fem.Solve.Solve.TotalSeconds,8:0.00} " +
                                  $"{fem.Solve.Iterations,6} {tip / theory,10:0.000} {Text.Megabytes(PeakWorkingSet()),9}  {solver.Name}{(fem.Solve.Converged ? "" : " NOT CONVERGED")}");
            }
        }
        return 0;
    }

    static void PrintAnalysis(CellGrid grid, FemResult fem, TimeSpan voxelTime)
    {
        var mesh = fem.Mesh;
        Console.WriteLine($"cells        grid {grid.SizeX} x {grid.SizeY} x {grid.SizeZ}, {grid.BlockCount} blocks of {CellGrid.BlockSize}^3, " +
                          $"{mesh.Cells.Length:N0} active, {mesh.DroppedCells:N0} dropped ({mesh.DroppedVolume:0.0} mm3 not connected to the clamp)");
        Console.WriteLine($"  voxelized  {voxelTime.TotalSeconds:0.00} s, {grid.TotalVolume:0.0} mm3 deposited, {grid.LostVolume:0.000} mm3 outside the grid");
        Console.WriteLine($"system       {mesh.NodeCount:N0} nodes, {fem.Dofs:N0} DOF, {fem.System.NonZeros / 1e6:0.0} M non-zeros, {Text.Megabytes(fem.System.Bytes)}; " +
                          $"mesh {fem.MeshTime.TotalSeconds:0.00} s, assembly {fem.AssemblyTime.TotalSeconds:0.00} s");
        Console.WriteLine($"solver       {fem.Solve.Solver}: {fem.Solve.Iterations} iterations, residual {fem.Solve.Residual:0.0e+0}{(fem.Solve.Converged ? "" : " NOT CONVERGED")}, " +
                          $"setup {fem.Solve.Setup.TotalSeconds:0.00} s, solve {fem.Solve.Solve.TotalSeconds:0.00} s; stresses {fem.StressTime.TotalSeconds:0.00} s");

        var worst = Enumerable.Range(0, mesh.Cells.Length).MaxBy(e => fem.VonMises[e]);
        var c = mesh.Cells[worst];
        var centre = grid.NodePosition(c.I, c.J, c.K) + new Vector3(grid.Pitch / 2, grid.Pitch / 2, grid.CellHeight(c.K) / 2);
        Console.WriteLine($"result       max |u| {fem.MaxDisplacement():0.0000} mm, max von Mises {fem.VonMises[worst]:0.00} MPa at {Text.Format(centre)} (fill {mesh.Fill[worst]:0.00}); " +
                          $"applied {Text.Format(fem.AppliedForce())} N");
        Console.WriteLine($"memory       peak working set {Text.Megabytes(PeakWorkingSet())}");
    }

    static double Timoshenko(IsotropicMaterial m, double length, double width, double height)
    {
        var inertia = width * height * height * height / 12;
        var kappa = 10 * (1 + m.PoissonRatio) / (12 + 11 * m.PoissonRatio);
        return length * length * length / (3 * m.YoungsModulus * inertia) + length / (kappa * m.ShearModulus * width * height);
    }

    static (Axis Axis, bool Max) ParseFace(string text) => text.ToLowerInvariant() switch
    {
        "xmin" => (Axis.X, false), "xmax" => (Axis.X, true),
        "ymin" => (Axis.Y, false), "ymax" => (Axis.Y, true),
        "zmin" => (Axis.Z, false), "zmax" => (Axis.Z, true),
        _ => throw new FormatException($"--fix expects xmin, xmax, ymin, ymax, zmin or zmax but got '{text}'."),
    };

    static long PeakWorkingSet()
    {
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        return process.PeakWorkingSet64;
    }
}
