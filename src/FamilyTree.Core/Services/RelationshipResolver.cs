using FamilyTree.Core.Abstractions;
using FamilyTree.Core.DTOs;
using FamilyTree.Core.Models;

namespace FamilyTree.Core.Services;

public sealed class RelationshipResolver : IRelationshipResolver
{
    public IReadOnlyList<ResolvedRelationshipDto> Resolve(Guid memberId, FamilyGraph graph)
    {
        var members = graph.Members.ToDictionary(m => m.MemberId);
        return graph.Relationships.Where(r => FamilyMemberService.Touches(r, memberId)).Select(r =>
        {
            var other = members[r.SourceMemberId == memberId ? r.TargetMemberId : r.SourceMemberId];
            return new ResolvedRelationshipDto(r.MemberRelationshipId, other,
                RelationshipRuleCatalog.Describe(r.Kind, other.Gender, r.Kind == RelationshipKind.Parent && r.TargetMemberId == memberId), r.Kind);
        }).OrderBy(r => r.Label).ThenBy(r => r.Member.Name).ToArray();
    }
}
