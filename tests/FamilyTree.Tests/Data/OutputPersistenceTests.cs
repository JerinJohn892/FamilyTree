using FamilyTree.Data;
using Xunit;

namespace FamilyTree.Tests.Data;

public sealed class OutputPersistenceTests : IDisposable
{
    private readonly TestWorkspace w = new();
    public void Dispose() => w.Dispose();

    [Fact]
    public void ConfigurationsAndPublishedBuilds_ResolveOneDataDirectory()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "example", "Output"));
        foreach (var folder in new[] { "FamilyTree.UI/Debug/net10.0-windows", "FamilyTree.UI/Release/net10.0-windows", "win-x64" })
        {
            var paths = new ApplicationPaths(Path.Combine(root, "Builds", folder));
            Assert.Equal(root, paths.OutputRoot);
            Assert.Equal(Path.Combine(root,"Data","familytree.db"), paths.DatabasePath);
        }
        Assert.Throws<ArgumentException>(() => new ApplicationPaths(root, "relative.db"));
        Assert.Equal(w.Database.DatabasePath, new ApplicationPaths(root, w.Database.DatabasePath).DatabasePath);
        Assert.Equal(Path.Combine(root, "standalone", "Output"), new ApplicationPaths(Path.Combine(root, "standalone")).OutputRoot);
    }

    [Fact]
    public void LegacyImport_PreservesSourceAndNeverOverwritesDestination()
    {
        var first = w.Add("Original saved person");
        var target = Path.Combine(Path.GetDirectoryName(w.Database.DatabasePath)!, "Output", "Data", "familytree.db");
        LegacyDatabaseImporter.ImportIfMissing(w.Database.DatabasePath, target);
        using (var unit = new SqliteDatabase(target).Open()) Assert.Equal(first, Assert.Single(unit.Members.List()));
        w.Add("Added to old database later");
        LegacyDatabaseImporter.ImportIfMissing(w.Database.DatabasePath, target);
        using (var unit = new SqliteDatabase(target).Open()) Assert.Equal(first, Assert.Single(unit.Members.List()));
        Assert.Equal(2, w.Members.List().Count);
    }

    [Fact]
    public void Initialization_RepairsMissingMasterRowAndTableWithoutLosingMembers()
    {
        var member = w.Add("Keep me");
        w.ExecuteSql("DELETE FROM RelationshipType WHERE RelationshipTypeId=3; DROP TABLE MemberRelationship;");
        w.Database.Initialize(); w.Database.Initialize();
        using var unit = w.Database.Open();
        Assert.Equal(member, Assert.Single(unit.Members.List()));
        Assert.Equal(3, unit.RelationshipTypes.List().Count);
        Assert.Empty(unit.Relationships.List());
    }
}
