using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FamilyTree.Core.Abstractions;
using FamilyTree.Core.DTOs;
using FamilyTree.Core.Models;
using FamilyTree.Core.Validation;

namespace FamilyTree.UI.ViewModels;

public sealed record DerivedMemberRow(string Relationship, string Name);

public partial class MainViewModel : ObservableObject
{
    private readonly IFamilyMemberService members;
    private readonly IRelationshipService relationships;
    private readonly IRelationshipResolver resolver;
    private readonly IFamilyTreeTraversalService traversal;
    private readonly IConfirmationService confirmation;
    private FamilyGraph graph = new([], []);
    public MemberEditorViewModel MemberEditor { get; }
    public RelationshipEditorViewModel RelationshipEditor { get; }
    [ObservableProperty] private IReadOnlyList<FamilyMemberDto> visibleMembers = [];
    [ObservableProperty] private FamilyMemberDto? selectedMember;
    [ObservableProperty] private ResolvedRelationshipDto? selectedRelationship;
    [ObservableProperty] private IReadOnlyList<ResolvedRelationshipDto> directRelationships = [];
    [ObservableProperty] private IReadOnlyList<DerivedMemberRow> derivedRelationships = [];
    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private string errorMessage = "";
    [ObservableProperty] private string statusMessage = "Loading family…";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool isBusy;
    public bool IsIdle => !IsBusy;

    public MainViewModel(IFamilyMemberService members, IRelationshipService relationships,
        IRelationshipResolver resolver, IFamilyTreeTraversalService traversal, IConfirmationService confirmation,
        MemberEditorViewModel memberEditor, RelationshipEditorViewModel relationshipEditor)
    {
        this.members = members; this.relationships = relationships; this.resolver = resolver;
        this.traversal = traversal; this.confirmation = confirmation;
        MemberEditor = memberEditor; RelationshipEditor = relationshipEditor;
        MemberEditor.RunAsync = RunAsync; RelationshipEditor.RunAsync = RunAsync;
        MemberEditor.SavedAsync = RefreshAsync; RelationshipEditor.SavedAsync = RefreshAsync;
    }

    public Task InitializeAsync() => RunAsync(() => RefreshAsync(null));

    internal async Task RunAsync(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = "";
        try { await action(); }
        catch (DomainValidationException ex) { ErrorMessage = string.Join(Environment.NewLine, ex.Errors.Select(e => e.Message)); }
        catch (Exception ex) { ErrorMessage = $"The operation could not be completed. {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task RefreshAsync(Guid? preferredId)
    {
        var previousId = preferredId ?? SelectedMember?.MemberId;
        graph = await Task.Run(members.GetGraph);
        // A successful save must remain visible even when its name no longer matches the filter.
        if (preferredId is not null) SearchText = "";
        FilterMembers();
        SelectedMember = VisibleMembers.FirstOrDefault(m => m.MemberId == previousId) ?? VisibleMembers.FirstOrDefault();
        UpdateDetails();
        StatusMessage = graph.Members.Count == 0 ? "Your family starts here. Add the first member." :
            $"{graph.Members.Count} members · {graph.Relationships.Count} recorded relationships";
    }

    partial void OnSearchTextChanged(string value)
    {
        FilterMembers();
        if (SelectedMember is not null && !VisibleMembers.Any(m => m.MemberId == SelectedMember.MemberId)) SelectedMember = null;
    }
    private void FilterMembers() => VisibleMembers = graph.Members
        .Where(m => m.Name.Contains(SearchText.Trim(), StringComparison.CurrentCultureIgnoreCase)).ToArray();

    partial void OnSelectedMemberChanged(FamilyMemberDto? value)
    {
        MemberEditor.CancelCommand.Execute(null);
        RelationshipEditor.CancelCommand.Execute(null);
        ErrorMessage = "";
        UpdateDetails();
        NotifyCommands();
    }
    partial void OnSelectedRelationshipChanged(ResolvedRelationshipDto? value) => NotifyCommands();
    partial void OnIsBusyChanged(bool value) => NotifyCommands();

    private void NotifyCommands()
    {
        EditMemberCommand.NotifyCanExecuteChanged(); DeleteMemberCommand.NotifyCanExecuteChanged();
        AddRelationshipCommand.NotifyCanExecuteChanged(); RemoveRelationshipCommand.NotifyCanExecuteChanged();
        MarkFormerSpouseCommand.NotifyCanExecuteChanged();
    }

    private void UpdateDetails()
    {
        SelectedRelationship = null;
        if (SelectedMember is not { } member) { DirectRelationships = []; DerivedRelationships = []; return; }
        var id = member.MemberId;
        DirectRelationships = resolver.Resolve(id, graph);
        var rows = new List<DerivedMemberRow>();
        void Add(IEnumerable<FamilyMemberDto> related, string male, string female) => rows.AddRange(related
            .Select(m => new DerivedMemberRow(m.Gender == Gender.Male ? male : female, m.Name)));
        Add(traversal.GetSiblings(id, graph), "Brother", "Sister");
        Add(traversal.GetGrandparents(id, graph), "Grandfather", "Grandmother");
        Add(traversal.GetGrandchildren(id, graph), "Grandson", "Granddaughter");
        Add(traversal.GetAncestors(id, graph), "Ancestor", "Ancestor");
        Add(traversal.GetDescendants(id, graph), "Descendant", "Descendant");
        DerivedRelationships = rows;
    }

    private bool HasMember() => !IsBusy && SelectedMember is not null;
    private bool HasRelationship() => !IsBusy && SelectedRelationship is not null;
    private bool HasSpouse() => HasRelationship() && SelectedRelationship!.Kind == RelationshipKind.Spouse;
    [RelayCommand]
    private Task ReloadAsync()
    {
        MemberEditor.CancelCommand.Execute(null);
        RelationshipEditor.CancelCommand.Execute(null);
        return RunAsync(() => RefreshAsync(null));
    }
    [RelayCommand] private void AddMember() { ErrorMessage = ""; RelationshipEditor.IsOpen = false; MemberEditor.Open(); }
    [RelayCommand(CanExecute = nameof(HasMember))]
    private void EditMember() { ErrorMessage = ""; RelationshipEditor.IsOpen = false; MemberEditor.Open(SelectedMember); }
    [RelayCommand(CanExecute = nameof(HasMember))]
    private void AddRelationship() { ErrorMessage = ""; MemberEditor.IsOpen = false; RelationshipEditor.Open(SelectedMember!, graph.Members); }

    [RelayCommand(CanExecute = nameof(HasMember))]
    private Task DeleteMemberAsync()
    {
        var member = SelectedMember!;
        if (!confirmation.Confirm("Delete member", $"Permanently delete {member.Name}? Members with relationships cannot be deleted.")) return Task.CompletedTask;
        return RunAsync(async () => { await Task.Run(() => members.Delete(member.MemberId)); await RefreshAsync(null); });
    }

    [RelayCommand(CanExecute = nameof(HasRelationship))]
    private Task RemoveRelationshipAsync()
    {
        var relationship = SelectedRelationship!;
        if (!confirmation.Confirm("Remove incorrect relationship", $"Remove the {relationship.Label.ToLowerInvariant()} relationship with {relationship.Member.Name}? Use ‘Mark as former spouse’ to record a divorce.")) return Task.CompletedTask;
        return RunAsync(async () => { await Task.Run(() => relationships.Remove(relationship.RelationshipId)); await RefreshAsync(null); });
    }

    [RelayCommand(CanExecute = nameof(HasSpouse))]
    private Task MarkFormerSpouseAsync()
    {
        var relationship = SelectedRelationship!;
        if (!confirmation.Confirm("Record divorce", $"Mark {relationship.Member.Name} as a former spouse? Both members will be marked Divorced.")) return Task.CompletedTask;
        return RunAsync(async () => { await Task.Run(() => relationships.MarkAsFormerSpouse(relationship.RelationshipId)); await RefreshAsync(null); });
    }
}
