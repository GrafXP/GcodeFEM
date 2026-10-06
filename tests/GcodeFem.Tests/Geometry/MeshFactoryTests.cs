using System.Numerics;
using GcodeFem.Core.Geometry;

namespace GcodeFem.Tests.Geometry;

public class MeshFactoryTests
{
    [Fact]
    public void L_bracket_is_a_closed_solid_of_the_right_volume()
    {
        var mesh = MeshFactory.LBracket(leg: 40, width: 10, height: 8);

        Assert.Equal(new Box3(Vector3.Zero, new Vector3(40, 40, 8)), mesh.Bounds);
        Assert.Equal(20, mesh.TriangleCount); // 4 per cap, 2 per side

        // Outward faces enclose the volume (divergence theorem): two 10 mm legs sharing a 10 × 10 corner.
        var volume = 0f;
        var edges = new Dictionary<(int, int), int>();
        for (var t = 0; t < mesh.TriangleCount; t++)
        {
            var (a, b, c) = mesh.Triangle(t);
            volume += Vector3.Dot(a, Vector3.Cross(b, c)) / 6;
            for (var e = 0; e < 3; e++)
            {
                var edge = (mesh.Indices[3 * t + e], mesh.Indices[3 * t + (e + 1) % 3]);
                edges[edge] = edges.GetValueOrDefault(edge) + 1;
            }
        }
        Assert.Equal((40 * 10 + 30 * 10) * 8, volume, 2);
        // Watertight: every edge is used once in each direction.
        Assert.All(edges, edge => Assert.Equal((1, 1), (edge.Value, edges.GetValueOrDefault((edge.Key.Item2, edge.Key.Item1)))));
    }

    [Fact]
    public void Prism_rejects_a_clockwise_outline()
    {
        Vector2[] clockwise = [new(0, 0), new(0, 10), new(10, 10), new(10, 0)];

        Assert.Throws<ArgumentException>(() => MeshFactory.Prism(clockwise, 5));
    }
}
