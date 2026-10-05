using FamilyTree.Core.Abstractions;
using FamilyTree.Data.Repositories;
using Microsoft.Data.Sqlite;

namespace FamilyTree.Data.Transactions;

internal sealed class SqliteUnitOfWork : IUnitOfWork
{
    private readonly SqliteConnection connection;
    private readonly SqliteTransaction transaction;
    public IFamilyMemberRepository Members { get; }
    public IRelationshipRepository Relationships { get; }
    public IRelationshipTypeRepository RelationshipTypes { get; }

    public SqliteUnitOfWork(SqliteConnection connection, bool write)
    {
        this.connection = connection;
        try { transaction = connection.BeginTransaction(deferred: !write); }
        catch { connection.Dispose(); throw; }
        Members = new FamilyMemberRepository(connection, transaction);
        Relationships = new RelationshipRepository(connection, transaction);
        RelationshipTypes = new RelationshipTypeRepository(connection, transaction);
    }

    public void Commit() => transaction.Commit();
    public void Dispose()
    {
        transaction.Dispose(); // An uncommitted transaction rolls back.
        connection.Dispose();
    }
}
