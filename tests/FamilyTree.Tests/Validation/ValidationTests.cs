using FamilyTree.Core.DTOs;
using FamilyTree.Core.Models;
using FamilyTree.Core.Validation;
using Xunit;

namespace FamilyTree.Tests.Validation;

public sealed class ValidationTests : IDisposable
{
    private readonly TestWorkspace w = new();
    public void Dispose() => w.Dispose();
    private static void Reject(string code, Action action) => Assert.Contains(Assert.Throws<DomainValidationException>(action).Errors, e => e.Code == code);

    [Fact]
    public void MemberValidation_RejectsInvalidFieldsAndTrimsNames()
    {
        var request = new SaveMemberRequest("   ", new DateOnly(2026, 10, 6), (Gender)0, (LifeStatus)8, (MaritalStatus)9);
        var errors = Assert.Throws<DomainValidationException>(() => w.Members.Create(request)).Errors;
        Assert.Equal(5, errors.Count);
        Assert.Contains(errors, e => e.Code == "FutureBirthDate" && e.Field == "DateOfBirth");
        Assert.Empty(w.Members.List());
        Reject("InvalidName", () => w.Add(new string('x', 201)));
        var valid = w.Add("  O'Connor  ");
        Assert.Equal("O'Connor", valid.Name);
        Assert.Null(valid.DateOfBirth);
        Assert.Equal(w.Clock.Now, valid.CreatedAt);
        var updated = w.Members.Update(valid.MemberId, TestWorkspace.Edit(valid) with { DateOfBirth = new DateOnly(2026, 10, 5) });
        Assert.Equal(new DateOnly(2026, 10, 5), updated.DateOfBirth);
    }

    [Fact]
    public void MemberUpdates_PreserveIdentityAndCreationTime()
    {
        var member = w.Add("Before", status: MaritalStatus.Married);
        w.Clock.Now = w.Clock.Now.AddHours(1);
        var updated = w.Members.Update(member.MemberId, TestWorkspace.Edit(member) with { Name = "After", LifeStatus = LifeStatus.Deceased });
        Assert.Equal(member.MemberId, updated.MemberId);
        Assert.Equal(member.CreatedAt, updated.CreatedAt);
        Assert.Equal(w.Clock.Now, updated.UpdatedAt);
        Assert.Equal(MaritalStatus.Married, updated.MaritalStatus);
        Assert.Equal(LifeStatus.Deceased, updated.LifeStatus);
        Assert.Empty(w.Members.GetGraph().Relationships);
    }

    [Fact]
    public void MissingEndpointsAndSelfRelationships_AreRejected()
    {
        var a = w.Add("A");
        Reject("MemberNotFound", () => w.Relationships.Add(new(a.MemberId, RelationshipLabel.Parent, Guid.NewGuid())));
        Reject("MemberNotFound", () => w.Relationships.Add(new(Guid.NewGuid(), RelationshipLabel.Parent, a.MemberId)));
        Reject("SelfRelationship", () => w.Link(a, RelationshipLabel.Spouse, a));
        Reject("SelfRelationship", () => w.Link(a, RelationshipLabel.Parent, a));
        Reject("MemberNotFound", () => w.Members.Update(Guid.NewGuid(), TestWorkspace.Edit(a)));
        Reject("MemberNotFound", () => w.Members.Delete(Guid.NewGuid()));
        Reject("MemberNotFound", () => w.Members.Get(Guid.NewGuid()));
        Reject("RelationshipNotFound", () => w.Relationships.Remove(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(RelationshipLabel.Father, Gender.Female)]
    [InlineData(RelationshipLabel.Mother, Gender.Male)]
    [InlineData(RelationshipLabel.Son, Gender.Female)]
    [InlineData(RelationshipLabel.Daughter, Gender.Male)]
    [InlineData(RelationshipLabel.Husband, Gender.Female)]
    [InlineData(RelationshipLabel.Wife, Gender.Male)]
    [InlineData(RelationshipLabel.ExHusband, Gender.Female)]
    [InlineData(RelationshipLabel.ExWife, Gender.Male)]
    public void GenderLabels_AreCheckedBeforeNormalization(RelationshipLabel label, Gender gender)
    {
        var a = w.Add("A", gender); var b = w.Add("B");
        Reject("GenderMismatch", () => w.Link(a, label, b));
        Assert.Empty(w.Members.GetGraph().Relationships);
    }

    [Theory]
    [InlineData(Gender.Male, "SecondFather")]
    [InlineData(Gender.Female, "SecondMother")]
    public void ChildHasAtMostOneParentOfEachGender(Gender gender, string code)
    {
        var first = w.Add("First", gender); var second = w.Add("Second", gender); var child = w.Add("Child");
        w.Link(first, RelationshipLabel.Parent, child);
        Reject(code, () => w.Link(second, RelationshipLabel.Parent, child));
        Assert.Single(w.Members.GetGraph().Relationships);
    }

    [Fact]
    public void GenderEdit_CannotInvalidateParentLimit()
    {
        var father = w.Add("Father"); var mother = w.Add("Mother", Gender.Female); var child = w.Add("Child");
        w.Link(father, RelationshipLabel.Parent, child); w.Link(mother, RelationshipLabel.Parent, child);
        Reject("SecondFather", () => w.Members.Update(mother.MemberId, TestWorkspace.Edit(mother) with { Gender = Gender.Male }));
        Assert.Equal(Gender.Female, w.Members.Get(mother.MemberId).Gender);
    }

    [Fact]
    public void AncestryCycles_AreRejected()
    {
        var a = w.Add("A"); var b = w.Add("B"); var c = w.Add("C");
        w.Link(a, RelationshipLabel.Parent, b); w.Link(b, RelationshipLabel.Parent, c);
        Reject("AncestryCycle", () => w.Link(c, RelationshipLabel.Parent, a));
        Assert.Equal(2, w.Members.GetGraph().Relationships.Count);
    }

    [Theory]
    [InlineData(RelationshipLabel.Spouse)]
    [InlineData(RelationshipLabel.FormerSpouse)]
    public void SpouseAncestorConflict_IsRejectedInBothMutationOrders(RelationshipLabel label)
    {
        var a = w.Add("A"); var b = w.Add("B"); var c = w.Add("C");
        w.Link(a, RelationshipLabel.Parent, b); var bc = w.Link(b, RelationshipLabel.Parent, c);
        Reject("SpouseAncestryConflict", () => w.Link(c, label, a));
        w.Relationships.Remove(bc.MemberRelationshipId);
        w.Link(c, label, a);
        Reject("SpouseAncestryConflict", () => w.Link(b, RelationshipLabel.Parent, c));
    }

    [Fact]
    public void DuplicatesAndSecondActiveSpouse_AreRejected()
    {
        var a = w.Add("A"); var b = w.Add("B"); var c = w.Add("C");
        w.Link(a, RelationshipLabel.Spouse, b);
        Reject("DuplicateRelationship", () => w.Link(b, RelationshipLabel.Husband, a));
        Reject("ActiveSpouseExists", () => w.Link(c, RelationshipLabel.Spouse, b));
        Reject("ActiveSpouseStatus", () => w.Members.Update(a.MemberId, TestWorkspace.Edit(a) with { MaritalStatus = MaritalStatus.Divorced }));
        w.Link(a, RelationshipLabel.Parent, c);
        Reject("DuplicateRelationship", () => w.Link(c, RelationshipLabel.Son, a));
    }

    [Fact]
    public void UnsupportedInactiveAndInvalidConversions_AreRejected()
    {
        var a = w.Add("A"); var b = w.Add("B");
        Reject("UnsupportedRelationship", () => w.Link(a, (RelationshipLabel)999, b));
        var parent = w.Link(a, RelationshipLabel.Parent, b);
        Reject("NotActiveSpouse", () => w.Relationships.MarkAsFormerSpouse(parent.MemberRelationshipId));
        w.Relationships.Remove(parent.MemberRelationshipId);
        w.ExecuteSql("UPDATE RelationshipType SET IsActive=0 WHERE RelationshipTypeId=2;");
        Reject("InactiveRelationshipType", () => w.Link(a, RelationshipLabel.Spouse, b));
    }

    [Fact]
    public void DeleteRequiresExplicitRelationshipRemoval()
    {
        var a = w.Add("A"); var b = w.Add("B");
        var link = w.Link(a, RelationshipLabel.Parent, b);
        Reject("MemberHasRelationships", () => w.Members.Delete(a.MemberId));
        Reject("MemberHasRelationships", () => w.Members.Delete(b.MemberId));
        w.Relationships.Remove(link.MemberRelationshipId);
        w.Members.Delete(a.MemberId);
        Assert.Equal(b.MemberId, Assert.Single(w.Members.List()).MemberId);
    }
}
