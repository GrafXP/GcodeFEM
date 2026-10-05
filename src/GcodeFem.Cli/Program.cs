using System.Globalization;
using GcodeFem.Core.Geometry;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

return args switch
{
    ["info", var path] => Info(path),
    _ => Usage(),
};

static int Info(string path)
{
    var mesh = Stl.Read(path);
    var b = mesh.Bounds;
    Console.WriteLine($"{Path.GetFileName(path)}");
    Console.WriteLine($"  triangles  {mesh.TriangleCount}");
    Console.WriteLine($"  vertices   {mesh.Positions.Length}");
    Console.WriteLine($"  bounds     ({b.Min.X:0.###}, {b.Min.Y:0.###}, {b.Min.Z:0.###}) .. ({b.Max.X:0.###}, {b.Max.Y:0.###}, {b.Max.Z:0.###}) mm");
    Console.WriteLine($"  size       {b.Size.X:0.###} x {b.Size.Y:0.###} x {b.Size.Z:0.###} mm");
    return 0;
}

static int Usage()
{
    Console.Error.WriteLine("usage: gcodefem info <model.stl>");
    return 2;
}
