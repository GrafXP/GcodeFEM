using System.Numerics;
using GcodeFem.Core.Geometry;

namespace GcodeFem.Tests.Geometry;

public class ConvexHullTests
{
    /// <summary>Every point inside or on the hull, every facet facing out, and each edge shared by two facets, run opposite ways.</summary>
    static void AssertClosedAndConvex(ConvexHull hull, IReadOnlyList<Vector3> points)
    {
        Assert.True(hull.FacetCount >= 4);
        var edges = new HashSet<(int, int)>();
        for (var f = 0; f < hull.FacetCount; f++)
        {
            var (a, b, c) = (hull.Facets[3 * f], hull.Facets[3 * f + 1], hull.Facets[3 * f + 2]);
            Assert.True(edges.Add((a, b)) && edges.Add((b, c)) && edges.Add((c, a)), "an edge is run the same way twice");
            var normal = hull.Normal(f);
            var offset = Vector3.Dot(normal, points[a]);
            Assert.All(points, p => Assert.True(Vector3.Dot(normal, p) - offset < 1e-3f, $"{p} is outside facet {f}"));
        }
        Assert.All(edges, e => Assert.Contains((e.Item2, e.Item1), edges));
        // Euler: corners − edges + facets = 2 for a closed surface without holes.
        var corners = hull.Facets.Distinct().Count();
        Assert.Equal(2, corners - edges.Count / 2 + hull.FacetCount);
    }

    [Fact]
    public void A_box_with_points_inside_is_the_twelve_triangles_of_its_sides()
    {
        var random = new Random(7);
        var points = MeshFactory.Box(Vector3.Zero, new Vector3(30, 20, 10)).Positions
            .Concat(Enumerable.Range(0, 200).Select(_ => new Vector3(30 * random.NextSingle(), 20 * random.NextSingle(), 10 * random.NextSingle())))
            .ToList();

        var hull = ConvexHull.Of(points);

        AssertClosedAndConvex(hull, points);
        Assert.Equal(12, hull.FacetCount);
        Assert.Equal(2 * (30 * 20 + 20 * 10 + 30 * 10), Enumerable.Range(0, 12).Sum(hull.Area), 3);
        Assert.All(Enumerable.Range(0, 12), f => Assert.Equal(1, MathF.Abs(hull.Normal(f).X) + MathF.Abs(hull.Normal(f).Y) + MathF.Abs(hull.Normal(f).Z), 5));
    }

    [Fact]
    public void Points_on_a_sphere_all_end_up_on_or_inside_it()
    {
        var random = new Random(11);
        var points = Enumerable.Range(0, 2000).Select(_ =>
        {
            var v = new Vector3(random.NextSingle() - 0.5f, random.NextSingle() - 0.5f, random.NextSingle() - 0.5f);
            return 25 * Vector3.Normalize(v) * (random.Next(4) == 0 ? random.NextSingle() : 1);
        }).ToList();

        AssertClosedAndConvex(ConvexHull.Of(points), points);
    }

    [Fact]
    public void Finely_meshed_flat_sides_stay_flat()
    {
        // A box whose sides are grids of points, flat only to the rounding of their floats, as the faces of an STL are.
        var points = new List<Vector3>();
        for (var u = 0; u <= 30; u++)
        for (var v = 0; v <= 30; v++)
        {
            float x = 41.3f * u / 30, y = 17.9f * v / 30, z = 9.7f * v / 30;
            points.AddRange([new(x, y, 0), new(x, y, 9.7f), new(x, 0, z), new(x, 17.9f, z), new(0, 41.3f * u / 30 * 17.9f / 41.3f, z), new(41.3f, 17.9f * u / 30, z)]);
        }

        var hull = ConvexHull.Of(points);

        AssertClosedAndConvex(hull, points);
        Assert.All(Enumerable.Range(0, hull.FacetCount), f => Assert.Equal(1, MathF.Max(MathF.Abs(hull.Normal(f).X), MathF.Max(MathF.Abs(hull.Normal(f).Y), MathF.Abs(hull.Normal(f).Z))), 4));
        Assert.Equal(2 * (41.3 * 17.9 + 17.9 * 9.7 + 41.3 * 9.7), Enumerable.Range(0, hull.FacetCount).Sum(hull.Area), 1);
    }

    [Fact]
    public void Points_that_span_no_volume_have_no_hull()
    {
        Assert.Equal(0, ConvexHull.Of([new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)]).FacetCount);
        Assert.Equal(0, ConvexHull.Of([.. Enumerable.Range(0, 50).Select(i => new Vector3(i % 7, i / 7, 0))]).FacetCount);
        Assert.Equal(0, ConvexHull.Of([.. Enumerable.Range(0, 50).Select(i => new Vector3(i, 2 * i, -i))]).FacetCount);
    }
}
