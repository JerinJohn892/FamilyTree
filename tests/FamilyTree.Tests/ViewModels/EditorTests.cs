using FamilyTree.Core.Models;
using FamilyTree.Core.Services;
using FamilyTree.UI.ViewModels;
using Xunit;

namespace FamilyTree.Tests.ViewModels;

public sealed class EditorTests : IDisposable
{
    private sealed class TreeWindowStub : ITreeWindowService { public Task ShowAsync() => Task.CompletedTask; }
    private readonly TestWorkspace w = new();
    private readonly ConfirmationStub confirmation = new();
    private readonly MainViewModel vm;

    public EditorTests() => vm = new(w.Members, w.Relationships, w.Resolver, w.Traversal, confirmation,
        new MemberEditorViewModel(w.Members), new RelationshipEditorViewModel(w.Relationships), new TreeWindowStub());
    public void Dispose() => w.Dispose();

    [Fact]
    public async Task EmptyFamily_CanCreateAndSelectFirstMember()
    {
        await vm.InitializeAsync();
        Assert.Empty(vm.VisibleMembers);
        Assert.False(vm.EditMemberCommand.CanExecute(null));
        vm.AddMemberCommand.Execute(null);
        vm.MemberEditor.Name = "Anna";
        vm.MemberEditor.Gender = Gender.Female;
        vm.MemberEditor.DateOfBirth = new DateTime(2000, 1, 2);
        await vm.MemberEditor.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Anna", vm.SelectedMember!.Name);
        Assert.Equal(new DateOnly(2000, 1, 2), vm.SelectedMember.DateOfBirth);
        Assert.False(vm.MemberEditor.IsOpen);
        Assert.True(vm.EditMemberCommand.CanExecute(null));
        Assert.Empty(vm.ErrorMessage);
        Assert.True(vm.IsIdle);
    }

    [Fact]
    public async Task Refresh_DiscardsUnsavedEditorEvenWhenSelectedMemberIsUnchanged()
    {
        w.Add("John");
        await vm.InitializeAsync();
        vm.EditMemberCommand.Execute(null);
        vm.MemberEditor.Name = "Unsaved";
        await vm.ReloadCommand.ExecuteAsync(null);
        Assert.False(vm.MemberEditor.IsOpen);
        Assert.Equal("John", vm.SelectedMember!.Name);
        Assert.Equal("John", Assert.Single(w.Members.List()).Name);
    }

    [Fact]
    public async Task InvalidSave_LeavesEditorOpenAndReportsError()
    {
        await vm.InitializeAsync();
        vm.AddMemberCommand.Execute(null);
        await vm.MemberEditor.SaveCommand.ExecuteAsync(null);
        Assert.True(vm.MemberEditor.IsOpen);
        Assert.Contains("name", vm.ErrorMessage);
        Assert.Empty(w.Members.List());
        vm.MemberEditor.Name = "Anna";
        vm.MemberEditor.DateInputError = "Invalid date";
        await vm.MemberEditor.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Invalid date", vm.ErrorMessage);
        Assert.Empty(w.Members.List());
        vm.MemberEditor.CancelCommand.Execute(null);
        Assert.False(vm.MemberEditor.IsOpen);
    }

    [Fact]
    public async Task RenameWhileFiltered_KeepsSavedMemberVisible()
    {
        w.Add("John"); w.Add("Mary", Gender.Female);
        await vm.InitializeAsync();
        vm.SearchText = "John";
        vm.SelectedMember = Assert.Single(vm.VisibleMembers);
        vm.EditMemberCommand.Execute(null);
        vm.MemberEditor.Name = "Jonathan";
        await vm.MemberEditor.SaveCommand.ExecuteAsync(null);
        Assert.Empty(vm.SearchText);
        Assert.Equal("Jonathan", vm.SelectedMember!.Name);
        Assert.Equal(2, vm.VisibleMembers.Count);
        Assert.False(vm.MemberEditor.IsOpen);
    }

    [Fact]
    public async Task RelationshipEditor_UsesSourceIsLabelOfTargetAndRefreshesDetails()
    {
        var john = w.Add("John"); var michael = w.Add("Michael");
        await vm.InitializeAsync();
        vm.SelectedMember = vm.VisibleMembers.Single(m => m.MemberId == michael.MemberId);
        vm.AddRelationshipCommand.Execute(null);
        Assert.DoesNotContain(vm.RelationshipEditor.Candidates, m => m.MemberId == michael.MemberId);
        vm.RelationshipEditor.Target = john;
        vm.RelationshipEditor.SelectedRule = RelationshipRuleCatalog.Rules.Single(r => r.Label == RelationshipLabel.Son);
        await vm.RelationshipEditor.SaveCommand.ExecuteAsync(null);
        var relation = Assert.Single(vm.DirectRelationships);
        Assert.Equal("Father", relation.Label);
        Assert.Equal(john.MemberId, relation.Member.MemberId);
        Assert.False(vm.RelationshipEditor.IsOpen);
    }

    [Fact]
    public async Task DivorceAndCorrectionCommands_AreDistinctAndRefreshBothMembers()
    {
        var john = w.Add("John"); var mary = w.Add("Mary", Gender.Female);
        w.Link(john, RelationshipLabel.Spouse, mary);
        await vm.InitializeAsync();
        vm.SelectedMember = vm.VisibleMembers.Single(m => m.MemberId == john.MemberId);
        vm.SelectedRelationship = Assert.Single(vm.DirectRelationships);
        Assert.True(vm.MarkFormerSpouseCommand.CanExecute(null));
        await vm.MarkFormerSpouseCommand.ExecuteAsync(null);
        Assert.All(vm.VisibleMembers, m => Assert.Equal(MaritalStatus.Divorced, m.MaritalStatus));
        vm.SelectedRelationship = Assert.Single(vm.DirectRelationships);
        Assert.Equal("Ex-wife", vm.SelectedRelationship.Label);
        Assert.False(vm.MarkFormerSpouseCommand.CanExecute(null));
        await vm.RemoveRelationshipCommand.ExecuteAsync(null);
        Assert.Empty(vm.DirectRelationships);
        Assert.All(vm.VisibleMembers, m => Assert.Equal(MaritalStatus.Single, m.MaritalStatus));
    }

    [Fact]
    public async Task CancelledDeleteAndRestrictedDelete_DoNotLoseMembers()
    {
        var john = w.Add("John"); var child = w.Add("Child");
        w.Link(john, RelationshipLabel.Parent, child);
        await vm.InitializeAsync();
        confirmation.Answer = false;
        await vm.DeleteMemberCommand.ExecuteAsync(null);
        Assert.Equal(2, w.Members.List().Count);
        Assert.Empty(vm.ErrorMessage);
        confirmation.Answer = true;
        await vm.DeleteMemberCommand.ExecuteAsync(null);
        Assert.Contains("relationships", vm.ErrorMessage);
        Assert.Equal(2, vm.VisibleMembers.Count);
        Assert.True(vm.IsIdle);
    }

    private sealed class ConfirmationStub : IConfirmationService
    {
        public bool Answer { get; set; } = true;
        public bool Confirm(string title, string message) => Answer;
    }
}
