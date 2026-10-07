using System.Numerics;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Study;

namespace GcodeFem.Tests.Study;

public sealed class StudyFileTests : IDisposable
{
    readonly string folder = Path.Combine(Path.GetTempPath(), "GcodeFem.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }

    PartStudy Sample()
    {
        var study = new PartStudy
        {
            ModelPath = Path.Combine(folder, "models", "bracket.stl"),
            GeometryHash = "0123456789abcdef",
            Rotation = new Vector3(90, 0, 45),
            Process = "0.20mm Standard @BBL X1C",
            Filament = "Bambu PLA Basic @BBL X1C",
            MeshLevel = 3,
        };
        var bolts = new PartInterface { Name = "Bolts", Kind = InterfaceKind.BoltHole, Triangles = [40, 41, 42, 43, 90, 91], Axial = false };
        var tip = new PartInterface { Name = "Tip", Kind = InterfaceKind.Force, Triangles = [7, 8] };
        var seat = new PartInterface { Name = "Seat", Kind = InterfaceKind.Pressure, Triangles = [12] };
        study.Interfaces.AddRange([bolts, tip, seat]);

        var hanging = new StudyLoadCase { Name = "Hanging" };
        hanging.Loads[tip] = new LoadValue(new Vector3(0, -30, 1.5f));
        hanging.Loads[seat] = new LoadValue(Pressure: 0.25f);
        var knock = new StudyLoadCase { Name = "Knock" };
        knock.Loads[tip] = new LoadValue(NormalForce: 12);
        study.LoadCases.AddRange([hanging, knock]);
        return study;
    }

    [Fact]
    public void A_study_comes_back_as_it_was_saved()
    {
        var path = Path.Combine(folder, "bracket.study.json");

        StudyFile.Save(Sample(), path);
        var study = StudyFile.Load(path);

        Assert.Equal(Path.Combine(folder, "models", "bracket.stl"), study.ModelPath);
        Assert.Equal("0123456789abcdef", study.GeometryHash);
        Assert.Equal(new Vector3(90, 0, 45), study.Rotation);
        Assert.Equal(("0.20mm Standard @BBL X1C", "Bambu PLA Basic @BBL X1C", 3), (study.Process, study.Filament, study.MeshLevel));

        Assert.Equal(new[] { "Bolts", "Tip", "Seat" }, study.Interfaces.Select(item => item.Name));
        Assert.Equal(new[] { InterfaceKind.BoltHole, InterfaceKind.Force, InterfaceKind.Pressure }, study.Interfaces.Select(item => item.Kind));
        Assert.Equal(new[] { 40, 41, 42, 43, 90, 91 }, study.Interfaces[0].Triangles);
        Assert.False(study.Interfaces[0].Axial);

        var (bolts, tip, seat) = (study.Interfaces[0], study.Interfaces[1], study.Interfaces[2]);
        Assert.Equal(new[] { "Hanging", "Knock" }, study.LoadCases.Select(loadCase => loadCase.Name));
        Assert.Equal(new LoadValue(new Vector3(0, -30, 1.5f)), study.LoadCases[0].Of(tip));
        Assert.Equal(new LoadValue(Pressure: 0.25f), study.LoadCases[0].Of(seat));
        Assert.Equal(new LoadValue(NormalForce: 12), study.LoadCases[1].Of(tip));
        Assert.Equal(new LoadValue(), study.LoadCases[1].Of(seat));
        Assert.Equal(new LoadValue(), study.LoadCases[0].Of(bolts));
    }

    [Fact]
    public void The_model_is_named_relative_to_the_study_and_faces_as_runs()
    {
        var path = Path.Combine(folder, "bracket.study.json");

        StudyFile.Save(Sample(), path);
        var text = File.ReadAllText(path);

        Assert.Contains("\"file\": \"models/bracket.stl\"", text);
        Assert.Contains("\"triangles\": \"40-43,90-91\"", text);
        Assert.Contains("\"kind\": \"boltHole\"", text);
        Assert.DoesNotContain(folder.Replace("\\", "\\\\"), text);
    }

    [Fact]
    public void Two_interfaces_of_one_name_cannot_be_saved()
    {
        var study = Sample();
        study.Interfaces[1].Name = "bolts";

        var error = Assert.Throws<InvalidOperationException>(() => StudyFile.Save(study, Path.Combine(folder, "twice.study.json")));

        Assert.Contains("'bolts'", error.Message);
    }

    [Fact]
    public void A_file_of_another_kind_is_refused()
    {
        Directory.CreateDirectory(folder);
        var notJson = Path.Combine(folder, "a.study.json");
        File.WriteAllText(notJson, "solid cube");
        var newer = Path.Combine(folder, "b.study.json");
        File.WriteAllText(newer, """{ "format": 99, "model": { "file": "x.stl", "hash": "" } }""");

        Assert.Throws<InvalidDataException>(() => StudyFile.Load(notJson));
        Assert.Contains("format 99", Assert.Throws<InvalidDataException>(() => StudyFile.Load(newer)).Message);
    }

    [Fact]
    public void A_changed_model_is_noticed_and_its_faces_dropped()
    {
        var box = MeshFactory.Box(Vector3.Zero, new Vector3(3, 2, 1));
        var study = Sample();
        study.GeometryHash = box.GeometryHash();

        Assert.True(study.Fits(box));
        Assert.False(study.Fits(MeshFactory.Box(Vector3.Zero, new Vector3(3, 2, 1.001f))));

        study.DropFaces();
        Assert.All(study.Interfaces, item => Assert.Empty(item.Triangles));
        Assert.Equal(3, study.Interfaces.Count);
    }

    [Fact]
    public void The_same_file_always_gives_the_same_hash()
    {
        var bytes = Stl.ToBinary(MeshFactory.LBracket(40, 10, 8, holes: 2, holeDiameter: 5));

        Assert.Equal(Stl.Read(bytes).GeometryHash(), Stl.Read(bytes).GeometryHash());
        Assert.Equal(32, Stl.Read(bytes).GeometryHash().Length);
    }

    [Theory]
    [InlineData(new int[0], "")]
    [InlineData(new[] { 5 }, "5")]
    [InlineData(new[] { 0, 1, 2, 3, 7, 9, 10, 11, 12 }, "0-3,7,9-12")]
    [InlineData(new[] { 12, 3, 4, 3 }, "3-4,12")]
    public void Triangles_are_written_as_runs(int[] triangles, string text)
    {
        Assert.Equal(text, TriangleRanges.Format(triangles));
        Assert.Equal(triangles.Distinct().Order(), TriangleRanges.Parse(text));
    }

    [Fact]
    public void Runs_are_read_in_any_order_and_nonsense_is_refused()
    {
        Assert.Equal(new[] { 1, 2, 5 }, TriangleRanges.Parse(" 5, 1-2 "));
        Assert.Throws<InvalidDataException>(() => TriangleRanges.Parse("3-1"));
        Assert.Throws<InvalidDataException>(() => TriangleRanges.Parse("a"));
        Assert.Throws<InvalidDataException>(() => TriangleRanges.Parse("1-2-3"));
    }
}
