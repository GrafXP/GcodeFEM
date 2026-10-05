using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace GcodeFem.Core.Geometry;

/// <summary>STL reading (ASCII and binary) and binary writing.</summary>
public static class Stl
{
    const int HeaderSize = 80;
    const int TriangleRecordSize = 50; // normal + 3 vertices (12 floats) + 2-byte attribute

    public static TriangleMesh Read(string path) => Read(File.ReadAllBytes(path));

    public static TriangleMesh Read(byte[] data) =>
        IsBinary(data) ? ReadBinary(data) : ReadAscii(Encoding.ASCII.GetString(data));

    /// <summary>
    /// Binary STLs may also start with "solid", so the size check decides: a binary file is
    /// exactly header + count + 50 bytes per triangle.
    /// </summary>
    static bool IsBinary(byte[] data) =>
        data.Length >= HeaderSize + 4 &&
        HeaderSize + 4L + TriangleRecordSize * (long)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(HeaderSize)) == data.Length;

    static TriangleMesh ReadBinary(byte[] data)
    {
        var count = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(HeaderSize));
        var welder = new Welder(count);
        var offset = HeaderSize + 4;
        for (int t = 0; t < count; t++, offset += TriangleRecordSize)
        {
            var record = data.AsSpan(offset + 12); // skip the stored normal, it is recomputed from the winding
            welder.AddTriangle(ReadVector(record), ReadVector(record[12..]), ReadVector(record[24..]));
        }
        return welder.ToMesh();

        static Vector3 ReadVector(ReadOnlySpan<byte> s) => new(
            BinaryPrimitives.ReadSingleLittleEndian(s),
            BinaryPrimitives.ReadSingleLittleEndian(s[4..]),
            BinaryPrimitives.ReadSingleLittleEndian(s[8..]));
    }

    static TriangleMesh ReadAscii(string text)
    {
        var welder = new Welder(text.Length / 250);
        Span<Vector3> corners = stackalloc Vector3[3];
        Span<Range> parts = stackalloc Range[5];
        var corner = 0;
        foreach (var rawLine in text.AsSpan().EnumerateLines())
        {
            var line = rawLine.Trim();
            if (!line.StartsWith("vertex", StringComparison.OrdinalIgnoreCase)) continue;

            var rest = line["vertex".Length..].Trim();
            if (rest.Split(parts, ' ', StringSplitOptions.RemoveEmptyEntries) != 3)
                throw new InvalidDataException($"Malformed STL vertex line: '{line}'.");

            corners[corner++] = new Vector3(
                float.Parse(rest[parts[0]], CultureInfo.InvariantCulture),
                float.Parse(rest[parts[1]], CultureInfo.InvariantCulture),
                float.Parse(rest[parts[2]], CultureInfo.InvariantCulture));
            if (corner == 3)
            {
                welder.AddTriangle(corners[0], corners[1], corners[2]);
                corner = 0;
            }
        }
        if (corner != 0) throw new InvalidDataException("STL ends in the middle of a triangle.");
        return welder.ToMesh();
    }

    public static void WriteBinary(TriangleMesh mesh, string path)
    {
        var data = new byte[HeaderSize + 4 + TriangleRecordSize * mesh.TriangleCount];
        Encoding.ASCII.GetBytes("GcodeFem binary STL").CopyTo(data, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(HeaderSize), (uint)mesh.TriangleCount);

        var offset = HeaderSize + 4;
        for (int t = 0; t < mesh.TriangleCount; t++, offset += TriangleRecordSize)
        {
            var (a, b, c) = mesh.Triangle(t);
            var record = data.AsSpan(offset);
            WriteVector(record, mesh.Normal(t));
            WriteVector(record[12..], a);
            WriteVector(record[24..], b);
            WriteVector(record[36..], c);
        }
        File.WriteAllBytes(path, data);

        static void WriteVector(Span<byte> s, Vector3 v)
        {
            BinaryPrimitives.WriteSingleLittleEndian(s, v.X);
            BinaryPrimitives.WriteSingleLittleEndian(s[4..], v.Y);
            BinaryPrimitives.WriteSingleLittleEndian(s[8..], v.Z);
        }
    }

    /// <summary>Merges bit-identical corners and drops triangles that collapse to a line.</summary>
    sealed class Welder(int triangleCapacity)
    {
        readonly Dictionary<Vector3, int> lookup = new(triangleCapacity / 2);
        readonly List<Vector3> positions = new(triangleCapacity / 2);
        readonly List<int> indices = new(triangleCapacity * 3);

        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c)
        {
            int ia = IndexOf(a), ib = IndexOf(b), ic = IndexOf(c);
            if (ia == ib || ib == ic || ia == ic) return;
            indices.Add(ia);
            indices.Add(ib);
            indices.Add(ic);
        }

        int IndexOf(Vector3 v)
        {
            if (!lookup.TryGetValue(v, out var index))
            {
                index = positions.Count;
                lookup.Add(v, index);
                positions.Add(v);
            }
            return index;
        }

        public TriangleMesh ToMesh()
        {
            if (indices.Count == 0) throw new InvalidDataException("STL contains no triangles.");
            return new TriangleMesh([.. positions], [.. indices]);
        }
    }
}
