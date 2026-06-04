using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;
using DeckDeckDeck.App.Services;
using DeckDeckDeck.App.ViewModels;
using DeckDeckDeck.App.Views;
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
}
