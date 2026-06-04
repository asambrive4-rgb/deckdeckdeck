using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;
using DeckDeckDeck.App.Services;
using DeckDeckDeck.App.ViewModels;
using DeckDeckDeck.App.Views;
using Microsoft.Data.Sqlite;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;
public sealed class DataPersistenceTests
{
    [Fact]
    public void CategoryAndSnippetPersistAcrossDbContexts()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(SlotKey.Numpad1, "Writing", "Draft prompts");
        services.SnippetService.Create(category.Id, SlotKey.Numpad3, "Structure", "Make this clearer.", null);

        var reloadedServices = CreateServices(services.Storage.AppDataPath);
        var categories = reloadedServices.CategoryService.GetAll();
        var snippets = reloadedServices.SnippetService.GetByCategoryId(category.Id);

        Assert.Single(categories);
        Assert.Equal("Writing", categories[0].Name);
        Assert.Single(snippets);
        Assert.Equal("Structure", snippets[0].Title);
        Assert.Equal("Make this clearer.", snippets[0].Content);
        Assert.Equal(SnippetActionType.PasteText, snippets[0].ActionType);
        Assert.Null(snippets[0].LaunchPath);
    }

    [Fact]
    public void LaunchSnippetPersistsAcrossDbContexts()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(SlotKey.Numpad1, "Tools", null);
        services.SnippetService.Create(
            category.Id,
            SlotKey.Numpad3,
            "Open notes",
            string.Empty,
            null,
            actionType: SnippetActionType.LaunchFile,
            launchPath: @"C:\notes");

        var reloadedServices = CreateServices(services.Storage.AppDataPath);
        var snippet = Assert.Single(reloadedServices.SnippetService.GetByCategoryId(category.Id));

        Assert.Equal(SnippetActionType.LaunchFile, snippet.ActionType);
        Assert.Equal(string.Empty, snippet.Content);
        Assert.Equal(@"C:\notes", snippet.LaunchPath);
    }

    [Fact]
    public void ExistingDatabaseWithoutActionColumnsIsPreservedAndBackfilled()
    {
        var storage = new FileStorageService(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        storage.EnsureCreated();
        var categoryId = Guid.NewGuid();
        var snippetId = Guid.NewGuid();
        CreateLegacyDatabase(storage.DatabasePath, categoryId, snippetId);

        var dbContextFactory = new AppDbContextFactory(storage.DatabasePath);
        dbContextFactory.EnsureCreated();
        var snippetService = new SnippetService(dbContextFactory);

        var snippet = Assert.Single(snippetService.GetByCategoryId(categoryId));
        Assert.Equal(snippetId, snippet.Id);
        Assert.Equal("Legacy", snippet.Title);
        Assert.Equal("Keep me", snippet.Content);
        Assert.Equal(SnippetActionType.PasteText, snippet.ActionType);
        Assert.Null(snippet.LaunchPath);
    }

    [Fact]
    public void DeletingCategoryDeletesItsSnippets()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(SlotKey.Numpad1, "Writing", null);
        services.SnippetService.Create(category.Id, SlotKey.Numpad3, "Structure", "Make this clearer.", null);

        services.CategoryService.Delete(category.Id);

        Assert.Empty(services.CategoryService.GetAll());
        Assert.Empty(services.SnippetService.GetByCategoryId(category.Id));
    }

    [Fact]
    public void CategoryAndSnippetImagePathsPersistAcrossDbContexts()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(
            SlotKey.Numpad1,
            "Writing",
            null,
            "category-original.png",
            "category-thumbnail.png");
        services.SnippetService.Create(
            category.Id,
            SlotKey.Numpad3,
            "Structure",
            "Make this clearer.",
            null,
            "snippet-original.png",
            "snippet-thumbnail.png");

        var reloadedServices = CreateServices(services.Storage.AppDataPath);
        var reloadedCategory = Assert.Single(reloadedServices.CategoryService.GetAll());
        var reloadedSnippet = Assert.Single(reloadedServices.SnippetService.GetByCategoryId(category.Id));

        Assert.Equal("category-original.png", reloadedCategory.ImagePath);
        Assert.Equal("category-thumbnail.png", reloadedCategory.ThumbnailPath);
        Assert.Equal("snippet-original.png", reloadedSnippet.ImagePath);
        Assert.Equal("snippet-thumbnail.png", reloadedSnippet.ThumbnailPath);
    }

    [Fact]
    public void CategoryAndSnippetImagePathsCanBeReplacedAndRemoved()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(
            SlotKey.Numpad1,
            "Writing",
            null,
            "category-original.png",
            "category-thumbnail.png");
        var snippet = services.SnippetService.Create(
            category.Id,
            SlotKey.Numpad3,
            "Structure",
            "Make this clearer.",
            null,
            "snippet-original.png",
            "snippet-thumbnail.png");

        var updatedCategory = services.CategoryService.Update(
            category.Id,
            "Writing",
            null,
            "category-new.png",
            "category-new-thumbnail.png");
        var updatedSnippet = services.SnippetService.Update(
            snippet.Id,
            "Structure",
            "Make this clearer.",
            null,
            "snippet-new.png",
            "snippet-new-thumbnail.png");
        var removedCategoryImage = services.CategoryService.Update(category.Id, "Writing", null, null, null);
        var removedSnippetImage = services.SnippetService.Update(snippet.Id, "Structure", "Make this clearer.", null, null, null);

        Assert.Equal("category-new.png", updatedCategory.ImagePath);
        Assert.Equal("category-new-thumbnail.png", updatedCategory.ThumbnailPath);
        Assert.Equal("snippet-new.png", updatedSnippet.ImagePath);
        Assert.Equal("snippet-new-thumbnail.png", updatedSnippet.ThumbnailPath);
        Assert.Null(removedCategoryImage.ImagePath);
        Assert.Null(removedCategoryImage.ThumbnailPath);
        Assert.Null(removedSnippetImage.ImagePath);
        Assert.Null(removedSnippetImage.ThumbnailPath);
    }

    [Fact]
    public void DeletingCategoryReturnsCategoryAndSnippetImagePaths()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(
            SlotKey.Numpad1,
            "Writing",
            null,
            "category-original.png",
            "category-thumbnail.png");
        services.SnippetService.Create(
            category.Id,
            SlotKey.Numpad3,
            "Structure",
            "Make this clearer.",
            null,
            "snippet-original.png",
            "snippet-thumbnail.png");

        var deletedImageFiles = services.CategoryService.Delete(category.Id);

        Assert.Collection(
            deletedImageFiles,
            categoryImage =>
            {
                Assert.Equal("category-original.png", categoryImage.ImagePath);
                Assert.Equal("category-thumbnail.png", categoryImage.ThumbnailPath);
            },
            snippetImage =>
            {
                Assert.Equal("snippet-original.png", snippetImage.ImagePath);
                Assert.Equal("snippet-thumbnail.png", snippetImage.ThumbnailPath);
            });
    }

    private static void CreateLegacyDatabase(string databasePath, Guid categoryId, Guid snippetId)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE Categories (
                Id TEXT NOT NULL CONSTRAINT PK_Categories PRIMARY KEY,
                SlotKey TEXT NOT NULL,
                Name TEXT NOT NULL,
                Description TEXT NULL,
                ImagePath TEXT NULL,
                ThumbnailPath TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE UNIQUE INDEX IX_Categories_SlotKey ON Categories (SlotKey);

            CREATE TABLE Snippets (
                Id TEXT NOT NULL CONSTRAINT PK_Snippets PRIMARY KEY,
                CategoryId TEXT NOT NULL,
                SlotKey TEXT NOT NULL,
                Title TEXT NOT NULL,
                Content TEXT NOT NULL,
                Description TEXT NULL,
                ImagePath TEXT NULL,
                ThumbnailPath TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                CONSTRAINT FK_Snippets_Categories_CategoryId FOREIGN KEY (CategoryId)
                    REFERENCES Categories (Id) ON DELETE CASCADE
            );

            CREATE UNIQUE INDEX IX_Snippets_CategoryId_SlotKey ON Snippets (CategoryId, SlotKey);

            INSERT INTO Categories (Id, SlotKey, Name, Description, ImagePath, ThumbnailPath, CreatedAt, UpdatedAt)
            VALUES ($categoryId, 'Numpad1', 'Tools', NULL, NULL, NULL, $now, $now);

            INSERT INTO Snippets (Id, CategoryId, SlotKey, Title, Content, Description, ImagePath, ThumbnailPath, CreatedAt, UpdatedAt)
            VALUES ($snippetId, $categoryId, 'Numpad3', 'Legacy', 'Keep me', NULL, NULL, NULL, $now, $now);
            """;
        command.Parameters.AddWithValue("$categoryId", categoryId.ToString());
        command.Parameters.AddWithValue("$snippetId", snippetId.ToString());
        command.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }
}
