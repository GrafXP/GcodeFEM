using System.Globalization;
using System.Numerics;

namespace GcodeFem.App;

/// <summary>
/// The app's command line: "model.stl --option value --flag". It can slice, solve, pick a view and
/// save a screenshot without anyone touching the window, which is how the views are checked:
///
///   GcodeFem.App model.stl [--rot x,y,z] [--process name] [--filament name]
///                [--view model|toolpaths|cells|results]
///                [--fix xmin|xmax|ymin|ymax|zmin|zmax] [--force x,y,z] [--level auto|0..5]
///                [--colour feature|nozzletemperature|fan|speed|time|width|vonmises|displacement|interlayerstress|elementsize]
///                [--layers first-last] [--hide "sparse infill,top surface"] [--thin] [--pass n] [--scale-top percent]
///                [--deform 0..1] [--no-elements] [--look x,y,z] [--zoom factor]
///                [--screenshot file.png] [--size 1400x900] [--wait seconds]
///
/// With --screenshot the app saves the picture and exits; its exit code is 1 if a step failed.
/// </summary>
sealed class StartupOptions
{
    readonly Dictionary<string, string?> options = new(StringComparer.OrdinalIgnoreCase);

    public string? Model { get; private set; }
    public string? Screenshot => Text("screenshot");

    public bool Has(string name) => options.ContainsKey(name);
    public string? Text(string name) => options.GetValueOrDefault(name);

    public double? Number(string name) =>
        Text(name) is { } text
            ? double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : throw new FormatException($"--{name} expects a number but got '{text}'.")
            : null;

    public Vector3? Triple(string name) => Text(name) is { } text ? MainViewModel.ParseTriple(text) : null;

    /// <summary>The option as a value of <typeparamref name="T"/>, or null if it is absent or names something else.</summary>
    public T? Enum<T>(string name) where T : struct, Enum =>
        Text(name) is { } text && System.Enum.TryParse<T>(text, ignoreCase: true, out var value) && System.Enum.IsDefined(value) ? value : null;

    /// <summary>"1400x900" → (1400, 900).</summary>
    public (double Width, double Height)? Size(string name) =>
        Text(name)?.Split('x') is [var width, var height]
            ? (double.Parse(width, CultureInfo.InvariantCulture), double.Parse(height, CultureInfo.InvariantCulture))
            : null;

    public static StartupOptions Parse(IEnumerable<string> arguments)
    {
        var result = new StartupOptions();
        var args = arguments.ToArray();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--", StringComparison.Ordinal))
            {
                var hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
                result.options[args[i][2..]] = hasValue ? args[++i] : null;
            }
            else result.Model ??= args[i];
        }
        return result;
    }
}
