using System.Numerics;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Study;

namespace GcodeFem.Cli;

/// <summary>Builds and shows study files without the app: the faces are picked by naming a point on them.</summary>
static class StudyCommands
{
    public static int Run(CommandLine commandLine) => commandLine.Positional[0] switch
    {
        "new" when commandLine.Positional.Count == 3 => New(commandLine),
        "add" when commandLine.Positional.Count == 2 => Add(commandLine),
        "show" when commandLine.Positional.Count == 2 => Show(commandLine.Positional[1]),
        var action => throw new ArgumentException($"Unknown study command '{action}' (use new <study> <model>, add <study> or show <study>)."),
    };

    static int New(CommandLine commandLine)
    {
        var model = Path.GetFullPath(commandLine.Positional[2]);
        var study = new PartStudy
        {
            ModelPath = model,
            GeometryHash = Stl.Read(model).GeometryHash(),
            Rotation = commandLine.Option("rot") is { } rot ? Text.Triple(rot, "rot") : default,
            Process = commandLine.Option("process"),
            Filament = commandLine.Option("filament"),
            MeshLevel = commandLine.Option("level") is { } level and not "auto" ? int.Parse(level) : null,
        };
        if (commandLine.Option("lay") is { } point)
        {
            // The face at that point of the model goes on the bed, by the shortest way from --rot.
            var mesh = Stl.Read(model);
            var (triangle, distance) = MeshPicker.Nearest(mesh, Text.Triple(point, "lay"));
            if (distance > 0.5f) throw new ArgumentException($"--lay {point} is {distance:0.##} mm off the model's surface.");
            var current = Orientation.FromEulerDegrees(study.Rotation.X, study.Rotation.Y, study.Rotation.Z);
            study.Rotation = Orientation.LayFlat(current, FaceRegions.FlatNormal(mesh, MeshTopology.Of(mesh), triangle));
        }
        study.LoadCases.Add(new StudyLoadCase { Name = commandLine.Option("case") ?? "Load case 1" });
        StudyFile.Save(study, commandLine.Positional[1]);
        return Show(commandLine.Positional[1]);
    }

    /// <summary>Adds an interface, or more faces or another load case's value to one that is there.</summary>
    static int Add(CommandLine commandLine)
    {
        var path = commandLine.Positional[1];
        var study = StudyFile.Load(path);
        var mesh = Model(study);
        var name = commandLine.Option("name") ?? throw new ArgumentException("--name says which interface.");

        var item = study.Interfaces.FirstOrDefault(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));
        if (item is null) study.Interfaces.Add(item = new PartInterface { Name = name });
        if (commandLine.Option("kind") is { } kind)
            item.Kind = Enum.TryParse<InterfaceKind>(kind, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
                ? parsed
                : throw new FormatException($"--kind expects fixed, sliding, bolthole, force, pressure or bearing but got '{kind}'.");
        if (commandLine.Flag("free-along")) item.Axial = false;

        if (commandLine.Option("at") is { } points)
        {
            var topology = MeshTopology.Of(mesh);
            var angle = (float)commandLine.Number("angle", 20);
            var faces = new HashSet<int>(item.Triangles);
            foreach (var point in points.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var (triangle, distance) = MeshPicker.Nearest(mesh, Text.Triple(point, "at"));
                if (distance > 0.5f) throw new ArgumentException($"--at {point} is {distance:0.##} mm off the model's surface.");
                faces.UnionWith(FaceRegions.Grow(mesh, topology, triangle, angle));
            }
            item.Triangles = [.. faces.Order()];
        }

        var loadCase = Case(study, commandLine.Option("case"), create: true);
        var value = loadCase.Of(item);
        if (commandLine.Option("force") is { } force) value = value with { Force = Text.Triple(force, "force"), NormalForce = null };
        if (commandLine.Option("normal-force") is not null) value = value with { NormalForce = (float)commandLine.Number("normal-force", 0) };
        if (commandLine.Option("pressure") is not null) value = value with { Pressure = (float)commandLine.Number("pressure", 0) };
        if (item.Kind.IsLoad()) loadCase.Loads[item] = value;

        StudyFile.Save(study, path);
        return Show(path);
    }

    static int Show(string path)
    {
        var study = StudyFile.Load(path);
        var mesh = File.Exists(study.ModelPath) ? Stl.Read(study.ModelPath) : null;
        var fits = mesh is not null && study.Fits(mesh);
        Console.WriteLine(Path.GetFileName(path));
        Console.WriteLine($"  model      {study.ModelPath}{(mesh is null ? "  <-- NOT FOUND" : fits ? "" : "  <-- CHANGED since the faces were picked")}");
        Console.WriteLine($"  print      rotation {study.Rotation.X:0.##}, {study.Rotation.Y:0.##}, {study.Rotation.Z:0.##} deg; process {study.Process ?? "(current)"}; filament {study.Filament ?? "(current)"}; " +
                          $"mesh {(study.MeshLevel is { } level ? $"level {level}" : "automatic")}");
        Console.WriteLine($"  interfaces {study.Interfaces.Count}");
        foreach (var item in study.Interfaces)
        {
            var faces = fits ? FaceDescription.Of(mesh!, item.Triangles).Replace("Ø", "D").Replace("²", "2") : $"{item.Triangles.Length} triangles";
            var kind = item.Kind == InterfaceKind.BoltHole && !item.Axial ? "BoltHole, free along the hole" : item.Kind.ToString();
            Console.WriteLine($"    {item.Name,-16} {kind,-10} {faces}");
        }
        foreach (var loadCase in study.LoadCases)
        {
            Console.WriteLine($"  load case  {loadCase.Name}");
            foreach (var item in study.Interfaces.Where(i => i.Kind.IsLoad()))
                Console.WriteLine($"    {item.Name,-16} {Describe(item.Kind, loadCase.Of(item))}");
        }
        return 0;
    }

    public static string Describe(InterfaceKind kind, LoadValue value) => kind switch
    {
        InterfaceKind.Pressure => $"{value.Pressure:0.###} MPa onto the face",
        InterfaceKind.Force when value.NormalForce is { } push => $"{push:0.###} N onto the face, along its normal",
        _ => $"{Text.Format(value.Force)} N in the model's directions",
    };

    /// <summary>The study's model, as long as it is still the one its faces were picked on.</summary>
    public static TriangleMesh Model(PartStudy study)
    {
        var mesh = Stl.Read(study.ModelPath);
        return study.Fits(mesh) ? mesh : throw new InvalidDataException($"'{study.ModelPath}' has changed since the study's faces were picked on it. Pick them again.");
    }

    /// <summary>The load case of that name, or the first one if no name is given.</summary>
    public static StudyLoadCase Case(PartStudy study, string? name, bool create = false)
    {
        if (name is null) return study.LoadCases[0];
        if (study.LoadCases.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)) is { } found) return found;
        if (!create) throw new KeyNotFoundException($"The study has no load case '{name}' (it has {string.Join(", ", study.LoadCases.Select(c => $"'{c.Name}'"))}).");
        var added = new StudyLoadCase { Name = name };
        // A new load case starts from the first one's values, as it does in the app.
        foreach (var (item, value) in study.LoadCases[0].Loads) added.Loads[item] = value;
        study.LoadCases.Add(added);
        return added;
    }
}
