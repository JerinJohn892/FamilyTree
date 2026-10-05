using FamilyTree.Core.Models;

namespace FamilyTree.Core.DTOs;

public sealed record FamilyMemberDto(Guid MemberId, string Name, DateOnly? DateOfBirth,
    Gender Gender, LifeStatus LifeStatus, MaritalStatus MaritalStatus,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record SaveMemberRequest(string Name, DateOnly? DateOfBirth, Gender Gender,
    LifeStatus LifeStatus, MaritalStatus MaritalStatus);

public sealed record MemberRelationshipDto(Guid MemberRelationshipId, Guid SourceMemberId,
    RelationshipKind Kind, Guid TargetMemberId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record RelationshipTypeDto(RelationshipKind Kind, string Code, string Name,
    bool IsPrimary, bool IsCustom, bool IsActive);

public sealed record AddRelationshipRequest(Guid SourceMemberId, RelationshipLabel Label, Guid TargetMemberId);
public sealed record FamilyGraph(IReadOnlyList<FamilyMemberDto> Members, IReadOnlyList<MemberRelationshipDto> Relationships);
public sealed record ResolvedRelationshipDto(Guid RelationshipId, FamilyMemberDto Member, string Label, RelationshipKind Kind);
public sealed record ValidationError(string Code, string Field, string Message);
