using FamilyTree.Core.DTOs;
using FamilyTree.Core.Models;

namespace FamilyTree.Core.Validation;

public sealed class RelationshipValidator
{
    public void Validate(MemberRelationshipDto candidate, FamilyGraph graph, IReadOnlyList<RelationshipTypeDto> types)
    {
        if (candidate.SourceMemberId == candidate.TargetMemberId)
            throw DomainValidationException.For("SelfRelationship", "TargetMemberId", "A member cannot have a relationship with themselves.");
        if (!types.Any(t => t.Kind == candidate.Kind && t.IsActive))
            throw DomainValidationException.For("InactiveRelationshipType", "Label", "This relationship type is not available.");
        if (graph.Relationships.Any(r => r.SourceMemberId == candidate.SourceMemberId && r.TargetMemberId == candidate.TargetMemberId &&
            (r.Kind == candidate.Kind || (r.Kind != RelationshipKind.Parent && candidate.Kind != RelationshipKind.Parent))))
            throw DomainValidationException.For("DuplicateRelationship", "TargetMemberId", "This relationship already exists. Use the former-spouse action to record a divorce.");
    }
}
