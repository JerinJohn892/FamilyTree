using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FamilyTree.Core.Abstractions;
using FamilyTree.Core.DTOs;
using FamilyTree.Core.Services;
using FamilyTree.Core.Validation;

namespace FamilyTree.UI.ViewModels;

public partial class RelationshipEditorViewModel(IRelationshipService relationships) : ObservableObject
{
    internal Func<Func<Task>, Task> RunAsync { get; set; } = action => action();
    internal Func<Guid?, Task> SavedAsync { get; set; } = _ => Task.CompletedTask;
    [ObservableProperty] private bool isOpen;
    [ObservableProperty] private FamilyMemberDto? source;
    [ObservableProperty] private FamilyMemberDto? target;
    [ObservableProperty] private RelationshipRule? selectedRule = RelationshipRuleCatalog.Rules[0];
    [ObservableProperty] private IReadOnlyList<FamilyMemberDto> candidates = [];
    public IReadOnlyList<RelationshipRule> Rules => RelationshipRuleCatalog.Rules;

    public void Open(FamilyMemberDto source, IReadOnlyList<FamilyMemberDto> members)
    {
        Source = source;
        Candidates = members.Where(m => m.MemberId != source.MemberId).ToArray();
        Target = null;
        SelectedRule = Rules[0];
        IsOpen = true;
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        if (Source is null || Target is null || SelectedRule is null)
            throw DomainValidationException.For("RelationshipSelectionRequired", "TargetMemberId", "Choose a relationship and another member.");
        var request = new AddRelationshipRequest(Source.MemberId, SelectedRule.Label, Target.MemberId);
        await Task.Run(() => relationships.Add(request));
        IsOpen = false;
        await SavedAsync(request.SourceMemberId);
    });

    [RelayCommand] private void Cancel() => IsOpen = false;
}
