using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FamilyTree.Core.Abstractions;
using FamilyTree.Core.DTOs;
using FamilyTree.Core.Models;
using FamilyTree.Core.Validation;

namespace FamilyTree.UI.ViewModels;

public partial class MemberEditorViewModel(IFamilyMemberService members) : ObservableObject
{
    private Guid? memberId;
    internal Func<Func<Task>, Task> RunAsync { get; set; } = action => action();
    internal Func<Guid?, Task> SavedAsync { get; set; } = _ => Task.CompletedTask;
    [ObservableProperty] private bool isOpen;
    [ObservableProperty] private string title = "Add member";
    [ObservableProperty] private string name = "";
    [ObservableProperty] private DateTime? dateOfBirth;
    [ObservableProperty] private Gender gender = Gender.Male;
    [ObservableProperty] private LifeStatus lifeStatus = LifeStatus.Living;
    [ObservableProperty] private MaritalStatus maritalStatus = MaritalStatus.Single;
    [ObservableProperty] private string dateInputError = "";
    public Array Genders { get; } = Enum.GetValues<Gender>();
    public Array LifeStatuses { get; } = Enum.GetValues<LifeStatus>();
    public Array MaritalStatuses { get; } = Enum.GetValues<MaritalStatus>();

    public void Open(FamilyMemberDto? member = null)
    {
        memberId = member?.MemberId;
        Title = member is null ? "Add member" : "Edit member";
        Name = member?.Name ?? "";
        DateOfBirth = member?.DateOfBirth?.ToDateTime(TimeOnly.MinValue);
        Gender = member?.Gender ?? Gender.Male;
        LifeStatus = member?.LifeStatus ?? LifeStatus.Living;
        MaritalStatus = member?.MaritalStatus ?? MaritalStatus.Single;
        DateInputError = "";
        IsOpen = true;
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        if (DateInputError.Length > 0)
            throw DomainValidationException.For("InvalidBirthDate", "DateOfBirth", DateInputError);
        var request = new SaveMemberRequest(Name, DateOfBirth is { } birth ? DateOnly.FromDateTime(birth) : null, Gender, LifeStatus, MaritalStatus);
        var id = memberId;
        var saved = await Task.Run(() => id is { } existing ? members.Update(existing, request) : members.Create(request));
        IsOpen = false;
        await SavedAsync(saved.MemberId);
    });

    [RelayCommand] private void Cancel() { IsOpen = false; DateInputError = ""; }
}
