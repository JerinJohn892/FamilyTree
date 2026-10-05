using FamilyTree.Core.Abstractions;
using FamilyTree.Core.DTOs;
using FamilyTree.Core.Models;
using FamilyTree.Core.Validation;

namespace FamilyTree.Core.Services;

public sealed class FamilyMemberService(IUnitOfWorkFactory units, MemberValidator validator,
    TreeIntegrityValidator integrity, TimeProvider clock) : IFamilyMemberService
{
    public IReadOnlyList<FamilyMemberDto> List()
    {
        using var unit = units.Open();
        return unit.Members.List();
    }

    public FamilyMemberDto Get(Guid id)
    {
        using var unit = units.Open();
        return Require(unit, id);
    }

    public FamilyGraph GetGraph()
    {
        using var unit = units.Open();
        return new(unit.Members.List(), unit.Relationships.List());
    }

    public FamilyMemberDto Create(SaveMemberRequest request)
    {
        validator.Validate(request);
        using var unit = units.Open(write: true);
        var now = clock.GetUtcNow();
        var member = new FamilyMemberDto(Guid.NewGuid(), request.Name.Trim(), request.DateOfBirth,
            request.Gender, request.LifeStatus, request.MaritalStatus, now, now);
        unit.Members.Add(member);
        unit.Commit();
        return member;
    }

    public FamilyMemberDto Update(Guid id, SaveMemberRequest request)
    {
        validator.Validate(request);
        using var unit = units.Open(write: true);
        var previous = Require(unit, id);
        var relationships = unit.Relationships.List();
        if (request.MaritalStatus != MaritalStatus.Married && relationships.Any(r => r.Kind == RelationshipKind.Spouse && Touches(r, id)))
            throw DomainValidationException.For("ActiveSpouseStatus", "MaritalStatus", "A member with an active spouse must remain Married. Record a divorce or remove the incorrect relationship first.");
        var updated = previous with { Name = request.Name.Trim(), DateOfBirth = request.DateOfBirth,
            Gender = request.Gender, LifeStatus = request.LifeStatus, MaritalStatus = request.MaritalStatus, UpdatedAt = clock.GetUtcNow() };
        integrity.Validate(new(unit.Members.List().Select(m => m.MemberId == id ? updated : m).ToArray(), relationships));
        unit.Members.Update(updated);
        unit.Commit();
        return updated;
    }

    public void Delete(Guid id)
    {
        using var unit = units.Open(write: true);
        Require(unit, id);
        if (unit.Relationships.List().Any(r => Touches(r, id)))
            throw DomainValidationException.For("MemberHasRelationships", "MemberId", "Remove this member's relationships before deleting the member.");
        unit.Members.Delete(id);
        unit.Commit();
    }

    internal static FamilyMemberDto Require(IUnitOfWork unit, Guid id) => unit.Members.Get(id)
        ?? throw DomainValidationException.For("MemberNotFound", "MemberId", "The selected member no longer exists. Refresh the member list.");
    internal static bool Touches(MemberRelationshipDto r, Guid id) => r.SourceMemberId == id || r.TargetMemberId == id;
}
