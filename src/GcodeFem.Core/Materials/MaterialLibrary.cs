using System.Security.Cryptography;
using System.Text;
using GcodeFem.Core.Gcode;

namespace GcodeFem.Core.Materials;

/// <summary>How a filament came by its material.</summary>
public enum MatchKind
{
    /// <summary>A material file names the filament.</summary>
    ByName,

    /// <summary>No file names it; a material that stands in for its filament type is used.</summary>
    ByType,

    /// <summary>Nothing fits: <see cref="Material.Generic"/>.</summary>
    None,
}

/// <summary>The material a filament preset is solved with, and how it was found. Anything but <see cref="MatchKind.ByName"/> has to be shown to the user.</summary>
public sealed record MaterialMatch(Material Material, MatchKind Kind, string Filament, string? Type)
{
    public bool IsStandIn => Kind != MatchKind.ByName;

    /// <summary>One sentence on where the material comes from; for a stand-in, that it is one.</summary>
    public string Describe()
    {
        var type = string.IsNullOrWhiteSpace(Type) ? "" : $" ({Type})";
        return Kind switch
        {
            MatchKind.ByName => $"{Material.Name}{Origin()}",
            MatchKind.ByType => $"No material data for '{Filament}'. {Material.Name} stands in for it, as for every {Type} filament without data of its own.",
            _ => $"No material data for '{Filament}'{type}, nor for its type. A generic stiffness of {Material.Number(MaterialProperty.YoungsModulusXY):0} MPa is used: " +
                 "the displacements are not this filament's.",
        };
    }

    string Origin() => Material.Documents.FirstOrDefault() is { Title.Length: > 0 } document ? $", from {document.Title}" : "";
}

/// <summary>
/// The material files of one folder, and which of them a filament preset is solved with: the one
/// that names the filament, else the one that stands in for the filament's type, else a generic one.
/// </summary>
public sealed class MaterialLibrary
{
    readonly List<Material> materials = [];
    readonly List<string> problems = [];

    MaterialLibrary(string directory) => Directory = directory;

    public string Directory { get; }

    /// <summary>By name.</summary>
    public IReadOnlyList<Material> Materials => materials;

    /// <summary>Files that could not be read and materials that get in each other's way. To be shown, not swallowed.</summary>
    public IReadOnlyList<string> Problems => problems;

    /// <summary>
    /// Where the materials are kept: the folder named by GCODEFEM_MATERIALS, else the nearest
    /// "materials" folder with material files at or above the program (the repository's, or one
    /// shipped beside the exe), else %APPDATA%\GcodeFem\materials.
    /// </summary>
    public static string DefaultDirectory()
    {
        if (Environment.GetEnvironmentVariable("GCODEFEM_MATERIALS") is { Length: > 0 } given) return given;
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "materials");
            if (System.IO.Directory.Exists(candidate) && System.IO.Directory.EnumerateFiles(candidate, "*" + MaterialFile.Extension).Any()) return candidate;
        }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GcodeFem", "materials");
    }

    /// <summary>Reads every material file of <paramref name="directory"/>. A folder that is not there is an empty library.</summary>
    public static MaterialLibrary Load(string? directory = null)
    {
        var library = new MaterialLibrary(Path.GetFullPath(directory ?? DefaultDirectory()));
        if (!System.IO.Directory.Exists(library.Directory)) return library;

        foreach (var path in System.IO.Directory.EnumerateFiles(library.Directory, "*" + MaterialFile.Extension).Order(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                library.materials.Add(MaterialFile.Load(path));
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                library.problems.Add(ex.Message);
            }
        }
        library.materials.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));

        // Whichever of two claimants comes first by name is used; which one that is must not pass unseen.
        foreach (var claim in library.materials.SelectMany(m => m.Filaments.Select(f => (Filament: f, Material: m))).GroupBy(c => NameKey(c.Filament)).Where(g => g.Count() > 1))
            library.problems.Add($"{Names(claim.Select(c => c.Material))} each name the filament '{claim.First().Filament}'; {claim.First().Material.Name} is used.");
        foreach (var claim in library.materials.Where(m => m.StandsInForType).GroupBy(m => m.Type, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            library.problems.Add($"{Names(claim)} each stand in for {claim.Key}; {claim.First().Name} is used.");
        return library;
    }

    public Material? Find(string name) => materials.FirstOrDefault(m => Same(m.Name, name));

    /// <summary>The material for a filament preset of that name and filament_type.</summary>
    public MaterialMatch Match(string filament, string? type)
    {
        // A preset named in full goes before the filament it is a preset of.
        var named = materials.FirstOrDefault(m => m.Filaments.Any(f => f.Contains('@') && SameFilament(f, filament)))
                    ?? materials.FirstOrDefault(m => m.Filaments.Any(f => !f.Contains('@') && SameFilament(f, Stem(filament))));
        if (named is not null) return new MaterialMatch(named, MatchKind.ByName, filament, type);

        if (!string.IsNullOrWhiteSpace(type) && materials.FirstOrDefault(m => m.StandsInForType && Same(m.Type, type)) is { } standIn)
            return new MaterialMatch(standIn, MatchKind.ByType, filament, type);
        return new MaterialMatch(Material.Generic(), MatchKind.None, filament, type);
    }

    /// <summary>The material for the filament a G-code file was sliced with, by the settings in its header.</summary>
    public MaterialMatch Match(Toolpath toolpath) =>
        Match(FirstOf(toolpath.Setting("filament_settings_id")) ?? "an unnamed filament", FirstOf(toolpath.Setting("filament_type")));

    /// <summary>
    /// Writes the material into the library, to the file it came from or to one named after it.
    /// A file of another material of that name is not overwritten unless <paramref name="replace"/> says so.
    /// </summary>
    public string Save(Material material, bool replace = false)
    {
        var path = material.Path ?? Path.Combine(Directory, Slug(material.Name) + MaterialFile.Extension);
        if (material.Path is null && File.Exists(path) && !replace)
            throw new InvalidOperationException($"There is a material file '{Path.GetFileName(path)}' already. Give this one another name, or replace that one.");
        MaterialFile.Save(material, path);
        materials.RemoveAll(m => m.Path is { } other && Same(other, material.Path!));
        materials.Add(material);
        materials.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
        return path;
    }

    /// <summary>
    /// Puts a copy of a source document into the library's "tds" folder and returns it as a document
    /// of a material there. The same file is copied only once; another file of the same name is refused.
    /// </summary>
    /// <param name="name">The copy's file name; the document's own if none is given.</param>
    public MaterialDocument Attach(string file, string id, string title, string? url = null, string? name = null)
    {
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
        var relative = Path.Combine("tds", name ?? Path.GetFileName(file));
        var target = Path.Combine(Directory, relative);
        if (!File.Exists(target))
        {
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
        else if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(target))).ToLowerInvariant() != hash)
            throw new InvalidOperationException($"'{target}' is another document than '{file}' under the same name. Rename one of them.");
        return new MaterialDocument(id, title, relative.Replace('\\', '/'), url, hash);
    }

    /// <summary>The full path of a material's document, or null if it names no file.</summary>
    public static string? DocumentPath(Material material, MaterialDocument document) =>
        document.File is null ? null : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(material.Path ?? ".")!, document.File));

    /// <summary>"Bambu PLA Basic @BBL X1C 0.2 nozzle" → "Bambu PLA Basic": the filament a preset is of.</summary>
    public static string Stem(string filament)
    {
        var at = filament.IndexOf('@');
        return (at < 0 ? filament : filament[..at]).Trim();
    }

    /// <summary>"Bambu PLA Silk+" → "bambu-pla-silk-plus": a file name for a material.</summary>
    public static string Slug(string name)
    {
        var slug = new StringBuilder();
        foreach (var c in name.Trim().ToLowerInvariant().Replace("+", " plus"))
        {
            if (char.IsAsciiLetterOrDigit(c)) slug.Append(c);
            else if (slug.Length > 0 && slug[^1] != '-') slug.Append('-');
        }
        return slug.ToString().TrimEnd('-') is { Length: > 0 } text ? text : "material";
    }

    /// <summary>A G-code setting lists one value per filament, quoted: the first one, plain.</summary>
    static string? FirstOf(string? setting) =>
        setting?.Split(';')[0].Trim().Trim('"') is { Length: > 0 } first ? first : null;

    /// <summary>
    /// Whether two names mean the same filament. A data sheet's "ASA Aero" is the slicer's "ASA-Aero",
    /// so only letters, digits and "+" count ("PLA Silk" is not "PLA Silk+").
    /// </summary>
    public static bool SameFilament(string a, string b) => NameKey(a) == NameKey(b);

    static string NameKey(string name) => string.Concat(name.Where(c => char.IsLetterOrDigit(c) || c is '+' or '@')).ToLowerInvariant();

    static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    static string Names(IEnumerable<Material> several) => string.Join(" and ", several.Select(m => m.Name));
}
