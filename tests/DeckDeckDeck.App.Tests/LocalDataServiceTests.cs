using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Services;
using DeckDeckDeck.App.ViewModels;
using DeckDeckDeck.App.Views;
using System.Windows;
using System.Windows.Threading;

namespace DeckDeckDeck.App.Tests;

public sealed class LocalDataServiceTests
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
    public void SettingsDefaultsAreCreated()
    {
        var services = CreateServices();

        var settings = services.SettingsService.Load();

        Assert.True(settings.AutoHideAfterPaste);
        Assert.True(settings.RestoreClipboardAfterPaste);
        Assert.True(settings.ShowDisabledSlots);
        Assert.All(SlotKeyCatalog.All, slotKey => Assert.True(settings.EnabledSlotKeys[slotKey]));
    }

    [Fact]
    public void SnippetEditViewLoadsForEmptySlot()
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                var services = CreateServices();
                var category = services.CategoryService.Create(SlotKey.Numpad1, "Writing", null);
                var viewModel = new SnippetEditViewModel(
                    category,
                    SlotKey.Numpad3,
                    snippet: null,
                    services.SnippetService,
                    new DialogService(),
                    () => { },
                    _ => { },
                    () => { },
                    _ => { });
                var view = new SnippetEditView { DataContext = viewModel };

                view.Measure(new Size(560, 680));
                view.Arrange(new Rect(0, 0, 560, 680));
                view.UpdateLayout();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(exception);
    }

    private static TestServices CreateServices(string? appDataPath = null)
    {
        var storage = new FileStorageService(appDataPath ?? Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        storage.EnsureCreated();

        var dbContextFactory = new AppDbContextFactory(storage.DatabasePath);
        dbContextFactory.EnsureCreated();

        var settingsService = new SettingsService(dbContextFactory);
        settingsService.EnsureDefaults();

        return new TestServices(
            storage,
            new CategoryService(dbContextFactory),
            new SnippetService(dbContextFactory),
            settingsService);
    }

    private sealed record TestServices(
        FileStorageService Storage,
        CategoryService CategoryService,
        SnippetService SnippetService,
        SettingsService SettingsService);
}
