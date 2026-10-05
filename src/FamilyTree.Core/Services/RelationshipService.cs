using FamilyTree.Core.Abstractions;
using FamilyTree.Core.DTOs;
using FamilyTree.Core.Models;
using FamilyTree.Core.Validation;

namespace FamilyTree.Core.Services;

public sealed class RelationshipService(IUnitOfWorkFactory units, RelationshipValidator validator,
    TreeIntegrityValidator integrity, TimeProvider clock) : IRelationshipService
{
    public MemberRelationshipDto Add(AddRelationshipRequest request)
    {
        using var unit = units.Open(write: true);
        var source = FamilyMemberService.Require(unit, request.SourceMemberId);
        FamilyMemberService.Require(unit, request.TargetMemberId);
        var (a, kind, b) = RelationshipRuleCatalog.Normalize(request, source);
        var now = clock.GetUtcNow();
        var candidate = new MemberRelationshipDto(Guid.NewGuid(), a, kind, b, now, now);
        var graph = new FamilyGraph(unit.Members.List(), unit.Relationships.List());
        validator.Validate(candidate, graph, unit.RelationshipTypes.List());
        integrity.Validate(graph with { Relationships = graph.Relationships.Append(candidate).ToArray() });
        unit.Relationships.Add(candidate);
        if (kind != RelationshipKind.Parent) RefreshStatuses(unit, candidate, now);
        unit.Commit();
        return candidate;
    }

    public void MarkAsFormerSpouse(Guid id)
    {
        using var unit = units.Open(write: true);
        var relationship = Require(unit, id);
        if (relationship.Kind != RelationshipKind.Spouse)
            throw DomainValidationException.For("NotActiveSpouse", "RelationshipId", "Only a current spouse can be marked as a former spouse.");
        if (!unit.RelationshipTypes.List().Any(t => t.Kind == RelationshipKind.FormerSpouse && t.IsActive))
            throw DomainValidationException.For("InactiveRelationshipType", "Label", "Former-spouse relationships are not available.");
        var now = clock.GetUtcNow();
        unit.Relationships.Update(relationship with { Kind = RelationshipKind.FormerSpouse, UpdatedAt = now });
        RefreshStatuses(unit, relationship, now);
        unit.Commit();
    }

    public void Remove(Guid id)
    {
        using var unit = units.Open(write: true);
        var relationship = Require(unit, id);
        unit.Relationships.Delete(id);
        if (relationship.Kind != RelationshipKind.Parent) RefreshStatuses(unit, relationship, clock.GetUtcNow());
        unit.Commit();
    }

    private static MemberRelationshipDto Require(IUnitOfWork unit, Guid id) => unit.Relationships.List()
        .SingleOrDefault(r => r.MemberRelationshipId == id)
        ?? throw DomainValidationException.For("RelationshipNotFound", "RelationshipId", "The selected relationship no longer exists. Refresh the member list.");

    private static void RefreshStatuses(IUnitOfWork unit, MemberRelationshipDto relationship, DateTimeOffset now)
    {
        var all = unit.Relationships.List();
        foreach (var id in new[] { relationship.SourceMemberId, relationship.TargetMemberId })
        {
            var links = all.Where(r => FamilyMemberService.Touches(r, id)).ToArray();
            var status = links.Any(r => r.Kind == RelationshipKind.Spouse) ? MaritalStatus.Married :
                links.Any(r => r.Kind == RelationshipKind.FormerSpouse) ? MaritalStatus.Divorced : MaritalStatus.Single;
            var member = FamilyMemberService.Require(unit, id);
            unit.Members.Update(member with { MaritalStatus = status, UpdatedAt = now });
        }
    }
}
