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
        EnsureSnippetActionColumns(dbContext);
    }

    private static void EnsureSnippetActionColumns(AppDbContext dbContext)
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
                "LaunchPath",
                "ALTER TABLE Snippets ADD COLUMN LaunchPath TEXT NULL");
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

    private static void AddColumnIfMissing(
        AppDbContext dbContext,
        HashSet<string> existingColumns,
        string columnName,
        string alterSql)
    {
        if (existingColumns.Contains(columnName))
        {
            return;
        }

        using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = alterSql;
        command.ExecuteNonQuery();
        existingColumns.Add(columnName);
    }
}
