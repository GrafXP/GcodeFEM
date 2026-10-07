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
        "sample" when commandLine.Positional.Count == 2 => SliceCommands.Sample(commandLine),
        "presets" => SliceCommands.Presets(commandLine),
        "slice" when commandLine.Positional.Count == 1 => await SliceCommands.Slice(commandLine),
        "parse" when commandLine.Positional.Count == 1 => SliceCommands.Parse(commandLine.Positional[0]),
        "solve" when commandLine.Positional.Count == 1 || commandLine.Flag("study") => await FemCommands.Solve(commandLine),
        "adapt" when commandLine.Positional.Count == 1 || commandLine.Flag("study") => await AdaptCommands.Adapt(commandLine),
        "study" when commandLine.Positional.Count >= 2 => StudyCommands.Run(commandLine),
        "bench" => commandLine.Flag("adaptive") ? AdaptCommands.Bench(commandLine) : FemCommands.Bench(commandLine),
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
          gcodefem solve <model.stl> [slice options] [load options] [--solver auto|amg|pcg] [--max-iterations n]
          gcodefem adapt <model.stl> [slice options] [load options] [octree options] [--reference] [--sweep "k=2;k=3,buffer=2"]
          gcodefem bench [--scales 2,4,6,8] [--solver both|amg|pcg] [--pcg-max-dofs 300000]
          gcodefem bench --adaptive [--scales 2,4,6,8] [octree options] [--verbose]
          gcodefem solve|adapt --study <study.json> [--case <name>] [...]
          gcodefem study new <study.json> <model.stl> [slice options] [--lay x,y,z] [--level auto|0..5] [--case <name>]
          gcodefem study add <study.json> --name <name> [--kind fixed|sliding|bolthole|force|pressure|bearing] [--at "x,y,z;x,y,z"]
                             [--angle 20] [--free-along] [--case <name>] [--force x,y,z | --normal-force n | --pressure p]
          gcodefem study show <study.json>
          gcodefem sample bracket <out.stl> [--leg 40] [--width 10] [--height 8] [--holes 2 --hole 5]
          gcodefem sample beam <out.stl> [--length 60] [--width 10] [--height 10]

        slice options:  --rot x,y,z (degrees, about X then Y then Z) --filament <name> --process <name> --machine <name>
                        Machine, process and filament default to the current Bambu Studio selection.
        load options:   --fix zmin --force 0,0,-100 --E 2500 --nu 0.35
                        Clamps one outer face of the printed cells (print frame: xmin, xmax, ymin, ymax, zmin = bed side,
                        zmax) and spreads --force (N, print frame) over the opposite face.
        octree options: --level auto|0..5 --k 2 --buffer 1 --energy 0.9 --change 0.03 --passes 10 --max-dofs n --no-interfaces
                        Coarse elements hold (2^level)^3 cells; cells stressed above peak / k end up at bead resolution.

        A study holds the model, how it is printed, its interfaces (the faces where it is held or loaded) and load cases.
        study new --lay turns the model so that its face at that point (model coordinates) lies on the bed.
        study add picks the face at each --at point (model coordinates; the face runs on while neighbouring facets differ
        by under --angle degrees) and adds it to the interface, or sets the interface's value in a load case. Forces are
        in the model's own directions; --normal-force and --pressure push onto the face. A bolt hole is held across
        the hole and along it, with --free-along only across.

        solve works at full bead resolution. adapt solves on an octree that is only fine where the stress is high;
        --reference also runs the full solve and compares, --sweep does that for several option sets (';' between sets).
        bench solves solid cantilevers of 800 x scale^3 bead cells and compares the tip with beam theory;
        with --adaptive it compares the octree with the full solve instead.
        --solver also takes amg-ilu0, amg-ilu0-single, amg-ilu1, amg-gs, amg-spai0 and amg-chebyshev.
        """);
    return 2;
}
