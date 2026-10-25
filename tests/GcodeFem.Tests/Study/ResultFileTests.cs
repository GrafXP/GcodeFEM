using System.IO.Compression;
using System.Numerics;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Materials;
using GcodeFem.Core.Slicing;
using GcodeFem.Core.Study;

namespace GcodeFem.Tests.Study;

/// <summary>
/// Results saved beside a study and read back, on the printed cube of the samples (no slicer needed):
/// solved on an octree, so that its leaves have to be grown again, and checked against the solve itself.
/// </summary>
public sealed class ResultFileTests : IDisposable
{
    static readonly TriangleMesh Cube = Stl.Read(TestPaths.Sample("cube20.stl"));

    static readonly Lazy<PrintedPart> Printed = new(() =>
    {
        var report = SlicerReport.Read(TestPaths.Sample("cube20_X1C_PLA.result.json"));
        var placement = Placement.FromSlice(Matrix4x4.Identity, Cube.Bounds, report.ObjectBounds);
        return PrintedPart.Read(new SliceResult(TestPaths.Root, TestPaths.Sample("cube20_X1C_PLA.gcode"), report, placement, Cube, true, TimeSpan.Zero));
    });

    /// <summary>Pushed sideways at its top, held at its bottom: refined in a few passes from elements of 8³ cells.</summary>
    static readonly Lazy<PartSolution> Solved = new(() =>
    {
        var study = Study();
        return PartSolution.Solve(Printed.Value, study, study.LoadCases[0], MaterialLibrary.Load(TestPaths.Materials).Match(Printed.Value.Toolpath),
            LinearSolvers.Best(), new AdaptiveOptions(RootLevel: 3, MinFill: PrintedPart.MinFill, KeepPassFields: true));
    });

    readonly string folder = Path.Combine(Path.GetTempPath(), "GcodeFem.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }

    static PartStudy Study()
    {
        int[] Facing(Vector3 way) => [.. Enumerable.Range(0, Cube.TriangleCount).Where(t => Vector3.Dot(Cube.Normal(t), way) > 0.99f)];
        var study = new PartStudy
        {
            ModelPath = TestPaths.Sample("cube20.stl"),
            GeometryHash = Cube.GeometryHash(),
            Process = "0.20mm Standard @BBL X1C",
            Filament = "Bambu PLA Basic @BBL X1C",
            MeshLevel = 3,
        };
        var bottom = new PartInterface { Name = "Bottom", Kind = InterfaceKind.Fixed, Triangles = Facing(-Vector3.UnitZ) };
        var top = new PartInterface { Name = "Top", Kind = InterfaceKind.Force, Triangles = Facing(Vector3.UnitZ) };
        study.Interfaces.AddRange([bottom, top]);
        var sideways = new StudyLoadCase { Name = "Sideways" };
        sideways.Loads[top] = new LoadValue(new Vector3(100, 0, 0));
        study.LoadCases.Add(sideways);
        return study;
    }

    string Saved(string name = "cube.results.zip")
    {
        var path = Path.Combine(folder, name);
        ResultFile.Save(path, Cube, Printed.Value, Solved.Value, "what the app said");
        return path;
    }

    [Fact]
    public void A_saved_result_reads_back_as_it_was_solved()
    {
        var solved = Solved.Value;
        Assert.True(solved.Result.Passes.Count > 1 && solved.Result.Octree.Leaves.Select(l => l.Level).Distinct().Count() > 1, "an octree of several levels");

        var saved = ResultFile.Load(Saved());
        var (before, after) = (solved.Result, saved.Solution.Result);
        Assert.Equal(before.Mesh.Cells, after.Mesh.Cells);
        Assert.Equal(before.Octree.Leaves, after.Octree.Leaves);
        Assert.Equal(before.NodeDisplacements, after.NodeDisplacements);
        // Worked out again from the same displacements by the same material.
        Assert.Equal(before.Stress, after.Stress);
        Assert.Equal(before.Failure!.SafetyFactor, after.Failure!.SafetyFactor);
        Assert.Equal(before.Failure.Weakest, after.Failure.Weakest);
        Assert.Equal(solved.MaxDisplacement, saved.Solution.MaxDisplacement);
        Assert.Equal(solved.Bonds.Factor, saved.Solution.Bonds.Factor);
        Assert.Equal(solved.Reach, saved.Solution.Reach);
        Assert.Equal(solved.Material.Material.Stiffness(), saved.Solution.Material.Material.Stiffness());

        Assert.Equal(before.StopReason, after.StopReason);
        Assert.Equal(before.RootLevel, after.RootLevel);
        Assert.Equal(before.Passes.Count, after.Passes.Count);
        foreach (var (a, b) in before.Passes.Zip(after.Passes))
        {
            Assert.Equal((a.Dofs, a.PeakCell, a.MaxVonMises, a.SafetyFactor, a.Solve.Iterations), (b.Dofs, b.PeakCell, b.MaxVonMises, b.SafetyFactor, b.Solve.Iterations));
            Assert.Equal(a.LeavesByLevel, b.LeavesByLevel);
            Assert.Equal(a.CellLevels, b.CellLevels);
            Assert.Equal(a.VonMises, b.VonMises);
            Assert.Equal(a.FailureIndex, b.FailureIndex);
        }

        Assert.Equal(Printed.Value.Grid.Occupied(PrintedPart.MinFill), saved.Part.Grid.Occupied(PrintedPart.MinFill));
        Assert.Equal(Printed.Value.Toolpath.Segments.Length, saved.Part.Toolpath.Segments.Length);
        Assert.Equal(("Sideways", "what the app said"), (saved.Header.LoadCase, saved.Header.Summary));
        Assert.Equal(solved.SafetyFactor, saved.Header.SafetyFactor, 9);
        Assert.Equal(Cube.GeometryHash(), saved.Model.GeometryHash());
        Assert.Equal(["Bottom", "Top"], saved.Header.Study.Interfaces.Select(i => i.Name));
    }

    [Fact]
    public void A_result_read_back_can_be_saved_again()
    {
        var first = ResultFile.Load(Saved());
        // Its G-code is in the first file now, not in a slicer's cache.
        var again = Path.Combine(folder, "again.results.zip");
        ResultFile.Save(again, first.Model, first.Part, first.Solution);
        var second = ResultFile.Load(again);
        Assert.Equal(Solved.Value.Result.NodeDisplacements, second.Solution.Result.NodeDisplacements);
        Assert.Equal(Solved.Value.SafetyFactor, second.Solution.SafetyFactor);
    }

    [Fact]
    public void The_file_goes_beside_its_study()
    {
        Assert.Equal(Path.GetFullPath(@"C:\parts\clip.results.zip"), ResultFile.BesideStudy(@"C:\parts\clip.study.json"));
        Assert.Equal(Path.GetFullPath(@"C:\parts\clip.results.zip"), ResultFile.BesideStudy(@"C:\parts\clip.json"));
        Assert.Equal(Path.GetFullPath(@"C:\parts\clip.results.zip"), ResultFile.BesideStudy(@"C:\parts\clip.STUDY.JSON"));
        Assert.Equal(Path.GetFullPath(@"C:\parts\clip.txt.results.zip"), ResultFile.BesideStudy(@"C:\parts\clip.txt"));
    }

    [Fact]
    public void A_result_counts_for_its_study_until_what_it_solved_changes()
    {
        var header = ResultFile.ReadHeader(Saved());
        Assert.Null(ResultFile.Mismatch(header, Study()));

        // Another load case, the sweep's settings or a preset left to the selection do not count.
        var more = Study();
        var other = new StudyLoadCase { Name = "Forwards" };
        other.Loads[more.Interfaces[1]] = new LoadValue(new Vector3(0, 50, 0));
        more.LoadCases.Add(other);
        more.Sweep = new SweepSettings(MinContact: 20);
        more.Process = null;
        Assert.Null(ResultFile.Mismatch(header, more));

        var pushed = Study();
        pushed.LoadCases[0].Loads[pushed.Interfaces[1]] = new LoadValue(new Vector3(120, 0, 0));
        Assert.Contains("loads", ResultFile.Mismatch(header, pushed));

        var held = Study();
        held.Interfaces[0].Triangles = held.Interfaces[0].Triangles[1..];
        Assert.Contains("mounts", ResultFile.Mismatch(header, held));

        var turned = Study();
        turned.Rotation = new Vector3(90, 0, 0);
        Assert.Contains("laid another way", ResultFile.Mismatch(header, turned));

        var renamed = Study();
        renamed.LoadCases[0].Name = "Pushed";
        Assert.Contains("no load case", ResultFile.Mismatch(header, renamed));

        var changed = Study();
        changed.GeometryHash = "0123";
        Assert.Contains("model", ResultFile.Mismatch(header, changed));

        var sliced = Study();
        sliced.Filament = "Bambu PETG HF @BBL X1C";
        Assert.Contains("sliced with", ResultFile.Mismatch(header, sliced));
    }

    [Fact]
    public void The_study_is_saved_laid_as_its_slice_was()
    {
        // A sweep solves each candidate from the study as laid at the start: the saved study takes the candidate's turn.
        var study = Study();
        study.Rotation = new Vector3(90, 0, 0);
        var solution = Solved.Value;
        var swept = new PartSolution
        {
            Result = solution.Result, LoadCase = solution.LoadCase, Study = study, Reach = solution.Reach, Material = solution.Material,
            Bonds = solution.Bonds, Strength = solution.Strength, NodeDisplacements = solution.NodeDisplacements, MaxDisplacement = solution.MaxDisplacement,
        };
        var path = Path.Combine(folder, "swept.results.zip");
        ResultFile.Save(path, Cube, Printed.Value, swept);
        Assert.Equal(Vector3.Zero, ResultFile.ReadHeader(path).Study.Rotation);
    }

    [Fact]
    public void A_damaged_file_is_refused_with_its_reason()
    {
        var path = Saved();
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            zip.GetEntry("solution/displacements.bin")!.Delete();
            using var writer = new BinaryWriter(zip.CreateEntry("solution/displacements.bin").Open());
            writer.Write(10);
            writer.Write(new byte[80]);
        }
        Assert.Contains("displacements.bin holds 10 values", Assert.Throws<InvalidDataException>(() => ResultFile.Load(path)).Message);

        var missing = Saved("missing.results.zip");
        using (var zip = ZipFile.Open(missing, ZipArchiveMode.Update)) zip.GetEntry("slice/plate_1.gcode")!.Delete();
        Assert.Contains("holds no slice/plate_1.gcode", Assert.Throws<InvalidDataException>(() => ResultFile.Load(missing)).Message);

        var model = Path.Combine(folder, "cube.study.json");
        File.WriteAllText(model, "not a zip");
        Assert.Throws<InvalidDataException>(() => ResultFile.ReadHeader(model));
    }

    [Fact]
    public void A_model_other_than_the_studys_is_not_saved()
    {
        var other = MeshFactory.Box(Vector3.Zero, new Vector3(10, 10, 10));
        Assert.Throws<InvalidOperationException>(() => ResultFile.Save(Path.Combine(folder, "x.results.zip"), other, Printed.Value, Solved.Value));
    }
}
