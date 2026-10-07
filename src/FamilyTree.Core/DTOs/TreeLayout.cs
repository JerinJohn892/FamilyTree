namespace FamilyTree.Core.DTOs;

public sealed record TreeCard(IReadOnlyList<FamilyMemberDto> Members, double X, double Y, double Width, double Height);
public sealed record TreePoint(double X, double Y);
public sealed record TreeBranch(Guid ChildId, IReadOnlyList<TreePoint> Points);
public sealed record TreeLayout(IReadOnlyList<TreeCard> Cards, IReadOnlyList<TreeBranch> Branches,
    double Width, double Height, string Notice);
