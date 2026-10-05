using FamilyTree.Core.Abstractions;
using FamilyTree.Core.DTOs;
using FamilyTree.Core.Models;

namespace FamilyTree.Core.Services;

public sealed class FamilyTreeTraversalService : IFamilyTreeTraversalService
{
    public IReadOnlyList<FamilyMemberDto> GetParents(Guid id, FamilyGraph graph) => Select(graph, Adjacent(id, graph, true));
    public IReadOnlyList<FamilyMemberDto> GetChildren(Guid id, FamilyGraph graph) => Select(graph, Adjacent(id, graph, false));
    public IReadOnlyList<FamilyMemberDto> GetSiblings(Guid id, FamilyGraph graph) => Select(graph,
        Adjacent(id, graph, true).SelectMany(parent => Adjacent(parent, graph, false)).Where(child => child != id));
    public IReadOnlyList<FamilyMemberDto> GetGrandparents(Guid id, FamilyGraph graph) => Select(graph,
        Adjacent(id, graph, true).SelectMany(parent => Adjacent(parent, graph, true)));
    public IReadOnlyList<FamilyMemberDto> GetGrandchildren(Guid id, FamilyGraph graph) => Select(graph,
        Adjacent(id, graph, false).SelectMany(child => Adjacent(child, graph, false)));
    public IReadOnlyList<FamilyMemberDto> GetAncestors(Guid id, FamilyGraph graph) => Walk(id, graph, true);
    public IReadOnlyList<FamilyMemberDto> GetDescendants(Guid id, FamilyGraph graph) => Walk(id, graph, false);

    private static IEnumerable<Guid> Adjacent(Guid id, FamilyGraph graph, bool upwards) => graph.Relationships
        .Where(r => r.Kind == RelationshipKind.Parent && (upwards ? r.TargetMemberId : r.SourceMemberId) == id)
        .Select(r => upwards ? r.SourceMemberId : r.TargetMemberId);

    private static IReadOnlyList<FamilyMemberDto> Walk(Guid start, FamilyGraph graph, bool upwards)
    {
        var edges = graph.Relationships.Where(r => r.Kind == RelationshipKind.Parent)
            .ToLookup(r => upwards ? r.TargetMemberId : r.SourceMemberId, r => upwards ? r.SourceMemberId : r.TargetMemberId);
        var visited = new HashSet<Guid> { start };
        var pending = new Queue<Guid>();
        pending.Enqueue(start);
        while (pending.TryDequeue(out var current))
            foreach (var next in edges[current])
                if (visited.Add(next)) pending.Enqueue(next);
        visited.Remove(start);
        return Select(graph, visited);
    }

    private static IReadOnlyList<FamilyMemberDto> Select(FamilyGraph graph, IEnumerable<Guid> ids)
    {
        var selected = ids.ToHashSet();
        return graph.Members.Where(m => selected.Contains(m.MemberId)).OrderBy(m => m.Name).ThenBy(m => m.MemberId).ToArray();
    }
}
