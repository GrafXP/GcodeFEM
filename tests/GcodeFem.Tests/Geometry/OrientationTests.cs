using System.Numerics;
using GcodeFem.Core.Geometry;

namespace GcodeFem.Tests.Geometry;

public class OrientationTests
{
    static Matrix4x4 Rotation(Vector3 angles) => Orientation.FromEulerDegrees(angles.X, angles.Y, angles.Z);

    static void Same(Matrix4x4 expected, Matrix4x4 actual, float tolerance = 1e-5f)
    {
        foreach (var axis in new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ })
            Assert.True(Vector3.Distance(Vector3.TransformNormal(axis, expected), Vector3.TransformNormal(axis, actual)) < tolerance,
                $"{axis} goes to {Vector3.TransformNormal(axis, actual)} instead of {Vector3.TransformNormal(axis, expected)}.");
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(90, 0, 0)]
    [InlineData(0, 0, 90)]
    [InlineData(30, -45, 110)]
    [InlineData(-170, 80, -20)]
    [InlineData(12.5f, -89, 33)]
    [InlineData(40, 90, 0)]   // Y at ±90°: X and Z turn about the same line
    [InlineData(40, -90, 25)]
    [InlineData(180, 0, 180)]
    public void Angles_taken_from_a_rotation_give_that_rotation_again(float x, float y, float z)
    {
        var rotation = Orientation.FromEulerDegrees(x, y, z);

        var angles = Orientation.ToEulerDegrees(rotation);

        Same(rotation, Rotation(angles));
        Assert.InRange(angles.Y, -90, 90);
    }

    [Fact]
    public void Simple_angles_come_back_as_they_went_in()
    {
        foreach (var angles in new[] { new Vector3(90, 0, 0), new(0, 90, 0), new(0, 0, -90), new(30, 20, 10), new(-120, -60, 170) })
            Assert.Equal(angles, Orientation.ToEulerDegrees(Rotation(angles)), (a, b) => Vector3.Distance(a, b) < 1e-4f);
    }

    [Fact]
    public void Each_side_of_a_box_is_laid_down_by_a_quarter_or_half_turn()
    {
        Assert.Equal(new Vector3(0, 0, 0), Orientation.LayFlat(Matrix4x4.Identity, -Vector3.UnitZ)); // it is the bottom already
        Assert.Equal(new Vector3(180, 0, 0), Orientation.LayFlat(Matrix4x4.Identity, Vector3.UnitZ));
        Assert.Equal(new Vector3(0, 90, 0), Orientation.LayFlat(Matrix4x4.Identity, Vector3.UnitX));
        Assert.Equal(new Vector3(0, -90, 0), Orientation.LayFlat(Matrix4x4.Identity, -Vector3.UnitX));
        Assert.Equal(new Vector3(-90, 0, 0), Orientation.LayFlat(Matrix4x4.Identity, Vector3.UnitY));
        Assert.Equal(new Vector3(90, 0, 0), Orientation.LayFlat(Matrix4x4.Identity, -Vector3.UnitY));
    }

    [Fact]
    public void Any_face_ends_up_pointing_straight_down()
    {
        var random = new Random(11);
        for (var n = 0; n < 500; n++)
        {
            var normal = Vector3.Normalize(new Vector3(random.NextSingle() - 0.5f, random.NextSingle() - 0.5f, random.NextSingle() - 0.5f));
            var current = Orientation.FromEulerDegrees(360 * random.NextSingle(), 180 * random.NextSingle() - 90, 360 * random.NextSingle());

            var laid = Rotation(Orientation.LayFlat(current, normal));

            // Within the rounding of the angles: a ten-thousandth of a degree is 1.7e-6.
            Assert.True(Vector3.Distance(-Vector3.UnitZ, Vector3.TransformNormal(normal, laid)) < 3e-6f, $"{normal} ends up at {Vector3.TransformNormal(normal, laid)}.");
        }
    }

    [Fact]
    public void The_part_is_turned_no_more_than_it_takes()
    {
        // The part's bottom is tipped up by 20°, and the part then turned by 30° on the bed.
        // Laying the bottom down again tips it back by those 20° and leaves the 30° alone.
        var heading = Orientation.FromEulerDegrees(0, 0, 30);
        var tipped = Orientation.FromEulerDegrees(20, 0, 0) * heading;

        var laid = Orientation.LayFlat(tipped, -Vector3.UnitZ);

        Assert.Equal(new Vector3(0, 0, 30), laid);
    }

    [Fact]
    public void A_face_that_is_down_already_leaves_the_part_as_it_is()
    {
        var current = Orientation.FromEulerDegrees(0, 0, 47);

        Same(current, Rotation(Orientation.LayFlat(current, -Vector3.UnitZ)));
    }

    [Fact]
    public void The_direction_of_a_flat_face_is_that_of_all_its_triangles()
    {
        var bracket = MeshFactory.LBracket(leg: 40, width: 10, height: 8, holes: 2, holeDiameter: 5);
        var topology = MeshTopology.Of(bracket);

        Assert.Equal(Vector3.UnitZ, FaceRegions.FlatNormal(bracket, topology, MeshPicker.Nearest(bracket, new Vector3(5, 5, 8)).Triangle));
        Assert.Equal(-Vector3.UnitY, FaceRegions.FlatNormal(bracket, topology, MeshPicker.Nearest(bracket, new Vector3(20, 0, 4)).Triangle));
        // On a hole's wall there is no flat face beyond the one piece: it is that piece's own direction.
        var piece = MeshPicker.Nearest(bracket, new Vector3(27.4f, 5.2f, 4)).Triangle;
        Assert.Equal(bracket.Normal(piece), FaceRegions.FlatNormal(bracket, topology, piece), (a, b) => Vector3.Distance(a, b) < 1e-5f);
    }
}
