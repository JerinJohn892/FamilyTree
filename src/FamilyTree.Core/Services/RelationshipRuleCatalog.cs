using FamilyTree.Core.DTOs;
using FamilyTree.Core.Models;
using FamilyTree.Core.Validation;

namespace FamilyTree.Core.Services;

public sealed record RelationshipRule(RelationshipLabel Label, string DisplayName,
    RelationshipKind Kind, bool Reverse = false, Gender? RequiredSourceGender = null);

public static class RelationshipRuleCatalog
{
    public static IReadOnlyList<RelationshipRule> Rules { get; } = Array.AsReadOnly(new[]
    {
        new RelationshipRule(RelationshipLabel.Parent, "Parent", RelationshipKind.Parent),
        new RelationshipRule(RelationshipLabel.Father, "Father", RelationshipKind.Parent, RequiredSourceGender: Gender.Male),
        new RelationshipRule(RelationshipLabel.Mother, "Mother", RelationshipKind.Parent, RequiredSourceGender: Gender.Female),
        new RelationshipRule(RelationshipLabel.Son, "Son", RelationshipKind.Parent, true, Gender.Male),
        new RelationshipRule(RelationshipLabel.Daughter, "Daughter", RelationshipKind.Parent, true, Gender.Female),
        new RelationshipRule(RelationshipLabel.Spouse, "Spouse", RelationshipKind.Spouse),
        new RelationshipRule(RelationshipLabel.Husband, "Husband", RelationshipKind.Spouse, RequiredSourceGender: Gender.Male),
        new RelationshipRule(RelationshipLabel.Wife, "Wife", RelationshipKind.Spouse, RequiredSourceGender: Gender.Female),
        new RelationshipRule(RelationshipLabel.FormerSpouse, "Former spouse", RelationshipKind.FormerSpouse),
        new RelationshipRule(RelationshipLabel.ExHusband, "Ex-husband", RelationshipKind.FormerSpouse, RequiredSourceGender: Gender.Male),
        new RelationshipRule(RelationshipLabel.ExWife, "Ex-wife", RelationshipKind.FormerSpouse, RequiredSourceGender: Gender.Female)
    });

    public static (Guid Source, RelationshipKind Kind, Guid Target) Normalize(
        AddRelationshipRequest request, FamilyMemberDto source)
    {
        var rule = Rules.SingleOrDefault(r => r.Label == request.Label)
            ?? throw DomainValidationException.For("UnsupportedRelationship", "Label", "Choose a supported relationship.");
        if (rule.RequiredSourceGender is { } gender && source.Gender != gender)
            throw DomainValidationException.For("GenderMismatch", "Label", $"{source.Name} cannot be entered as {rule.DisplayName} with the recorded gender.");
        var (a, b) = rule.Reverse ? (request.TargetMemberId, request.SourceMemberId) : (request.SourceMemberId, request.TargetMemberId);
        if (rule.Kind != RelationshipKind.Parent && string.CompareOrdinal(a.ToString("D"), b.ToString("D")) > 0)
            (a, b) = (b, a);
        return (a, rule.Kind, b);
    }

    public static string Describe(RelationshipKind kind, Gender gender, bool isParent = false) => kind switch
    {
        RelationshipKind.Parent when isParent => gender == Gender.Male ? "Father" : "Mother",
        RelationshipKind.Parent => gender == Gender.Male ? "Son" : "Daughter",
        RelationshipKind.Spouse => gender == Gender.Male ? "Husband" : "Wife",
        RelationshipKind.FormerSpouse => gender == Gender.Male ? "Ex-husband" : "Ex-wife",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
