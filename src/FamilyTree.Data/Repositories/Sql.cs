using System.Globalization;
using Microsoft.Data.Sqlite;

namespace FamilyTree.Data.Repositories;

internal static class Sql
{
    public static SqliteCommand Command(SqliteConnection connection, SqliteTransaction transaction,
        string sql, params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }

    public static string Id(Guid value) => value.ToString("D");
    public static string Timestamp(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    public static DateTimeOffset ReadTimestamp(string value) => DateTimeOffset.ParseExact(value, "O", CultureInfo.InvariantCulture);
}
