using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;
using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.UseCases.Ports;
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
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", "Draft prompts");
        services.SnippetRepository.Create(category.Id, SlotKey.Numpad3, "Structure", "Make this clearer.", null);

        var reloadedServices = CreateServices(services.Storage.AppDataPath);
        var categories = reloadedServices.CategoryRepository.GetAll();
        var snippets = reloadedServices.SnippetRepository.GetByCategoryId(category.Id);

        Assert.Single(categories);
        Assert.Equal("Writing", categories[0].Name);
        Assert.Single(snippets);
        Assert.Equal("Structure", snippets[0].Title);
        Assert.Equal("Make this clearer.", snippets[0].Content);
        Assert.Equal(SnippetActionType.PasteText, snippets[0].ActionType);
        Assert.Null(snippets[0].LaunchPath);
        Assert.Null(snippets[0].LaunchUrl);
        Assert.Null(snippets[0].MediaProvider);
        Assert.Null(snippets[0].MediaCommand);
        Assert.Equal(SlotImageMode.Auto, snippets[0].SlotImageMode);
        Assert.Equal(PasteShortcutMode.CtrlV, snippets[0].PasteShortcutMode);
    }

    [Fact]
    public void PasteShortcutModePersistsAcrossDbContexts()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Terminal", null);
        services.SnippetRepository.Create(
            category.Id,
            SlotKey.Numpad3,
            "Terminal paste",
            "Run this",
            null,
            pasteShortcutMode: PasteShortcutMode.CtrlShiftV);

        var reloadedServices = CreateServices(services.Storage.AppDataPath);
        var snippet = Assert.Single(reloadedServices.SnippetRepository.GetByCategoryId(category.Id));

        Assert.Equal(PasteShortcutMode.CtrlShiftV, snippet.PasteShortcutMode);
    }

    [Fact]
    public void LaunchSnippetPersistsAcrossDbContexts()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Tools", null);
        var autoIcon = new AutoIconCacheEntry(
            "cache-icon.png",
            @"C:\notes.exe",
            new DateTime(2026, 6, 4, 0, 0, 0, DateTimeKind.Utc),
            123);
        services.SnippetRepository.Create(
            category.Id,
            SlotKey.Numpad3,
            "Open notes",
            string.Empty,
            null,
            actionType: SnippetActionType.LaunchFile,
            launchPath: @"C:\notes",
            autoIcon: autoIcon,
            pasteShortcutMode: PasteShortcutMode.CtrlShiftV);

        var reloadedServices = CreateServices(services.Storage.AppDataPath);
        var snippet = Assert.Single(reloadedServices.SnippetRepository.GetByCategoryId(category.Id));

        Assert.Equal(SnippetActionType.LaunchFile, snippet.ActionType);
        Assert.Equal(string.Empty, snippet.Content);
        Assert.Equal(@"C:\notes", snippet.LaunchPath);
        Assert.Null(snippet.LaunchUrl);
        Assert.Null(snippet.MediaProvider);
        Assert.Null(snippet.MediaCommand);
        Assert.Equal(SlotImageMode.Auto, snippet.SlotImageMode);
        Assert.Equal(PasteShortcutMode.CtrlV, snippet.PasteShortcutMode);
        Assert.Equal(autoIcon.IconPath, snippet.AutoIconPath);
        Assert.Equal(autoIcon.SourcePath, snippet.AutoIconSourcePath);
        Assert.Equal(autoIcon.SourceLastWriteTimeUtc, snippet.AutoIconSourceLastWriteTimeUtc);
        Assert.Equal(autoIcon.SourceLength, snippet.AutoIconSourceLength);
    }

    [Fact]
    public void LaunchUrlSnippetPersistsAcrossDbContexts()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Web", null);
        services.SnippetRepository.Create(
            category.Id,
            SlotKey.Numpad3,
            "Open docs",
            "unused",
            null,
            actionType: SnippetActionType.LaunchUrl,
            launchPath: @"C:\unused",
            launchUrl: "https://example.com/docs");

        var reloadedServices = CreateServices(services.Storage.AppDataPath);
        var snippet = Assert.Single(reloadedServices.SnippetRepository.GetByCategoryId(category.Id));

        Assert.Equal(SnippetActionType.LaunchUrl, snippet.ActionType);
        Assert.Equal(string.Empty, snippet.Content);
        Assert.Null(snippet.LaunchPath);
        Assert.Equal("https://example.com/docs", snippet.LaunchUrl);
        Assert.Null(snippet.MediaProvider);
        Assert.Null(snippet.MediaCommand);
        Assert.Null(snippet.AutoIconPath);
    }

    [Fact]
    public void MediaActionSnippetPersistsAcrossDbContexts()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Media", null);
        services.SnippetRepository.Create(
            category.Id,
            SlotKey.Numpad3,
            "Next",
            "unused",
            null,
            actionType: SnippetActionType.MediaAction,
            launchPath: @"C:\unused",
            launchUrl: "https://example.com",
            mediaProvider: SnippetMediaProvider.System,
            mediaCommand: SnippetMediaCommand.NextTrack);

        var reloadedServices = CreateServices(services.Storage.AppDataPath);
        var snippet = Assert.Single(reloadedServices.SnippetRepository.GetByCategoryId(category.Id));

        Assert.Equal(SnippetActionType.MediaAction, snippet.ActionType);
        Assert.Equal(string.Empty, snippet.Content);
        Assert.Null(snippet.LaunchPath);
        Assert.Null(snippet.LaunchUrl);
        Assert.Equal(SnippetMediaProvider.System, snippet.MediaProvider);
        Assert.Equal(SnippetMediaCommand.NextTrack, snippet.MediaCommand);
        Assert.Null(snippet.AutoIconPath);
    }

    [Fact]
    public void TerminalCommandSnippetPersistsAcrossDbContexts()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Tools", null);
        services.SnippetRepository.Create(
            category.Id,
            SlotKey.Numpad3,
            "Reconnect Bluetooth",
            "unused",
            null,
            actionType: SnippetActionType.TerminalCommand,
            launchPath: @"C:\unused",
            launchUrl: "https://example.com",
            mediaProvider: SnippetMediaProvider.System,
            mediaCommand: SnippetMediaCommand.NextTrack,
            terminalCommand: "  echo reconnect  ",
            terminalShell: SnippetTerminalShell.PowerShell,
            runAsAdministrator: false);

        var reloadedServices = CreateServices(services.Storage.AppDataPath);
        var snippet = Assert.Single(reloadedServices.SnippetRepository.GetByCategoryId(category.Id));

        Assert.Equal(SnippetActionType.TerminalCommand, snippet.ActionType);
        Assert.Equal(string.Empty, snippet.Content);
        Assert.Null(snippet.LaunchPath);
        Assert.Null(snippet.LaunchUrl);
        Assert.Null(snippet.MediaProvider);
        Assert.Null(snippet.MediaCommand);
        Assert.Equal("echo reconnect", snippet.TerminalCommand);
        Assert.Equal(SnippetTerminalShell.PowerShell, snippet.TerminalShell);
        Assert.False(snippet.RunAsAdministrator);
        Assert.Null(snippet.AutoIconPath);
    }

    [Fact]
    public void SpotifyMediaActionSnippetPersistsAcrossDbContexts()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Media", null);
        services.SnippetRepository.Create(
            category.Id,
            SlotKey.Numpad3,
            "Shuffle",
            "unused",
            null,
            actionType: SnippetActionType.MediaAction,
            mediaProvider: SnippetMediaProvider.Spotify,
            mediaCommand: SnippetMediaCommand.ToggleShuffle);

        var reloadedServices = CreateServices(services.Storage.AppDataPath);
        var snippet = Assert.Single(reloadedServices.SnippetRepository.GetByCategoryId(category.Id));

        Assert.Equal(SnippetActionType.MediaAction, snippet.ActionType);
        Assert.Equal(SnippetMediaProvider.Spotify, snippet.MediaProvider);
        Assert.Equal(SnippetMediaCommand.ToggleShuffle, snippet.MediaCommand);
    }

    [Fact]
    public void SpotifyOpenAndResumeSnippetPersistsAcrossDbContexts()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Media", null);
        services.SnippetRepository.Create(
            category.Id,
            SlotKey.Numpad3,
            "Open Spotify",
            "unused",
            null,
            actionType: SnippetActionType.MediaAction,
            mediaProvider: SnippetMediaProvider.Spotify,
            mediaCommand: SnippetMediaCommand.OpenSpotifyAndResume);

        var reloadedServices = CreateServices(services.Storage.AppDataPath);
        var snippet = Assert.Single(reloadedServices.SnippetRepository.GetByCategoryId(category.Id));

        Assert.Equal(SnippetActionType.MediaAction, snippet.ActionType);
        Assert.Equal(SnippetMediaProvider.Spotify, snippet.MediaProvider);
        Assert.Equal(SnippetMediaCommand.OpenSpotifyAndResume, snippet.MediaCommand);
    }

    [Fact]
    public void ExistingDatabaseWithoutActionColumnsIsPreservedAndBackfilled()
    {
        var storage = new AppStoragePaths(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        storage.EnsureCreated();
        var categoryId = Guid.NewGuid();
        var snippetId = Guid.NewGuid();
        CreateLegacyDatabase(storage.DatabasePath, categoryId, snippetId);

        var dbContextFactory = new AppDbContextFactory(storage.DatabasePath);
        dbContextFactory.EnsureCreated();
        var snippetService = new SnippetRepository(dbContextFactory);

        var snippet = Assert.Single(snippetService.GetByCategoryId(categoryId));
        Assert.Equal(snippetId, snippet.Id);
        Assert.Equal("Legacy", snippet.Title);
        Assert.Equal("Keep me", snippet.Content);
        Assert.Equal(SnippetActionType.PasteText, snippet.ActionType);
        Assert.Null(snippet.LaunchPath);
        Assert.Null(snippet.LaunchUrl);
        Assert.Null(snippet.MediaProvider);
        Assert.Null(snippet.MediaCommand);
        Assert.Equal(SlotImageMode.Auto, snippet.SlotImageMode);
        Assert.Equal(PasteShortcutMode.CtrlV, snippet.PasteShortcutMode);
    }

    [Fact]
    public void ExistingDatabaseWithSnippetImageIsBackfilledAsCustomImage()
    {
        var storage = new AppStoragePaths(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        storage.EnsureCreated();
        var categoryId = Guid.NewGuid();
        var snippetId = Guid.NewGuid();
        CreateLegacyDatabase(
            storage.DatabasePath,
            categoryId,
            snippetId,
            "legacy-snippet.png",
            "legacy-snippet-thumbnail.png");

        var dbContextFactory = new AppDbContextFactory(storage.DatabasePath);
        dbContextFactory.EnsureCreated();
        var snippetService = new SnippetRepository(dbContextFactory);

        var snippet = Assert.Single(snippetService.GetByCategoryId(categoryId));

        Assert.Equal(SlotImageMode.Custom, snippet.SlotImageMode);
        Assert.Equal("legacy-snippet.png", snippet.ImagePath);
        Assert.Equal("legacy-snippet-thumbnail.png", snippet.ThumbnailPath);
    }

    [Fact]
    public void DeletingCategoryDeletesItsSnippets()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", null);
        services.SnippetRepository.Create(category.Id, SlotKey.Numpad3, "Structure", "Make this clearer.", null);

        services.CategoryRepository.Delete(category.Id);

        Assert.Empty(services.CategoryRepository.GetAll());
        Assert.Empty(services.SnippetRepository.GetByCategoryId(category.Id));
    }

    [Fact]
    public void CategoryAndSnippetImagePathsPersistAcrossDbContexts()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(
            SlotKey.Numpad1,
            "Writing",
            null,
            "category-original.png",
            "category-thumbnail.png");
        services.SnippetRepository.Create(
            category.Id,
            SlotKey.Numpad3,
            "Structure",
            "Make this clearer.",
            null,
            "snippet-original.png",
            "snippet-thumbnail.png");

        var reloadedServices = CreateServices(services.Storage.AppDataPath);
        var reloadedCategory = Assert.Single(reloadedServices.CategoryRepository.GetAll());
        var reloadedSnippet = Assert.Single(reloadedServices.SnippetRepository.GetByCategoryId(category.Id));

        Assert.Equal("category-original.png", reloadedCategory.ImagePath);
        Assert.Equal("category-thumbnail.png", reloadedCategory.ThumbnailPath);
        Assert.Equal("snippet-original.png", reloadedSnippet.ImagePath);
        Assert.Equal("snippet-thumbnail.png", reloadedSnippet.ThumbnailPath);
    }

    [Fact]
    public void StoredPathMigrationNormalizesManagedAbsolutePaths()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(
            SlotKey.Numpad1,
            "Writing",
            null,
            @"C:\Users\Old\AppData\Roaming\NumpadPromptLauncher\images\originals\category.png",
            Path.Combine(services.Storage.ImageThumbnailsPath, "category.png"));
        services.SnippetRepository.Create(
            category.Id,
            SlotKey.Numpad3,
            "Open app",
            string.Empty,
            null,
            @"C:\Users\Old\AppData\Roaming\NumpadPromptLauncher\images\originals\snippet.png",
            Path.Combine(services.Storage.ImageThumbnailsPath, "snippet.png"),
            SnippetActionType.LaunchFile,
            @"C:\tools\app.exe",
            SlotImageMode.Auto,
            new AutoIconCacheEntry(
                @"C:\Users\Old\AppData\Roaming\NumpadPromptLauncher\icon-cache\app.png",
                @"C:\tools\app.exe",
                DateTime.UtcNow,
                123));
        var dbContextFactory = new AppDbContextFactory(services.Storage.DatabasePath);

        new StoredPathMigration(dbContextFactory, services.Storage).NormalizeManagedPaths();

        var migratedCategory = services.CategoryRepository.GetBySlotKey(SlotKey.Numpad1)!;
        var migratedSnippet = Assert.Single(services.SnippetRepository.GetByCategoryId(category.Id));
        Assert.Equal("images/originals/category.png", migratedCategory.ImagePath);
        Assert.Equal("images/thumbnails/category.png", migratedCategory.ThumbnailPath);
        Assert.Equal("images/originals/snippet.png", migratedSnippet.ImagePath);
        Assert.Equal("images/thumbnails/snippet.png", migratedSnippet.ThumbnailPath);
        Assert.Equal("icon-cache/app.png", migratedSnippet.AutoIconPath);
        Assert.Equal(@"C:\tools\app.exe", migratedSnippet.LaunchPath);
    }

    [Fact]
    public void CategoryAndSnippetImagePathsCanBeReplacedAndRemoved()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(
            SlotKey.Numpad1,
            "Writing",
            null,
            "category-original.png",
            "category-thumbnail.png");
        var snippet = services.SnippetRepository.Create(
            category.Id,
            SlotKey.Numpad3,
            "Structure",
            "Make this clearer.",
            null,
            "snippet-original.png",
            "snippet-thumbnail.png");

        var updatedCategory = services.CategoryRepository.Update(
            category.Id,
            "Writing",
            null,
            "category-new.png",
            "category-new-thumbnail.png");
        var updatedSnippet = services.SnippetRepository.Update(
            snippet.Id,
            "Structure",
            "Make this clearer.",
            null,
            "snippet-new.png",
            "snippet-new-thumbnail.png");
        var removedCategoryImage = services.CategoryRepository.Update(category.Id, "Writing", null, null, null);
        var removedSnippetImage = services.SnippetRepository.Update(snippet.Id, "Structure", "Make this clearer.", null, null, null);

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
        var category = services.CategoryRepository.Create(
            SlotKey.Numpad1,
            "Writing",
            null,
            "category-original.png",
            "category-thumbnail.png");
        services.SnippetRepository.Create(
            category.Id,
            SlotKey.Numpad3,
            "Structure",
            "Make this clearer.",
            null,
            "snippet-original.png",
            "snippet-thumbnail.png");

        var deletedImageFiles = services.CategoryRepository.Delete(category.Id);

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

    [Fact]
    public void CopyingCategoryToSlotOverwritesTargetAndCopiesSnippets()
    {
        var services = CreateServices();
        var source = services.CategoryRepository.Create(
            SlotKey.Numpad4,
            "Writing",
            "Draft prompts",
            "source-category.png",
            "source-category-thumbnail.png");
        services.SnippetRepository.Create(
            source.Id,
            SlotKey.Numpad3,
            "Structure",
            "Make this clearer.",
            "Draft helper",
            "source-snippet.png",
            "source-snippet-thumbnail.png");
        services.CategoryRepository.Create(SlotKey.Numpad5, "Old", null, "old-category.png", "old-category-thumbnail.png");

        var result = services.CategoryRepository.CopyToSlot(
            source.Id,
            SlotKey.Numpad5,
            imageFiles => new ImageFileSet(
                imageFiles.ImagePath is null ? null : $"copy-{imageFiles.ImagePath}",
                imageFiles.ThumbnailPath is null ? null : $"copy-{imageFiles.ThumbnailPath}"));

        var categories = services.CategoryRepository.GetAll();
        var copiedCategory = services.CategoryRepository.GetBySlotKey(SlotKey.Numpad5);
        var copiedSnippet = Assert.Single(services.SnippetRepository.GetByCategoryId(copiedCategory!.Id));

        Assert.Equal(2, categories.Count);
        Assert.NotEqual(source.Id, copiedCategory.Id);
        Assert.Equal("Writing", copiedCategory.Name);
        Assert.Equal("Draft prompts", copiedCategory.Description);
        Assert.Equal("copy-source-category.png", copiedCategory.ImagePath);
        Assert.Equal("copy-source-category-thumbnail.png", copiedCategory.ThumbnailPath);
        Assert.Equal("Structure", copiedSnippet.Title);
        Assert.Equal("Make this clearer.", copiedSnippet.Content);
        Assert.Equal("Draft helper", copiedSnippet.Description);
        Assert.Equal(PasteShortcutMode.CtrlV, copiedSnippet.PasteShortcutMode);
        Assert.Equal("copy-source-snippet.png", copiedSnippet.ImagePath);
        Assert.Collection(
            result.OverwrittenImageFiles,
            imageFiles =>
            {
                Assert.Equal("old-category.png", imageFiles.ImagePath);
                Assert.Equal("old-category-thumbnail.png", imageFiles.ThumbnailPath);
            });
    }

    [Fact]
    public void CopyingCategoryPreservesLaunchUrlSnippets()
    {
        var services = CreateServices();
        var source = services.CategoryRepository.Create(SlotKey.Numpad4, "Web", null);
        services.SnippetRepository.Create(
            source.Id,
            SlotKey.Numpad3,
            "Open docs",
            "unused",
            null,
            actionType: SnippetActionType.LaunchUrl,
            launchPath: @"C:\unused",
            launchUrl: "https://example.com/docs");

        services.CategoryRepository.CopyToSlot(source.Id, SlotKey.Numpad5, imageFiles => imageFiles);

        var copiedCategory = services.CategoryRepository.GetBySlotKey(SlotKey.Numpad5);
        var copiedSnippet = Assert.Single(services.SnippetRepository.GetByCategoryId(copiedCategory!.Id));

        Assert.Equal(SnippetActionType.LaunchUrl, copiedSnippet.ActionType);
        Assert.Equal(string.Empty, copiedSnippet.Content);
        Assert.Null(copiedSnippet.LaunchPath);
        Assert.Equal("https://example.com/docs", copiedSnippet.LaunchUrl);
    }

    [Fact]
    public void CopyingCategoryPreservesMediaActionSnippets()
    {
        var services = CreateServices();
        var source = services.CategoryRepository.Create(SlotKey.Numpad4, "Media", null);
        services.SnippetRepository.Create(
            source.Id,
            SlotKey.Numpad3,
            "Mute",
            "unused",
            null,
            actionType: SnippetActionType.MediaAction,
            mediaProvider: SnippetMediaProvider.System,
            mediaCommand: SnippetMediaCommand.Mute);

        services.CategoryRepository.CopyToSlot(source.Id, SlotKey.Numpad5, imageFiles => imageFiles);

        var copiedCategory = services.CategoryRepository.GetBySlotKey(SlotKey.Numpad5);
        var copiedSnippet = Assert.Single(services.SnippetRepository.GetByCategoryId(copiedCategory!.Id));

        Assert.Equal(SnippetActionType.MediaAction, copiedSnippet.ActionType);
        Assert.Equal(string.Empty, copiedSnippet.Content);
        Assert.Null(copiedSnippet.LaunchPath);
        Assert.Null(copiedSnippet.LaunchUrl);
        Assert.Equal(SnippetMediaProvider.System, copiedSnippet.MediaProvider);
        Assert.Equal(SnippetMediaCommand.Mute, copiedSnippet.MediaCommand);
    }

    [Fact]
    public void CopyingCategoryPreservesTerminalCommandSnippets()
    {
        var services = CreateServices();
        var source = services.CategoryRepository.Create(SlotKey.Numpad4, "Tools", null);
        services.SnippetRepository.Create(
            source.Id,
            SlotKey.Numpad3,
            "Reconnect Bluetooth",
            "unused",
            null,
            actionType: SnippetActionType.TerminalCommand,
            terminalCommand: "echo reconnect",
            terminalShell: SnippetTerminalShell.Cmd,
            runAsAdministrator: true);

        services.CategoryRepository.CopyToSlot(source.Id, SlotKey.Numpad5, imageFiles => imageFiles);

        var copiedCategory = services.CategoryRepository.GetBySlotKey(SlotKey.Numpad5);
        var copiedSnippet = Assert.Single(services.SnippetRepository.GetByCategoryId(copiedCategory!.Id));

        Assert.Equal(SnippetActionType.TerminalCommand, copiedSnippet.ActionType);
        Assert.Equal("echo reconnect", copiedSnippet.TerminalCommand);
        Assert.Equal(SnippetTerminalShell.Cmd, copiedSnippet.TerminalShell);
        Assert.True(copiedSnippet.RunAsAdministrator);
    }

    [Fact]
    public void CopyingCategoryPreservesPasteShortcutMode()
    {
        var services = CreateServices();
        var source = services.CategoryRepository.Create(SlotKey.Numpad4, "Terminal", null);
        services.SnippetRepository.Create(
            source.Id,
            SlotKey.Numpad3,
            "Terminal paste",
            "Run this",
            null,
            pasteShortcutMode: PasteShortcutMode.CtrlShiftV);

        services.CategoryRepository.CopyToSlot(source.Id, SlotKey.Numpad5, imageFiles => imageFiles);

        var copiedCategory = services.CategoryRepository.GetBySlotKey(SlotKey.Numpad5);
        var copiedSnippet = Assert.Single(services.SnippetRepository.GetByCategoryId(copiedCategory!.Id));

        Assert.Equal(PasteShortcutMode.CtrlShiftV, copiedSnippet.PasteShortcutMode);
    }

    [Fact]
    public void MovingCategoryToSlotOverwritesTargetAndKeepsSnippets()
    {
        var services = CreateServices();
        var source = services.CategoryRepository.Create(SlotKey.Numpad4, "Writing", null);
        services.SnippetRepository.Create(source.Id, SlotKey.Numpad3, "Structure", "Make this clearer.", null);
        services.CategoryRepository.Create(SlotKey.Numpad5, "Old", null, "old-category.png", "old-category-thumbnail.png");

        var result = services.CategoryRepository.MoveToSlot(source.Id, SlotKey.Numpad5);

        var movedCategory = services.CategoryRepository.GetBySlotKey(SlotKey.Numpad5);
        var movedSnippet = Assert.Single(services.SnippetRepository.GetByCategoryId(source.Id));

        Assert.Null(services.CategoryRepository.GetBySlotKey(SlotKey.Numpad4));
        Assert.Equal(source.Id, movedCategory!.Id);
        Assert.Equal("Writing", movedCategory.Name);
        Assert.Equal("Structure", movedSnippet.Title);
        Assert.Collection(
            result.OverwrittenImageFiles,
            imageFiles =>
            {
                Assert.Equal("old-category.png", imageFiles.ImagePath);
                Assert.Equal("old-category-thumbnail.png", imageFiles.ThumbnailPath);
            });
    }

    [Fact]
    public void CopyingSnippetToSlotOverwritesTargetAndPreservesFields()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Tools", null);
        var autoIcon = new AutoIconCacheEntry(
            "cache-icon.png",
            @"C:\tools\app.exe",
            new DateTime(2026, 6, 9, 0, 0, 0, DateTimeKind.Utc),
            123);
        var source = services.SnippetRepository.Create(
            category.Id,
            SlotKey.Numpad3,
            "Open app",
            "unused",
            "Launch helper",
            "source-snippet.png",
            "source-snippet-thumbnail.png",
            SnippetActionType.LaunchFile,
            @"C:\tools\app.exe",
            SlotImageMode.Custom,
            autoIcon);
        services.SnippetRepository.Create(
            category.Id,
            SlotKey.Numpad5,
            "Old",
            "Bye",
            null,
            "old-snippet.png",
            "old-snippet-thumbnail.png");

        var result = services.SnippetRepository.CopyToSlot(
            source.Id,
            SlotKey.Numpad5,
            imageFiles => new ImageFileSet(
                imageFiles.ImagePath is null ? null : $"copy-{imageFiles.ImagePath}",
                imageFiles.ThumbnailPath is null ? null : $"copy-{imageFiles.ThumbnailPath}"));

        var snippets = services.SnippetRepository.GetByCategoryId(category.Id);
        var copiedSnippet = snippets.Single(snippet => snippet.SlotKey == SlotKey.Numpad5);

        Assert.Equal(2, snippets.Count);
        Assert.NotEqual(source.Id, copiedSnippet.Id);
        Assert.Equal("Open app", copiedSnippet.Title);
        Assert.Equal(string.Empty, copiedSnippet.Content);
        Assert.Equal("Launch helper", copiedSnippet.Description);
        Assert.Equal(SnippetActionType.LaunchFile, copiedSnippet.ActionType);
        Assert.Equal(@"C:\tools\app.exe", copiedSnippet.LaunchPath);
        Assert.Equal(SlotImageMode.Custom, copiedSnippet.SlotImageMode);
        Assert.Equal("copy-source-snippet.png", copiedSnippet.ImagePath);
        Assert.Equal("copy-source-snippet-thumbnail.png", copiedSnippet.ThumbnailPath);
        Assert.Equal(autoIcon.IconPath, copiedSnippet.AutoIconPath);
        Assert.Equal(autoIcon.SourcePath, copiedSnippet.AutoIconSourcePath);
        Assert.Collection(
            result.OverwrittenImageFiles,
            imageFiles =>
            {
                Assert.Equal("old-snippet.png", imageFiles.ImagePath);
                Assert.Equal("old-snippet-thumbnail.png", imageFiles.ThumbnailPath);
            });
    }

    [Fact]
    public void CopyingSnippetToSlotPreservesMediaActionFields()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Media", null);
        var source = services.SnippetRepository.Create(
            category.Id,
            SlotKey.Numpad3,
            "Volume up",
            "unused",
            null,
            actionType: SnippetActionType.MediaAction,
            mediaProvider: SnippetMediaProvider.System,
            mediaCommand: SnippetMediaCommand.VolumeUp);

        var result = services.SnippetRepository.CopyToSlot(source.Id, SlotKey.Numpad5, imageFiles => imageFiles);

        Assert.Equal(SlotKey.Numpad5, result.Snippet.SlotKey);
        Assert.Equal(SnippetActionType.MediaAction, result.Snippet.ActionType);
        Assert.Equal(string.Empty, result.Snippet.Content);
        Assert.Null(result.Snippet.LaunchPath);
        Assert.Null(result.Snippet.LaunchUrl);
        Assert.Equal(SnippetMediaProvider.System, result.Snippet.MediaProvider);
        Assert.Equal(SnippetMediaCommand.VolumeUp, result.Snippet.MediaCommand);
    }

    [Fact]
    public void CopyingSnippetToSlotPreservesPasteShortcutMode()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Terminal", null);
        var source = services.SnippetRepository.Create(
            category.Id,
            SlotKey.Numpad3,
            "Terminal paste",
            "Run this",
            null,
            pasteShortcutMode: PasteShortcutMode.CtrlShiftV);

        var result = services.SnippetRepository.CopyToSlot(source.Id, SlotKey.Numpad5, imageFiles => imageFiles);

        Assert.Equal(PasteShortcutMode.CtrlShiftV, result.Snippet.PasteShortcutMode);
    }

    [Fact]
    public void CopyingSnippetToSlotPreservesSpotifyOpenAndResumeCommand()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Media", null);
        var source = services.SnippetRepository.Create(
            category.Id,
            SlotKey.Numpad3,
            "Open Spotify",
            "unused",
            null,
            actionType: SnippetActionType.MediaAction,
            mediaProvider: SnippetMediaProvider.Spotify,
            mediaCommand: SnippetMediaCommand.OpenSpotifyAndResume);

        var result = services.SnippetRepository.CopyToSlot(source.Id, SlotKey.Numpad5, imageFiles => imageFiles);

        Assert.Equal(SlotKey.Numpad5, result.Snippet.SlotKey);
        Assert.Equal(SnippetActionType.MediaAction, result.Snippet.ActionType);
        Assert.Equal(SnippetMediaProvider.Spotify, result.Snippet.MediaProvider);
        Assert.Equal(SnippetMediaCommand.OpenSpotifyAndResume, result.Snippet.MediaCommand);
    }

    [Fact]
    public void MovingSnippetToSlotOverwritesTargetAndKeepsId()
    {
        var services = CreateServices();
        var category = services.CategoryRepository.Create(SlotKey.Numpad1, "Writing", null);
        var source = services.SnippetRepository.Create(
            category.Id,
            SlotKey.Numpad3,
            "Paste",
            "Hello",
            null,
            pasteShortcutMode: PasteShortcutMode.CtrlShiftV);
        services.SnippetRepository.Create(
            category.Id,
            SlotKey.Numpad5,
            "Old",
            "Bye",
            null,
            "old-snippet.png",
            "old-snippet-thumbnail.png");

        var result = services.SnippetRepository.MoveToSlot(source.Id, SlotKey.Numpad5);

        var movedSnippet = Assert.Single(services.SnippetRepository.GetByCategoryId(category.Id));

        Assert.Equal(source.Id, movedSnippet.Id);
        Assert.Equal(SlotKey.Numpad5, movedSnippet.SlotKey);
        Assert.Equal("Paste", movedSnippet.Title);
        Assert.Equal(PasteShortcutMode.CtrlShiftV, movedSnippet.PasteShortcutMode);
        Assert.Collection(
            result.OverwrittenImageFiles,
            imageFiles =>
            {
                Assert.Equal("old-snippet.png", imageFiles.ImagePath);
                Assert.Equal("old-snippet-thumbnail.png", imageFiles.ThumbnailPath);
            });
    }

    private static void CreateLegacyDatabase(
        string databasePath,
        Guid categoryId,
        Guid snippetId,
        string? snippetImagePath = null,
        string? snippetThumbnailPath = null)
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
            VALUES ($snippetId, $categoryId, 'Numpad3', 'Legacy', 'Keep me', NULL, $snippetImagePath, $snippetThumbnailPath, $now, $now);
            """;
        command.Parameters.AddWithValue("$categoryId", categoryId.ToString());
        command.Parameters.AddWithValue("$snippetId", snippetId.ToString());
        command.Parameters.AddWithValue("$snippetImagePath", (object?)snippetImagePath ?? DBNull.Value);
        command.Parameters.AddWithValue("$snippetThumbnailPath", (object?)snippetThumbnailPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }
}
