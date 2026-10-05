using System.Globalization;
using System.Numerics;
using System.Text;

namespace GcodeFem.Core.Gcode;

/// <summary>
/// Reads Bambu Studio G-code into extrusion segments. Understands G0/G1, G2/G3 arcs (split into
/// straight pieces), G4, G90/G91, G92, M82/M83, M104/M109, M106/M107 and Bambu's comment markers
/// (CONFIG_BLOCK, CHANGE_LAYER, Z_HEIGHT, LAYER_HEIGHT, FEATURE, LINE_WIDTH, MACHINE_END_GCODE_START).
/// </summary>
/// <remarks>
/// Only extrusions after the first CHANGE_LAYER and before the end G-code are recorded, so the
/// purge line in the start G-code never counts as part of the print.
/// Segments are in bed coordinates: G-code coordinates plus the machine's extruder_offset (the X1C
/// writes Y 2 mm short of where the part sits on the bed).
/// </remarks>
public static class GcodeParser
{
    public static Toolpath Parse(string path)
    {
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1 << 16);
        return Parse(reader);
    }

    public static Toolpath Parse(TextReader reader)
    {
        var parser = new Parser();
        while (reader.ReadLine() is { } line) parser.Feed(line);
        return parser.Finish();
    }

    sealed class Parser
    {
        /// <summary>Largest allowed gap between an arc and its straight pieces (mm).</summary>
        const double ArcChordTolerance = 0.005;

        readonly List<ExtrusionSegment> segments = new(1 << 16);
        readonly List<ToolpathLayer> layers = [];
        readonly Dictionary<string, string> settings = new(StringComparer.Ordinal);
        readonly HashSet<string> unknownFeatures = new(StringComparer.Ordinal);
        readonly double[] words = new double[26];
        int wordMask;

        bool absolutePositions = true;
        bool relativeExtrusion;
        double x, y, z, e;
        double feedrate; // mm/min
        double time;     // s

        bool inConfig;
        bool afterEndGcode;
        float filamentDiameter = 1.75f;
        double filamentArea = Area(1.75);
        float defaultWidth, defaultHeight;
        Vector3 extruderOffset;

        FeatureType feature = FeatureType.Unknown;
        float width, height;
        ushort nozzleTemperature;
        byte fan;

        int layer = -1;
        int layerFirstSegment;
        double layerStartTime;
        float layerZ = float.NaN, layerHeight = float.NaN;

        bool Recording => layer >= 0 && !afterEndGcode;

        public void Feed(string line)
        {
            var span = line.AsSpan().Trim();
            if (span.IsEmpty) return;
            if (span[0] == ';')
            {
                Comment(span[1..].TrimStart());
                return;
            }
            var semicolon = span.IndexOf(';');
            if (semicolon >= 0) span = span[..semicolon].TrimEnd();
            if (!span.IsEmpty) Command(span);
        }

        void Comment(ReadOnlySpan<char> c)
        {
            if (inConfig)
            {
                if (c.SequenceEqual("CONFIG_BLOCK_END"))
                {
                    inConfig = false;
                    ApplySettings();
                    return;
                }
                var equals = c.IndexOf('=');
                if (equals > 0) settings[c[..equals].TrimEnd().ToString()] = c[(equals + 1)..].Trim().ToString();
                return;
            }

            if (c.StartsWith("FEATURE:"))
            {
                var name = c[8..].Trim();
                feature = FeatureTypes.Parse(name);
                if (feature == FeatureType.Unknown) unknownFeatures.Add(name.ToString());
            }
            else if (c.StartsWith("LINE_WIDTH:")) TryNumber(c[11..], ref width);
            else if (c.StartsWith("LAYER_HEIGHT:"))
            {
                TryNumber(c[13..], ref height);
                if (float.IsNaN(layerHeight)) layerHeight = height;
            }
            else if (c.StartsWith("Z_HEIGHT:")) TryNumber(c[9..], ref layerZ);
            else if (c.SequenceEqual("CHANGE_LAYER")) StartLayer();
            else if (c.SequenceEqual("CONFIG_BLOCK_START")) inConfig = true;
            else if (c.SequenceEqual("MACHINE_END_GCODE_START")) afterEndGcode = true;
        }

        void ApplySettings()
        {
            if (FirstNumber("filament_diameter") is { } diameter and > 0)
            {
                filamentDiameter = diameter;
                filamentArea = Area(diameter);
            }
            defaultWidth = FirstNumber("line_width") ?? 0;
            defaultHeight = FirstNumber("layer_height") ?? 0;

            // "0x2" or "0x2,0x0" (one per extruder): X and Y in mm, first extruder only.
            if (settings.TryGetValue("extruder_offset", out var offset))
            {
                var xy = offset.Split(',')[0].Split('x');
                if (xy.Length == 2 &&
                    float.TryParse(xy[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var ox) &&
                    float.TryParse(xy[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var oy))
                    extruderOffset = new Vector3(ox, oy, 0);
            }
        }

        float? FirstNumber(string key) =>
            settings.TryGetValue(key, out var text) &&
            float.TryParse(text.Split(',')[0].Trim().TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : null;

        void StartLayer()
        {
            CloseLayer();
            layer++;
            layerFirstSegment = segments.Count;
            layerStartTime = time;
            layerZ = float.NaN;
            layerHeight = float.NaN;
        }

        void CloseLayer()
        {
            if (layer < 0) return;
            layers.Add(new ToolpathLayer(layer, layerZ, layerHeight, layerFirstSegment, segments.Count - layerFirstSegment, layerStartTime, time));
        }

        void Command(ReadOnlySpan<char> span)
        {
            var letter = char.ToUpperInvariant(span[0]);
            var i = 1;
            while (i < span.Length && char.IsAsciiDigit(span[i])) i++;
            if (i == 1 || !int.TryParse(span[1..i], NumberStyles.None, CultureInfo.InvariantCulture, out var code)) return;
            if (i < span.Length && span[i] == '.') return; // sub-codes like G29.1 are not motion
            ReadWords(span[i..]);

            switch (letter, code)
            {
                case ('G', 0 or 1): Linear(); break;
                case ('G', 2): Arc(clockwise: true); break;
                case ('G', 3): Arc(clockwise: false); break;
                case ('G', 4): time += Has('P') ? Get('P') / 1000 : Has('S') ? Get('S') : 0; break;
                case ('G', 90): absolutePositions = true; break;
                case ('G', 91): absolutePositions = false; break;
                case ('G', 92): SetPosition(); break;
                case ('M', 82): relativeExtrusion = false; break;
                case ('M', 83): relativeExtrusion = true; break;
                case ('M', 104 or 109) when Has('S'): nozzleTemperature = (ushort)Math.Clamp(Get('S'), 0, ushort.MaxValue); break;
                case ('M', 106) when IsPartFan(): fan = (byte)Math.Clamp(Has('S') ? Get('S') : 255, 0, 255); break;
                case ('M', 107) when IsPartFan(): fan = 0; break;
            }
        }

        /// <summary>Bambu: no P or P1 is the part-cooling fan; P2 is the aux fan, P3 the chamber fan.</summary>
        bool IsPartFan() => !Has('P') || Get('P') is 0 or 1;

        void ReadWords(ReadOnlySpan<char> s)
        {
            wordMask = 0;
            var i = 0;
            while (i < s.Length)
            {
                var c = char.ToUpperInvariant(s[i]);
                if (c is < 'A' or > 'Z')
                {
                    i++;
                    continue;
                }
                var start = ++i;
                while (i < s.Length && (char.IsAsciiDigit(s[i]) || s[i] is '.' or '-' or '+')) i++;
                if (i > start && double.TryParse(s[start..i], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    words[c - 'A'] = value;
                    wordMask |= 1 << (c - 'A');
                }
            }
        }

        bool Has(char c) => (wordMask & (1 << (c - 'A'))) != 0;
        double Get(char c) => words[c - 'A'];

        double Target(char axis, double current) =>
            !Has(axis) ? current : absolutePositions ? Get(axis) : current + Get(axis);

        double TakeExtrusion()
        {
            if (!Has('E')) return 0;
            var delta = relativeExtrusion ? Get('E') : Get('E') - e;
            e += delta;
            return delta;
        }

        void SetPosition()
        {
            if (Has('X')) x = Get('X');
            if (Has('Y')) y = Get('Y');
            if (Has('Z')) z = Get('Z');
            if (Has('E')) e = Get('E');
        }

        void Linear()
        {
            if (Has('F')) feedrate = Get('F');
            MoveTo(Target('X', x), Target('Y', y), Target('Z', z), TakeExtrusion());
        }

        void Arc(bool clockwise)
        {
            if (Has('F')) feedrate = Get('F');
            var cx = x + (Has('I') ? Get('I') : 0);
            var cy = y + (Has('J') ? Get('J') : 0);
            double tx = Target('X', x), ty = Target('Y', y), tz = Target('Z', z);
            var extrusion = TakeExtrusion();
            var radius = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            if (radius < 1e-9)
            {
                MoveTo(tx, ty, tz, extrusion);
                return;
            }

            var a0 = Math.Atan2(y - cy, x - cx);
            var sweep = Math.Atan2(ty - cy, tx - cx) - a0;
            var closed = Math.Abs(tx - x) < 1e-6 && Math.Abs(ty - y) < 1e-6;
            if (closed)
                sweep = (clockwise ? -1 : 1) * 2 * Math.PI * Math.Max(1, Has('P') ? (int)Get('P') : 1);
            else if (clockwise && sweep >= 0) sweep -= 2 * Math.PI;
            else if (!clockwise && sweep <= 0) sweep += 2 * Math.PI;

            var maxStep = Math.Min(Math.PI / 8, 2 * Math.Acos(Math.Max(-1, 1 - ArcChordTolerance / radius)));
            var pieces = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / maxStep));
            var startZ = z;
            for (var k = 1; k <= pieces; k++)
            {
                var t = (double)k / pieces;
                var angle = a0 + sweep * t;
                var last = k == pieces;
                MoveTo(
                    last ? tx : cx + radius * Math.Cos(angle),
                    last ? ty : cy + radius * Math.Sin(angle),
                    startZ + (tz - startZ) * t,
                    extrusion / pieces);
            }
        }

        void MoveTo(double tx, double ty, double tz, double extrusion)
        {
            double dx = tx - x, dy = ty - y, dz = tz - z;
            var planar = Math.Sqrt(dx * dx + dy * dy);
            var distance = Math.Sqrt(planar * planar + dz * dz);
            if (distance == 0) distance = Math.Abs(extrusion); // pure retract / unretract
            var duration = feedrate > 0 ? distance / (feedrate / 60) : 0;

            if (extrusion > 1e-9 && planar > 1e-6 && Recording)
            {
                segments.Add(new ExtrusionSegment(
                    new Vector3((float)x, (float)y, (float)z) + extruderOffset,
                    new Vector3((float)tx, (float)ty, (float)tz) + extruderOffset,
                    width > 0 ? width : defaultWidth,
                    height > 0 ? height : defaultHeight,
                    (float)(extrusion * filamentArea),
                    (float)time,
                    (float)duration,
                    layer,
                    feature,
                    nozzleTemperature,
                    fan));
            }

            time += duration;
            x = tx;
            y = ty;
            z = tz;
        }

        public Toolpath Finish()
        {
            CloseLayer();
            return new Toolpath
            {
                Segments = [.. segments],
                Layers = [.. layers],
                Settings = settings,
                UnknownFeatures = unknownFeatures,
                FilamentDiameter = filamentDiameter,
                ExtruderOffset = extruderOffset,
                TotalTime = time,
            };
        }

        static double Area(double diameter) => Math.PI * diameter * diameter / 4;

        static void TryNumber(ReadOnlySpan<char> text, ref float target)
        {
            if (float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) target = value;
        }
    }
}
