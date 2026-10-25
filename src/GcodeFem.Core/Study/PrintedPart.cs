using System.Numerics;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Gcode;
using GcodeFem.Core.Materials;
using GcodeFem.Core.Slicing;
using GcodeFem.Core.Visual;

namespace GcodeFem.Core.Study;

/// <summary>A part sliced one way and turned into bead cells: what every solve of that orientation starts from.</summary>
public sealed class PrintedPart
{
    /// <summary>Cells holding less than this share of plastic do not count.</summary>
    public const float MinFill = 0.05f;

    public required SliceResult Slice { get; init; }
    public required Toolpath Toolpath { get; init; }

    /// <summary>When each line was printed, on the slicer's times: the clock the cells were filled by.</summary>
    public required PrintClock Clock { get; init; }

    public required CellGrid Grid { get; init; }

    /// <summary>mm³ of support plastic, its interfaces included; result.json does not give it, the toolpath does.</summary>
    public double SupportVolume => Toolpath.Segments.Where(s => s.Feature is FeatureType.Support or FeatureType.SupportInterface or FeatureType.SupportTransition).Sum(s => (double)s.Volume);

    /// <param name="step">Told what is being done, for a progress bar.</param>
    public static async Task<PrintedPart> SliceAsync(BambuSlicer slicer, SliceRequest request, Action<string>? step = null, CancellationToken cancellationToken = default)
    {
        step?.Invoke("Bambu Studio at work");
        var slice = await slicer.SliceAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
        return Read(slice, step);
    }

    /// <summary>The toolpath of a slice, and its cells.</summary>
    public static PrintedPart Read(SliceResult slice, Action<string>? step = null)
    {
        step?.Invoke("reading the toolpath");
        return Read(slice, GcodeParser.Parse(slice.GcodePath), step);
    }

    /// <summary>The cells of a slice whose G-code has been read already.</summary>
    public static PrintedPart Read(SliceResult slice, Toolpath toolpath, Action<string>? step = null)
    {
        // The slicer's own times per line type include acceleration, which the bond of the layer lines depends on.
        step?.Invoke("turning the toolpath into cells");
        var clock = PrintClock.Calibrated(toolpath, slice.Report.FeatureSeconds);
        var grid = Voxelizer.Voxelize(toolpath, slice.Placement, slice.PrintMesh.Bounds, clock);
        return new PrintedPart { Slice = slice, Toolpath = toolpath, Clock = clock, Grid = grid };
    }
}

/// <summary>
/// One load case of a study solved on a printed part: the result, what each interface got hold of on
/// the cells, how the layer lines bonded, and what the cells were checked against.
/// </summary>
public sealed class PartSolution
{
    public required AdaptiveResult Result { get; init; }

    /// <summary>The name of the load case.</summary>
    public required string LoadCase { get; init; }

    /// <summary>The study as it was solved: its interfaces and load cases then, whatever has been changed since.</summary>
    public required PartStudy Study { get; init; }

    public required IReadOnlyList<InterfaceReach> Reach { get; init; }

    /// <summary>The material it was solved with, and how the filament came by it.</summary>
    public required MaterialMatch Material { get; init; }

    /// <summary>Per cell of the result's mesh: its bond to the cell below.</summary>
    public required LayerBonds Bonds { get; init; }

    /// <summary>What the cells were checked against; null if the material gives no strength, and why in <see cref="NoStrength"/>.</summary>
    public CellStrength? Strength { get; init; }

    public string? NoStrength { get; init; }

    /// <summary>Per node of the result's mesh (mm, print frame).</summary>
    public required Vector3[] NodeDisplacements { get; init; }

    public required float MaxDisplacement { get; init; }

    /// <summary>The factor on the loads at which the part starts to break; NaN without strengths.</summary>
    public double SafetyFactor => Result.Failure?.SafetyFactor ?? double.NaN;

    /// <param name="material">That of the filament the part was sliced with, as the library has it now.</param>
    public static PartSolution Solve(PrintedPart part, PartStudy study, StudyLoadCase loadCase, MaterialMatch material, ILinearSolver solver, AdaptiveOptions options,
        Action<AdaptivePass>? onPass = null, Action<AdaptiveSolving>? onSolving = null)
    {
        // The interfaces are faces of the model; the slice says how it was turned for this print.
        var mapped = InterfaceMapper.Map(part.Slice.PrintMesh, part.Slice.Placement.Rotation, part.Grid.Pitch, study.Interfaces, loadCase);
        var mesh = FemMesh.Build(part.Grid, options.MinFill, mapped);
        var reach = Assembler.Reach(mesh, mapped);
        // The layer lines are as strong as they have bonded: the refinement goes where the part is nearest to breaking.
        var bonds = new BondModel().Cells(mesh.Grid, mesh.Cells, material.Material.Bonding(part.Toolpath), options.MinFill);
        var strength = material.Material.Strength(out var noStrength);
        var cells = strength is null ? null : new CellStrength(mesh, strength, bonds.Factor);
        var result = AdaptiveAnalysis.Run(mesh, material.Material.Stiffness(), mapped, solver, options, onPass, cells, onSolving);
        var displacements = ResultFields.NodeDisplacements(result);
        return new PartSolution
        {
            Result = result,
            LoadCase = loadCase.Name,
            Study = study,
            Reach = reach,
            Material = material,
            Bonds = bonds,
            Strength = cells,
            NoStrength = noStrength,
            NodeDisplacements = displacements,
            MaxDisplacement = displacements.Length == 0 ? 0 : displacements.Max(u => u.Length()),
        };
    }
}
