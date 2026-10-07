using System.Globalization;
using System.Numerics;

namespace GcodeFem.Core.Visual;

/// <summary>
/// The viewer's colours as sRGB red, green, blue, alpha in 0..1 (the layout of a vertex colour).
/// All of them are steps of one palette picked for the dark viewport: eight categorical hues in a
/// fixed order, one blue ramp for magnitudes and a blue–grey–red pair for signed values.
/// Lightness carries the magnitude, so a field stays readable without colour vision; a rainbow would not.
/// </summary>
public static class Palette
{
    public static readonly Vector4 Surface = Hex("#1a1a19");
    public static readonly Vector4 Ink = Hex("#ffffff");
    public static readonly Vector4 SecondaryInk = Hex("#c3c2b7");
    public static readonly Vector4 MutedInk = Hex("#898781");
    public static readonly Vector4 Gridline = Hex("#2c2c2a");

    /// <summary>
    /// Identity colours, assigned in this order and never cycled. Neighbours in the list are told
    /// apart with any colour vision; the full eight side by side are not (orange and red, magenta
    /// and aqua), so whatever uses them also needs a legend that can isolate one entry.
    /// </summary>
    public static readonly Vector4[] Categorical =
    [
        Hex("#3987e5"), Hex("#d95926"), Hex("#199e70"), Hex("#c98500"),
        Hex("#d55181"), Hex("#008300"), Hex("#9085e9"), Hex("#e66767"),
    ];

    /// <summary>For what carries no identity of its own, such as lines that are not part of the model.</summary>
    public static readonly Vector4 Neutral = Hex("#898781");

    /// <summary>Light to dark; on the dark viewport the light end is the high end.</summary>
    static readonly Vector4[] Blues =
    [
        Hex("#cde2fb"), Hex("#b7d3f6"), Hex("#9ec5f4"), Hex("#86b6ef"), Hex("#6da7ec"), Hex("#5598e7"),
        Hex("#3987e5"), Hex("#2a78d6"), Hex("#256abf"), Hex("#1c5cab"), Hex("#184f95"),
    ];

    /// <summary>Low values are dark and recede, high values are light; the dark end still stands clear of the background.</summary>
    public static readonly Vector4[] Sequential = Ramp([.. Blues.Reverse()]);

    /// <summary>
    /// Blue for negative, red for positive, and a grey with no hue where the value is zero. The grey
    /// is lighter than a chart's would be on this background: a part that is mostly near zero must
    /// still stand out as a solid.
    /// </summary>
    public static readonly Vector4[] Diverging = Ramp([Hex("#5598e7"), Hex("#5a5a57"), Hex("#e66767")]);

    /// <summary>
    /// Up to six ordered steps of the blue ramp, lightest first and spread over all of it, for a
    /// quantity with a few ordered values. More steps than six would be too close to tell apart.
    /// </summary>
    public static Vector4[] Ordinal(int count)
    {
        if (count is < 1 or > 6) throw new ArgumentOutOfRangeException(nameof(count), "One to six steps.");
        var last = Sequential.Length - 1;
        return [.. Enumerable.Range(0, count).Select(step => Sequential[count == 1 ? last : last - last * step / (count - 1)])];
    }

    public static Vector4 Hex(string hex)
    {
        var value = uint.Parse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new Vector4(((value >> 16) & 255) / 255f, ((value >> 8) & 255) / 255f, (value & 255) / 255f, 1);
    }

    /// <summary>256 colours through evenly spaced stops, interpolated in OKLab so equal steps look equal.</summary>
    static Vector4[] Ramp(Vector4[] stops)
    {
        var lab = stops.Select(ToOklab).ToArray();
        var ramp = new Vector4[256];
        for (var n = 0; n < ramp.Length; n++)
        {
            var at = n / (ramp.Length - 1f) * (stops.Length - 1);
            var stop = Math.Min((int)at, stops.Length - 2);
            ramp[n] = FromOklab(Vector3.Lerp(lab[stop], lab[stop + 1], at - stop));
        }
        return ramp;
    }

    /// <summary>Perceived lightness, 0 (black) to 1 (white).</summary>
    public static float Lightness(Vector4 colour) => ToOklab(colour).X;

    static Vector3 ToOklab(Vector4 c)
    {
        float r = ToLinear(c.X), g = ToLinear(c.Y), b = ToLinear(c.Z);
        var l = MathF.Cbrt(0.4122214708f * r + 0.5363325363f * g + 0.0514459929f * b);
        var m = MathF.Cbrt(0.2119034982f * r + 0.6806995451f * g + 0.1073969566f * b);
        var s = MathF.Cbrt(0.0883024619f * r + 0.2817188376f * g + 0.6299787005f * b);
        return new Vector3(
            0.2104542553f * l + 0.7936177850f * m - 0.0040720468f * s,
            1.9779984951f * l - 2.4285922050f * m + 0.4505937099f * s,
            0.0259040371f * l + 0.7827717662f * m - 0.8086757660f * s);
    }

    static Vector4 FromOklab(Vector3 lab)
    {
        var l = lab.X + 0.3963377774f * lab.Y + 0.2158037573f * lab.Z;
        var m = lab.X - 0.1055613458f * lab.Y - 0.0638541728f * lab.Z;
        var s = lab.X - 0.0894841775f * lab.Y - 1.2914855480f * lab.Z;
        (l, m, s) = (l * l * l, m * m * m, s * s * s);
        return new Vector4(
            FromLinear(4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s),
            FromLinear(-1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s),
            FromLinear(-0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s),
            1);
    }

    static float ToLinear(float v) => v <= 0.04045f ? v / 12.92f : MathF.Pow((v + 0.055f) / 1.055f, 2.4f);

    static float FromLinear(float v) =>
        Math.Clamp(v <= 0.0031308f ? 12.92f * v : 1.055f * MathF.Pow(v, 1 / 2.4f) - 0.055f, 0, 1);
}

/// <summary>Maps a range of values onto a colour ramp; values outside the range take the end colours.</summary>
public sealed class ColourScale
{
    ColourScale(float min, float max, Vector4[] ramp, bool diverging)
    {
        (Min, Max, Ramp, IsDiverging) = (min, max, ramp, diverging);
    }

    public float Min { get; }
    public float Max { get; }
    public bool IsDiverging { get; }

    /// <summary>The colours from <see cref="Min"/> to <see cref="Max"/>, evenly spaced.</summary>
    public IReadOnlyList<Vector4> Ramp { get; }

    /// <summary>For a magnitude: dark at <paramref name="min"/>, light at <paramref name="max"/>.</summary>
    public static ColourScale Sequential(float min, float max) => new(min, max, Palette.Sequential, false);

    /// <summary>For a signed value: grey at zero, blue down to −<paramref name="extent"/>, red up to +<paramref name="extent"/>.</summary>
    public static ColourScale Diverging(float extent) => new(-extent, extent, Palette.Diverging, true);

    public Vector4 Colour(float value)
    {
        var t = Max > Min ? (value - Min) / (Max - Min) : 0.5f;
        if (!(t > 0)) t = 0; // also catches NaN
        return Ramp[(int)(MathF.Min(t, 1) * (Ramp.Count - 1) + 0.5f)];
    }
}

/// <param name="Key">What the entry stands for, for whoever built the legend (a feature group, an octree level).</param>
public sealed record LegendEntry(int Key, string Label, Vector4 Colour);

/// <summary>What the colours in a view mean: named entries, or a scale with a unit.</summary>
public sealed record Legend(string Title, string Unit, IReadOnlyList<LegendEntry> Entries, ColourScale? Scale = null);
