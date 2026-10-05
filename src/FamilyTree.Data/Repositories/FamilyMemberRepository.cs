using System.Globalization;
using FamilyTree.Core.Abstractions;
using FamilyTree.Core.DTOs;
using FamilyTree.Core.Models;
using Microsoft.Data.Sqlite;

namespace FamilyTree.Data.Repositories;

internal sealed class FamilyMemberRepository(SqliteConnection connection, SqliteTransaction transaction) : IFamilyMemberRepository
{
    private const string Columns = "MemberId, Name, DateOfBirth, Gender, LifeStatus, MaritalStatus, CreatedAt, UpdatedAt";

    public IReadOnlyList<FamilyMemberDto> List()
    {
        using var command = Sql.Command(connection, transaction, $"SELECT {Columns} FROM FamilyMember ORDER BY Name, MemberId");
        using var reader = command.ExecuteReader();
        var members = new List<FamilyMemberDto>();
        while (reader.Read()) members.Add(Read(reader));
        return members;
    }

    public FamilyMemberDto? Get(Guid id)
    {
        using var command = Sql.Command(connection, transaction, $"SELECT {Columns} FROM FamilyMember WHERE MemberId = $id", ("$id", Sql.Id(id)));
        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    public void Add(FamilyMemberDto member) => Save(member, $"INSERT INTO FamilyMember ({Columns}) VALUES ($id,$name,$birth,$gender,$life,$marital,$created,$updated)");
    public void Update(FamilyMemberDto member) => Save(member, """
        UPDATE FamilyMember SET Name=$name, DateOfBirth=$birth, Gender=$gender,
            LifeStatus=$life, MaritalStatus=$marital, UpdatedAt=$updated WHERE MemberId=$id
        """);

    public void Delete(Guid id)
    {
        using var command = Sql.Command(connection, transaction, "DELETE FROM FamilyMember WHERE MemberId=$id", ("$id", Sql.Id(id)));
        command.ExecuteNonQuery();
    }

    private void Save(FamilyMemberDto m, string sql)
    {
        using var command = Sql.Command(connection, transaction, sql,
            ("$id", Sql.Id(m.MemberId)), ("$name", m.Name), ("$birth", m.DateOfBirth?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            ("$gender", (int)m.Gender), ("$life", (int)m.LifeStatus), ("$marital", (int)m.MaritalStatus),
            ("$created", Sql.Timestamp(m.CreatedAt)), ("$updated", Sql.Timestamp(m.UpdatedAt)));
        if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("The member could not be saved.");
    }

    private static FamilyMemberDto Read(SqliteDataReader r) => new(Guid.Parse(r.GetString(0)), r.GetString(1),
        r.IsDBNull(2) ? null : DateOnly.ParseExact(r.GetString(2), "yyyy-MM-dd", CultureInfo.InvariantCulture),
        (Gender)r.GetInt32(3), (LifeStatus)r.GetInt32(4), (MaritalStatus)r.GetInt32(5),
        Sql.ReadTimestamp(r.GetString(6)), Sql.ReadTimestamp(r.GetString(7)));
}
