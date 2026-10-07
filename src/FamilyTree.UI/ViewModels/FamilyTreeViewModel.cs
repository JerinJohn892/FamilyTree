using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FamilyTree.Core.Abstractions;
using FamilyTree.Core.DTOs;
using FamilyTree.Core.Models;

namespace FamilyTree.UI.ViewModels;

public sealed record TreeMemberPresentation(FamilyMemberDto Member)
{
    public string Color => Member.Gender == Gender.Male ? "#DDEFFC" : "#FBE2EC";
    public string Detail => Member.DateOfBirth is { } birth ? $"{birth:yyyy-MM-dd} · {Member.LifeStatus}" : Member.LifeStatus.ToString();
}
public sealed record TreeCardPresentation(TreeCard Card)
{
    public IReadOnlyList<TreeMemberPresentation> Members => Card.Members.Select(m => new TreeMemberPresentation(m)).ToArray();
}
public sealed record TreeBranchPresentation(string Path);

public partial class FamilyTreeViewModel(IFamilyMemberService members, IFamilyTreeLayoutService layout) : ObservableObject
{
    [ObservableProperty] private IReadOnlyList<TreeCardPresentation> cards = [];
    [ObservableProperty] private IReadOnlyList<TreeBranchPresentation> branches = [];
    [ObservableProperty] private double width = 400;
    [ObservableProperty] private double height = 300;
    [ObservableProperty] private string notice = "Loading saved family…";
    [ObservableProperty] private string errorMessage = "";
    [ObservableProperty] private bool isBusy;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        ErrorMessage = "";
        try
        {
            var result = await Task.Run(() => layout.Layout(members.GetGraph()));
            Cards = result.Cards.Select(c => new TreeCardPresentation(c)).ToArray();
            Branches = result.Branches.Select(b => new TreeBranchPresentation(ToPath(b))).ToArray();
            Width = result.Width; Height = result.Height; Notice = result.Notice;
        }
        catch (Exception ex) { ErrorMessage = $"Unable to load the saved family tree. {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private static string ToPath(TreeBranch branch)
    {
        string Point(TreePoint p) => string.Create(CultureInfo.InvariantCulture, $"{p.X},{p.Y}");
        var end = branch.Points[^1];
        return "M " + string.Join(" L ", branch.Points.Select(Point)) +
            $" M {Point(new(end.X - 5, end.Y - 8))} L {Point(end)} L {Point(new(end.X + 5, end.Y - 8))}";
    }
}
