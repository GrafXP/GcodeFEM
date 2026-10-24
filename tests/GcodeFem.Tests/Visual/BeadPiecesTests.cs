using System.Numerics;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Gcode;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Slicing;
using GcodeFem.Core.Visual;

namespace GcodeFem.Tests.Visual;

/// <summary>A result on the print lines: the beads cut at the cells they run through, drawn, deformed and clicked.</summary>
public class BeadPiecesTests
{
    const float Width = 0.42f, Height = 0.2f;
    static readonly Placement OnBed = new(Matrix4x4.Identity, Vector3.Zero);

    /// <summary>A bead from (x0, y0) to (x1, y1), in pitches.</summary>
    static ExtrusionSegment Bead(float x0, float y0, float x1, float y1, int layer, FeatureType feature)
    {
        var z = (layer + 1) * Height;
        return new(new Vector3(x0 * Width, y0 * Width, z), new Vector3(x1 * Width, y1 * Width, z), Width, Height,
            Width * Height * Width * Vector2.Distance(new(x0, y0), new(x1, y1)), 0, 1, layer, feature, 220, 0);
    }

    /// <summary>
    /// A wall line along row 3 from the middle of cell 2 to the middle of cell 6, in two layers; a skirt
    /// line; a line of sparse infill off on its own, which no mount holds; and a line of no length.
    /// </summary>
    static Toolpath Print()
    {
        ExtrusionSegment[] segments =
        [
            Bead(2.5f, 3.5f, 6.5f, 3.5f, 0, FeatureType.OuterWall),
            Bead(1, 15.5f, 8, 15.5f, 0, FeatureType.Skirt),
            Bead(12.5f, 12.5f, 16.5f, 12.5f, 0, FeatureType.SparseInfill),
            Bead(2.5f, 3.5f, 6.5f, 3.5f, 1, FeatureType.InnerWall),
            Bead(4, 3.5f, 4, 3.5f, 1, FeatureType.InnerWall),
        ];
        return new Toolpath
        {
            Segments = segments,
            Layers = [new ToolpathLayer(0, Height, Height, 0, 3, 0, 0), new ToolpathLayer(1, 2 * Height, Height, 3, 2, 0, 0)],
            Settings = new Dictionary<string, string> { ["line_width"] = "0.42" },
            UnknownFeatures = new HashSet<string>(),
            FilamentDiameter = 1.75f,
            TotalTime = 0,
            ExtruderOffset = Vector3.Zero,
        };
    }

    /// <summary>The print's cells, held where the wall lines start.</summary>
    static (Toolpath Toolpath, FemMesh Mesh, BeadPieces Pieces) Cut()
    {
        var toolpath = Print();
        var grid = Voxelizer.Voxelize(toolpath, OnBed, new Box3(Vector3.Zero, new Vector3(20 * Width, 20 * Width, 2 * Height)));
        var mesh = FemMesh.Build(grid, 0.05f, [new Fixture(p => p.X < 2.01f * Width)]);
        return (toolpath, mesh, BeadPieces.Cut(toolpath, OnBed, mesh));
    }

    static int[] Of(BeadPieces pieces, int segment) => [.. Enumerable.Range(0, pieces.Count).Where(p => pieces.Segments[p] == segment)];

    static void Near(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, 4);
        Assert.Equal(expected.Y, actual.Y, 4);
        Assert.Equal(expected.Z, actual.Z, 4);
    }

    [Fact]
    public void A_bead_is_cut_at_each_cell_it_runs_through()
    {
        var (_, mesh, pieces) = Cut();

        var wall = Of(pieces, 0);

        Assert.Equal([2, 3, 4, 5, 6], wall.Select(p => mesh.Cells[pieces.Cells[p]].I));
        Assert.All(wall, p => Assert.Equal((3, 0), (mesh.Cells[pieces.Cells[p]].J, mesh.Cells[pieces.Cells[p]].K)));
        Assert.Equal([BeadPieces.StartTip, 0, 0, 0, BeadPieces.EndTip], wall.Select(p => pieces.Tips[p]));
        // On the bead's centre line, half a bead below the nozzle, cut at the cell boundaries and joined there.
        Near(new Vector3(2.5f * Width, 3.5f * Width, Height / 2), pieces.Ends[2 * wall[0]]);
        Assert.Equal([3, 4, 5, 6, 6.5f], wall.Select(p => MathF.Round(pieces.Ends[2 * p + 1].X / Width, 4)));
        Assert.All(wall.Skip(1), p => Assert.Equal(pieces.Ends[2 * p - 1], pieces.Ends[2 * p]));
        Assert.Empty(Of(pieces, 4)); // no length, nothing to draw
    }

    [Fact]
    public void Lines_outside_the_solve_have_no_cell_and_stay_in_one_piece()
    {
        var (_, mesh, pieces) = Cut();

        var skirt = Of(pieces, 1);
        var island = Of(pieces, 2);

        Assert.True(mesh.DroppedCells >= 5); // the island
        foreach (var line in new[] { skirt, island })
        {
            var piece = Assert.Single(line);
            Assert.Equal(-1, pieces.Cells[piece]);
            Assert.Equal(BeadPieces.StartTip | BeadPieces.EndTip, pieces.Tips[piece]);
        }
        Near(new Vector3(16.5f * Width, 12.5f * Width, Height / 2), pieces.Ends[2 * island[0] + 1]);
    }

    [Theory]
    [InlineData(1.3f, 0.7f, 17.9f, 13.2f)]
    [InlineData(1.6f, 18.2f, 17.1f, 0.9f)]
    [InlineData(1.5f, 1.5f, 18.5f, 18.5f)]
    public void A_slanted_line_has_a_cell_all_along_it(float x0, float y0, float x1, float y1)
    {
        // Slanted lines clip the corners of cells, where the voxelizer's samples may have left none of their plastic.
        var toolpath = Print();
        var line = new Toolpath
        {
            Segments = [Bead(x0, y0, x1, y1, 0, FeatureType.SparseInfill)],
            Layers = [new ToolpathLayer(0, Height, Height, 0, 1, 0, 0)],
            Settings = toolpath.Settings,
            UnknownFeatures = toolpath.UnknownFeatures,
            FilamentDiameter = toolpath.FilamentDiameter,
            TotalTime = 0,
            ExtruderOffset = Vector3.Zero,
        };
        var grid = Voxelizer.Voxelize(line, OnBed, new Box3(Vector3.Zero, new Vector3(20 * Width, 20 * Width, Height)));
        var mesh = FemMesh.Build(grid, 0.05f, [new Fixture(p => p.X < 2 * Width)]);

        var pieces = BeadPieces.Cut(line, OnBed, mesh);

        Assert.Equal(0, mesh.DroppedCells);
        Assert.All(pieces.Cells, cell => Assert.True(cell >= 0));
        Assert.True(pieces.Count >= 17, $"{pieces.Count} pieces");
    }

    [Fact]
    public void Whole_beads_are_one_piece_each_in_no_cell()
    {
        var toolpath = Print();

        var pieces = BeadPieces.Whole(toolpath, OnBed);

        Assert.Equal([0, 1, 2, 3], pieces.Segments);
        Assert.All(pieces.Cells, cell => Assert.Equal(-1, cell));
        Assert.All(pieces.Tips, tips => Assert.Equal(BeadPieces.StartTip | BeadPieces.EndTip, tips));
        Near(new Vector3(6.5f * Width, 3.5f * Width, 1.5f * Height), pieces.Ends[7]);
    }

    [Fact]
    public void Pieces_move_with_the_displacement_inside_their_cell_and_stay_joined()
    {
        var (_, mesh, pieces) = Cut();
        // A linear field, which the corners of a cell carry exactly to any point inside it.
        static Vector3 Field(Vector3 p) => new(0.01f * p.X + 0.03f * p.Z, -0.02f * p.X, 0.05f * p.Y);
        var nodes = Enumerable.Range(0, mesh.NodeCount).Select(n => Field(mesh.NodePosition(n))).ToArray();

        var moved = pieces.Displaced(mesh, nodes, 10);

        for (var p = 0; p < pieces.Count; p++)
        for (var end = 2 * p; end < 2 * p + 2; end++)
            if (pieces.Cells[p] >= 0) Near(pieces.Ends[end] + 10 * Field(pieces.Ends[end]), moved[end]);
            else Assert.Equal(pieces.Ends[end], moved[end]);
        foreach (var p in Of(pieces, 3).Skip(1)) Near(moved[2 * p - 1], moved[2 * p]);
    }

    [Fact]
    public void A_cut_bead_comes_to_a_point_only_where_it_ends()
    {
        var (toolpath, mesh, pieces) = Cut();
        var wall = Of(pieces, 0);

        var geometry = ToolpathGeometry.Build(toolpath, pieces);

        Assert.True(geometry.Tubes);
        Assert.Equal(10 * pieces.Count, geometry.Positions.Length);
        // The plastic reaches half a width beyond the nozzle's path at the ends of the bead, as whole.
        var bounds = Box3.Of([.. wall.SelectMany(p => geometry.Positions.Skip(10 * p).Take(10))]);
        Assert.Equal(2 * Width, bounds.Min.X, 4);
        Assert.Equal(7 * Width, bounds.Max.X, 4);
        // Where it is only cut, the tube ends flat: the tip lies at the cut.
        Near(pieces.Ends[2 * wall[2]], geometry.Positions[10 * wall[2] + 8]);
        Near(pieces.Ends[2 * wall[2] + 1], geometry.Positions[10 * wall[2] + 9]);
        Assert.Equal(48, geometry.Indices([wall[2]]).Length);

        // Deformed, each piece keeps its bead's cross-section and its normals.
        var nodes = Enumerable.Range(0, mesh.NodeCount).Select(n => new Vector3(0, 0, 0.1f * mesh.NodePosition(n).X)).ToArray();
        var moved = pieces.Displaced(mesh, nodes, 1);
        var deformed = ToolpathGeometry.Build(toolpath, pieces, tubes: true, ends: moved);
        Assert.Equal(geometry.Normals!, deformed.Normals!);
        for (var m = 0; m < 4; m++)
            Near(geometry.Positions[10 * wall[2] + m] - pieces.Ends[2 * wall[2]], deformed.Positions[10 * wall[2] + m] - moved[2 * wall[2]]);
    }

    [Fact]
    public void Each_piece_takes_its_own_colour()
    {
        var (toolpath, _, pieces) = Cut();
        var colours = Enumerable.Range(0, pieces.Count).Select(p => new Vector4(p / 16f, 0, 0, 1)).ToArray();

        foreach (var tubes in new[] { true, false })
        {
            var geometry = ToolpathGeometry.Build(toolpath, pieces, tubes);
            var vertices = geometry.VertexColours(p => colours[p]);
            for (var v = 0; v < vertices.Length; v++) Assert.Equal(colours[v / geometry.VerticesPerPiece], vertices[v]);
        }
    }

    [Fact]
    public void The_layer_range_and_the_line_types_choose_the_pieces_shown()
    {
        var (toolpath, _, pieces) = Cut();

        Assert.Equal(pieces.Count, pieces.Shown(toolpath, 0, 2, _ => true).Length);
        Assert.Equal(Of(pieces, 3), pieces.Shown(toolpath, 1, 2, _ => true));
        Assert.Equal([.. Of(pieces, 0), .. Of(pieces, 2)], pieces.Shown(toolpath, 0, 1, f => f != FeatureType.Skirt));
    }

    [Fact]
    public void A_click_finds_the_line_under_it()
    {
        var (toolpath, mesh, pieces) = Cut();
        var all = pieces.Shown(toolpath, 0, 2, _ => true);
        var (above, down) = (new Vector3(4.5f * Width, 3.5f * Width, 5), -Vector3.UnitZ);
        CellIndex CellOf(int piece) => mesh.Cells[pieces.Cells[piece]];

        // From above onto cell 4 of the wall: the upper layer first, the lower one with the upper not shown.
        Assert.Equal(new CellIndex(4, 3, 1), CellOf(pieces.Pick(toolpath, pieces.Ends, all, above, down)));
        Assert.Equal(new CellIndex(4, 3, 0), CellOf(pieces.Pick(toolpath, pieces.Ends, pieces.Shown(toolpath, 0, 1, _ => true), above, down)));
        // From the side, level with the lower layer: a bead's height below the upper one, which it passes.
        Assert.Equal(new CellIndex(5, 3, 0), CellOf(pieces.Pick(toolpath, pieces.Ends, all, new Vector3(5.2f * Width, -5, Height / 2), Vector3.UnitY)));
        // More than half a width beside the line, and away from it: nothing.
        Assert.Equal(-1, pieces.Pick(toolpath, pieces.Ends, all, above + new Vector3(0, 0.6f * Width, 0), down));
        Assert.Equal(-1, pieces.Pick(toolpath, pieces.Ends, all, above, Vector3.UnitZ));
    }

    [Fact]
    public void The_highest_value_of_the_lines_shown_is_found()
    {
        var (toolpath, mesh, pieces) = Cut();
        var measure = mesh.Cells.Select(c => c.I + 10f * c.K).ToArray();

        Assert.Equal(new CellIndex(6, 3, 1), mesh.Cells[pieces.Cells[pieces.Highest(measure, pieces.Shown(toolpath, 0, 2, _ => true))]]);
        Assert.Equal(new CellIndex(6, 3, 0), mesh.Cells[pieces.Cells[pieces.Highest(measure, pieces.Shown(toolpath, 0, 1, _ => true))]]);
        Assert.Equal(-1, pieces.Highest(measure, pieces.Shown(toolpath, 0, 1, f => f == FeatureType.Skirt)));
    }
}
