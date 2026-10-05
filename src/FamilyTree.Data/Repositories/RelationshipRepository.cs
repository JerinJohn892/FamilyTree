using FamilyTree.Core.Abstractions;
using FamilyTree.Core.DTOs;
using FamilyTree.Core.Models;
using Microsoft.Data.Sqlite;

namespace FamilyTree.Data.Repositories;

internal sealed class RelationshipRepository(SqliteConnection connection, SqliteTransaction transaction) : IRelationshipRepository
{
    public IReadOnlyList<MemberRelationshipDto> List()
    {
        using var command = Sql.Command(connection, transaction, """
            SELECT MemberRelationshipId,SourceMemberId,RelationshipTypeId,TargetMemberId,CreatedAt,UpdatedAt
            FROM MemberRelationship ORDER BY CreatedAt,MemberRelationshipId
            """);
        using var reader = command.ExecuteReader();
        var relationships = new List<MemberRelationshipDto>();
        while (reader.Read()) relationships.Add(new(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)),
            (RelationshipKind)reader.GetInt32(2), Guid.Parse(reader.GetString(3)), Sql.ReadTimestamp(reader.GetString(4)), Sql.ReadTimestamp(reader.GetString(5))));
        return relationships;
    }

    public void Add(MemberRelationshipDto r) => Save(r, """
        INSERT INTO MemberRelationship (MemberRelationshipId,SourceMemberId,RelationshipTypeId,TargetMemberId,CreatedAt,UpdatedAt)
        VALUES ($id,$source,$kind,$target,$created,$updated)
        """);

    public void Update(MemberRelationshipDto r) => Save(r, """
        UPDATE MemberRelationship SET SourceMemberId=$source,RelationshipTypeId=$kind,
            TargetMemberId=$target,UpdatedAt=$updated WHERE MemberRelationshipId=$id
        """);

    public void Delete(Guid id)
    {
        using var command = Sql.Command(connection, transaction, "DELETE FROM MemberRelationship WHERE MemberRelationshipId=$id", ("$id", Sql.Id(id)));
        command.ExecuteNonQuery();
    }

    private void Save(MemberRelationshipDto r, string sql)
    {
        using var command = Sql.Command(connection, transaction, sql,
            ("$id", Sql.Id(r.MemberRelationshipId)), ("$source", Sql.Id(r.SourceMemberId)), ("$kind", (int)r.Kind),
            ("$target", Sql.Id(r.TargetMemberId)), ("$created", Sql.Timestamp(r.CreatedAt)), ("$updated", Sql.Timestamp(r.UpdatedAt)));
        if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("The relationship could not be saved.");
    }
}
