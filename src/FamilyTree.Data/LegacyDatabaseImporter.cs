using Microsoft.Data.Sqlite;

namespace FamilyTree.Data;

public static class LegacyDatabaseImporter
{
    // SQLite backup includes committed WAL data; a raw file copy may lose it.
    // Publish only a complete, validated backup and never overwrite a destination.
    public static void ImportIfMissing(string sourcePath, string destinationPath)
    {
        if (File.Exists(destinationPath) || !File.Exists(sourcePath)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        var temporary = destinationPath + "." + Guid.NewGuid().ToString("N") + ".import";
        try
        {
            using (var source = new SqliteConnection(new SqliteConnectionStringBuilder
                { DataSource = sourcePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
            using (var target = new SqliteConnection(new SqliteConnectionStringBuilder
                { DataSource = temporary, Pooling = false }.ToString()))
            {
                source.Open(); target.Open(); source.BackupDatabase(target);
            }
            var imported = new SqliteDatabase(temporary);
            imported.Initialize();
            using (var unit = imported.Open()) { _ = unit.Members.List(); _ = unit.Relationships.List(); }
            try { File.Move(temporary, destinationPath); }
            catch (IOException) when (File.Exists(destinationPath)) { /* Another startup imported it first. */ }
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
