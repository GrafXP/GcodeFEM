using System.Globalization;
using GcodeFem.Cli;
using GcodeFem.Core.Slicing;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var commandLine = CommandLine.Parse(args);
try
{
    return commandLine.Command switch
    {
        "info" when commandLine.Positional.Count == 1 => SliceCommands.Info(commandLine.Positional[0]),
        "presets" => SliceCommands.Presets(commandLine),
        "slice" when commandLine.Positional.Count == 1 => await SliceCommands.Slice(commandLine),
        "parse" when commandLine.Positional.Count == 1 => SliceCommands.Parse(commandLine.Positional[0]),
        "solve" when commandLine.Positional.Count == 1 => await FemCommands.Solve(commandLine),
        "bench" => FemCommands.Bench(commandLine),
        _ => Usage(),
    };
}
catch (Exception ex) when (ex is IOException or InvalidDataException or SlicerException or KeyNotFoundException
                               or FormatException or ArgumentException or InvalidOperationException or DllNotFoundException)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    return 1;
}

static int Usage()
{
    Console.Error.WriteLine("""
        usage:
          gcodefem info <model.stl>
          gcodefem presets [--machine <name>] [--all]
          gcodefem slice <model.stl> [slice options] [--no-cache]
          gcodefem parse <file.gcode>
          gcodefem solve <model.stl> [slice options] [--fix zmin] [--force 0,0,-100] [--solver auto|amg|pcg] [--E 2500] [--nu 0.35]
          gcodefem bench [--scales 2,4,6,8] [--solver both|amg|pcg] [--pcg-max-dofs 300000]

        slice options: --rot x,y,z (degrees, about X then Y then Z) --filament <name> --process <name> --machine <name>
        Machine, process and filament default to the current Bambu Studio selection.
        solve clamps one face of the model's bounding box (print frame: xmin, xmax, ymin, ymax, zmin = bed side, zmax)
        and spreads --force (N, print frame) over the opposite face.
        bench solves solid cantilevers of 800 x scale^3 bead cells and compares the tip with beam theory.
        """);
    return 2;
}
