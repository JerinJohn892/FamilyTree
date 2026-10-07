namespace FamilyTree.Data;

/// <summary>Resolves paths from the executable, never from the working directory.</summary>
public sealed class ApplicationPaths
{
    public string OutputRoot { get; }
    public string DataRoot => Path.Combine(OutputRoot, "Data");
    public string DatabasePath { get; }
    public bool IsOverride { get; }

    public ApplicationPaths(string baseDirectory, string? databaseOverride = null)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(baseDirectory));
        var builds = directory;
        while (builds is not null && !(builds.Name.Equals("Builds", StringComparison.OrdinalIgnoreCase) &&
            builds.Parent?.Name.Equals("Output", StringComparison.OrdinalIgnoreCase) == true)) builds = builds.Parent;
        OutputRoot = builds?.Parent!.FullName ?? Path.Combine(directory.FullName, "Output");
        IsOverride = !string.IsNullOrWhiteSpace(databaseOverride);
        if (IsOverride && !Path.IsPathFullyQualified(databaseOverride!))
            throw new ArgumentException("FAMILYTREE_DB_PATH must be an absolute database file path.");
        DatabasePath = IsOverride ? Path.GetFullPath(databaseOverride!) : Path.Combine(DataRoot, "familytree.db");
    }
}
