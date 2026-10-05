using FamilyTree.Core.DTOs;
using FamilyTree.Core.Models;

namespace FamilyTree.Core.Validation;

public sealed class TreeIntegrityValidator
{
    public void Validate(FamilyGraph graph)
    {
        var members = graph.Members.ToDictionary(m => m.MemberId);
        var parents = graph.Relationships.Where(r => r.Kind == RelationshipKind.Parent).ToArray();
        foreach (var child in parents.GroupBy(r => r.TargetMemberId))
        {
            foreach (var gender in new[] { Gender.Male, Gender.Female })
                if (child.Count(p => members[p.SourceMemberId].Gender == gender) > 1)
                    throw DomainValidationException.For(gender == Gender.Male ? "SecondFather" : "SecondMother",
                        "TargetMemberId", $"{members[child.Key].Name} already has a {(gender == Gender.Male ? "father" : "mother")}.");
        }

        var children = parents.ToLookup(r => r.SourceMemberId, r => r.TargetMemberId);
        // Kahn's algorithm avoids recursive stack growth on large or malformed trees.
        var incoming = members.Keys.ToDictionary(id => id, _ => 0);
        foreach (var p in parents) incoming[p.TargetMemberId]++;
        var ready = new Queue<Guid>(incoming.Where(x => x.Value == 0).Select(x => x.Key));
        var visited = 0;
        while (ready.TryDequeue(out var id))
        {
            visited++;
            foreach (var child in children[id])
                if (--incoming[child] == 0) ready.Enqueue(child);
        }
        if (visited != members.Count)
            throw DomainValidationException.For("AncestryCycle", "TargetMemberId", "This relationship would create an ancestry cycle.");

        var spouses = graph.Relationships.Where(r => r.Kind != RelationshipKind.Parent).ToArray();
        var active = spouses.Where(r => r.Kind == RelationshipKind.Spouse)
            .SelectMany(r => new[] { r.SourceMemberId, r.TargetMemberId });
        if (active.GroupBy(id => id).Any(g => g.Count() > 1))
            throw DomainValidationException.For("ActiveSpouseExists", "TargetMemberId", "A member may have only one active spouse. Record the divorce first.");
        foreach (var spouse in spouses)
            if (Reaches(spouse.SourceMemberId, spouse.TargetMemberId, children) || Reaches(spouse.TargetMemberId, spouse.SourceMemberId, children))
                throw DomainValidationException.For("SpouseAncestryConflict", "TargetMemberId", "Current or former spouses cannot be ancestors or descendants of each other.");
    }

    private static bool Reaches(Guid start, Guid target, ILookup<Guid, Guid> children)
    {
        var visited = new HashSet<Guid> { start };
        var pending = new Stack<Guid>();
        pending.Push(start);
        while (pending.TryPop(out var id))
            foreach (var child in children[id])
            {
                if (child == target) return true;
                if (visited.Add(child)) pending.Push(child);
            }
        return false;
    }
}
