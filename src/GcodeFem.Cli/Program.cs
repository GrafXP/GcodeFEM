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
        "sweep" => await SweepCommands.Sweep(commandLine),
        "results" when commandLine.Positional.Count == 2 => await ResultCommands.Run(commandLine),
        "material" when commandLine.Positional.Count >= 1 => MaterialCommands.Run(commandLine),
        "bars" => await FemCommands.Bars(commandLine),
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
          gcodefem bars [--length 60] [--width 4] [--height 1.6] [--force 20] [slice options] [--material <name>] [--isotropic]
          gcodefem bench [--scales 2,4,6,8] [--solver both|amg|pcg] [--pcg-max-dofs 300000]
          gcodefem bench --adaptive [--scales 2,4,6,8] [octree options] [--verbose]
          gcodefem solve|adapt --study <study.json> [--case <name>] [...]
          gcodefem study new <study.json> <model.stl> [slice options] [--lay x,y,z] [--level auto|0..5] [--case <name>]
          gcodefem study add <study.json> --name <name> [--kind fixed|sliding|bolthole|force|pressure|bearing] [--at "x,y,z;x,y,z"]
                             [--angle 20] [--free-along] [--case <name>] [--force x,y,z | --normal-force n | --pressure p]
          gcodefem study show <study.json>
          gcodefem sweep --study <study.json> [--min-contact mm2] [--count n] [--spins] [--save] [--list] [--best 3] [--screen-level 1] [--check]
          gcodefem results solve <study.json> [--case <name>] [--level auto|0..5] [--out <file.results.zip>]
          gcodefem results show <file.results.zip | study.json> [--top 10] [--by governing|layers|beads|lines|crushed|tsaiwu|vonmises]
                                [--feature "outer wall"] [--box "x,y,z;x,y,z"] [--cell n] [--at x,y,z] [--along "x,y,z;x,y,z" --steps 20]
          gcodefem material list [--machine <name>] [--all]
          gcodefem material show <name>
          gcodefem material import <tds.pdf> [--name <name>] [--type <type>] [--filaments "a;b"] [--stand-in] [--url <where from>]
                                   [--replace] [--dry-run]
          gcodefem sample bracket <out.stl> [--leg 40] [--width 10] [--height 8] [--holes 2 --hole 5]
          gcodefem sample beam <out.stl> [--length 60] [--width 10] [--height 10]

        slice options:  --rot x,y,z (degrees, about X then Y then Z) --filament <name> --process <name> --machine <name>
                        Machine, process and filament default to the current Bambu Studio selection.
        load options:   --fix zmin --force 0,0,-100 [--material <name>] [--isotropic] [--E 2500] [--nu 0.35] [--across-line 2060]
                        Clamps one outer face of the printed cells (print frame: xmin, xmax, ymin, ymax, zmin = bed side,
                        zmax) and spreads --force (N, print frame) over the opposite face.
        octree options: --level auto|0..5 --k 2 --buffer 1 --energy 0.9 --change 0.03 --passes 10 --max-dofs n --no-interfaces
                        --kf 4 --mark failure|vonmises
                        Coarse elements hold (2^level)^3 cells; cells stressed above peak / k, and cells whose safety
                        factor is under kf x the lowest, end up at bead resolution. --mark vonmises leaves out the
                        second, as before M8.

        The stiffness is that of the filament's material: the material file that names the filament, else the one that
        stands in for the filament's type, else a generic one, with a warning for the last two. Each cell has the
        sheet's modulus in the plane of the layers, along its lines and across them, and the sheet's modulus across the
        layers in Z. --material names another material, --isotropic takes the modulus in the plane every way, --E and
        --nu give the numbers directly (--E the same every way), and --across-line sets the modulus across the lines in
        the plane, for cells that are stiffer along their lines than across them. solve and adapt also show how well
        each layer line has bonded, against the sheet's specimens: from nozzle temperature, fan and the time since the
        layer below, with times stretched to the slicer's own. --materials <folder> reads the materials from elsewhere.
        material list shows the materials and which one each filament preset gets; material import reads a technical
        data sheet (Bambu Lab's layout), shows every value with its page and saves it with a copy of the sheet.
        --stand-in makes the material the one used for every filament of its type that has none of its own.

        A study holds the model, how it is printed, its interfaces (the faces where it is held or loaded) and load cases.
        study new --lay turns the model so that its face at that point (model coordinates) lies on the bed.
        study add picks the face at each --at point (model coordinates; the face runs on while neighbouring facets differ
        by under --angle degrees) and adds it to the interface, or sets the interface's value in a load case. Forces are
        in the model's own directions; --normal-force and --pressure push onto the face. A bolt hole is held across
        the hole and along it, with --free-along only across.

        solve and adapt check every cell for failure: in the frame of its lines, the layer line below it pulling apart
        (the sheet's strength across the layers x the bond), beads of a layer pulling apart, lines breaking, crushing;
        with Tsai-Wu as a cross-check. The safety factor is the factor on the loads at which the part starts to break.
        solve works at full bead resolution. adapt solves on an octree that is only fine where the part is nearest
        to breaking; --reference also runs the full solve and compares, --sweep does that for several option sets
        (';' between sets, refined by stress).
        sweep tries the faces the part can be laid on (planes of its convex hull that it touches with at least
        --min-contact mm2, the largest --count of them; --spins also spins each by 45 and 90 deg on the bed). Each is
        sliced and screened in every load case, refined down to elements of 2^--screen-level cells only, the --best are
        solved in full, and all are ranked
        by the lowest safety factor, then the largest displacement, print time, filament and support. Without a smallest
        contact (here or saved with the study by --save) it only lists the faces. --check solves every candidate in
        full and says whether the screening ranked them in the same order.
        A result is saved beside its study as <name>.results.zip, by the app when it saves the study and by results solve,
        which slices and solves one load case as the app does. The zip holds the study as it was solved, the model, the
        material, the G-code and the solution, so the app shows the result again when it opens the study, without
        slicing or solving. results show rebuilds it and lists cells with their stresses in the frame of their lines and
        each mode's safety factor: by default the --top 10 weakest, ranked --by the mode that governs or by one mode,
        of one --feature (line type) or inside a --box (print frame); --cell, --at and --along pick cells directly.
        bars slices a bar lying flat and standing up and pulls each along its length: their stiffnesses should stand
        as the material's along the lines to across the layers, and they break along the lines and across the layers.
        bench solves solid cantilevers of 800 x scale^3 bead cells and compares the tip with beam theory;
        with --adaptive it compares the octree with the full solve instead.
        --solver also takes amg-ilu0, amg-ilu0-single, amg-ilu1, amg-gs, amg-spai0 and amg-chebyshev.
        """);
    return 2;
}
