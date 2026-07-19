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
        EnsureHotkeyActionsTable(dbContext);
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
                "FileActionMode",
                "ALTER TABLE Snippets ADD COLUMN FileActionMode TEXT NOT NULL DEFAULT 'Launch'");
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
            AddColumnIfMissing(
                dbContext,
                existingColumns,
                "TerminalCommand",
                "ALTER TABLE Snippets ADD COLUMN TerminalCommand TEXT NULL");
            AddColumnIfMissing(
                dbContext,
                existingColumns,
                "TerminalShell",
                "ALTER TABLE Snippets ADD COLUMN TerminalShell TEXT NULL");
            AddColumnIfMissing(
                dbContext,
                existingColumns,
                "RunAsAdministrator",
                "ALTER TABLE Snippets ADD COLUMN RunAsAdministrator INTEGER NOT NULL DEFAULT 1");
            AddColumnIfMissing(
                dbContext,
                existingColumns,
                "OpenTerminalWindow",
                "ALTER TABLE Snippets ADD COLUMN OpenTerminalWindow INTEGER NOT NULL DEFAULT 0");
            AddColumnIfMissing(
                dbContext,
                existingColumns,
                "TerminalWorkingDirectory",
                "ALTER TABLE Snippets ADD COLUMN TerminalWorkingDirectory TEXT NULL");
            AddColumnIfMissing(
                dbContext,
                existingColumns,
                "AdbDeviceIp",
                "ALTER TABLE Snippets ADD COLUMN AdbDeviceIp TEXT NULL");
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

    private static void EnsureHotkeyActionsTable(AppDbContext dbContext)
    {
        dbContext.Database.OpenConnection();

        try
        {
            using var createTableCommand = dbContext.Database.GetDbConnection().CreateCommand();
            createTableCommand.CommandText = """
                CREATE TABLE IF NOT EXISTS HotkeyActions (
                    Id TEXT NOT NULL CONSTRAINT PK_HotkeyActions PRIMARY KEY,
                    Title TEXT NOT NULL,
                    HotkeyVirtualKey INTEGER NULL,
                    HotkeyModifiers INTEGER NOT NULL DEFAULT 0,
                    IsEnabled INTEGER NOT NULL DEFAULT 1,
                    Content TEXT NOT NULL DEFAULT '',
                    ActionType TEXT NOT NULL DEFAULT 'PasteText',
                    PasteShortcutMode TEXT NOT NULL DEFAULT 'CtrlV',
                    LaunchPath TEXT NULL,
                    FileActionMode TEXT NOT NULL DEFAULT 'Launch',
                    LaunchUrl TEXT NULL,
                    MediaProvider TEXT NULL,
                    MediaCommand TEXT NULL,
                    TerminalCommand TEXT NULL,
                    TerminalShell TEXT NULL,
                    RunAsAdministrator INTEGER NOT NULL DEFAULT 1,
                    OpenTerminalWindow INTEGER NOT NULL DEFAULT 0,
                    TerminalWorkingDirectory TEXT NULL,
                    AdbDeviceIp TEXT NULL,
                    SlotImageMode TEXT NOT NULL DEFAULT 'Auto',
                    Description TEXT NULL,
                    ImagePath TEXT NULL,
                    ThumbnailPath TEXT NULL,
                    AutoIconPath TEXT NULL,
                    AutoIconSourcePath TEXT NULL,
                    AutoIconSourceLastWriteTimeUtc TEXT NULL,
                    AutoIconSourceLength INTEGER NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                )
                """;
            createTableCommand.ExecuteNonQuery();

            var existingColumns = GetTableColumns(dbContext, "HotkeyActions");
            AddColumnIfMissing(
                dbContext,
                existingColumns,
                "FileActionMode",
                "ALTER TABLE HotkeyActions ADD COLUMN FileActionMode TEXT NOT NULL DEFAULT 'Launch'");
            AddColumnIfMissing(
                dbContext,
                existingColumns,
                "OpenTerminalWindow",
                "ALTER TABLE HotkeyActions ADD COLUMN OpenTerminalWindow INTEGER NOT NULL DEFAULT 0");
            AddColumnIfMissing(
                dbContext,
                existingColumns,
                "TerminalWorkingDirectory",
                "ALTER TABLE HotkeyActions ADD COLUMN TerminalWorkingDirectory TEXT NULL");
            AddColumnIfMissing(
                dbContext,
                existingColumns,
                "AdbDeviceIp",
                "ALTER TABLE HotkeyActions ADD COLUMN AdbDeviceIp TEXT NULL");

            using var createIndexCommand = dbContext.Database.GetDbConnection().CreateCommand();
            createIndexCommand.CommandText = """
                CREATE INDEX IF NOT EXISTS IX_HotkeyActions_HotkeyVirtualKey_HotkeyModifiers
                ON HotkeyActions (HotkeyVirtualKey, HotkeyModifiers)
                """;
            createIndexCommand.ExecuteNonQuery();
        }
        finally
        {
            dbContext.Database.CloseConnection();
        }
    }

    private static HashSet<string> GetSnippetColumns(AppDbContext dbContext)
    {
        return GetTableColumns(dbContext, "Snippets");
    }

    private static HashSet<string> GetTableColumns(AppDbContext dbContext, string tableName)
    {
        using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\")";

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
