namespace GcodeFem.Core.Gcode;

/// <summary>Line types from Bambu's "; FEATURE:" comments.</summary>
public enum FeatureType : byte
{
    Unknown,
    OuterWall,
    InnerWall,
    OverhangWall,
    SparseInfill,
    InternalSolidInfill,
    TopSurface,
    BottomSurface,
    Bridge,
    InternalBridge,
    GapInfill,
    FloatingVerticalShell,
    Ironing,
    Skirt,
    Brim,
    Support,
    SupportInterface,
    SupportTransition,
    PrimeTower,
    Custom,
}

/// <summary>
/// What a piece of the part is, in the groups that behave differently: the line types of the part,
/// with both bridge types together and the rare ones (gap infill, overhang wall, ironing, floating
/// vertical shell) as Other.
/// </summary>
public enum FeatureClass : byte
{
    OuterWall,
    InnerWall,
    SparseInfill,
    SolidInfill,
    TopSurface,
    BottomSurface,
    Bridge,
    Other,
}

public static class FeatureTypes
{
    public const int ClassCount = 8;

    static readonly Dictionary<string, FeatureType> ByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Outer wall"] = FeatureType.OuterWall,
        ["Inner wall"] = FeatureType.InnerWall,
        ["Overhang wall"] = FeatureType.OverhangWall,
        ["Sparse infill"] = FeatureType.SparseInfill,
        ["Internal solid infill"] = FeatureType.InternalSolidInfill,
        ["Top surface"] = FeatureType.TopSurface,
        ["Bottom surface"] = FeatureType.BottomSurface,
        ["Bridge"] = FeatureType.Bridge,
        ["Internal bridge"] = FeatureType.InternalBridge,
        ["Gap infill"] = FeatureType.GapInfill,
        ["Floating vertical shell"] = FeatureType.FloatingVerticalShell,
        ["Ironing"] = FeatureType.Ironing,
        ["Skirt"] = FeatureType.Skirt,
        ["Brim"] = FeatureType.Brim,
        ["Support"] = FeatureType.Support,
        ["Support interface"] = FeatureType.SupportInterface,
        ["Support transition"] = FeatureType.SupportTransition,
        ["Prime tower"] = FeatureType.PrimeTower,
        ["Custom"] = FeatureType.Custom,
    };

    static readonly Dictionary<FeatureType, string> NameOf = ByName.ToDictionary(p => p.Value, p => p.Key);

    public static FeatureType Parse(ReadOnlySpan<char> name) =>
        ByName.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(name.Trim(), out var feature) ? feature : FeatureType.Unknown;

    public static string DisplayName(this FeatureType feature) => NameOf.GetValueOrDefault(feature, "Unknown");

    /// <summary>Lines that end up as the part itself (not skirt, brim, support, prime tower or start/end G-code).</summary>
    public static bool IsPartMaterial(this FeatureType feature) =>
        feature is >= FeatureType.OuterWall and <= FeatureType.Ironing;

    /// <summary>The class of a line of the part; anything that is not part material counts as Other.</summary>
    public static FeatureClass Class(this FeatureType feature) => feature switch
    {
        FeatureType.OuterWall => FeatureClass.OuterWall,
        FeatureType.InnerWall => FeatureClass.InnerWall,
        FeatureType.SparseInfill => FeatureClass.SparseInfill,
        FeatureType.InternalSolidInfill => FeatureClass.SolidInfill,
        FeatureType.TopSurface => FeatureClass.TopSurface,
        FeatureType.BottomSurface => FeatureClass.BottomSurface,
        FeatureType.Bridge or FeatureType.InternalBridge => FeatureClass.Bridge,
        _ => FeatureClass.Other,
    };

    public static string DisplayName(this FeatureClass feature) => feature switch
    {
        FeatureClass.OuterWall => "Outer wall",
        FeatureClass.InnerWall => "Inner wall",
        FeatureClass.SparseInfill => "Sparse infill",
        FeatureClass.SolidInfill => "Solid infill",
        FeatureClass.TopSurface => "Top surface",
        FeatureClass.BottomSurface => "Bottom surface",
        FeatureClass.Bridge => "Bridge",
        _ => "Other",
    };
}
