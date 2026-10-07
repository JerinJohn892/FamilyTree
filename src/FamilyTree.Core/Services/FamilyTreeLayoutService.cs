using FamilyTree.Core.Abstractions;
using FamilyTree.Core.DTOs;
using FamilyTree.Core.Models;

namespace FamilyTree.Core.Services;

/// <summary>Deterministic generation rows. Couples share a card; incoming edges retain the child's identity.</summary>
public sealed class FamilyTreeLayoutService : IFamilyTreeLayoutService
{
    public const double MemberWidth = 180;
    public const double CardHeight = 96;
    private const double Gap = 48;
    private const double RowHeight = 200;

    public TreeLayout Layout(FamilyGraph graph) => Build(graph, pairSpouses: true);

    private static TreeLayout Build(FamilyGraph graph, bool pairSpouses)
    {
        var ordered = graph.Members.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ThenBy(m => m.MemberId).ToArray();
        var byId = ordered.ToDictionary(m => m.MemberId);
        var partners = new Dictionary<Guid, Guid>();
        if (pairSpouses)
            foreach (var r in graph.Relationships.Where(r => r.Kind == RelationshipKind.Spouse))
            {
                partners.Add(r.SourceMemberId, r.TargetMemberId);
                partners.Add(r.TargetMemberId, r.SourceMemberId);
            }
        var groups = new List<FamilyMemberDto[]>();
        var groupOf = new Dictionary<Guid, int>();
        foreach (var m in ordered)
        {
            if (groupOf.ContainsKey(m.MemberId)) continue;
            var group = partners.TryGetValue(m.MemberId, out var partner)
                ? new[] { m, byId[partner] }.OrderBy(p => p.Gender).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.MemberId).ToArray()
                : [m];
            foreach (var person in group) groupOf.Add(person.MemberId, groups.Count);
            groups.Add(group);
        }
        var facts = graph.Relationships.Where(r => r.Kind == RelationshipKind.Parent).ToArray();
        var edges = facts.Select(r => (From: groupOf[r.SourceMemberId], To: groupOf[r.TargetMemberId])).Distinct().ToArray();
        var outgoing = edges.ToLookup(e => e.From, e => e.To);
        var incoming = edges.ToLookup(e => e.To, e => e.From);
        var remaining = Enumerable.Range(0, groups.Count).Select(i => incoming[i].Count()).ToArray();
        var levels = new int[groups.Count];
        var ready = new Queue<int>(Enumerable.Range(0, groups.Count).Where(i => remaining[i] == 0));
        var visited = 0;
        while (ready.TryDequeue(out var current))
        {
            visited++;
            foreach (var child in outgoing[current])
            {
                levels[child] = Math.Max(levels[child], levels[current] + 1);
                if (--remaining[child] == 0) ready.Enqueue(child);
            }
        }
        // Valid ancestry can still form a cycle when spouse nodes are contracted (cross-generation marriages).
        // Keep all people/facts visible instead of looping or silently omitting part of the family.
        if (visited != groups.Count)
        {
            if (pairSpouses) return Build(graph, false) with { Notice = "Some marriages span generations. Individual cards are used to keep ancestry readable." };
            throw new InvalidOperationException("The saved parent relationships contain a cycle. Correct them before displaying the tree.");
        }
        var cards = new TreeCard[groups.Count];
        foreach (var row in Enumerable.Range(0, groups.Count).GroupBy(i => levels[i]).OrderBy(g => g.Key))
        {
            double ParentCenter(int i) => incoming[i].Any()
                ? incoming[i].Average(p => cards[p].X + cards[p].Width / 2) : 0;
            var x = 40d;
            foreach (var index in row.OrderBy(ParentCenter).ThenBy(i => i))
            {
                var width = groups[index].Length * MemberWidth;
                x = Math.Max(x, ParentCenter(index) - width / 2);
                cards[index] = new(groups[index], x, 40 + row.Key * RowHeight, width, CardHeight);
                x += width + Gap;
            }
        }
        var branches = new List<TreeBranch>();
        // Both parents in one couple produce one branch per child, not two duplicate lines.
        foreach (var relation in facts.Select(r => (Parent: groupOf[r.SourceMemberId], Child: r.TargetMemberId)).Distinct())
        {
            var parent = cards[relation.Parent];
            var child = cards[groupOf[relation.Child]];
            var half = Array.FindIndex(groups[groupOf[relation.Child]], m => m.MemberId == relation.Child);
            var sx = parent.X + parent.Width / 2;
            var sy = parent.Y + CardHeight;
            var tx = child.X + (half + 0.5) * MemberWidth;
            var ty = child.Y;
            if (ty - parent.Y > RowHeight)
            {
                // Route generation-skipping edges outside all cards to avoid crossing intervening people.
                var lane = cards.Max(c => c.X + c.Width) + 30 + branches.Count * 8;
                branches.Add(new(relation.Child, [new(sx, sy), new(sx, sy + 30), new(lane, sy + 30),
                    new(lane, ty - 30), new(tx, ty - 30), new(tx, ty)]));
            }
            else branches.Add(new(relation.Child, [new(sx, sy), new(sx, sy + 45), new(tx, sy + 45), new(tx, ty)]));
        }
        var widthExtent = Math.Max(cards.Select(c => c.X + c.Width).DefaultIfEmpty(400).Max(),
            branches.SelectMany(b => b.Points).Select(p => p.X).DefaultIfEmpty(0).Max()) + 40;
        return new(cards, branches, widthExtent, cards.Select(c => c.Y + c.Height + 40).DefaultIfEmpty(300).Max(),
            graph.Members.Count == 0 ? "Your family starts here. Add members and relationships in the main window, then refresh." :
            "Current spouses share a card. Former spouses remain separate; arrows show recorded parent relationships.");
    }
}
