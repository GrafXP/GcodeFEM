using System.IO.Compression;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Gcode;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Materials;
using GcodeFem.Core.Slicing;
using GcodeFem.Core.Visual;

namespace GcodeFem.Core.Study;

/// <summary>What a results file says about itself, read without rebuilding the result.</summary>
/// <param name="Study">The study as it was solved; its model is the copy in the file.</param>
/// <param name="Summary">The words the app showed with the result, if it was saved from the app.</param>
public sealed record ResultHeader(DateTimeOffset SavedAt, string Program, string LoadCase, PartStudy Study, string? Summary,
    double SafetyFactor, float MaxDisplacement, double MaxVonMises);

/// <summary>A result read back from its file, rebuilt as it was solved.</summary>
public sealed class SavedResult
{
    public required ResultHeader Header { get; init; }

    /// <summary>The model the study's faces were picked on, from the file.</summary>
    public required TriangleMesh Model { get; init; }

    public required PrintedPart Part { get; init; }
    public required PartSolution Solution { get; init; }
}

/// <summary>
/// A solve saved beside its study, "*.results.zip": everything it takes to show the result again
/// without Bambu Studio or the solver, and to look into it from the command line. The zip holds
/// <list type="bullet">
/// <item>result.json: when and by what it was saved, the load case, the numbers worth reading at a
/// glance (safety factor, the weakest cell and its stresses, the weakest layer line, the passes), the
/// slicer's report and the exact turn the part was sliced at;</item>
/// <item>study.json, model.stl and material.json: the study as it was solved, its model, and the
/// material as it was then, so that later edits to any of them do not change the result;</item>
/// <item>slice/plate_1.gcode: the slicer's G-code, from which the cells are rebuilt;</item>
/// <item>solution/*.bin: what cannot be rebuilt (little-endian arrays): the octree's leaves, the
/// displacement of each of its nodes, each cell's layer bond, and each pass's fields for stepping
/// through the passes.</item>
/// </list>
/// The stresses and the failure check are worked out again from the displacements, which takes a
/// second or two; the cells must come out as they were, and the file is refused if they do not.
/// </summary>
public static class ResultFile
{
    public const string Extension = ".results.zip";

    const int Format = 1;
    const string HeaderEntry = "result.json";
    const string StudyEntry = "study.json";
    const string ModelEntry = "model.stl";
    const string MaterialEntry = "material.json";
    const string GcodeEntry = "slice/plate_1.gcode";
    const string LeavesEntry = "solution/leaves.bin";
    const string DisplacementEntry = "solution/displacements.bin";
    const string BondEntry = "solution/bonds.bin";

    static string PassEntry(int pass) => $"solution/pass{pass + 1}.bin";

    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    sealed record FileDto(int Format, DateTimeOffset SavedAt, string Program, string LoadCase, string? Summary, ResultDto Result,
        SliceDto Slice, MaterialDto Material, SolveDto Solve, List<ReachDto> Reach);

    /// <summary>For reading by eye: nothing here is read back.</summary>
    sealed record ResultDto(double? SafetyFactor, double MaxVonMises, float[] MaxVonMisesAt, float MaxDisplacement, string? NoStrength,
        CellDto? Weakest, CellDto? WeakestLayerLine, double? LowestTsaiWu);

    sealed record CellDto(int Cell, int[] Ijk, int Layer, float[] At, string Feature, float LineAngle, float Spread, float Fill, float Bond,
        string Mode, double SafetyFactor, Dictionary<string, double> Modes, double TsaiWu, float VonMises, float[] Stress, double[] InLineFrame);

    /// <param name="Key">The slicer cache's name for the slice, to find it there.</param>
    /// <param name="Rotation">Part frame → print frame as the slicer got it, row by row: the study's angles may be rounded.</param>
    sealed record SliceDto(string Key, string? Plate, float[] Rotation, ReportDto Report);

    sealed record ReportDto(int ReturnCode, string Error, string Warning, float[] BoundsMin, float[] BoundsMax, double PrintSeconds, double FilamentGrams,
        Dictionary<string, double> FeatureSeconds);

    sealed record MaterialDto(string Name, MatchKind Kind, string Filament, string? Type);

    /// <param name="CellHash">Of the cells' indices, to check that they are rebuilt as they were.</param>
    sealed record SolveDto(int RootLevel, float MinFill, int Cells, string CellHash, int OctreeNodes, string StopReason, List<PassDto> Passes);

    sealed record PassDto(int Index, int[] LeavesByLevel, int Dofs, int HangingNodes, double MaxVonMises, int PeakCell, int PeakLevel, double Compliance,
        int Marked, string Solver, int Iterations, double Residual, bool Converged, bool Retried, double SetupSeconds, double SolveSeconds,
        double BuildSeconds, double StressSeconds, double? SafetyFactor);

    sealed record ReachDto(string Name, bool IsLoad, int Faces, double Area, float[] Force);

    /// <summary>The results file that goes with a study: "part.study.json" → "part.results.zip".</summary>
    public static string BesideStudy(string studyPath)
    {
        var full = Path.GetFullPath(studyPath);
        var name = Path.GetFileName(full);
        foreach (var ending in new[] { ".study.json", ".json" })
            if (name.EndsWith(ending, StringComparison.OrdinalIgnoreCase))
                return Path.Combine(Path.GetDirectoryName(full)!, name[..^ending.Length] + Extension);
        return full + Extension;
    }

    /// <param name="model">The model the solved study's faces were picked on.</param>
    /// <param name="part">The print the solution was solved on.</param>
    /// <param name="summary">What the app says about the result, to keep with it.</param>
    public static void Save(string path, TriangleMesh model, PrintedPart part, PartSolution solution, string? summary = null)
    {
        var study = solution.Study;
        if (!study.Fits(model)) throw new InvalidOperationException("The model is not the one this result's study was solved on.");
        var result = solution.Result;
        var mesh = result.Mesh;
        var rotation = part.Slice.Placement.Rotation;
        var report = part.Slice.Report;
        var failure = result.Failure;
        var peak = ArgMax(result.VonMises);

        var file = new FileDto(
            Format,
            DateTimeOffset.Now,
            Program(),
            solution.LoadCase,
            summary,
            new ResultDto(
                failure?.SafetyFactor, result.VonMises[peak], Array(ResultMarks.Centre(mesh.Grid, mesh.Cells[peak])), solution.MaxDisplacement, solution.NoStrength,
                failure is null ? null : Cell(solution, failure.Weakest),
                failure is null ? null : Cell(solution, failure.WeakestLayerLine),
                failure?.TsaiWu.Min()),
            new SliceDto(
                Path.GetFileName(part.Slice.Directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                part.Slice.Plate,
                [rotation.M11, rotation.M12, rotation.M13, rotation.M14, rotation.M21, rotation.M22, rotation.M23, rotation.M24,
                 rotation.M31, rotation.M32, rotation.M33, rotation.M34, rotation.M41, rotation.M42, rotation.M43, rotation.M44],
                new ReportDto(report.ReturnCode, report.Error, report.Warning, Array(report.ObjectBounds.Min), Array(report.ObjectBounds.Max), report.PrintSeconds,
                    report.FilamentGrams, new Dictionary<string, double>(report.FeatureSeconds))),
            new MaterialDto(solution.Material.Material.Name, solution.Material.Kind, solution.Material.Filament, solution.Material.Type),
            new SolveDto(result.RootLevel, PrintedPart.MinFill, mesh.Cells.Length, CellHash(mesh.Cells), result.Octree.NodeCount, result.StopReason,
                [.. result.Passes.Select(p => new PassDto(p.Index, p.LeavesByLevel, p.Dofs, p.HangingNodes, p.MaxVonMises, p.PeakCell, p.PeakLevel, p.Compliance,
                    p.Marked, p.Solve.Solver, p.Solve.Iterations, p.Solve.Residual, p.Solve.Converged, p.Solve.Retried, p.Solve.Setup.TotalSeconds,
                    p.Solve.Solve.TotalSeconds, p.BuildTime.TotalSeconds, p.StressTime.TotalSeconds, double.IsNaN(p.SafetyFactor) ? null : p.SafetyFactor))]),
            [.. solution.Reach.Select(r => new ReachDto(r.Name, r.IsLoad, r.Faces, r.Area, Array(r.Force)))]);

        // Written beside and then moved over the old one, so that a failed save leaves the old file as it was.
        var folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(folder);
        var temporary = path + ".saving";
        using (var zip = ZipFile.Open(temporary, ZipArchiveMode.Create))
        {
            WriteText(zip, HeaderEntry, JsonSerializer.Serialize(file, Options));
            WriteText(zip, StudyEntry, Snapshot(study, rotation, folder));
            WriteBytes(zip, ModelEntry, Stl.ToBinary(model));
            WriteText(zip, MaterialEntry, MaterialFile.ToJson(solution.Material.Material));
            using (var gcode = OpenGcode(part.Slice))
            using (var entry = zip.CreateEntry(GcodeEntry, CompressionLevel.Optimal).Open())
                gcode.CopyTo(entry);

            var leaves = result.Octree.Leaves;
            var packed = new int[4 * leaves.Length];
            for (var e = 0; e < leaves.Length; e++)
                (packed[4 * e], packed[4 * e + 1], packed[4 * e + 2], packed[4 * e + 3]) = (leaves[e].Level, leaves[e].I, leaves[e].J, leaves[e].K);
            WriteArrays(zip, LeavesEntry, w => w.Write<int>(packed));
            WriteArrays(zip, DisplacementEntry, w => w.Write<double>(result.NodeDisplacements));
            WriteArrays(zip, BondEntry, w =>
            {
                w.Write<float>(solution.Bonds.Factor);
                w.Write<float>(solution.Bonds.Gap);
            });
            foreach (var pass in result.Passes.Where(p => p.CellLevels is not null || p.VonMises is not null))
                WriteArrays(zip, PassEntry(pass.Index), w =>
                {
                    w.Write<byte>(pass.CellLevels ?? []);
                    w.Write<float>(pass.VonMises ?? []);
                    w.Write<float>(pass.FailureIndex ?? []);
                });
        }
        File.Move(temporary, path, overwrite: true);
    }

    public static ResultHeader ReadHeader(string path)
    {
        using var zip = Open(path);
        return Header(zip, path, out _);
    }

    /// <summary>Reads a results file and rebuilds its result: the print from its G-code, the solution from its displacements.</summary>
    /// <param name="step">Told what is being done, for a progress bar.</param>
    public static SavedResult Load(string path, Action<string>? step = null)
    {
        using var zip = Open(path);
        var header = Header(zip, path, out var file);
        var study = header.Study;
        var model = Stl.Read(Bytes(zip, ModelEntry));
        if (!study.Fits(model)) throw Broken(path, "its model is not the one its study was solved on");

        step?.Invoke("reading the toolpath");
        var r = file.Slice.Rotation;
        if (r.Length != 16) throw Broken(path, "the turn of its slice is not a 4 x 4 matrix");
        var rotation = new Matrix4x4(r[0], r[1], r[2], r[3], r[4], r[5], r[6], r[7], r[8], r[9], r[10], r[11], r[12], r[13], r[14], r[15]);
        var printMesh = model.Transformed(rotation);
        var saved = file.Slice.Report;
        var report = new SlicerReport(saved.ReturnCode, saved.Error ?? "", saved.Warning ?? "", new Box3(Vector(saved.BoundsMin), Vector(saved.BoundsMax)),
            saved.PrintSeconds, saved.FilamentGrams, saved.FeatureSeconds ?? []);
        var placement = Placement.FromSlice(rotation, printMesh.Bounds, report.ObjectBounds);
        Toolpath toolpath;
        using (var reader = new StreamReader(Entry(zip, path, GcodeEntry).Open()))
            toolpath = GcodeParser.Parse(reader);
        // The G-code stays in the zip; nothing reads it again from the slice's path.
        var slice = new SliceResult(path, Path.Combine(path, GcodeEntry), report, placement, printMesh, true, TimeSpan.Zero, file.Slice.Plate);
        step?.Invoke("turning the toolpath into cells");
        var part = PrintedPart.Read(slice, toolpath);

        step?.Invoke("rebuilding the solution");
        var material = MaterialFile.FromJson(Text(zip, path, MaterialEntry), path);
        var match = new MaterialMatch(material, file.Material.Kind, file.Material.Filament ?? "", file.Material.Type);
        var loadCase = study.LoadCases.FirstOrDefault(c => c.Name == header.LoadCase) ?? throw Broken(path, $"its study has no load case '{header.LoadCase}'");
        var mapped = InterfaceMapper.Map(printMesh, rotation, part.Grid.Pitch, study.Interfaces, loadCase);
        var mesh = FemMesh.Build(part.Grid, file.Solve.MinFill, mapped);
        if (mesh.Cells.Length != file.Solve.Cells || CellHash(mesh.Cells) != file.Solve.CellHash)
            throw Broken(path, $"its cells come out otherwise than they were solved ({mesh.Cells.Length:N0} against {file.Solve.Cells:N0}): it was saved by another version of the program");
        var cells = mesh.Cells.Length;

        var bonds = Arrays(zip, path, BondEntry);
        var bond = new LayerBonds(bonds.Read<float>(cells), bonds.Read<float>(cells));
        var strength = material.Strength(out var noStrength);
        var cellStrength = strength is null ? null : new CellStrength(mesh, strength, bond.Factor);

        var packed = Arrays(zip, path, LeavesEntry).ReadAll<int>();
        var leaves = new HashSet<OctreeLeaf>();
        for (var e = 0; e + 3 < packed.Length; e += 4) leaves.Add(new OctreeLeaf(packed[e], packed[e + 1], packed[e + 2], packed[e + 3]));
        var forest = new OctreeForest(mesh, material.Stiffness(), file.Solve.RootLevel);
        Grow(forest, leaves, path);
        var octree = OctreeMesh.Build(forest);
        if (octree.NodeCount != file.Solve.OctreeNodes) throw Broken(path, "its octree comes out otherwise than it was solved");
        var nodeDisplacements = Arrays(zip, path, DisplacementEntry).Read<double>(3 * octree.NodeCount);
        var (stress, vonMises) = octree.CellStresses(nodeDisplacements);

        var passes = new List<AdaptivePass>();
        foreach (var p in file.Solve.Passes)
        {
            byte[]? levels = null;
            float[]? passVonMises = null, passFailure = null;
            if (zip.GetEntry(PassEntry(p.Index)) is not null)
            {
                var fields = Arrays(zip, path, PassEntry(p.Index));
                levels = fields.Read<byte>(cells);
                passVonMises = fields.Read<float>(cells);
                passFailure = fields.Remaining > 0 ? fields.Read<float>(cells) : null;
            }
            passes.Add(new AdaptivePass(p.Index, p.LeavesByLevel, p.Dofs, p.HangingNodes, p.MaxVonMises, p.PeakCell, p.PeakLevel, p.Compliance, p.Marked,
                new SolveStatistics(p.Solver, p.Iterations, p.Residual, p.Converged, TimeSpan.FromSeconds(p.SetupSeconds), TimeSpan.FromSeconds(p.SolveSeconds)) { Retried = p.Retried },
                TimeSpan.FromSeconds(p.BuildSeconds), TimeSpan.FromSeconds(p.StressSeconds))
            {
                SafetyFactor = p.SafetyFactor ?? double.NaN,
                CellLevels = levels,
                VonMises = passVonMises,
                FailureIndex = passFailure,
            });
        }
        if (passes.Count == 0) throw Broken(path, "it has no passes");

        var result = new AdaptiveResult
        {
            Mesh = mesh,
            Octree = octree,
            NodeDisplacements = nodeDisplacements,
            Stress = stress,
            VonMises = vonMises,
            Failure = cellStrength?.Check(stress),
            Passes = passes,
            StopReason = file.Solve.StopReason,
            RootLevel = file.Solve.RootLevel,
        };
        var displacements = ResultFields.NodeDisplacements(result);
        var solution = new PartSolution
        {
            Result = result,
            LoadCase = header.LoadCase,
            Study = study,
            Reach = Assembler.Reach(mesh, mapped),
            Material = match,
            Bonds = bond,
            Strength = cellStrength,
            NoStrength = noStrength,
            NodeDisplacements = displacements,
            MaxDisplacement = displacements.Length == 0 ? 0 : displacements.Max(u => u.Length()),
        };
        return new SavedResult { Header = header, Model = model, Part = part, Solution = solution };
    }

    /// <summary>
    /// Why a result saved for <paramref name="solved"/> is not one of <paramref name="current"/>, or null if
    /// it is: the same model laid the same way, the same presets, and the same mounts, mesh level and loads
    /// in its load case. Other load cases and the sweep's settings do not count.
    /// </summary>
    public static string? Mismatch(ResultHeader solved, PartStudy current) => Mismatch(solved.Study, solved.LoadCase, current);

    /// <inheritdoc cref="Mismatch(ResultHeader, PartStudy)"/>
    /// <param name="then">The study as it was solved.</param>
    /// <param name="loadCase">The load case that was solved.</param>
    public static string? Mismatch(PartStudy then, string loadCase, PartStudy current)
    {
        if (then.GeometryHash != current.GeometryHash) return "the model has changed since it was solved";
        if (!Near(Orientation.FromEulerDegrees(then.Rotation.X, then.Rotation.Y, then.Rotation.Z), Orientation.FromEulerDegrees(current.Rotation.X, current.Rotation.Y, current.Rotation.Z)))
            return $"it was solved laid another way ({then.Rotation.X:0.##}, {then.Rotation.Y:0.##}, {then.Rotation.Z:0.##})";
        // A study that names no preset takes Bambu Studio's selection, whatever it is.
        if ((current.Process is not null && then.Process != current.Process) || (current.Filament is not null && then.Filament != current.Filament))
            return $"it was sliced with {then.Process} and {then.Filament}";
        if (current.LoadCases.All(c => c.Name != loadCase)) return $"the study has no load case '{loadCase}' any more";
        return Canonical(then, loadCase) == Canonical(current, loadCase) ? null : "its mounts, loads or mesh level have changed since it was solved";
    }

    /// <summary>What of a study the solve of one load case depends on beyond model, turn and presets, as the study file writes it.</summary>
    static string Canonical(PartStudy study, string loadCase)
    {
        var copy = StudyFile.FromJson(StudyFile.ToJson(study, Path.GetTempPath()), Path.GetTempPath(), "the study");
        copy.ModelPath = Path.Combine(Path.GetTempPath(), ModelEntry);
        (copy.Rotation, copy.Process, copy.Filament) = (Vector3.Zero, null, null);
        copy.Sweep = new SweepSettings();
        copy.LoadCases.RemoveAll(c => c.Name != loadCase);
        return StudyFile.ToJson(copy, Path.GetTempPath());
    }

    /// <summary>The study as solved, naming the model copied beside it in the zip and turned as the slice was.</summary>
    static string Snapshot(PartStudy study, Matrix4x4 rotation, string folder)
    {
        var copy = StudyFile.FromJson(StudyFile.ToJson(study, folder), folder, "the study");
        copy.ModelPath = Path.Combine(folder, ModelEntry);
        // A sweep solves each candidate from the study as it was laid at the start.
        var stated = Orientation.FromEulerDegrees(copy.Rotation.X, copy.Rotation.Y, copy.Rotation.Z);
        if (!Near(stated, rotation))
        {
            var angles = Orientation.ToEulerDegrees(rotation);
            copy.Rotation = new Vector3(MathF.Round(angles.X, 4), MathF.Round(angles.Y, 4), MathF.Round(angles.Z, 4));
        }
        return StudyFile.ToJson(copy, folder);
    }

    /// <summary>The same turn to well under a thousandth of a degree.</summary>
    static bool Near(Matrix4x4 a, Matrix4x4 b) =>
        MathF.Abs(a.M11 - b.M11) + MathF.Abs(a.M12 - b.M12) + MathF.Abs(a.M13 - b.M13) + MathF.Abs(a.M21 - b.M21) + MathF.Abs(a.M22 - b.M22) +
        MathF.Abs(a.M23 - b.M23) + MathF.Abs(a.M31 - b.M31) + MathF.Abs(a.M32 - b.M32) + MathF.Abs(a.M33 - b.M33) < 1e-5f;

    /// <summary>The slice's G-code: in the slicer's cache, or in the results file a slice read back from one came from.</summary>
    static Stream OpenGcode(SliceResult slice)
    {
        if (File.Exists(slice.GcodePath)) return File.OpenRead(slice.GcodePath);
        if (!slice.Directory.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) || !File.Exists(slice.Directory))
            throw new FileNotFoundException($"The slice's G-code is gone from '{slice.GcodePath}'.");
        using var zip = Open(slice.Directory);
        return new MemoryStream(Bytes(zip, GcodeEntry));
    }

    /// <summary>Refines a new forest until its leaves are <paramref name="leaves"/>: those of the saved solve's last pass.</summary>
    static void Grow(OctreeForest forest, HashSet<OctreeLeaf> leaves, string path)
    {
        while (forest.Leaves.Where(leaf => !leaves.Contains(leaf)).ToList() is { Count: > 0 } coarser)
            if (forest.Refine(coarser) == 0) throw Broken(path, "its octree's leaves are not in the forest of its cells");
        if (forest.LeafCount != leaves.Count) throw Broken(path, "its octree's leaves are not in the forest of its cells");
    }

    static ResultHeader Header(ZipArchive zip, string path, out FileDto file)
    {
        try
        {
            file = JsonSerializer.Deserialize<FileDto>(Text(zip, path, HeaderEntry), Options) ?? throw Broken(path, $"its {HeaderEntry} is empty");
        }
        catch (JsonException ex)
        {
            throw Broken(path, $"its {HeaderEntry} cannot be read: {ex.Message}");
        }
        if (file.Format != Format) throw new InvalidDataException($"'{path}' holds results of format {file.Format}; this version reads format {Format}.");
        var study = StudyFile.FromJson(Text(zip, path, StudyEntry), Path.GetDirectoryName(Path.GetFullPath(path))!, $"{path}: {StudyEntry}");
        return new ResultHeader(file.SavedAt, file.Program ?? "", file.LoadCase ?? "", study, file.Summary,
            file.Result?.SafetyFactor ?? double.NaN, file.Result?.MaxDisplacement ?? 0, file.Result?.MaxVonMises ?? 0);
    }

    /// <summary>A cell as worth reading at a glance: where it is, what it is, its stresses and how near each mode is to breaking it.</summary>
    static CellDto Cell(PartSolution solution, int e)
    {
        var mesh = solution.Result.Mesh;
        var c = mesh.Cells[e];
        var stress = solution.Result.Stress.AsSpan(6 * e, 6);
        var failure = solution.Result.Failure!;
        var strength = solution.Strength!;
        Span<double> modes = stackalloc double[FailureModes.Count];
        var tsaiWu = strength.Evaluate(e, stress, modes);
        Span<double> local = stackalloc double[6];
        strength.LocalStress(e, stress, local);
        var (angle, spread) = mesh.Grid.Direction(c.I, c.J, c.K);
        var byMode = new Dictionary<string, double>();
        for (var m = 0; m < FailureModes.Count; m++) byMode[((FailureMode)m).DisplayName()] = Math.Round(modes[m] > 0 ? 1 / modes[m] : double.PositiveInfinity, 4);
        return new CellDto(e, [c.I, c.J, c.K], c.K + 1, Array(ResultMarks.Centre(mesh.Grid, c)),
            mesh.Grid.Feature(c.I, c.J, c.K)?.DisplayName() ?? "", angle * 180 / MathF.PI, spread, mesh.Fill[e], strength.Bond(e),
            failure.Mode[e].DisplayName(), failure.SafetyFactorOf(e), byMode, tsaiWu, solution.Result.VonMises[e], stress.ToArray(), local.ToArray());
    }

    static string Program()
    {
        var assembly = typeof(ResultFile).Assembly;
        return $"GcodeFem {assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "?"}";
    }

    /// <summary>FNV-1a over the cells' indices.</summary>
    static string CellHash(CellIndex[] cells)
    {
        var hash = 14695981039346656037UL;
        foreach (var value in MemoryMarshal.Cast<CellIndex, int>(cells))
            for (var b = 0; b < 4; b++)
                hash = (hash ^ (byte)(value >> (8 * b))) * 1099511628211UL;
        return hash.ToString("x16");
    }

    static int ArgMax(float[] values)
    {
        var best = 0;
        for (var e = 1; e < values.Length; e++)
            if (values[e] > values[best]) best = e;
        return best;
    }

    static float[] Array(Vector3 v) => [v.X, v.Y, v.Z];

    static Vector3 Vector(float[]? v) => v is [var x, var y, var z] ? new Vector3(x, y, z) : Vector3.Zero;

    static InvalidDataException Broken(string path, string why) => new($"'{path}' cannot be read as a result: {why}.");

    static ZipArchive Open(string path)
    {
        try
        {
            return ZipFile.OpenRead(path);
        }
        catch (InvalidDataException ex)
        {
            throw Broken(path, ex.Message);
        }
    }

    static ZipArchiveEntry Entry(ZipArchive zip, string path, string name) => zip.GetEntry(name) ?? throw Broken(path, $"it holds no {name}");

    static string Text(ZipArchive zip, string path, string name)
    {
        using var reader = new StreamReader(Entry(zip, path, name).Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    static byte[] Bytes(ZipArchive zip, string name)
    {
        var entry = zip.GetEntry(name) ?? throw new InvalidDataException($"The results file holds no {name}.");
        using var stream = entry.Open();
        using var copy = new MemoryStream((int)entry.Length);
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    static ArrayReader Arrays(ZipArchive zip, string path, string name)
    {
        Entry(zip, path, name);
        return new ArrayReader(Bytes(zip, name), path, name);
    }

    static void WriteText(ZipArchive zip, string name, string text)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
        writer.Write(text);
    }

    static void WriteBytes(ZipArchive zip, string name, byte[] bytes)
    {
        using var stream = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
        stream.Write(bytes);
    }

    static void WriteArrays(ZipArchive zip, string name, Action<ArrayWriter> write)
    {
        using var stream = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
        write(new ArrayWriter(stream));
    }

    /// <summary>Arrays one after the other, each as its length and then its values.</summary>
    sealed class ArrayWriter(Stream stream)
    {
        public void Write<T>(ReadOnlySpan<T> values) where T : unmanaged
        {
            stream.Write(BitConverter.GetBytes(values.Length));
            stream.Write(MemoryMarshal.AsBytes(values));
        }

        public void Write<T>(T[] values) where T : unmanaged => Write((ReadOnlySpan<T>)values);
    }

    sealed class ArrayReader(byte[] data, string path, string name)
    {
        int at;

        public int Remaining => data.Length - at;

        /// <summary>The next array, which must hold <paramref name="count"/> values.</summary>
        public T[] Read<T>(int count) where T : unmanaged
        {
            var values = ReadAll<T>();
            return values.Length == count ? values : throw Broken(path, $"its {name} holds {values.Length:N0} values where {count:N0} were expected");
        }

        public T[] ReadAll<T>() where T : unmanaged
        {
            if (Remaining < 4) throw Broken(path, $"its {name} ends early");
            var count = BitConverter.ToInt32(data, at);
            var size = (long)count * Marshal.SizeOf<T>();
            if (count < 0 || 4 + size > Remaining) throw Broken(path, $"its {name} ends early");
            var values = MemoryMarshal.Cast<byte, T>(data.AsSpan(at + 4, (int)size)).ToArray();
            at += 4 + (int)size;
            return values;
        }
    }
}
