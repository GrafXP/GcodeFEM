using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GcodeFem.Core.Study;

/// <summary>
/// A <see cref="PartStudy"/> as a JSON file. The model is named relative to the file, so a study
/// and its model can be moved together. Faces are stored as the model's triangle numbers along
/// with the model's hash, so that a model that has changed since is noticed (<see cref="PartStudy.Fits"/>).
/// </summary>
public static class StudyFile
{
    const int Format = 1;

    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    sealed record FileDto(int Format, ModelDto Model, PrintDto Print, int? MeshLevel, List<InterfaceDto> Interfaces, List<LoadCaseDto> LoadCases);

    sealed record ModelDto(string File, string Hash);

    sealed record PrintDto(float[] Rotation, string? Process, string? Filament);

    sealed record InterfaceDto(string Name, InterfaceKind Kind, string Triangles, bool? Axial);

    sealed record LoadCaseDto(string Name, Dictionary<string, LoadDto> Loads);

    sealed record LoadDto(float[]? Force, float? NormalForce, float? Pressure);

    public static void Save(PartStudy study, string path)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in study.Interfaces)
        {
            if (string.IsNullOrWhiteSpace(item.Name)) throw new InvalidOperationException("An interface has no name.");
            if (!names.Add(item.Name)) throw new InvalidOperationException($"Two interfaces are called '{item.Name}'. Give each its own name.");
        }

        var folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        var file = new FileDto(
            Format,
            new ModelDto(Path.GetRelativePath(folder, study.ModelPath).Replace('\\', '/'), study.GeometryHash),
            new PrintDto([study.Rotation.X, study.Rotation.Y, study.Rotation.Z], study.Process, study.Filament),
            study.MeshLevel,
            [.. study.Interfaces.Select(item => new InterfaceDto(item.Name, item.Kind, TriangleRanges.Format(item.Triangles), item.Kind == InterfaceKind.BoltHole ? item.Axial : null))],
            [.. study.LoadCases.Select(loadCase => new LoadCaseDto(loadCase.Name, study.Interfaces.Where(item => item.Kind.IsLoad()).ToDictionary(item => item.Name, item => ToDto(item.Kind, loadCase.Of(item)))))]);
        Directory.CreateDirectory(folder);
        File.WriteAllText(path, JsonSerializer.Serialize(file, Options));
    }

    public static PartStudy Load(string path)
    {
        FileDto file;
        try
        {
            file = JsonSerializer.Deserialize<FileDto>(File.ReadAllText(path), Options) ?? throw new InvalidDataException($"'{path}' is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"'{path}' is not a study file: {ex.Message}", ex);
        }
        if (file.Format != Format) throw new InvalidDataException($"'{path}' is a study of format {file.Format}; this version reads format {Format}.");
        if (file.Model?.File is not { Length: > 0 } model) throw new InvalidDataException($"'{path}' names no model.");

        var study = new PartStudy
        {
            ModelPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, model)),
            GeometryHash = file.Model.Hash ?? "",
            Rotation = file.Print?.Rotation is [var x, var y, var z] ? new Vector3(x, y, z) : Vector3.Zero,
            Process = file.Print?.Process,
            Filament = file.Print?.Filament,
            MeshLevel = file.MeshLevel,
        };
        foreach (var item in file.Interfaces ?? [])
            study.Interfaces.Add(new PartInterface { Name = item.Name, Kind = item.Kind, Triangles = TriangleRanges.Parse(item.Triangles ?? ""), Axial = item.Axial ?? true });
        foreach (var saved in file.LoadCases ?? [])
        {
            var loadCase = new StudyLoadCase { Name = saved.Name };
            foreach (var (name, load) in saved.Loads ?? [])
                if (study.Interfaces.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)) is { } item)
                    loadCase.Loads[item] = new LoadValue(load.Force is [var fx, var fy, var fz] ? new Vector3(fx, fy, fz) : Vector3.Zero, load.NormalForce, load.Pressure ?? 0);
            study.LoadCases.Add(loadCase);
        }
        if (study.LoadCases.Count == 0) study.LoadCases.Add(new StudyLoadCase { Name = "Load case 1" });
        return study;
    }

    /// <summary>Only what the interface's kind uses, so the file says nothing that does not count.</summary>
    static LoadDto ToDto(InterfaceKind kind, LoadValue value) => kind switch
    {
        InterfaceKind.Pressure => new LoadDto(null, null, value.Pressure),
        InterfaceKind.Force when value.NormalForce is { } push => new LoadDto(null, push, null),
        _ => new LoadDto([value.Force.X, value.Force.Y, value.Force.Z], null, null),
    };
}

/// <summary>Triangle numbers written as runs, "0-11,40,42-57": a face is mostly neighbours in the file.</summary>
public static class TriangleRanges
{
    public static string Format(IEnumerable<int> triangles)
    {
        var text = new StringBuilder();
        int? first = null;
        var last = 0;
        foreach (var t in triangles.Distinct().Order().Append(int.MinValue))
        {
            if (first is not null && t == last + 1)
            {
                last = t;
                continue;
            }
            if (first is { } from)
            {
                if (text.Length > 0) text.Append(',');
                text.Append(from.ToString(CultureInfo.InvariantCulture));
                if (last > from) text.Append('-').Append(last.ToString(CultureInfo.InvariantCulture));
            }
            (first, last) = (t, t);
        }
        return text.ToString();
    }

    public static int[] Parse(string text)
    {
        var triangles = new List<int>();
        foreach (var run in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var ends = run.Split('-');
            if (ends.Length is < 1 or > 2 || !int.TryParse(ends[0], NumberStyles.None, CultureInfo.InvariantCulture, out var from)
                                          || !int.TryParse(ends[^1], NumberStyles.None, CultureInfo.InvariantCulture, out var to) || to < from)
                throw new InvalidDataException($"'{run}' is not a triangle or a run of triangles.");
            for (var t = from; t <= to; t++) triangles.Add(t);
        }
        return [.. triangles.Distinct().Order()];
    }
}
