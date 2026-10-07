using FamilyTree.Core.Models;
using FamilyTree.Core.Services;
using FamilyTree.Core.DTOs;
using FamilyTree.UI.ViewModels;
using Xunit;

namespace FamilyTree.Tests.Services;

public sealed class TreeLayoutTests : IDisposable
{
    private readonly TestWorkspace w = new();
    private readonly FamilyTreeLayoutService layout = new();
    public void Dispose() => w.Dispose();

    [Fact]
    public async Task PersistedThreeGenerations_MarriedSonAndDaughter_AttachToTheirOwnHalves()
    {
        var john = w.Add("John"); var mary = w.Add("Mary", Gender.Female);
        var michael = w.Add("Michael"); var anna = w.Add("Anna", Gender.Female);
        var susan = w.Add("Susan", Gender.Female); var david = w.Add("David"); var baby = w.Add("Baby");
        w.Link(john, RelationshipLabel.Spouse, mary);
        foreach (var child in new[] { michael, anna })
        { w.Link(john, RelationshipLabel.Parent, child); w.Link(mary, RelationshipLabel.Parent, child); }
        w.Link(michael, RelationshipLabel.Spouse, susan); w.Link(anna, RelationshipLabel.Spouse, david);
        w.Link(michael, RelationshipLabel.Parent, baby); w.Link(susan, RelationshipLabel.Parent, baby);
        var reopened = new FamilyTree.Data.SqliteDatabase(w.Database.DatabasePath);
        reopened.Initialize();
        FamilyGraph graph;
        using (var unit = reopened.Open()) graph = new FamilyGraph(unit.Members.List(), unit.Relationships.List());
        Assert.Equal(7, graph.Members.Count); Assert.Equal(9, graph.Relationships.Count);
        var result = layout.Layout(graph);
        TreeCard Card(Guid id) => Assert.Single(result.Cards, c => c.Members.Any(m => m.MemberId == id));
        Assert.Equal(4, result.Cards.Count);
        Assert.Equal(3, result.Branches.Count);
        Assert.Equal(new[] { michael.MemberId, susan.MemberId }, Card(michael.MemberId).Members.Select(m => m.MemberId));
        Assert.Equal(new[] { david.MemberId, anna.MemberId }, Card(anna.MemberId).Members.Select(m => m.MemberId));
        foreach (var (child, offset) in new[] { (michael, 90d), (anna, 270d), (baby, 90d) })
        {
            var card = Card(child.MemberId);
            var end = Assert.Single(result.Branches, b => b.ChildId == child.MemberId).Points[^1];
            Assert.Equal(card.X + offset, end.X); Assert.Equal(card.Y, end.Y);
        }
        Assert.True(Card(john.MemberId).Y < Card(michael.MemberId).Y);
        Assert.True(Card(michael.MemberId).Y < Card(baby.MemberId).Y);
        AssertNoOverlap(result);
        Assert.Equal(result.Cards.Select(c => (c.X,c.Y)), layout.Layout(graph with
        { Members = graph.Members.Reverse().ToArray(), Relationships = graph.Relationships.Reverse().ToArray() }).Cards.Select(c => (c.X,c.Y)));
        var vm = new FamilyTreeViewModel(w.Members, layout);
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Empty(vm.ErrorMessage); Assert.Equal(4, vm.Cards.Count);
        w.Add("New saved member", Gender.Female);
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(5, vm.Cards.Count);
    }

    [Fact]
    public void EmptyDisconnectedAndFormerSpouseFamilies_AreNotLost()
    {
        Assert.Empty(layout.Layout(w.Members.GetGraph()).Cards);
        var dad = w.Add("Father"); var mom = w.Add("Mother", Gender.Female); var child = w.Add("Child");
        var marriage = w.Link(dad, RelationshipLabel.Spouse, mom);
        w.Relationships.MarkAsFormerSpouse(marriage.MemberRelationshipId);
        w.Link(dad, RelationshipLabel.Parent, child); w.Link(mom, RelationshipLabel.Parent, child);
        for (var i = 0; i < 25; i++) w.Add($"Disconnected {i}");
        var result = layout.Layout(w.Members.GetGraph());
        Assert.Equal(28, result.Cards.Count); Assert.Equal(2, result.Branches.Count); AssertNoOverlap(result);
    }

    [Fact]
    public void ContractedMarriageCycle_FallsBackWithoutLosingAncestry()
    {
        var a = w.Add("A"); var b = w.Add("B", Gender.Female);
        var c = w.Add("C"); var d = w.Add("D", Gender.Female);
        w.Link(a, RelationshipLabel.Parent, c); w.Link(d, RelationshipLabel.Parent, b);
        w.Link(a, RelationshipLabel.Spouse, b); w.Link(c, RelationshipLabel.Spouse, d);
        var result = layout.Layout(w.Members.GetGraph());
        Assert.Equal(4, result.Cards.Count); Assert.Equal(2, result.Branches.Count);
        Assert.Contains("span generations", result.Notice); AssertNoOverlap(result);
    }

    private static void AssertNoOverlap(TreeLayout result)
    {
        foreach (var row in result.Cards.GroupBy(c => c.Y))
        {
            var sorted = row.OrderBy(c => c.X).ToArray();
            for (var i = 1; i < sorted.Length; i++) Assert.True(sorted[i-1].X + sorted[i-1].Width < sorted[i].X);
        }
    }
}
