using FamilyTree.Core.DTOs;
using FamilyTree.Core.Models;
using FamilyTree.Core.Validation;
using Xunit;

namespace FamilyTree.Tests.Services;

public sealed class AcceptanceTests : IDisposable
{
    private readonly TestWorkspace w = new();
    public void Dispose() => w.Dispose();

    [Fact]
    public void JohnMaryMichaelAnna_HasCanonicalFactsReverseLabelsAndSiblings()
    {
        var john = w.Add("John"); var mary = w.Add("Mary", Gender.Female);
        var michael = w.Add("Michael"); var anna = w.Add("Anna", Gender.Female);
        w.Link(john, RelationshipLabel.Husband, mary);
        w.Link(john, RelationshipLabel.Father, michael);
        w.Link(mary, RelationshipLabel.Mother, michael);
        w.Link(anna, RelationshipLabel.Daughter, john);
        w.Link(anna, RelationshipLabel.Daughter, mary);
        var graph = w.Members.GetGraph();
        Assert.Equal(5, graph.Relationships.Count);
        Assert.Equal(MaritalStatus.Married, w.Members.Get(john.MemberId).MaritalStatus);
        Assert.Equal(MaritalStatus.Married, w.Members.Get(mary.MemberId).MaritalStatus);
        var johns = w.Resolver.Resolve(john.MemberId, graph);
        Assert.Contains(johns, r => r.Member.MemberId == mary.MemberId && r.Label == "Wife");
        Assert.Contains(johns, r => r.Member.MemberId == michael.MemberId && r.Label == "Son");
        Assert.Contains(johns, r => r.Member.MemberId == anna.MemberId && r.Label == "Daughter");
        var annas = w.Resolver.Resolve(anna.MemberId, graph);
        Assert.Contains(annas, r => r.Member.MemberId == john.MemberId && r.Label == "Father");
        Assert.Contains(annas, r => r.Member.MemberId == mary.MemberId && r.Label == "Mother");
        Assert.Equal(michael.MemberId, Assert.Single(w.Traversal.GetSiblings(anna.MemberId, graph)).MemberId);
        Assert.Equal(2, w.Traversal.GetParents(anna.MemberId, graph).Count);
        Assert.Equal(2, w.Traversal.GetChildren(john.MemberId, graph).Count);
    }

    [Fact]
    public void DivorceAndCorrection_PreserveConnectionThenRecalculateStatuses()
    {
        var john = w.Add("John"); var mary = w.Add("Mary", Gender.Female);
        var marriage = w.Link(john, RelationshipLabel.Spouse, mary);
        w.Clock.Now = w.Clock.Now.AddDays(1);
        w.Relationships.MarkAsFormerSpouse(marriage.MemberRelationshipId);
        var former = Assert.Single(w.Members.GetGraph().Relationships);
        Assert.Equal(marriage.MemberRelationshipId, former.MemberRelationshipId);
        Assert.Equal(RelationshipKind.FormerSpouse, former.Kind);
        Assert.Equal(marriage.CreatedAt, former.CreatedAt);
        Assert.Equal(w.Clock.Now, former.UpdatedAt);
        Assert.All(w.Members.List(), m => Assert.Equal(MaritalStatus.Divorced, m.MaritalStatus));
        Assert.Equal("Ex-wife", Assert.Single(w.Resolver.Resolve(john.MemberId, w.Members.GetGraph())).Label);
        Assert.Equal("Ex-husband", Assert.Single(w.Resolver.Resolve(mary.MemberId, w.Members.GetGraph())).Label);
        w.Relationships.Remove(marriage.MemberRelationshipId);
        Assert.Empty(w.Members.GetGraph().Relationships);
        Assert.All(w.Members.List(), m => Assert.Equal(MaritalStatus.Single, m.MaritalStatus));
    }

    [Fact]
    public void HistoricalFormerSpouse_PreservesAnotherCurrentMarriage()
    {
        var a = w.Add("A"); var b = w.Add("B"); var c = w.Add("C");
        var current = w.Link(a, RelationshipLabel.Spouse, b);
        w.Link(a, RelationshipLabel.FormerSpouse, c);
        Assert.Equal(MaritalStatus.Married, w.Members.Get(a.MemberId).MaritalStatus);
        Assert.Equal(MaritalStatus.Married, w.Members.Get(b.MemberId).MaritalStatus);
        Assert.Equal(MaritalStatus.Divorced, w.Members.Get(c.MemberId).MaritalStatus);
        Assert.Equal("Husband", Assert.Single(w.Resolver.Resolve(b.MemberId, w.Members.GetGraph())).Label);
        w.Relationships.Remove(current.MemberRelationshipId);
        Assert.Equal(MaritalStatus.Divorced, w.Members.Get(a.MemberId).MaritalStatus);
        Assert.Equal(MaritalStatus.Single, w.Members.Get(b.MemberId).MaritalStatus);
    }

    [Fact]
    public void Traversal_DeduplicatesSharedPathsAndIncludesHalfSiblings()
    {
        var grandfather = w.Add("Grandfather"); var grandmother = w.Add("Grandmother", Gender.Female);
        var father = w.Add("Father"); var mother = w.Add("Mother", Gender.Female);
        var child = w.Add("Child"); var halfSibling = w.Add("Half sibling");
        w.Link(grandfather, RelationshipLabel.Parent, father);
        w.Link(grandmother, RelationshipLabel.Parent, father);
        w.Link(grandfather, RelationshipLabel.Parent, mother);
        w.Link(grandmother, RelationshipLabel.Parent, mother);
        w.Link(father, RelationshipLabel.Parent, child);
        w.Link(mother, RelationshipLabel.Parent, child);
        w.Link(father, RelationshipLabel.Parent, halfSibling);
        var graph = w.Members.GetGraph();
        Assert.Equal(halfSibling.MemberId, Assert.Single(w.Traversal.GetSiblings(child.MemberId, graph)).MemberId);
        Assert.Equal(2, w.Traversal.GetGrandparents(child.MemberId, graph).Count);
        Assert.Equal(2, w.Traversal.GetGrandchildren(grandfather.MemberId, graph).Count);
        Assert.Equal(4, w.Traversal.GetAncestors(child.MemberId, graph).Count);
        Assert.Equal(4, w.Traversal.GetDescendants(grandfather.MemberId, graph).Count);
        Assert.Empty(w.Traversal.GetAncestors(grandfather.MemberId, graph));
    }

    [Fact]
    public void Traversal_TerminatesEvenForMalformedCyclicSnapshot()
    {
        var a = w.Add("A"); var b = w.Add("B");
        MemberRelationshipDto Edge(Guid source, Guid target) => new(Guid.NewGuid(), source, RelationshipKind.Parent, target, w.Clock.Now, w.Clock.Now);
        var graph = new FamilyGraph([a, b], [Edge(a.MemberId, b.MemberId), Edge(b.MemberId, a.MemberId)]);
        Assert.Equal(b.MemberId, Assert.Single(w.Traversal.GetAncestors(a.MemberId, graph)).MemberId);
        Assert.Equal(b.MemberId, Assert.Single(w.Traversal.GetDescendants(a.MemberId, graph)).MemberId);
    }

    [Fact]
    public void MultipleFormerPartners_AreAllowedButOnePairCannotHaveTwoStates()
    {
        var a = w.Add("A"); var b = w.Add("B"); var c = w.Add("C");
        w.Link(a, RelationshipLabel.FormerSpouse, b);
        w.Link(c, RelationshipLabel.FormerSpouse, a);
        Assert.Equal(2, w.Members.GetGraph().Relationships.Count);
        var error = Assert.Throws<DomainValidationException>(() => w.Link(b, RelationshipLabel.Spouse, a));
        Assert.Equal("DuplicateRelationship", Assert.Single(error.Errors).Code);
    }
}
