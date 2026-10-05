using FamilyTree.Core.DTOs;

namespace FamilyTree.Core.Abstractions;

public interface IFamilyMemberService
{
    IReadOnlyList<FamilyMemberDto> List();
    FamilyMemberDto Get(Guid id);
    FamilyGraph GetGraph();
    FamilyMemberDto Create(SaveMemberRequest request);
    FamilyMemberDto Update(Guid id, SaveMemberRequest request);
    void Delete(Guid id);
}

public interface IRelationshipService
{
    MemberRelationshipDto Add(AddRelationshipRequest request);
    void Remove(Guid id);
    void MarkAsFormerSpouse(Guid id);
}

public interface IRelationshipResolver
{
    IReadOnlyList<ResolvedRelationshipDto> Resolve(Guid memberId, FamilyGraph graph);
}

public interface IFamilyTreeTraversalService
{
    IReadOnlyList<FamilyMemberDto> GetParents(Guid id, FamilyGraph graph);
    IReadOnlyList<FamilyMemberDto> GetChildren(Guid id, FamilyGraph graph);
    IReadOnlyList<FamilyMemberDto> GetSiblings(Guid id, FamilyGraph graph);
    IReadOnlyList<FamilyMemberDto> GetGrandparents(Guid id, FamilyGraph graph);
    IReadOnlyList<FamilyMemberDto> GetGrandchildren(Guid id, FamilyGraph graph);
    IReadOnlyList<FamilyMemberDto> GetAncestors(Guid id, FamilyGraph graph);
    IReadOnlyList<FamilyMemberDto> GetDescendants(Guid id, FamilyGraph graph);
}
