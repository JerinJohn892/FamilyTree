using FamilyTree.Core.DTOs;

namespace FamilyTree.Core.Abstractions;

public interface IFamilyMemberRepository
{
    IReadOnlyList<FamilyMemberDto> List();
    FamilyMemberDto? Get(Guid id);
    void Add(FamilyMemberDto member);
    void Update(FamilyMemberDto member);
    void Delete(Guid id);
}

public interface IRelationshipRepository
{
    IReadOnlyList<MemberRelationshipDto> List();
    void Add(MemberRelationshipDto relationship);
    void Update(MemberRelationshipDto relationship);
    void Delete(Guid id);
}

public interface IRelationshipTypeRepository { IReadOnlyList<RelationshipTypeDto> List(); }

public interface IUnitOfWork : IDisposable
{
    IFamilyMemberRepository Members { get; }
    IRelationshipRepository Relationships { get; }
    IRelationshipTypeRepository RelationshipTypes { get; }
    void Commit();
}

public interface IUnitOfWorkFactory
{
    // Write units acquire the database write reservation BEFORE reading validation state.
    IUnitOfWork Open(bool write = false);
}
