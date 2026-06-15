using Microsoft.EntityFrameworkCore;

namespace DeckDeckDeck.App.Data;

public sealed class AppDbContextFactory
{
    private readonly string _databasePath;

    public AppDbContextFactory(string databasePath)
    {
        _databasePath = databasePath;
    }

    public AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .Options;

        return new AppDbContext(options);
    }

    public void EnsureCreated()
    {
        using var dbContext = Create();
        dbContext.Database.EnsureCreated();
        EnsureSnippetColumns(dbContext);
    }

    private static void EnsureSnippetColumns(AppDbContext dbContext)
    {
        dbContext.Database.OpenConnection();

        try
        {
            var existingColumns = GetSnippetColumns(dbContext);
            AddColumnIfMissing(
                dbContext,
                existingColumns,
                "ActionType",
                "ALTER TABLE Snippets ADD COLUMN ActionType TEXT NOT NULL DEFAULT 'PasteText'");
            AddColumnIfMissing(
                dbContext,
                existingColumns,
                "PasteShortcutMode",
                "ALTER TABLE Snippets ADD COLUMN PasteShortcutMode TEXT NOT NULL DEFAULT 'CtrlV'");
            AddColumnIfMissing(
                dbContext,
                existingColumns,
                "LaunchPath",
                "ALTER TABLE Snippets ADD COLUMN LaunchPath TEXT NULL");
            AddColumnIfMissing(
                dbContext,
                existingColumns,
                "LaunchUrl",
                "ALTER TABLE Snippets ADD COLUMN LaunchUrl TEXT NULL");
            AddColumnIfMissing(
                dbContext,
                existingColumns,
                "MediaProvider",
                "ALTER TABLE Snippets ADD COLUMN MediaProvider TEXT NULL");
            AddColumnIfMissing(
                dbContext,
                existingColumns,
                "MediaCommand",
                "ALTER TABLE Snippets ADD COLUMN MediaCommand TEXT NULL");
            var addedSlotImageMode = AddColumnIfMissing(
                dbContext,
                existingColumns,
                "SlotImageMode",
                "ALTER TABLE Snippets ADD COLUMN SlotImageMode TEXT NOT NULL DEFAULT 'Auto'");
            AddColumnIfMissing(
                dbContext,
                existingColumns,
                "AutoIconPath",
                "ALTER TABLE Snippets ADD COLUMN AutoIconPath TEXT NULL");
            AddColumnIfMissing(
                dbContext,
                existingColumns,
                "AutoIconSourcePath",
                "ALTER TABLE Snippets ADD COLUMN AutoIconSourcePath TEXT NULL");
            AddColumnIfMissing(
                dbContext,
                existingColumns,
                "AutoIconSourceLastWriteTimeUtc",
                "ALTER TABLE Snippets ADD COLUMN AutoIconSourceLastWriteTimeUtc TEXT NULL");
            AddColumnIfMissing(
                dbContext,
                existingColumns,
                "AutoIconSourceLength",
                "ALTER TABLE Snippets ADD COLUMN AutoIconSourceLength INTEGER NULL");

            if (addedSlotImageMode)
            {
                BackfillSnippetImageModes(dbContext);
            }
        }
        finally
        {
            dbContext.Database.CloseConnection();
        }
    }

    private static HashSet<string> GetSnippetColumns(AppDbContext dbContext)
    {
        using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = "PRAGMA table_info(\"Snippets\")";

        using var reader = command.ExecuteReader();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private static bool AddColumnIfMissing(
        AppDbContext dbContext,
        HashSet<string> existingColumns,
        string columnName,
        string alterSql)
    {
        if (existingColumns.Contains(columnName))
        {
            return false;
        }

        using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = alterSql;
        command.ExecuteNonQuery();
        existingColumns.Add(columnName);

        return true;
    }

    private static void BackfillSnippetImageModes(AppDbContext dbContext)
    {
        using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            UPDATE Snippets
            SET SlotImageMode = 'Custom'
            WHERE ImagePath IS NOT NULL AND TRIM(ImagePath) <> ''
            """;
        command.ExecuteNonQuery();
    }
}
