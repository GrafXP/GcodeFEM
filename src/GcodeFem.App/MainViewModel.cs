using System.Globalization;
using System.IO;
using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;
using GcodeFem.Core.Geometry;
using HelixToolkit;
using HelixToolkit.SharpDX;

namespace GcodeFem.App;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    public DefaultEffectsManager EffectsManager { get; } = new();

    public TriangleMesh? Part { get; private set; }

    [ObservableProperty]
    public partial MeshGeometry3D? PartGeometry { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; } = "Open an STL to start.";

    public void LoadStl(string path)
    {
        var mesh = Stl.Read(path);
        Part = mesh;
        PartGeometry = ToFlatShaded(mesh);

        var size = mesh.Bounds.Size;
        Status = string.Create(CultureInfo.InvariantCulture,
            $"{Path.GetFileName(path)} — {mesh.TriangleCount} triangles, {size.X:0.##} × {size.Y:0.##} × {size.Z:0.##} mm");
    }

    /// <summary>STL faces are flat, so every triangle gets its own corners and face normal.</summary>
    static MeshGeometry3D ToFlatShaded(TriangleMesh mesh)
    {
        var n = mesh.TriangleCount * 3;
        var positions = new Vector3Collection(n);
        var normals = new Vector3Collection(n);
        var indices = new IntCollection(n);
        for (int t = 0; t < mesh.TriangleCount; t++)
        {
            var (a, b, c) = mesh.Triangle(t);
            var normal = mesh.Normal(t);
            positions.Add(a);
            positions.Add(b);
            positions.Add(c);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            indices.Add(3 * t);
            indices.Add(3 * t + 1);
            indices.Add(3 * t + 2);
        }
        return new MeshGeometry3D { Positions = positions, Normals = normals, Indices = indices };
    }

    public void Dispose() => EffectsManager.Dispose();
}
