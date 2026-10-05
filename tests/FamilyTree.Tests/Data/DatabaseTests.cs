using FamilyTree.Core.DTOs;
using FamilyTree.Core.Models;
using FamilyTree.Core.Services;
using FamilyTree.Core.Validation;
using FamilyTree.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FamilyTree.Tests.Data;

public sealed class DatabaseTests : IDisposable
{
    private readonly TestWorkspace w = new();
    public void Dispose() => w.Dispose();

    [Fact]
    public void Reopening_PreservesMembersFactsAndMigrationSeeds()
    {
        var member = w.Add("O'Connor; DROP TABLE FamilyMember;--", Gender.Female);
        member = w.Members.Update(member.MemberId, TestWorkspace.Edit(member) with { DateOfBirth = new DateOnly(1988, 2, 29) });
        var child = w.Add("Child");
        var relationship = w.Link(member, RelationshipLabel.Mother, child);
        var reopened = new SqliteDatabase(w.Database.DatabasePath);
        reopened.Initialize(); reopened.Initialize();
        using var unit = reopened.Open();
        Assert.Equal(member, unit.Members.Get(member.MemberId));
        Assert.Equal(relationship, Assert.Single(unit.Relationships.List()));
        Assert.Equal(new[] { RelationshipKind.Parent, RelationshipKind.Spouse, RelationshipKind.FormerSpouse }, unit.RelationshipTypes.List().Select(t => t.Kind));
        Assert.All(unit.RelationshipTypes.List(), t => Assert.True(t.IsActive && t.IsPrimary && !t.IsCustom));
    }

    [Fact]
    public void UncommittedWork_RollsBack()
    {
        var member = w.Add("Before");
        using (var unit = w.Database.Open(write: true)) unit.Members.Update(member with { Name = "Uncommitted" });
        Assert.Equal("Before", w.Members.Get(member.MemberId).Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedMemberStatusWrite_RollsBackLinkAndBothMemberStatuses(bool divorce)
    {
        var john = w.Add("John"); var mary = w.Add("Mary", Gender.Female);
        var marriage = divorce ? w.Link(john, RelationshipLabel.Spouse, mary) : null;
        w.ExecuteSql($"""
            CREATE TRIGGER FailMaryStatus BEFORE UPDATE OF MaritalStatus ON FamilyMember
            WHEN NEW.MemberId = '{mary.MemberId:D}' BEGIN SELECT RAISE(ABORT, 'Injected write failure'); END;
            """);
        if (marriage is null)
            Assert.Throws<SqliteException>(() => w.Link(john, RelationshipLabel.Spouse, mary));
        else
            Assert.Throws<SqliteException>(() => w.Relationships.MarkAsFormerSpouse(marriage.MemberRelationshipId));
        var graph = w.Members.GetGraph();
        Assert.All(graph.Members, m => Assert.Equal(divorce ? MaritalStatus.Married : MaritalStatus.Single, m.MaritalStatus));
        if (divorce) Assert.Equal(RelationshipKind.Spouse, Assert.Single(graph.Relationships).Kind);
        else Assert.Empty(graph.Relationships);
    }

    [Fact]
    public async Task ConcurrentFathers_OnlyOneWriteCanPassValidation()
    {
        var first = w.Add("First father"); var second = w.Add("Second father"); var child = w.Add("Child");
        using var gate = new ManualResetEventSlim(false);
        Task<Exception?> Attempt(FamilyMemberDto parent) => Task.Run<Exception?>(() =>
        {
            gate.Wait();
            return Record.Exception(() => w.Link(parent, RelationshipLabel.Parent, child));
        });
        var one = Attempt(first); var two = Attempt(second);
        gate.Set();
        var results = await Task.WhenAll(one, two);
        Assert.Single(results, e => e is null);
        var error = Assert.IsType<DomainValidationException>(Assert.Single(results, e => e is not null));
        Assert.Equal("SecondFather", Assert.Single(error.Errors).Code);
        Assert.Single(w.Members.GetGraph().Relationships);
    }

    [Fact]
    public async Task ConcurrentSpouses_OnlyOneMarriageIsCommitted()
    {
        var a = w.Add("A"); var b = w.Add("B"); var c = w.Add("C");
        using var gate = new ManualResetEventSlim(false);
        Task<Exception?> Attempt(FamilyMemberDto partner) => Task.Run<Exception?>(() =>
        {
            gate.Wait();
            return Record.Exception(() => w.Link(a, RelationshipLabel.Spouse, partner));
        });
        var one = Attempt(b); var two = Attempt(c); gate.Set();
        var results = await Task.WhenAll(one, two);
        Assert.Single(results, e => e is null);
        var error = Assert.IsType<DomainValidationException>(Assert.Single(results, e => e is not null));
        Assert.Equal("ActiveSpouseExists", Assert.Single(error.Errors).Code);
        Assert.Single(w.Members.GetGraph().Relationships);
        Assert.Equal(2, w.Members.List().Count(m => m.MaritalStatus == MaritalStatus.Married));
    }

    [Fact]
    public void ForeignKeysAndDeleteRestrictions_AreEnabledOnEveryConnection()
    {
        var a = w.Add("A"); var b = w.Add("B");
        w.Link(a, RelationshipLabel.Parent, b);
        using (var unit = w.Database.Open(write: true))
        {
            Assert.Throws<SqliteException>(() => unit.Members.Delete(a.MemberId));
            Assert.Throws<SqliteException>(() => unit.Relationships.Add(new(Guid.NewGuid(), a.MemberId,
                RelationshipKind.Parent, Guid.NewGuid(), w.Clock.Now, w.Clock.Now)));
        }
        using var another = w.Database.Open(write: true);
        Assert.Throws<SqliteException>(() => another.Members.Delete(b.MemberId));
    }

    [Fact]
    public void Schema_EnforcesCanonicalPairsUniquenessAndNoSelfLinks()
    {
        var a = w.Add("A"); var b = w.Add("B");
        var ids = new[] { a.MemberId, b.MemberId }.OrderBy(id => id.ToString("D"), StringComparer.Ordinal).ToArray();
        MemberRelationshipDto Link(Guid source, RelationshipKind kind, Guid target) => new(Guid.NewGuid(), source, kind, target, w.Clock.Now, w.Clock.Now);
        using var unit = w.Database.Open(write: true);
        Assert.Throws<SqliteException>(() => unit.Relationships.Add(Link(ids[1], RelationshipKind.Spouse, ids[0])));
        Assert.Throws<SqliteException>(() => unit.Relationships.Add(Link(ids[0], RelationshipKind.Parent, ids[0])));
        Assert.Throws<SqliteException>(() => unit.Relationships.Add(Link(ids[0], (RelationshipKind)99, ids[1])));
        var spouse = Link(ids[0], RelationshipKind.Spouse, ids[1]);
        unit.Relationships.Add(spouse);
        Assert.Throws<SqliteException>(() => unit.Relationships.Add(spouse with { MemberRelationshipId = Guid.NewGuid() }));
        Assert.Throws<SqliteException>(() => unit.Relationships.Add(Link(ids[0], RelationshipKind.FormerSpouse, ids[1])));
    }

    [Fact]
    public void Schema_EnforcesNamesEnumsAndBooleanMetadata()
    {
        var member = w.Add("Valid");
        using (var unit = w.Database.Open(write: true))
        {
            Assert.Throws<SqliteException>(() => unit.Members.Update(member with { Name = "" }));
            Assert.Throws<SqliteException>(() => unit.Members.Update(member with { Name = " Untrimmed " }));
            Assert.Throws<SqliteException>(() => unit.Members.Update(member with { Gender = (Gender)99 }));
            Assert.Throws<SqliteException>(() => unit.Members.Update(member with { LifeStatus = (LifeStatus)99 }));
            Assert.Throws<SqliteException>(() => unit.Members.Update(member with { MaritalStatus = (MaritalStatus)99 }));
        }
        Assert.Throws<SqliteException>(() => w.ExecuteSql("UPDATE RelationshipType SET IsActive=7 WHERE RelationshipTypeId=1"));
    }

    [Fact]
    public void NewerSchema_IsRejectedWithoutChangingData()
    {
        var a = w.Add("Retained");
        w.ExecuteSql("PRAGMA user_version=999;");
        Assert.Throws<InvalidOperationException>(w.Database.Initialize);
        Assert.Equal(a, w.Members.Get(a.MemberId));
    }
}
