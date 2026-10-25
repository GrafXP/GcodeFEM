using System.Numerics;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Slicing;

namespace GcodeFem.Tests.Slicing;

public class PlacementTests
{
    [Fact]
    public void Quarter_turns_are_exact()
    {
        var rotation = Orientation.FromEulerDegrees(90, 0, 0);

        Assert.Equal(new Vector3(0, 0, 1), Vector3.Transform(Vector3.UnitY, rotation));
        Assert.Equal(new Vector3(0, -1, 0), Vector3.Transform(Vector3.UnitZ, rotation));
    }

    [Fact]
    public void Maps_bed_coordinates_back_to_the_part()
    {
        var rotation = Orientation.FromEulerDegrees(90, 0, 0);
        var part = MeshFactory.Box(Vector3.Zero, new Vector3(20));
        var print = part.Transformed(rotation).Bounds; // (0,-20,0)..(20,0,20)
        var bed = new Box3(new Vector3(118, 118, 0), new Vector3(138, 138, 20));

        var placement = Placement.FromSlice(rotation, print, bed);

        Assert.Equal(new Vector3(118, 138, 0), placement.BedOffset);
        var corner = placement.BedToPart(new Vector3(118, 118, 0)); // print (0,-20,0) is part (0,0,20)
        Assert.Equal(0, corner.X, 4);
        Assert.Equal(0, corner.Y, 4);
        Assert.Equal(20, corner.Z, 4);
    }

    [Fact]
    public void Rejects_a_slicer_that_turned_the_part()
    {
        var print = new Box3(Vector3.Zero, new Vector3(30, 10, 5));
        var turned = new Box3(new Vector3(100, 100, 0), new Vector3(110, 130, 5));

        Assert.Throws<SlicerException>(() => Placement.FromSlice(Matrix4x4.Identity, print, turned));
    }

    /// <summary>
    /// The turn Bambu Studio gave the part on the plate, from the 3mf it exports. The first matrix is
    /// what its arrange did to a part spun 45° on the bed (31.07° about Z) before it was told not to.
    /// </summary>
    [Theory]
    [InlineData("0.856576323 0.516020351 0 -0.516020351 0.856576323 0 0 0 1 104.36221 118.3071 0", 31.07)]
    [InlineData("1 0 0 0 1 0 0 0 1 103.301 138.62381 0", 0)]
    [InlineData("-1 0 0 0 -1 0 0 0 1 128 128 0", 180)]
    public void The_turn_on_the_plate_is_read_from_the_3mf(string transform, double degrees)
    {
        var path = Path.Combine(Path.GetTempPath(), $"gcodefem-plate-{Guid.NewGuid():N}.3mf");
        try
        {
            using (var archive = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Create))
            using (var writer = new StreamWriter(archive.CreateEntry("3D/3dmodel.model").Open()))
                writer.Write($"""<model><build><item objectid="2" p:UUID="0" transform="{transform}" printable="1"/></build></model>""");

            Assert.Equal(degrees, BambuSlicer.TurnOnPlate(path)!.Value, 1);
        }
        finally
        {
            File.Delete(path);
        }
        Assert.Null(BambuSlicer.TurnOnPlate(path));
    }
}
