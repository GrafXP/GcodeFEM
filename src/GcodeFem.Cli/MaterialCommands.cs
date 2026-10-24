using GcodeFem.Core.Fem;
using GcodeFem.Core.Gcode;
using GcodeFem.Core.Materials;
using GcodeFem.Core.Slicing;

namespace GcodeFem.Cli;

/// <summary>The material library from the command line: what is in it, which filament gets which material, and new materials from data sheets.</summary>
static class MaterialCommands
{
    public static int Run(CommandLine commandLine) => commandLine.Positional[0] switch
    {
        "list" when commandLine.Positional.Count == 1 => List(commandLine),
        "show" when commandLine.Positional.Count == 2 => Show(commandLine),
        "import" when commandLine.Positional.Count == 2 => Import(commandLine),
        var action => throw new ArgumentException($"Unknown material command '{action}' (use list, show <name> or import <tds.pdf>)."),
    };

    public static MaterialLibrary Library(CommandLine commandLine)
    {
        var library = MaterialLibrary.Load(commandLine.Option("materials"));
        foreach (var problem in library.Problems) Console.Error.WriteLine($"warning: {problem}");
        return library;
    }

    /// <summary>The material a part sliced into <paramref name="toolpath"/> is solved with: the one named by --material, else the filament's.</summary>
    public static MaterialMatch For(CommandLine commandLine, Toolpath toolpath)
    {
        var library = Library(commandLine);
        var match = library.Match(toolpath);
        if (commandLine.Option("material") is not { } name) return match;
        var named = library.Find(name) ?? throw new KeyNotFoundException($"There is no material '{name}' in {library.Directory} (gcodefem material list shows what there is).");
        return match with { Material = named, Kind = MatchKind.ByName };
    }

    /// <summary>The material and the stiffness that is solved with, and loudly if the material is not the filament's own.</summary>
    /// <param name="changes">What the command line changed of the material's stiffness, in words; empty if nothing.</param>
    public static void Print(MaterialMatch match, OrthotropicMaterial stiffness, IReadOnlyList<string> changes)
    {
        var material = match.Material;
        Console.WriteLine($"material     {Text.Ascii(match.Kind == MatchKind.ByName ? match.Describe() : material.Name)}");
        if (match.IsStandIn) Console.WriteLine($"  WARNING    {Text.Ascii(match.Describe())}");
        var across = material.Number(MaterialProperty.YoungsModulusZ) is { } z ? $", {z:0} MPa across the layers" : "";
        var given = material.Number(MaterialProperty.YoungsModulusXY) is { } xy ? $"E {xy:0} MPa in the plane of the layers{across}; " : "";
        var poisson = material.Of(MaterialProperty.PoissonRatio) is { IsAssumed: true } && !changes.Any(c => c.Contains("nu")) ? " (assumed)" : "";
        var solved = stiffness.IsIsotropic ? $"E {stiffness.AlongLine:0} MPa in every direction"
            : stiffness.IsInPlaneIsotropic ? $"E {stiffness.AlongLine:0} MPa in the plane of the layers, {stiffness.AcrossLayers:0} across them"
            : $"E {stiffness.AlongLine:0} MPa along the lines, {stiffness.AcrossLine:0} across them in the plane, {stiffness.AcrossLayers:0} across the layers";
        Console.WriteLine($"  stiffness  {given}solved with {solved}, nu {stiffness.PoissonRatio:0.###}{poisson}{(changes.Count > 0 ? $"; {string.Join(", ", changes)}" : "")}");
        if (material.Strength(out var lacking) is not { } strength)
        {
            Console.WriteLine($"  strength   none: {Text.Ascii(lacking!)}");
            return;
        }
        Console.WriteLine($"  strength   {strength.AlongLine:0.#} MPa along the lines, {strength.AcrossLine:0.#} across them, {strength.AcrossLayers:0.#} across the layers x the bond, " +
                          $"{strength.Compression:0.#} in compression; shear {strength.InPlaneShear:0.#} in the layer, {strength.LayerShear:0.#} across it x the bond");
        foreach (var assumption in strength.Assumed) Console.WriteLine($"  assumed    {Text.Ascii(assumption)}");
    }

    static int List(CommandLine commandLine)
    {
        var library = Library(commandLine);
        Console.WriteLine($"materials in {library.Directory}: {library.Materials.Count}");
        Console.WriteLine($"  {"name",-28} {"type",-9} {"E xy / z, MPa",15} {"strength xy / z, MPa",21} {"density",8}  from");
        foreach (var material in library.Materials)
        {
            var notes = new List<string>();
            if (material.Documents.FirstOrDefault() is { } document) notes.Add(Text.Ascii(document.Title));
            if (material.StandsInForType) notes.Add($"stands in for {material.Type}");
            if (material.Lacking().Count > 0) notes.Add("CANNOT BE SOLVED WITH: no " + string.Join(", no ", material.Lacking().Select(p => p.Label())));
            Console.WriteLine($"  {material.Name,-28} {material.Type,-9} {Pair(material, MaterialProperty.YoungsModulusXY, MaterialProperty.YoungsModulusZ, "0.#"),15} " +
                              $"{Pair(material, MaterialProperty.TensileStrengthXY, MaterialProperty.TensileStrengthZ, "0.#"),21} {material.Number(MaterialProperty.Density),8:0.00}  {string.Join("; ", notes)}");
        }

        if (BambuInstallation.TryLocate() is not { } bambu) return 0;
        var selection = BambuSelection.Read(bambu);
        var presets = PresetLibrary.Load(bambu, selection.UserPresetFolder);
        var machine = commandLine.Option("machine") ?? selection.Machine;
        var filaments = presets.CompatibleWith(machine, PresetKind.Filament).Select(p => presets.SummarizeFilament(p.Name))
            .OrderBy(f => !f.IsUser).ThenBy(f => f.Type).ThenBy(f => f.Name).ToList();
        Console.WriteLine();
        Console.WriteLine($"filaments for {machine}: {filaments.Count} ({filaments.Count(f => f.IsUser)} yours)");
        foreach (var filament in filaments.Where(f => f.IsUser || commandLine.Flag("all")))
        {
            var match = library.Match(filament.Name, filament.Type);
            var how = match.Kind switch
            {
                MatchKind.ByName => match.Material.Name,
                MatchKind.ByType => $"{match.Material.Name}, standing in for {filament.Type}",
                _ => "no data: generic stiffness",
            };
            Console.WriteLine($"  {(filament.IsUser ? "user  " : "system")}  {filament.Name,-44} {filament.Type,-9} -> {how}");
        }
        if (!commandLine.Flag("all")) Console.WriteLine("  (--all lists system presets too)");
        return 0;
    }

    static int Show(CommandLine commandLine)
    {
        var library = Library(commandLine);
        var name = commandLine.Positional[1];
        var material = library.Find(name) ?? throw new KeyNotFoundException($"There is no material '{name}' in {library.Directory}.");
        Print(material, library);
        return 0;
    }

    /// <summary>
    /// Reads a data sheet, shows what it gave and saves it as a material, with a copy of the sheet
    /// in the library's tds folder. The file is the form: it is there to be looked over and corrected.
    /// </summary>
    static int Import(CommandLine commandLine)
    {
        var pdf = Path.GetFullPath(commandLine.Positional[1]);
        var library = Library(commandLine);
        var import = TdsImporter.Import(pdf);
        var material = import.Material;
        if (import.Template is null)
            throw new InvalidDataException($"No template knows the layout of '{Path.GetFileName(pdf)}' (Bambu Lab's is the only one so far). " +
                                           "Enter its values in the app, under Materials, with the sheet alongside.");

        if (commandLine.Option("name") is { } name) (material.Name, material.Filaments[0]) = (name, name);
        if (commandLine.Option("filaments") is { } filaments)
        {
            material.Filaments.Clear();
            material.Filaments.AddRange(filaments.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        }
        var checks = BambuInstallation.TryLocate() is { } bambu
            ? BambuTds.AlignWithStudio(material, PresetLibrary.Load(bambu, userPresetFolder: null), takeType: commandLine.Option("type") is null)
            : [];
        if (commandLine.Option("type") is { } type) material.Type = type;
        material.StandsInForType = commandLine.Flag("stand-in");

        var sheet = material.Documents[0];
        if (commandLine.Flag("dry-run")) material.Documents[0] = sheet with { File = pdf, Url = commandLine.Option("url") };
        else
        {
            if (library.Find(material.Name) is { Path: { } existing })
            {
                if (!commandLine.Flag("replace")) throw new InvalidOperationException($"There is a material '{material.Name}' already ({existing}). --replace overwrites it.");
                material.Path = existing;
            }
            // The copy is named after the material and the sheet's version: vendors' own file names say neither reliably.
            var copy = $"{MaterialLibrary.Slug(material.Name)}-tds{(import.Version.Length > 0 ? "-" + import.Version.ToLowerInvariant() : "")}.pdf";
            material.Documents[0] = library.Attach(pdf, sheet.Id, sheet.Title, commandLine.Option("url"), copy);
            library.Save(material, replace: commandLine.Flag("replace"));
        }

        Console.WriteLine($"read         {Path.GetFileName(pdf)} as a {import.Template} data sheet{(import.Version.Length > 0 ? $", {import.Version}" : "")}");
        Print(material, library);
        foreach (var check in checks) Console.WriteLine($"  check      {Text.Ascii(check)}");
        if (import.Missing.Count > 0)
            Console.WriteLine($"  NOT FOUND  {string.Join("; ", import.Missing.Select(p => p.Label()))}");
        foreach (var remark in import.Remarks) Console.WriteLine($"  NOTE       {Text.Ascii(remark)}");
        if (material.Lacking().Count > 0)
            Console.WriteLine($"  WARNING    it cannot be solved with as it is: no {string.Join(", no ", material.Lacking().Select(p => p.Label()))}");
        Console.WriteLine(commandLine.Flag("dry-run") ? "nothing was saved (--dry-run)" : $"saved        {material.Path}");
        return 0;
    }

    static void Print(Material material, MaterialLibrary library)
    {
        Console.WriteLine($"{material.Name} ({string.Join(", ", new[] { material.Vendor, material.Type }.Where(part => part.Length > 0))})");
        if (material.Path is { } path) Console.WriteLine($"  file       {path}");
        var standIn = material.StandsInForType ? $"; stands in for every {material.Type} filament without data of its own" : "";
        Console.WriteLine($"  for        {(material.Filaments.Count > 0 ? string.Join(", ", material.Filaments) : "no filament by name")}{standIn}");
        foreach (var document in material.Documents)
        {
            Console.WriteLine($"  {document.Id,-10} {Text.Ascii(document.Title)}");
            if (document.File is not null) Console.WriteLine($"             {document.File}{(document.Sha256 is { } hash ? $", sha256 {hash[..16]}..." : "")}");
            if (document.Url is not null) Console.WriteLine($"             {document.Url}");
        }
        if (material.SpecimenNote.Length > 0) Console.WriteLine($"  specimens  {Text.Ascii(material.SpecimenNote)}");

        Console.WriteLine();
        Console.WriteLine($"  {"property",-40} {"value",8} {"+-",6}  {"unit",-6} {"method",-20} source");
        foreach (var (property, value) in material.Values)
        {
            var source = value.IsAssumed ? $"ASSUMED: {value.Assumed}" : $"{value.Source}{(value.Page is { } page ? $", page {page}" : "")}";
            Console.WriteLine($"  {property.Label(),-40} {value.Value,8:0.###} {value.Spread,6:0.###}  {Text.Ascii(property.Unit()),-6} {Text.Ascii(value.Method ?? ""),-20} {Text.Ascii(source)}");
        }
    }

    static string Pair(Material material, MaterialProperty inPlane, MaterialProperty across, string format) =>
        material.Number(inPlane) is { } xy ? $"{xy.ToString(format)} / {material.Number(across)?.ToString(format) ?? "-"}" : "-";
}
