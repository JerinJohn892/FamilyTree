using FamilyTree.Core.DTOs;

namespace FamilyTree.Core.Abstractions;

public interface IFamilyTreeLayoutService
{
    TreeLayout Layout(FamilyGraph graph);
}
