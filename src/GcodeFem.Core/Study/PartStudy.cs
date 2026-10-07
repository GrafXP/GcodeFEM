using System.Numerics;
using GcodeFem.Core.Geometry;

namespace GcodeFem.Core.Study;

/// <summary>What happens at an interface: the first three hold the part, the others load it.</summary>
public enum InterfaceKind
{
    /// <summary>Held in every direction.</summary>
    Fixed,

    /// <summary>Held against the face only: free to slide along it.</summary>
    Sliding,

    /// <summary>The wall of a round hole, held across the hole and, for a bolt done up tight, along it.</summary>
    BoltHole,

    /// <summary>A force spread over the face.</summary>
    Force,

    /// <summary>A pressure onto the face.</summary>
    Pressure,

    /// <summary>A force from a pin or bolt in a hole: it presses on the side of the wall it pushes against, most in the middle.</summary>
    Bearing,
}

public static class InterfaceKinds
{
    public static bool IsLoad(this InterfaceKind kind) => kind is InterfaceKind.Force or InterfaceKind.Pressure or InterfaceKind.Bearing;
}

/// <summary>
/// A face of the part where it mounts to or touches another part. It belongs to the part as
/// modelled (part frame), so it stays put however the part is turned for printing.
/// </summary>
public sealed class PartInterface
{
    public string Name { get; set; } = "";
    public InterfaceKind Kind { get; set; }

    /// <summary>The model's triangles that make up the face, ascending.</summary>
    public int[] Triangles { get; set; } = [];

    /// <summary>For a bolt hole: held along the hole as well (a bolt done up tight), not only across it (a pin).</summary>
    public bool Axial { get; set; } = true;
}

/// <summary>What a load interface carries in one load case; the interface's kind says which value counts.</summary>
/// <param name="Force">N in the model's own directions (part frame): for a force and for a bearing load.</param>
/// <param name="NormalForce">For a force: N pushing onto the face along its normal (negative pulls), used instead of <paramref name="Force"/> when set.</param>
/// <param name="Pressure">MPa onto the face.</param>
public sealed record LoadValue(Vector3 Force = default, float? NormalForce = null, float Pressure = 0);

/// <summary>One situation the part must stand up to: a value for each load interface. The mounts are the same in all of them.</summary>
public sealed class StudyLoadCase
{
    public string Name { get; set; } = "";

    public Dictionary<PartInterface, LoadValue> Loads { get; } = [];

    /// <summary>The value at <paramref name="item"/>; nothing if none was given.</summary>
    public LoadValue Of(PartInterface item) => Loads.GetValueOrDefault(item) ?? new LoadValue();
}

/// <summary>A part, how it is to be printed, and what is asked of it.</summary>
public sealed class PartStudy
{
    /// <summary>The model's file, full path.</summary>
    public string ModelPath { get; set; } = "";

    /// <summary><see cref="TriangleMesh.GeometryHash"/> of the model the interfaces were picked on.</summary>
    public string GeometryHash { get; set; } = "";

    /// <summary>Degrees about X, then Y, then Z: part frame → print frame.</summary>
    public Vector3 Rotation { get; set; }

    /// <summary>Bambu Studio presets by name; null leaves it to the current selection.</summary>
    public string? Process { get; set; }

    public string? Filament { get; set; }

    /// <summary>Coarse elements are 2^level cells per side; null lets the solver choose.</summary>
    public int? MeshLevel { get; set; }

    public List<PartInterface> Interfaces { get; } = [];
    public List<StudyLoadCase> LoadCases { get; } = [];

    /// <summary>
    /// Whether the picked faces still refer to <paramref name="mesh"/>. If not, <see cref="DropFaces"/>
    /// keeps everything but the faces, which then have to be picked again.
    /// </summary>
    public bool Fits(TriangleMesh mesh) => GeometryHash == mesh.GeometryHash();

    public void DropFaces()
    {
        foreach (var item in Interfaces) item.Triangles = [];
    }
}

/// <summary>Says in a few words what a set of picked triangles is.</summary>
public static class FaceDescription
{
    public static string Of(TriangleMesh mesh, IReadOnlyCollection<int> triangles)
    {
        if (triangles.Count == 0) return "No faces picked yet";
        var patch = new SurfacePatch(mesh, triangles);
        var parts = patch.Components();
        var round = parts.Select(part => Cylinder.Fit(mesh, part)).ToList();

        string shape;
        if (round.All(c => c is { IsHole: true }))
        {
            var widths = round.Select(c => $"Ø{2 * c!.Radius:0.##} x {c.Length:0.##} mm").Distinct().ToList();
            shape = (parts.Length == 1 ? "Hole" : $"{parts.Length} holes") + (widths.Count == 1 ? $" {widths[0]}" : "");
        }
        else if (round.All(c => c is not null)) shape = parts.Length == 1 ? "Round face" : $"{parts.Length} round faces";
        else if (patch.MeanNormal.Length() > 0.999f) shape = parts.Length == 1 ? "Flat face" : $"{parts.Length} flat faces";
        else shape = parts.Length == 1 ? "Face" : $"{parts.Length} faces";
        return $"{shape}, {patch.Area:0.#} mm², {triangles.Count:N0} triangles";
    }
}
