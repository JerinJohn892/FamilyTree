using FamilyTree.Core.Abstractions;
using FamilyTree.Data.Migrations;
using FamilyTree.Data.Transactions;
using Microsoft.Data.Sqlite;

namespace FamilyTree.Data;

public sealed class SqliteDatabase : IUnitOfWorkFactory
{
    private readonly string connectionString;
    public string DatabasePath { get; }

    public SqliteDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        DatabasePath = Path.GetFullPath(databasePath);
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            ForeignKeys = true,
            Pooling = false,
            DefaultTimeout = 15
        }.ToString();
    }

    public void Initialize()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        using var connection = Connect();
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "PRAGMA user_version;";
        var version = Convert.ToInt32(command.ExecuteScalar());
        if (version > Schema.Version)
            throw new InvalidOperationException("This database was created by a newer FamilyTree version. Update the application before opening it.");
        if (version == 0)
        {
            command.CommandText = Schema.Initial;
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public IUnitOfWork Open(bool write = false) => new SqliteUnitOfWork(Connect(), write);

    private SqliteConnection Connect()
    {
        var connection = new SqliteConnection(connectionString);
        try { connection.Open(); return connection; }
        catch { connection.Dispose(); throw; }
    }
}
