using FamilyTree.Core.DTOs;
using FamilyTree.Core.Models;
using FamilyTree.Core.Services;
using FamilyTree.Core.Validation;
using FamilyTree.Data;
using Microsoft.Data.Sqlite;

namespace FamilyTree.Tests;

public sealed class TestWorkspace : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "FamilyTree.Tests", Guid.NewGuid().ToString("N"));
    public SqliteDatabase Database { get; }
    public FamilyMemberService Members { get; }
    public RelationshipService Relationships { get; }
    public TestClock Clock { get; } = new();
    public RelationshipResolver Resolver { get; } = new();
    public FamilyTreeTraversalService Traversal { get; } = new();

    public TestWorkspace()
    {
        Database = new SqliteDatabase(Path.Combine(directory, "family.db"));
        Database.Initialize();
        Members = new(Database, new MemberValidator(Clock), new TreeIntegrityValidator(), Clock);
        Relationships = new(Database, new RelationshipValidator(), new TreeIntegrityValidator(), Clock);
    }

    public FamilyMemberDto Add(string name, Gender gender = Gender.Male, MaritalStatus status = MaritalStatus.Single) =>
        Members.Create(new(name, null, gender, LifeStatus.Living, status));

    public MemberRelationshipDto Link(FamilyMemberDto source, RelationshipLabel label, FamilyMemberDto target) =>
        Relationships.Add(new(source.MemberId, label, target.MemberId));

    public static SaveMemberRequest Edit(FamilyMemberDto member) => new(member.Name, member.DateOfBirth, member.Gender, member.LifeStatus, member.MaritalStatus);

    public void ExecuteSql(string sql)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Database.DatabasePath, ForeignKeys = true, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public void Dispose() => Directory.Delete(directory, true);
}

public sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
