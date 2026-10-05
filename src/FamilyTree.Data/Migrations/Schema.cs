namespace FamilyTree.Data.Migrations;

internal static class Schema
{
    public const int Version = 1;
    public const string Initial = """
        CREATE TABLE FamilyMember (
            MemberId TEXT PRIMARY KEY NOT NULL,
            Name TEXT NOT NULL CHECK (length(Name) BETWEEN 1 AND 200 AND Name = trim(Name)),
            DateOfBirth TEXT NULL,
            Gender INTEGER NOT NULL CHECK (Gender IN (1,2)),
            LifeStatus INTEGER NOT NULL CHECK (LifeStatus IN (1,2)),
            MaritalStatus INTEGER NOT NULL CHECK (MaritalStatus IN (1,2,3)),
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL
        );
        CREATE TABLE RelationshipType (
            RelationshipTypeId INTEGER PRIMARY KEY,
            Code TEXT NOT NULL UNIQUE,
            Name TEXT NOT NULL,
            IsPrimary INTEGER NOT NULL CHECK (IsPrimary IN (0,1)),
            IsCustom INTEGER NOT NULL CHECK (IsCustom IN (0,1)),
            IsActive INTEGER NOT NULL CHECK (IsActive IN (0,1))
        );
        INSERT INTO RelationshipType VALUES
            (1, 'Parent', 'Parent', 1, 0, 1),
            (2, 'Spouse', 'Spouse', 1, 0, 1),
            (3, 'FormerSpouse', 'Former spouse', 1, 0, 1);
        CREATE TABLE MemberRelationship (
            MemberRelationshipId TEXT PRIMARY KEY NOT NULL,
            SourceMemberId TEXT NOT NULL REFERENCES FamilyMember(MemberId) ON DELETE RESTRICT,
            RelationshipTypeId INTEGER NOT NULL REFERENCES RelationshipType(RelationshipTypeId) ON DELETE RESTRICT,
            TargetMemberId TEXT NOT NULL REFERENCES FamilyMember(MemberId) ON DELETE RESTRICT,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL,
            CHECK (SourceMemberId <> TargetMemberId),
            CHECK (RelationshipTypeId NOT IN (2,3) OR SourceMemberId < TargetMemberId),
            UNIQUE (SourceMemberId, RelationshipTypeId, TargetMemberId)
        );
        CREATE UNIQUE INDEX UX_SpousePair ON MemberRelationship(SourceMemberId, TargetMemberId)
            WHERE RelationshipTypeId IN (2,3);
        CREATE INDEX IX_RelationshipTarget ON MemberRelationship(TargetMemberId, RelationshipTypeId, SourceMemberId);
        CREATE INDEX IX_RelationshipType ON MemberRelationship(RelationshipTypeId);
        PRAGMA user_version = 1;
        """;
}
