using System.Globalization;
using System.Numerics;

namespace GcodeFem.Cli;

/// <summary>"command positional... --option value --flag".</summary>
sealed class CommandLine
{
    readonly Dictionary<string, string?> options = new(StringComparer.OrdinalIgnoreCase);

    public string? Command { get; private set; }
    public List<string> Positional { get; } = [];

    public string? Option(string name) => options.GetValueOrDefault(name);
    public bool Flag(string name) => options.ContainsKey(name);

    public double Number(string name, double fallback) =>
        Option(name) is { } text
            ? double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new FormatException($"--{name} expects a number but got '{text}'.")
            : fallback;

    public static CommandLine Parse(string[] args)
    {
        var result = new CommandLine();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--", StringComparison.Ordinal))
            {
                var hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
                result.options[args[i][2..]] = hasValue ? args[++i] : null;
            }
            else if (result.Command is null) result.Command = args[i];
            else result.Positional.Add(args[i]);
        }
        return result;
    }
}

static class Text
{
    public static string Format(Vector3 v) => $"({v.X:0.###}, {v.Y:0.###}, {v.Z:0.###})";

    public static string Duration(double seconds)
    {
        var t = TimeSpan.FromSeconds(seconds);
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:00}m" : $"{t.Minutes}m {t.Seconds:00}s";
    }

    public static string Megabytes(long bytes) => $"{bytes / (1024.0 * 1024):0} MB";

    /// <summary>"x,y,z" → three floats.</summary>
    public static Vector3 Triple(string text, string option)
    {
        var parts = text.Split(',');
        if (parts.Length != 3 || !parts.All(p => float.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out _)))
            throw new FormatException($"--{option} expects three numbers like 0,90,0 but got '{text}'.");
        var v = parts.Select(p => float.Parse(p, CultureInfo.InvariantCulture)).ToArray();
        return new Vector3(v[0], v[1], v[2]);
    }
}
