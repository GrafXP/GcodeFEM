namespace GcodeFem.Tests;

static class TestPaths
{
    /// <summary>Repository root, found by walking up from the test binaries to GcodeFem.slnx.</summary>
    public static string Root { get; } = FindRoot();

    public static string Sample(string name) => Path.Combine(Root, "samples", name);

    static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "GcodeFem.slnx")))
                return dir.FullName;
        throw new DirectoryNotFoundException("GcodeFem.slnx not found above " + AppContext.BaseDirectory);
    }
}
