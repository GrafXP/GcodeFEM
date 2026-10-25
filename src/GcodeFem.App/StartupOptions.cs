using System.Globalization;
using System.Numerics;

namespace GcodeFem.App;

/// <summary>
/// The app's command line: "model.stl --option value --flag". It can slice, solve, pick a view and
/// save a screenshot without anyone touching the window, which is how the views are checked:
///
///   GcodeFem.App model.stl | --study file.study.json
///                [--rot x,y,z] [--process name] [--filament name]
///                [--view model|toolpaths|cells|results|compare] [--stage setup|loads|preview|results|compare] [--level auto|0..5]
///                [--min-contact mm2] [--count n] [--spins] [--untick "3,4"] [--sweep] [--row n] [--open n] [--compare]
///                [--saved] [--case name] [--fix xmin|xmax|ymin|ymax|zmin|zmax] [--force x,y,z]
///                [--add fixed|sliding|bolthole|force|pressure|bearing] [--select name] [--name new-name] [--value "0,0,-20"]
///                [--lay "x,y"] [--pick-angle degrees] [--click "x,y;x,y"] [--save-study file.study.json]
///                [--colour feature|nozzletemperature|fan|speed|time|width|fill|direction|bond|gap|interfaces
///                          |safetyfactor|failuremode|vonmises|displacement|interlayerstress|elementsize]
///                [--on cells|lines] [--layers first-last] [--hide "sparse infill,top surface"] [--thin] [--pass n] [--scale-top percent]
///                [--deform 0..1] [--no-elements] [--look x,y,z] [--zoom factor] [--probe "x,y"]
///                [--materials-window [name]] [--import-tds sheet.pdf]
///                [--screenshot file.png] [--size 1400x900] [--wait seconds] [--busy-screenshot file.png] [--busy-after seconds]
///
/// --fix and --force set up a quick load case: the model's face on that side of the print is fixed and the opposite
/// one carries the force (N, in the print's directions). --add makes a new interface and --select picks an existing
/// one; --name, --value and --click then apply to it. --value is what would be typed into the value field: a force is
/// in the print's directions as the part is turned by --rot. --click picks faces in the model view as the mouse
/// would, at pixels read off a screenshot of the window; --lay lays the face at such a pixel on the bed first.
/// --probe clicks on the result at such a pixel, which shows that cell's numbers. --on lines draws the result on the
/// print lines instead of the cells; --hide and --thin then apply to it as to the toolpath view.
/// --view compare shows the faces the part can lie on, as --min-contact, --count and --spins choose them; --untick
/// leaves out rows by number and --click there ticks copies off or on; --sweep runs the sweep, --row selects a row
/// and --open opens its result in the results view, to which the other options of a view then apply. --compare
/// ends on the compare view, where a solve by --view results has joined the comparison.
/// --stage shows that step's panel at the end, as a click on it in the top bar would; with --view model, loads is the
/// panel of the mounts and loads.
/// --saved shows the result saved beside the study (part.results.zip, as the app saves it with the study) instead of
/// slicing and solving; the options of the results view then apply to it.
/// --materials-window opens the materials window, on the material of that name if one is given, and --import-tds
/// reads a data sheet into its form (nothing is saved); the screenshot is then of that window.
/// With --screenshot the app saves the picture and exits; its exit code is 1 if a step failed.
/// --busy-screenshot saves one more while the solve runs, --busy-after seconds into it (2 by default).
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
