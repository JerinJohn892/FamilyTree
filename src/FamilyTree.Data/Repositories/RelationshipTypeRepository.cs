using FamilyTree.Core.Abstractions;
using FamilyTree.Core.DTOs;
using FamilyTree.Core.Models;
using Microsoft.Data.Sqlite;

namespace FamilyTree.Data.Repositories;

internal sealed class RelationshipTypeRepository(SqliteConnection connection, SqliteTransaction transaction) : IRelationshipTypeRepository
{
    public IReadOnlyList<RelationshipTypeDto> List()
    {
        using var command = Sql.Command(connection, transaction, "SELECT RelationshipTypeId,Code,Name,IsPrimary,IsCustom,IsActive FROM RelationshipType ORDER BY RelationshipTypeId");
        using var reader = command.ExecuteReader();
        var types = new List<RelationshipTypeDto>();
        while (reader.Read()) types.Add(new((RelationshipKind)reader.GetInt32(0), reader.GetString(1), reader.GetString(2),
            reader.GetBoolean(3), reader.GetBoolean(4), reader.GetBoolean(5)));
        return types;
    }
}
