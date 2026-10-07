using System.Diagnostics;
using System.Numerics;

namespace GcodeFem.Core.Fem;

/// <summary>
/// Defaults as measured in M3 on printed parts: k = 2 with one buffer ring and 90 % of the energy in
/// fine elements puts the peak stress within 5 % of a full bead-resolution solve; k = 3 gets it
/// within 1 % for about half as many unknowns again.
/// </summary>
/// <param name="RootLevel">Coarse elements are 2^RootLevel cells per side; 0 solves every bead cell. Null picks a level from the part's size and free memory.</param>
/// <param name="RefineFactor">k: a leaf is refined when one of its cells has a von Mises stress above peak / k.</param>
/// <param name="Buffer">Rings of same-size neighbours refined along with each marked leaf.</param>
/// <param name="RefineInterfaces">Always refine leaves that touch a fixture or a load.</param>
/// <param name="EnergyFraction">Share of the strain energy that must sit in elements of at most 2³ cells: the coarser leaves holding the most energy are refined until it does. 0 turns it off.</param>
/// <param name="ConvergedChange">Stop once the peak sits in bead-resolution cells and the last pass moved the stress there by less than this fraction.</param>
/// <param name="MaxDofs">A refinement that would exceed this is not solved; null derives it from free memory.</param>
/// <param name="KeepPassFields">Keep every pass's element levels and stresses per cell, for a viewer to step through; 5 bytes per cell and pass.</param>
public sealed record AdaptiveOptions(
    int? RootLevel = null,
    double RefineFactor = 2,
    int Buffer = 1,
    bool RefineInterfaces = true,
    double EnergyFraction = 0.9,
    double ConvergedChange = 0.03,
    int MaxPasses = 10,
    int? MaxDofs = null,
    float MinFill = 0.05f,
    double Tolerance = 1e-8,
    int MaxIterations = 50_000,
    bool KeepPassFields = false);

/// <param name="LeavesByLevel">Elements per level, bead cells first.</param>
/// <param name="PeakCell">Bead cell with the highest von Mises stress, and <paramref name="PeakLevel"/> the level of its leaf.</param>
/// <param name="Compliance">f · u, twice the strain energy: lower than the true value while the mesh is too stiff.</param>
/// <param name="Marked">Leaves this pass marked for refinement.</param>
/// <param name="BuildTime">Refinement, Galerkin stiffness, mesh and assembly.</param>
public sealed record AdaptivePass(
    int Index,
    int[] LeavesByLevel,
    int Dofs,
    int HangingNodes,
    double MaxVonMises,
    int PeakCell,
    int PeakLevel,
    double Compliance,
    int Marked,
    SolveStatistics Solve,
    TimeSpan BuildTime,
    TimeSpan StressTime)
{
    public int Leaves => LeavesByLevel.Sum();
    public TimeSpan Time => BuildTime + Solve.Setup + Solve.Solve + StressTime;

    /// <summary>Per bead cell: the level of its element in this pass. Only with <see cref="AdaptiveOptions.KeepPassFields"/>.</summary>
    public byte[]? CellLevels { get; init; }

    /// <summary>Per bead cell: its von Mises stress as this pass saw it. Only with <see cref="AdaptiveOptions.KeepPassFields"/>.</summary>
    public float[]? VonMises { get; init; }
}

public sealed class AdaptiveResult
{
    /// <summary>The bead cells; <see cref="Stress"/> and <see cref="VonMises"/> are per cell of this mesh.</summary>
    public required FemMesh Mesh { get; init; }

    /// <summary>The octree mesh of the last pass.</summary>
    public required OctreeMesh Octree { get; init; }

    /// <summary>ux, uy, uz per octree node (mm), print frame.</summary>
    public required double[] NodeDisplacements { get; init; }

    /// <summary>σxx, σyy, σzz, τxy, τyz, τzx per bead cell at its centre (MPa), print frame.</summary>
    public required float[] Stress { get; init; }

    public required float[] VonMises { get; init; }
    public required IReadOnlyList<AdaptivePass> Passes { get; init; }
    public required string StopReason { get; init; }
    public required int RootLevel { get; init; }

    public AdaptivePass Final => Passes[^1];
    public TimeSpan Time => Passes.Aggregate(TimeSpan.Zero, (sum, pass) => sum + pass.Time);

    /// <summary>Displacement at a node of the bead-cell mesh.</summary>
    public Vector3 Displacement(int node)
    {
        var (i, j, k) = Mesh.Undense(Mesh.DenseOfNode[node]);
        var (x, y, z) = Octree.DisplacementAt(NodeDisplacements, i, j, k);
        return new Vector3((float)x, (float)y, (float)z);
    }
}

/// <summary>
/// Linear static analysis on an octree over the bead cells: solve with coarse elements, read the
/// stress of every bead cell off that solution, refine where it is high, and repeat. The cost then
/// follows the size of the hotspots instead of the size of the part.
/// </summary>
public static class AdaptiveAnalysis
{
    /// <summary>Elements up to this level (2³ cells) count as fine for <see cref="AdaptiveOptions.EnergyFraction"/>.</summary>
    const int FineLevel = 1;

    sealed record Solved(OctreeMesh Octree, double[] NodeDisplacements, float[] Stress, float[] VonMises);

    public static AdaptiveResult Run(CellGrid grid, IsotropicMaterial material, LoadCase loadCase, ILinearSolver solver, AdaptiveOptions? options = null, Action<AdaptivePass>? onPass = null)
    {
        options ??= new AdaptiveOptions();
        return Run(FemMesh.Build(grid, options.MinFill, loadCase.Fixtures), material, loadCase, solver, options, onPass);
    }

    /// <summary>Runs on a mesh that is already built (with the load case's fixtures).</summary>
    /// <param name="onPass">Called after every pass, for progress display.</param>
    public static AdaptiveResult Run(FemMesh mesh, IsotropicMaterial material, LoadCase loadCase, ILinearSolver solver, AdaptiveOptions? options = null, Action<AdaptivePass>? onPass = null)
    {
        options ??= new AdaptiveOptions();
        var maxDofs = options.MaxDofs ?? OctreeAdvisor.DofBudget();
        var rootLevel = options.RootLevel ?? OctreeAdvisor.SuggestRootLevel(mesh, maxDofs);

        var watch = Stopwatch.StartNew();
        var loadedCells = new HashSet<int>();
        var fineLoads = Assembler.Loads(mesh, loadCase, loadedCells);
        var fineFixed = Assembler.FixedDofs(mesh, loadCase);
        var interfaces = options.RefineInterfaces ? InterfaceCells(mesh, fineFixed, loadedCells) : [];
        var forest = new OctreeForest(mesh, material, rootLevel);

        var passes = new List<AdaptivePass>();
        Solved? last = null;
        string stop;
        while (true)
        {
            var octree = OctreeMesh.Build(forest);
            if (last is not null && octree.Dofs > maxDofs)
            {
                stop = $"the next pass needs {octree.Dofs:N0} DOF, over the budget of {maxDofs:N0}";
                break;
            }
            var system = octree.Assemble(octree.RestrictLoads(fineLoads), octree.RestrictFixtures(fineFixed));
            var unknowns = last is null ? new double[octree.Dofs] : octree.Interpolate(last.Octree, last.NodeDisplacements);
            for (var d = 0; d < unknowns.Length; d++)
                if (system.Fixed[d]) unknowns[d] = 0;
            var buildTime = watch.Elapsed;

            var statistics = solver.Solve(system, unknowns, options.Tolerance, options.MaxIterations);

            watch.Restart();
            var nodeDisplacements = octree.NodeDisplacements(unknowns);
            var (stress, vonMises) = octree.CellStresses(nodeDisplacements, material);
            var peak = 0;
            for (var e = 1; e < vonMises.Length; e++)
                if (vonMises[e] > vonMises[peak]) peak = e;
            var marked = Mark(octree, vonMises, vonMises[peak], interfaces, nodeDisplacements, options);
            double compliance = 0;
            for (var d = 0; d < unknowns.Length; d++) compliance += system.RightHandSide[d] * unknowns[d];

            var settled = last is not null && Settled(last.VonMises, vonMises, passes[^1].PeakCell, peak, octree, options.ConvergedChange);
            passes.Add(new AdaptivePass(passes.Count, octree.LeavesByLevel(), octree.Dofs, octree.HangingCount, vonMises[peak], peak,
                octree.Leaves[octree.CellLeaf[peak]].Level, compliance, marked.Count, statistics, buildTime, watch.Elapsed)
            {
                CellLevels = options.KeepPassFields ? octree.CellLevels() : null,
                VonMises = options.KeepPassFields ? vonMises : null,
            });
            last = new Solved(octree, nodeDisplacements, stress, vonMises);
            onPass?.Invoke(passes[^1]);

            if (marked.Count == 0)
            {
                stop = "every hotspot is at bead resolution";
                break;
            }
            if (settled)
            {
                stop = $"the peak is at bead resolution and the last pass moved it by under {options.ConvergedChange:0.#%}";
                break;
            }
            if (passes.Count >= options.MaxPasses)
            {
                stop = $"pass limit ({options.MaxPasses})";
                break;
            }

            watch.Restart();
            forest.Refine(marked.Select(leaf => octree.Leaves[leaf]));
        }

        return new AdaptiveResult
        {
            Mesh = mesh,
            Octree = last.Octree,
            NodeDisplacements = last.NodeDisplacements,
            Stress = last.Stress,
            VonMises = last.VonMises,
            Passes = passes,
            StopReason = stop,
            RootLevel = rootLevel,
        };
    }

    /// <summary>Cells that touch a fixture (a held corner) or carry a load.</summary>
    static int[] InterfaceCells(FemMesh mesh, bool[] fineFixed, HashSet<int> loadedCells)
    {
        var cells = new HashSet<int>(loadedCells);
        for (var c = 0; c < mesh.CellNodes.Length; c++)
        {
            var dof = 3 * mesh.CellNodes[c];
            if (fineFixed[dof] || fineFixed[dof + 1] || fineFixed[dof + 2]) cells.Add(c / 8);
        }
        return [.. cells];
    }

    /// <summary>The coarse leaves to split next, as indices into the octree's leaves.</summary>
    static HashSet<int> Mark(OctreeMesh octree, float[] vonMises, double peak, int[] interfaces, double[] nodeDisplacements, AdaptiveOptions options)
    {
        var marked = new HashSet<int>();
        void Add(int leaf)
        {
            if (octree.Leaves[leaf].Level > 0) marked.Add(leaf);
        }

        var threshold = peak / options.RefineFactor;
        for (var e = 0; e < vonMises.Length; e++)
            if (vonMises[e] >= threshold) Add(octree.CellLeaf[e]);
        foreach (var cell in interfaces) Add(octree.CellLeaf[cell]);

        if (options.EnergyFraction > 0)
        {
            // Coarse elements are too stiff, and that shifts load between the walls, skins and infill
            // around a hotspot. So the elements that do most of the deforming must not stay coarse.
            var energy = octree.LeafEnergies(nodeDisplacements);
            var coarse = Enumerable.Range(0, energy.Length).Where(e => octree.Leaves[e].Level > FineLevel).OrderByDescending(e => energy[e]).ToList();
            var allowed = (1 - options.EnergyFraction) * energy.Sum();
            var held = coarse.Sum(e => energy[e]);
            foreach (var leaf in coarse)
            {
                if (held <= allowed) break;
                held -= energy[leaf];
                marked.Add(leaf);
            }
        }

        for (var ring = 0; ring < options.Buffer; ring++)
            foreach (var index in marked.ToArray())
            {
                var leaf = octree.Leaves[index];
                for (var dk = -1; dk <= 1; dk++)
                for (var dj = -1; dj <= 1; dj++)
                for (var di = -1; di <= 1; di++)
                    if (octree.TryLeafIndex(new OctreeLeaf(leaf.Level, leaf.I + di * leaf.Size, leaf.J + dj * leaf.Size, leaf.K + dk * leaf.Size), out var neighbour))
                        marked.Add(neighbour);
            }
        return marked;
    }

    /// <summary>
    /// The peak cell is a bead-resolution element, and neither it nor the previous pass's peak cell
    /// changed by more than <paramref name="change"/> of the peak. Comparing cell by cell keeps this
    /// working when several places are equally loaded and the highest value hops between them.
    /// Coarse elements read far too low on printed structures (about half at 8³ cells), so a peak
    /// that still sits in one never counts as settled.
    /// </summary>
    static bool Settled(float[] before, float[] now, int peakBefore, int peak, OctreeMesh octree, double change) =>
        octree.Leaves[octree.CellLeaf[peak]].Level == 0
        && Math.Abs(now[peak] - before[peak]) <= change * now[peak]
        && Math.Abs(now[peakBefore] - before[peakBefore]) <= change * now[peak];
}

/// <summary>Picks the octree's coarseness and size limit from the part and the machine.</summary>
public static class OctreeAdvisor
{
    /// <summary>Peak memory per unknown of an AMGCL solve with double-precision ILU(0), measured at 1.3 M and 2.4 M DOF: matrix, AMG hierarchy and ILU factors.</summary>
    public const long BytesPerDof = 5000;

    /// <summary>The coarse pass may use this share of the DOF budget; refinement needs the rest.</summary>
    const int CoarseShare = 20;

    /// <summary>
    /// Below this many unknowns at bead resolution, a part is solved directly: the octree's passes
    /// together cost about as much as one solve of its last mesh, so it only pays off on big parts.
    /// </summary>
    public const int DirectDofs = 250_000;

    /// <summary>Unknowns that fit in 60 % of the memory that is free right now: RAM or commit, whichever is tighter.</summary>
    public static int DofBudget()
    {
        var (physical, commit) = MemoryStatus.Available();
        return (int)Math.Clamp(0.6 * Math.Min(physical, commit) / BytesPerDof, 100_000, 20_000_000);
    }

    /// <summary>Unknowns of the coarse pass: three per corner of the occupied root boxes.</summary>
    public static int CoarseDofs(FemMesh mesh, int rootLevel)
    {
        var boxes = new HashSet<(int, int, int)>();
        foreach (var c in mesh.Cells) boxes.Add((c.I >> rootLevel, c.J >> rootLevel, c.K >> rootLevel));
        var corners = new HashSet<(int, int, int)>();
        foreach (var (i, j, k) in boxes)
            for (var a = 0; a < 8; a++)
                corners.Add((i + HexElement.CornerX[a], j + HexElement.CornerY[a], k + HexElement.CornerZ[a]));
        return 3 * corners.Count;
    }

    /// <summary>
    /// 0 (every bead cell, no octree) for a small part. Otherwise the finest of the Balanced, Fast and
    /// Big-part levels (3, 4, 5) whose coarse pass leaves most of the budget to refinement; the
    /// level changes the cost of the first passes, not the result. Level 2 is only ever the user's choice.
    /// </summary>
    public static int SuggestRootLevel(FemMesh mesh, int dofBudget)
    {
        if (3L * mesh.NodeCount <= Math.Min(DirectDofs, dofBudget)) return 0;
        for (var level = 3; level < OctreeForest.MaxRootLevel; level++)
            if ((long)CoarseDofs(mesh, level) * CoarseShare <= dofBudget) return level;
        return OctreeForest.MaxRootLevel;
    }
}
