using System.Numerics;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Visual;

namespace GcodeFem.Tests.Visual;

public class CellSurfaceTests
{
    const float Pitch = 0.42f, Layer = 0.2f;

    static CellIndex[] Cells(CellGrid grid) => [.. grid.Occupied(0.05f)];

    /// <summary>A 5 × 5 × 5 block of cells with its inner 3 × 3 × 3 left empty.</summary>
    static CellGrid Shell()
    {
        var z = Enumerable.Range(0, 6).Select(k => k * Layer).ToArray();
        var grid = new CellGrid(Vector2.Zero, Pitch, z, 5, 5);
        for (var k = 0; k < 5; k++)
        for (var j = 0; j < 5; j++)
        for (var i = 0; i < 5; i++)
            if (i is 0 or 4 || j is 0 or 4 || k is 0 or 4)
                grid.Deposit(i, j, k, grid.CellVolume(k));
        return grid;
    }

    [Fact]
    public void A_solid_block_shows_only_its_outer_faces()
    {
        var grid = CellGrid.Solid(3, 2, 2, Pitch, Layer);
        var cells = Cells(grid);

        var surface = CellSurface.Build(grid, cells, CellRange.All(grid));

        Assert.Equal(2 * (3 * 2 + 2 * 2 + 3 * 2), surface.FaceCount);
        Assert.Equal(4 * surface.FaceCount, surface.Positions.Length);
        Assert.Equal(6 * surface.FaceCount, surface.Indices.Length);
        Assert.Equal(new Box3(Vector3.Zero, new Vector3(3 * Pitch, 2 * Pitch, 2 * Layer)), Box3.Of(surface.Positions));
    }

    [Fact]
    public void Faces_point_away_from_their_cell_and_wind_counter_clockwise()
    {
        var grid = CellGrid.Solid(2, 2, 2, Pitch, Layer);
        var cells = Cells(grid);

        var surface = CellSurface.Build(grid, cells, CellRange.All(grid));

        for (var f = 0; f < surface.FaceCount; f++)
        {
            var cell = cells[surface.FaceCells[f]];
            var cellCentre = grid.NodePosition(cell.I, cell.J, cell.K) + new Vector3(Pitch, Pitch, Layer) / 2;
            var faceCentre = (surface.Positions[4 * f] + surface.Positions[4 * f + 2]) / 2;
            Assert.True(Vector3.Dot(surface.Normals[4 * f], faceCentre - cellCentre) > 0);
            for (var t = 0; t < 2; t++)
            {
                Vector3 a = surface.Positions[surface.Indices[6 * f + 3 * t]], b = surface.Positions[surface.Indices[6 * f + 3 * t + 1]], c = surface.Positions[surface.Indices[6 * f + 3 * t + 2]];
                Assert.True(Vector3.Dot(Vector3.Cross(b - a, c - a), surface.Normals[4 * f]) > 0);
            }
        }
    }

    [Fact]
    public void A_sealed_void_stays_hidden_until_the_range_cuts_it_open()
    {
        var grid = Shell();
        var cells = Cells(grid);

        var whole = CellSurface.Build(grid, cells, CellRange.All(grid));
        var cut = CellSurface.Build(grid, cells, CellRange.Layers(grid, 0, 3));

        Assert.Equal(6 * 25, whole.FaceCount);
        // Below the cut: the bottom, three layers of sides, the cut ring, and now the void's floor and walls.
        Assert.Equal(25 + 4 * 5 * 3 + 16 + 9 + 4 * 3 * 2, cut.FaceCount);
    }

    [Fact]
    public void An_empty_range_shows_nothing()
    {
        var grid = CellGrid.Solid(2, 2, 2, Pitch, Layer);

        Assert.Equal(0, CellSurface.Build(grid, Cells(grid), CellRange.Layers(grid, 1, 1)).FaceCount);
    }

    [Fact]
    public void Colours_and_displacements_follow_the_cells_and_nodes()
    {
        var grid = CellGrid.Solid(3, 2, 2, Pitch, Layer);
        var mesh = FemMesh.Build(grid, 0.05f, [new Fixture(_ => true)]);
        var surface = CellSurface.Build(grid, mesh.Cells, CellRange.All(grid));

        var colours = surface.VertexColours(cell => new Vector4(cell, 0, 0, 1));
        // Every node moves along X by its own height: a shear of the block.
        var displacements = Enumerable.Range(0, mesh.NodeCount).Select(n => new Vector3(mesh.NodePosition(n).Z, 0, 0)).ToArray();
        var moved = surface.Displaced(mesh, displacements, 10);

        for (var v = 0; v < surface.Positions.Length; v++)
        {
            Assert.Equal(surface.FaceCells[v / 4], colours[v].X);
            Assert.Equal(surface.Positions[v] + new Vector3(10 * surface.Positions[v].Z, 0, 0), moved[v]);
        }
    }
}

public class OctreeWireframeTests
{
    const float Pitch = 0.42f, Layer = 0.2f;

    static FemMesh Block(int size) => FemMesh.Build(CellGrid.Solid(size, size, size, Pitch, Layer), 0.05f, [new Fixture(_ => true)]);

    static byte[] Levels(FemMesh mesh, byte level) => [.. Enumerable.Repeat(level, mesh.Cells.Length)];

    [Fact]
    public void One_coarse_element_is_outlined_by_its_twelve_edges()
    {
        var mesh = Block(4);

        var lines = OctreeWireframe.Build(mesh, Levels(mesh, 2), CellRange.All(mesh.Grid));

        Assert.Equal(2 * 12, lines.Length);
        Assert.Equal(new Box3(Vector3.Zero, new Vector3(4 * Pitch, 4 * Pitch, 4 * Layer)), Box3.Of(lines));
    }

    [Fact]
    public void Shared_edges_are_drawn_once()
    {
        var mesh = Block(4);

        var lines = OctreeWireframe.Build(mesh, Levels(mesh, 1), CellRange.All(mesh.Grid), minLevel: 1);

        // 2 × 2 × 2 elements: a lattice of 3 × 3 lines of two edges each, in three directions.
        Assert.Equal(2 * 3 * 9 * 2, lines.Length);
    }

    [Fact]
    public void Outlines_keep_to_the_material()
    {
        // One element of 4 × 4 × 4 cells whose far half (i ≥ 2) is empty.
        var grid = CellGrid.Solid(2, 4, 4, Pitch, Layer);
        var wide = new CellGrid(Vector2.Zero, Pitch, grid.ZBoundaries.ToArray(), 4, 4);
        foreach (var c in grid.Occupied(0.05f)) wide.Deposit(c.I, c.J, c.K, wide.CellVolume(c.K));
        var mesh = FemMesh.Build(wide, 0.05f, [new Fixture(_ => true)]);

        var lines = OctreeWireframe.Build(mesh, Levels(mesh, 2), CellRange.All(wide));

        // The four edges at i = 0 in full, and the four along X as far as the material goes.
        Assert.Equal(2 * 8, lines.Length);
        Assert.Equal(2 * Pitch, lines.Max(p => p.X));
        var length = 0f;
        for (var n = 0; n < lines.Length; n += 2) length += Vector3.Distance(lines[n], lines[n + 1]);
        Assert.Equal(2 * 4 * Pitch + 2 * 4 * Layer + 4 * 2 * Pitch, length, 4);
    }

    [Fact]
    public void Fine_elements_get_no_outline()
    {
        var mesh = Block(4);

        Assert.Empty(OctreeWireframe.Build(mesh, Levels(mesh, 1), CellRange.All(mesh.Grid)));
        Assert.Empty(OctreeWireframe.Build(mesh, Levels(mesh, 0), CellRange.All(mesh.Grid), minLevel: 1));
    }

    [Fact]
    public void Outlines_stop_at_the_shown_range_and_follow_the_deformation()
    {
        var mesh = Block(4);

        var lines = OctreeWireframe.Build(mesh, Levels(mesh, 2), CellRange.Layers(mesh.Grid, 0, 1), (_, _, k) => new Vector3(0, 0, k));

        Assert.Equal(2 * 12, lines.Length);
        Assert.Equal(0, lines.Min(p => p.Z));
        Assert.Equal(Layer + 1, lines.Max(p => p.Z), 5);
    }
}
