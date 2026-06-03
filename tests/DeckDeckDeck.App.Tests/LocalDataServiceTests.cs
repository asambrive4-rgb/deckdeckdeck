using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;
using DeckDeckDeck.App.Services;
using DeckDeckDeck.App.ViewModels;
using DeckDeckDeck.App.Views;
using System.Runtime.InteropServices;
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
        Assert.All(SlotKeyCatalog.All, slotKey => Assert.True(settings.EnabledSlotKeys[slotKey]));
    }

    [Fact]
    public void SettingsSavePersistsAutoHideAndClipboardRestore()
    {
        var services = CreateServices();
        var settings = services.SettingsService.Load();
        settings.AutoHideAfterPaste = false;
        settings.RestoreClipboardAfterPaste = false;

        services.SettingsService.Save(settings);

        var reloaded = CreateServices(services.Storage.AppDataPath).SettingsService.Load();
        Assert.False(reloaded.AutoHideAfterPaste);
        Assert.False(reloaded.RestoreClipboardAfterPaste);
    }

    [Fact]
    public void SettingsViewModelSavesSettingsAndReturns()
    {
        var services = CreateServices();
        var returned = false;
        var status = string.Empty;
        var viewModel = new SettingsViewModel(
            services.SettingsService,
            () => { },
            () => returned = true,
            message => status = message,
            services.LoggingService)
        {
            AutoHideAfterPaste = false,
            RestoreClipboardAfterPaste = false
        };

        viewModel.SaveCommand.Execute(null);

        var reloaded = services.SettingsService.Load();
        Assert.False(reloaded.AutoHideAfterPaste);
        Assert.False(reloaded.RestoreClipboardAfterPaste);
        Assert.True(returned);
        Assert.Equal("Settings saved.", status);
    }

    [Fact]
    public void SettingsViewLoads()
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                var services = CreateServices();
                var viewModel = new SettingsViewModel(
                    services.SettingsService,
                    () => { },
                    () => { },
                    _ => { },
                    services.LoggingService);
                var view = new SettingsView { DataContext = viewModel };

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

    [Fact]
    public void LoggingServiceWritesAppLog()
    {
        var services = CreateServices();
        var logPath = Path.Combine(services.Storage.LogsPath, "app.log");

        services.LoggingService.Log("Paste failed.");

        Assert.True(File.Exists(logPath));
        Assert.Contains("Paste failed.", File.ReadAllText(logPath));
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

    [Fact]
    public void ThumbnailServiceCopiesImageAndCreatesThumbnail()
    {
        var services = CreateServices();
        var sourcePath = CreateTinyBmp(services.Storage.TempPath);

        var storedImage = RunInSta(() => services.ThumbnailService.StoreImage(sourcePath));

        Assert.True(File.Exists(storedImage.ImagePath));
        Assert.True(File.Exists(storedImage.ThumbnailPath));
        Assert.EndsWith(".bmp", storedImage.ImagePath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(".png", storedImage.ThumbnailPath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(
            Path.GetFullPath(services.Storage.ImageOriginalsPath),
            Path.GetFullPath(storedImage.ImagePath),
            StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(
            Path.GetFullPath(services.Storage.ImageThumbnailsPath),
            Path.GetFullPath(storedImage.ThumbnailPath),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ThumbnailServiceRejectsMissingImage()
    {
        var services = CreateServices();
        var missingPath = Path.Combine(services.Storage.TempPath, "missing.png");

        var exception = Assert.Throws<InvalidOperationException>(() => services.ThumbnailService.StoreImage(missingPath));

        Assert.Contains("does not exist", exception.Message);
    }

    [Fact]
    public void ThumbnailServiceRejectsUnsupportedImageType()
    {
        var services = CreateServices();
        var textPath = Path.Combine(services.Storage.TempPath, "not-image.txt");
        File.WriteAllText(textPath, "not an image");

        var exception = Assert.Throws<InvalidOperationException>(() => services.ThumbnailService.StoreImage(textPath));

        Assert.Contains("Unsupported image type", exception.Message);
    }

    [Fact]
    public void SlotViewModelReportsThumbnailWhenPathIsPresent()
    {
        var slot = new SlotViewModel(
            SlotKey.Numpad1,
            "Writing",
            "thumbnail.png",
            true,
            _ => { });

        Assert.True(slot.HasThumbnail);
        Assert.Equal("thumbnail.png", slot.ThumbnailPath);
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

    [Fact]
    public void HomeHotkeyAlwaysReturnsToHome()
    {
        var services = CreateServices();
        services.CategoryService.Create(SlotKey.Numpad1, "Writing", null);
        var viewModel = CreateMainViewModel(services);
        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);

        viewModel.OpenHomeFromHotkey();

        Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);
    }

    [Fact]
    public void DirectCategoryHotkeyOpensExistingCategory()
    {
        var services = CreateServices();
        services.CategoryService.Create(SlotKey.Numpad1, "Writing", null);
        var viewModel = CreateMainViewModel(services);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);

        Assert.IsType<CategoryViewModel>(viewModel.CurrentViewModel);
        Assert.Equal("Writing category", viewModel.StatusMessage);
    }

    [Fact]
    public void DirectCategoryHotkeyOpensCategoryEditorForEmptySlot()
    {
        var services = CreateServices();
        var enteredEditMode = false;
        var viewModel = CreateMainViewModel(
            services,
            enterEditMode: () => enteredEditMode = true);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad2);

        Assert.IsType<CategoryEditViewModel>(viewModel.CurrentViewModel);
        Assert.Equal("New category for 2", viewModel.StatusMessage);
        Assert.True(enteredEditMode);
    }

    [Fact]
    public void DirectCategoryHotkeyDoesNotNavigateWhenSlotIsDisabled()
    {
        var services = CreateServices();
        services.CategoryService.Create(SlotKey.Numpad1, "Writing", null);
        services.SettingsService.SetSlotEnabled(SlotKey.Numpad1, false);
        var viewModel = CreateMainViewModel(services);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);

        Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);
        Assert.Equal("1 slot is disabled.", viewModel.StatusMessage);
    }

    [Fact]
    public void HomeSettingsCommandOpensSettingsViewModel()
    {
        var services = CreateServices();
        var viewModel = CreateMainViewModel(services);
        var home = Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);

        home.SettingsCommand.Execute(null);

        Assert.IsType<SettingsViewModel>(viewModel.CurrentViewModel);
        Assert.Equal("Settings", viewModel.StatusMessage);
    }

    [Fact]
    public void HomeSlotEditCommandOpensCategoryEditor()
    {
        var services = CreateServices();
        services.CategoryService.Create(SlotKey.Numpad1, "Writing", null);
        var viewModel = CreateMainViewModel(services);
        var home = Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);

        home.NumpadGrid.Numpad1.EditCommand.Execute(null);

        Assert.IsType<CategoryEditViewModel>(viewModel.CurrentViewModel);
        Assert.Equal("Edit Writing", viewModel.StatusMessage);
    }

    [Fact]
    public void CategoryEditorCancelReturnsHome()
    {
        var services = CreateServices();
        services.CategoryService.Create(SlotKey.Numpad1, "Writing", null);
        var viewModel = CreateMainViewModel(services);
        var home = Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);
        home.NumpadGrid.Numpad1.EditCommand.Execute(null);
        var editor = Assert.IsType<CategoryEditViewModel>(viewModel.CurrentViewModel);

        editor.CancelCommand.Execute(null);

        Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);
    }

    [Fact]
    public void CategoryEditorSaveReturnsHome()
    {
        var services = CreateServices();
        services.CategoryService.Create(SlotKey.Numpad1, "Writing", null);
        var viewModel = CreateMainViewModel(services);
        var home = Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);
        home.NumpadGrid.Numpad1.EditCommand.Execute(null);
        var editor = Assert.IsType<CategoryEditViewModel>(viewModel.CurrentViewModel);
        editor.Name = "Writing Updated";

        editor.SaveCommand.Execute(null);

        Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);
        Assert.Equal("Writing Updated", Assert.Single(services.CategoryService.GetAll()).Name);
    }

    [Fact]
    public void CategorySlotEditCommandOpensSnippetEditor()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(SlotKey.Numpad1, "Writing", null);
        services.SnippetService.Create(category.Id, SlotKey.Numpad3, "Structure", "Make this clearer.", null);
        var viewModel = CreateMainViewModel(services);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);
        var categoryViewModel = Assert.IsType<CategoryViewModel>(viewModel.CurrentViewModel);
        categoryViewModel.NumpadGrid.Numpad3.EditCommand.Execute(null);

        Assert.IsType<SnippetEditViewModel>(viewModel.CurrentViewModel);
        Assert.Equal("Edit Structure", viewModel.StatusMessage);
    }

    [Fact]
    public void DisabledSlotEditCommandStillOpensCategoryEditor()
    {
        var services = CreateServices();
        services.SettingsService.SetSlotEnabled(SlotKey.Numpad2, false);
        var viewModel = CreateMainViewModel(services);
        var home = Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);

        Assert.False(home.NumpadGrid.Numpad2.SelectCommand.CanExecute(null));
        home.NumpadGrid.Numpad2.EditCommand.Execute(null);

        Assert.IsType<CategoryEditViewModel>(viewModel.CurrentViewModel);
        Assert.Equal("New category for 2", viewModel.StatusMessage);
    }

    [Fact]
    public void EmptyCategorySlotEditorCanSaveSlotEnabledOnly()
    {
        var services = CreateServices();
        var viewModel = CreateMainViewModel(services);
        var home = Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);

        home.NumpadGrid.Numpad2.EditCommand.Execute(null);
        var editor = Assert.IsType<CategoryEditViewModel>(viewModel.CurrentViewModel);
        editor.IsSlotEnabled = false;
        editor.SaveCommand.Execute(null);

        Assert.False(services.SettingsService.Load().EnabledSlotKeys[SlotKey.Numpad2]);
        Assert.IsType<HomeViewModel>(viewModel.CurrentViewModel);
    }

    [Fact]
    public void EmptySnippetSlotEditorCanSaveSlotEnabledOnly()
    {
        var services = CreateServices();
        services.CategoryService.Create(SlotKey.Numpad1, "Writing", null);
        var viewModel = CreateMainViewModel(services);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);
        var categoryViewModel = Assert.IsType<CategoryViewModel>(viewModel.CurrentViewModel);
        categoryViewModel.NumpadGrid.Numpad4.EditCommand.Execute(null);
        var editor = Assert.IsType<SnippetEditViewModel>(viewModel.CurrentViewModel);
        editor.IsSlotEnabled = false;
        editor.SaveCommand.Execute(null);

        Assert.False(services.SettingsService.Load().EnabledSlotKeys[SlotKey.Numpad4]);
        Assert.IsType<CategoryViewModel>(viewModel.CurrentViewModel);
    }

    [Fact]
    public async Task ClipboardPasteBacksUpPastesAndRestoresClipboard()
    {
        var originalClipboard = new TestDataObject();
        var clipboard = new FakeClipboardService(originalClipboard);
        var keyboard = new FakeKeyboardInputService();
        var focus = new FakeWindowFocusService();
        var service = new ClipboardPasteService(clipboard, keyboard, focus, TimeSpan.Zero, TimeSpan.Zero);
        var snippet = new Snippet { Content = "Line 1\r\n**Line 2**" };

        var pasted = await service.PasteSnippetAsync(snippet, new IntPtr(123), new AppSettings());

        Assert.True(pasted);
        Assert.Same(originalClipboard, clipboard.Backup);
        Assert.Equal("Line 1\r\n**Line 2**", Assert.Single(clipboard.SetTexts));
        Assert.Equal(new IntPtr(123), focus.ActivatedHandle);
        Assert.True(keyboard.SentCtrlV);
        Assert.Same(originalClipboard, clipboard.Restored);
    }

    [Fact]
    public async Task ClipboardPasteDoesNotRestoreWhenSettingIsDisabled()
    {
        var originalClipboard = new TestDataObject();
        var clipboard = new FakeClipboardService(originalClipboard);
        var service = new ClipboardPasteService(
            clipboard,
            new FakeKeyboardInputService(),
            new FakeWindowFocusService(),
            TimeSpan.Zero,
            TimeSpan.Zero);
        var settings = new AppSettings { RestoreClipboardAfterPaste = false };

        var pasted = await service.PasteSnippetAsync(
            new Snippet { Content = "Paste me" },
            new IntPtr(123),
            settings);

        Assert.True(pasted);
        Assert.Null(clipboard.Restored);
    }

    [Fact]
    public async Task ClipboardPasteSendsCtrlVEvenWhenFocusRestoreFails()
    {
        var clipboard = new FakeClipboardService(new TestDataObject());
        var keyboard = new FakeKeyboardInputService();
        var focus = new FakeWindowFocusService { CanActivate = false };
        var service = new ClipboardPasteService(clipboard, keyboard, focus, TimeSpan.Zero, TimeSpan.Zero);

        var pasted = await service.PasteSnippetAsync(
            new Snippet { Content = "Paste me anyway" },
            new IntPtr(123),
            new AppSettings());

        Assert.True(pasted);
        Assert.Equal(new IntPtr(123), focus.ActivatedHandle);
        Assert.True(keyboard.SentCtrlV);
    }

    [Fact]
    public void Win32InputStructUsesNativeSize()
    {
        var expectedSize = IntPtr.Size == 8 ? 40 : 28;

        Assert.Equal(expectedSize, Marshal.SizeOf<User32.Input>());
    }

    [Fact]
    public void ExistingSnippetSlotPastesInsteadOfOpeningEditor()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(SlotKey.Numpad1, "Writing", null);
        services.SnippetService.Create(category.Id, SlotKey.Numpad3, "Structure", "Make this clearer.", null);
        var pasteService = new RecordingClipboardPasteService();
        var hidden = false;
        var completedPasteSelection = false;
        var viewModel = CreateMainViewModel(
            services,
            pasteService,
            () => new IntPtr(123),
            () => hidden = true,
            completePasteSelection: () => completedPasteSelection = true);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);
        viewModel.SelectSlot(SlotKey.Numpad3);

        Assert.IsType<CategoryViewModel>(viewModel.CurrentViewModel);
        var call = Assert.Single(pasteService.Calls);
        Assert.Equal("Make this clearer.", call.Snippet.Content);
        Assert.Equal(new IntPtr(123), call.TargetWindowHandle);
        Assert.True(hidden);
        Assert.True(completedPasteSelection);
    }

    [Fact]
    public void EmptySnippetSlotStillOpensEditor()
    {
        var services = CreateServices();
        services.CategoryService.Create(SlotKey.Numpad1, "Writing", null);
        var pasteService = new RecordingClipboardPasteService();
        var enteredEditMode = false;
        var viewModel = CreateMainViewModel(
            services,
            pasteService,
            () => new IntPtr(123),
            () => { },
            enterEditMode: () => enteredEditMode = true);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);
        viewModel.SelectSlot(SlotKey.Numpad4);

        Assert.IsType<SnippetEditViewModel>(viewModel.CurrentViewModel);
        Assert.Empty(pasteService.Calls);
        Assert.True(enteredEditMode);
    }

    [Fact]
    public void PasteSelectionIsCompletedWhenPasteServiceThrows()
    {
        var services = CreateServices();
        var category = services.CategoryService.Create(SlotKey.Numpad1, "Writing", null);
        services.SnippetService.Create(category.Id, SlotKey.Numpad3, "Structure", "Make this clearer.", null);
        var completedPasteSelection = false;
        var viewModel = CreateMainViewModel(
            services,
            new ThrowingClipboardPasteService(),
            () => new IntPtr(123),
            () => { },
            completePasteSelection: () => completedPasteSelection = true);

        viewModel.OpenCategoryFromHotkey(SlotKey.Numpad1);
        viewModel.SelectSlot(SlotKey.Numpad3);

        Assert.IsType<CategoryViewModel>(viewModel.CurrentViewModel);
        Assert.True(completedPasteSelection);
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
            settingsService,
            new LoggingService(storage),
            new ThumbnailService(storage));
    }

    private static MainViewModel CreateMainViewModel(
        TestServices services,
        IClipboardPasteService? clipboardPasteService = null,
        Func<IntPtr>? getPasteTargetWindowHandle = null,
        Action? hideWindowAfterPaste = null,
        Action? enterEditMode = null,
        Action? completePasteSelection = null)
    {
        return new MainViewModel(
            services.CategoryService,
            new DialogService(),
            services.SettingsService,
            new SlotService(),
            services.SnippetService,
            clipboardPasteService,
            getPasteTargetWindowHandle,
            hideWindowAfterPaste,
            enterEditMode,
            completePasteSelection,
            loggingService: services.LoggingService,
            thumbnailService: services.ThumbnailService);
    }

    private sealed record TestServices(
        FileStorageService Storage,
        CategoryService CategoryService,
        SnippetService SnippetService,
        SettingsService SettingsService,
        LoggingService LoggingService,
        ThumbnailService ThumbnailService);

    private static string CreateTinyBmp(string directory)
    {
        var path = Path.Combine(directory, "source.bmp");
        byte[] bytes =
        [
            0x42, 0x4D, 0x46, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x36, 0x00, 0x00, 0x00, 0x28, 0x00,
            0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x02, 0x00,
            0x00, 0x00, 0x01, 0x00, 0x18, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0x00,
            0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00, 0xFF,
            0x00, 0xFF, 0xFF, 0xFF, 0x00, 0x00
        ];

        File.WriteAllBytes(path, bytes);

        return path;
    }

    private static T RunInSta<T>(Func<T> action)
    {
        T? result = default;
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
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

        if (exception is not null)
        {
            throw exception;
        }

        return result!;
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public FakeClipboardService(IDataObject? backup)
        {
            Backup = backup;
        }

        public IDataObject? Backup { get; }

        public IDataObject? Restored { get; private set; }

        public List<string> SetTexts { get; } = [];

        public IDataObject? GetDataObject()
        {
            return Backup;
        }

        public void SetText(string text)
        {
            SetTexts.Add(text);
        }

        public void SetDataObject(IDataObject dataObject)
        {
            Restored = dataObject;
        }
    }

    private sealed class FakeKeyboardInputService : IKeyboardInputService
    {
        public bool SentCtrlV { get; private set; }

        public bool SendCtrlV()
        {
            SentCtrlV = true;
            return true;
        }
    }

    private sealed class FakeWindowFocusService : IWindowFocusService
    {
        public bool CanActivate { get; set; } = true;

        public IntPtr ActivatedHandle { get; private set; }

        public IntPtr GetForegroundWindow()
        {
            return IntPtr.Zero;
        }

        public bool TryActivate(IntPtr windowHandle)
        {
            ActivatedHandle = windowHandle;
            return CanActivate;
        }
    }

    private sealed class RecordingClipboardPasteService : IClipboardPasteService
    {
        public List<PasteCall> Calls { get; } = [];

        public Task<bool> PasteSnippetAsync(Snippet snippet, IntPtr targetWindowHandle, AppSettings settings)
        {
            Calls.Add(new PasteCall(snippet, targetWindowHandle, settings));
            return Task.FromResult(true);
        }
    }

    private sealed record PasteCall(Snippet Snippet, IntPtr TargetWindowHandle, AppSettings Settings);

    private sealed class ThrowingClipboardPasteService : IClipboardPasteService
    {
        public Task<bool> PasteSnippetAsync(Snippet snippet, IntPtr targetWindowHandle, AppSettings settings)
        {
            return Task.FromException<bool>(new InvalidOperationException("Paste failed."));
        }
    }

    private sealed class TestDataObject : IDataObject
    {
        public object GetData(string format)
        {
            return string.Empty;
        }

        public object GetData(Type format)
        {
            return string.Empty;
        }

        public object GetData(string format, bool autoConvert)
        {
            return string.Empty;
        }

        public bool GetDataPresent(string format)
        {
            return false;
        }

        public bool GetDataPresent(Type format)
        {
            return false;
        }

        public bool GetDataPresent(string format, bool autoConvert)
        {
            return false;
        }

        public string[] GetFormats()
        {
            return [];
        }

        public string[] GetFormats(bool autoConvert)
        {
            return [];
        }

        public void SetData(object data)
        {
        }

        public void SetData(string format, object data)
        {
        }

        public void SetData(Type format, object data)
        {
        }

        public void SetData(string format, object data, bool autoConvert)
        {
        }
    }
}
