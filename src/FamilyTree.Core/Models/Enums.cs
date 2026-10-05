namespace FamilyTree.Core.Models;

public enum Gender { Male = 1, Female = 2 }
public enum LifeStatus { Living = 1, Deceased = 2 }
public enum MaritalStatus { Single = 1, Married = 2, Divorced = 3 }
public enum RelationshipKind { Parent = 1, Spouse = 2, FormerSpouse = 3 }

// The source person IS the selected label OF the target person.
public enum RelationshipLabel
{
    Parent, Father, Mother, Son, Daughter, Spouse, Husband, Wife,
    FormerSpouse, ExHusband, ExWife
}
