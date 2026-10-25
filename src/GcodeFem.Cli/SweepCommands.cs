using System.Diagnostics;
using System.Globalization;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Slicing;
using GcodeFem.Core.Study;

namespace GcodeFem.Cli;

static class SweepCommands
{
    /// <summary>
    /// The orientation sweep of a study: the faces the part can be laid on, each sliced and screened with
    /// the adaptive loop stopped short of bead cells in every load case, the best solved in full, and the ranking. --check solves every
    /// candidate in full and says whether the screening ranked them as the full solves do.
    /// </summary>
    public static async Task<int> Sweep(CommandLine commandLine)
    {
        var path = commandLine.Option("study") ?? throw new FormatException("sweep needs --study <study.json>.");
        var study = StudyFile.Load(path);
        var model = StudyCommands.Model(study);
        if (!study.Fits(model)) throw new InvalidDataException("The model has changed since the study's faces were picked: pick them again first.");

        var settings = study.Sweep with
        {
            MinContact = commandLine.Option("min-contact") is not null ? (float)commandLine.Number("min-contact", 0) : study.Sweep.MinContact,
            Count = commandLine.Option("count") is not null ? (int)commandLine.Number("count", 0) : study.Sweep.Count,
            Spins = study.Sweep.Spins || commandLine.Flag("spins"),
        };
        if (commandLine.Flag("save"))
        {
            study.Sweep = settings;
            StudyFile.Save(study, path);
            Console.WriteLine($"saved        the sweep's settings in {Path.GetFileName(path)}");
        }

        var bambu = BambuInstallation.Locate();
        var selection = BambuSelection.Read(bambu);
        var presets = PresetLibrary.Load(bambu, selection.UserPresetFolder);
        var machine = commandLine.Option("machine") ?? selection.Machine;
        var process = commandLine.Option("process") ?? study.Process ?? selection.Process;
        var filament = commandLine.Option("filament") ?? study.Filament ?? selection.Filament;
        var overhang = OverhangDegrees(presets, process);
        Console.WriteLine($"Bambu Studio {bambu.Version}: {machine}, {process}, {filament}");

        var watch = Stopwatch.StartNew();
        var faces = LayFaces.Find(model);
        var current = Orientation.FromEulerDegrees(study.Rotation.X, study.Rotation.Y, study.Rotation.Z);
        var candidates = OrientationCandidates.Find(model, current, settings, overhang, faces);
        Console.WriteLine($"faces        {faces.Count} the part can lie on (planes of its convex hull it touches with area), found in {watch.Elapsed.TotalSeconds:0.00} s; " +
                          $"smallest contact {(settings.MinContact is { } least ? $"{least:0.#} mm2" : "not set")}, " +
                          $"{(settings.Count is { } count ? $"the largest {count}" : "all of them")}{(settings.Spins ? ", each also spun by 45 and 90 deg" : "")}");
        Console.WriteLine($"  overhangs  faces down at under {overhang:0} deg from the bed, as the process's support threshold");
        Console.WriteLine($"  {"#",3} {"contact mm2",11} {"height mm",9} {"overhang mm2",12} {"spin",5}  {"rotation deg",-22} face's normal (model)");
        foreach (var c in candidates)
            Console.WriteLine($"  {c.Number,3} {c.Face.ContactArea,11:0.0} {c.Height,9:0.0} {c.OverhangArea,12:0.0} {c.Spin,5:0}  {Rotation(c),-22} {Text.Format(c.Face.Normal)}");
        if (!settings.CanStart)
        {
            Console.WriteLine("no sweep     the smallest contact is not set: give --min-contact <mm2> (and --save to keep it with the study)");
            return 0;
        }
        if (commandLine.Flag("list") || candidates.Count == 0) return 0;

        var check = commandLine.Flag("check");
        var options = new SweepOptions(Best: check ? candidates.Count : (int)commandLine.Number("best", 3), ScreenLevel: (int)commandLine.Number("screen-level", 1));
        var slicer = new BambuSlicer(bambu, presets, commandLine.Option("cache") ?? BambuSlicer.DefaultCacheDirectory);
        var sweep = new OrientationSweep(study, candidates, OrientationSweep.Slicing(slicer, model, machine, process, filament),
            MaterialCommands.Library(commandLine), LinearSolvers.ByName(commandLine.Option("solver") ?? "auto"), options);
        Console.WriteLine();
        Console.WriteLine($"sweep        {candidates.Count} candidates x {study.LoadCases.Count} load cases ({string.Join(", ", study.LoadCases.Select(c => $"'{c.Name}'"))}); " +
                          $"screened down to elements of {1 << options.ScreenLevel}^3 cells, the best {options.Best} solved in full");

        watch.Restart();
        var told = new HashSet<SweepEntry>();
        await sweep.RunAsync(entry =>
        {
            // Told once when screened, and again when solved in full; a small part is solved directly when screened.
            var screening = told.Add(entry);
            var what = !screening ? "in full " : ReferenceEquals(entry.Full, entry.Screened) ? "directly" : "screened";
            Console.WriteLine(entry.Outcomes is null
                ? $"  #{entry.Candidate.Number,-2} failed   {entry.Error}"
                : $"  #{entry.Candidate.Number,-2} {what} at {watch.Elapsed.TotalSeconds,6:0.0} s: SF {Cases(entry.Outcomes, o => o.SafetyFactor, "0.000")}; max displacement {Cases(entry.Outcomes, o => o.MaxDisplacement, "0.000")} mm" +
                  (!screening ? "" : $"; {Text.Duration(entry.PrintSeconds)}, {entry.FilamentGrams:0.0} g{(entry.Part!.Slice.FromCache ? "" : $", sliced in {entry.Part.Slice.Elapsed.TotalSeconds:0.0} s")}" +
                                     (entry.Part.Slice.Report.Warning is { Length: > 0 } warning ? $"; the slicer warns: {warning}" : "")));
        });

        Console.WriteLine();
        Console.WriteLine($"ranking      after {watch.Elapsed.TotalSeconds:0.0} s, peak working set {Text.Megabytes(FemCommands.PeakWorkingSet())}; SF and displacement are the worst of the load cases, of the full solve where there was one; those only screened rank after, by their screening");
        Console.WriteLine($"  {"rank",4} {"#",3} {"solved",8} {"SF",6} {"screen SF",9} {"displ. mm",9} {"print",8} {"filament g",10} {"support mm3",11} {"contact mm2",11} {"height mm",9}  rotation deg");
        foreach (var e in OrientationSweep.Ranked(sweep.Entries))
            Console.WriteLine(e.Outcomes is null
                ? $"  {"-",4} {e.Candidate.Number,3} {"failed",8}  {e.Error}"
                : $"  {e.Rank,4} {e.Candidate.Number,3} {(e.Full is null ? "screened" : "in full"),8} {(e.Full is null ? "-" : e.SafetyFactor.ToString("0.00", CultureInfo.InvariantCulture)),6} {e.Screened!.Min(o => o.SafetyFactor),9:0.00} {e.MaxDisplacement,9:0.000} " +
                  $"{Text.Duration(e.PrintSeconds),8} {e.FilamentGrams,10:0.0} {e.SupportVolume,11:0} {e.Candidate.Face.ContactArea,11:0.0} {e.Candidate.Height,9:0.0}  {Rotation(e.Candidate)}");

        if (check) PrintCheck(sweep.Entries);
        return 0;
    }

    /// <summary>Whether the screening put the candidates in the order of their full solves, and how high it read.</summary>
    static void PrintCheck(IReadOnlyList<SweepEntry> entries)
    {
        var both = entries.Where(e => e.Full is not null && e.Screened is not null).ToList();
        var byScreen = both.OrderByDescending(e => e.Screened!.Min(o => o.SafetyFactor)).Select(e => e.Candidate.Number).ToList();
        var byFull = both.OrderByDescending(e => e.Full!.Min(o => o.SafetyFactor)).Select(e => e.Candidate.Number).ToList();
        var ratios = both.Select(e => e.Screened!.Min(o => o.SafetyFactor) / e.Full!.Min(o => o.SafetyFactor)).ToList();
        Console.WriteLine();
        Console.WriteLine($"check        by the screening #{string.Join(", #", byScreen)}; in full #{string.Join(", #", byFull)}: {(byScreen.SequenceEqual(byFull) ? "the same order" : "NOT the same order")}");
        Console.WriteLine($"  best 3     {(byScreen.Take(3).Order().SequenceEqual(byFull.Take(3).Order()) ? "the same three" : "NOT the same three")}; the screen read the safety factor {ratios.Min():0.00} to {ratios.Max():0.00} x the full solve's");
    }

    /// <summary>The process's support threshold, Bambu Studio's default where it gives none.</summary>
    static float OverhangDegrees(PresetLibrary presets, string process) =>
        PresetLibrary.First(presets.Resolve(process), "support_threshold_angle") is { } text
        && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var degrees) && degrees > 0
            ? degrees
            : OrientationCandidates.DefaultOverhangDegrees;

    static string Rotation(OrientationCandidate c) => $"{Tidy(c.Rotation.X)}, {Tidy(c.Rotation.Y)}, {Tidy(c.Rotation.Z)}";

    /// <summary>Two decimals, and never a minus zero.</summary>
    static string Tidy(float degrees) => (MathF.Round(degrees, 2) + 0f).ToString("0.##", CultureInfo.InvariantCulture);

    static string Cases(IReadOnlyList<CaseOutcome> outcomes, Func<CaseOutcome, double> value, string format) =>
        string.Join(" / ", outcomes.Select(o => value(o).ToString(format, CultureInfo.InvariantCulture)));
}
