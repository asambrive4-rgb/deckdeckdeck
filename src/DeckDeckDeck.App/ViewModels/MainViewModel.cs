using CommunityToolkit.Mvvm.ComponentModel;
using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Services;

namespace DeckDeckDeck.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly CategoryService _categoryService;
    private readonly DialogService _dialogService;
    private readonly IClipboardPasteService _clipboardPasteService;
    private readonly SettingsService _settingsService;
    private readonly SlotService _slotService;
    private readonly SnippetService _snippetService;
    private readonly LoggingService? _loggingService;
    private readonly ThumbnailService? _thumbnailService;
    private readonly Func<IntPtr> _getPasteTargetWindowHandle;
    private readonly Action _hideWindowAfterPaste;
    private readonly Action _enterEditMode;
    private readonly Action _completePasteSelection;
    private object _currentViewModel = null!;
    private string _statusMessage = "Ready.";

    public MainViewModel(
        Func<IntPtr>? getPasteTargetWindowHandle = null,
        Action? hideWindowAfterPaste = null,
        Action? enterEditMode = null,
        Action? completePasteSelection = null)
        : this(CreateDefaultServices(), getPasteTargetWindowHandle, hideWindowAfterPaste, enterEditMode, completePasteSelection)
    {
    }

    public MainViewModel(
        CategoryService categoryService,
        DialogService dialogService,
        SettingsService settingsService,
        SlotService slotService,
        SnippetService snippetService,
        IClipboardPasteService? clipboardPasteService = null,
        Func<IntPtr>? getPasteTargetWindowHandle = null,
        Action? hideWindowAfterPaste = null,
        Action? enterEditMode = null,
        Action? completePasteSelection = null,
        LoggingService? loggingService = null,
        ThumbnailService? thumbnailService = null)
    {
        _categoryService = categoryService;
        _dialogService = dialogService;
        _clipboardPasteService = clipboardPasteService ?? new ClipboardPasteService();
        _settingsService = settingsService;
        _slotService = slotService;
        _snippetService = snippetService;
        _loggingService = loggingService;
        _thumbnailService = thumbnailService;
        _getPasteTargetWindowHandle = getPasteTargetWindowHandle ?? (() => IntPtr.Zero);
        _hideWindowAfterPaste = hideWindowAfterPaste ?? (() => { });
        _enterEditMode = enterEditMode ?? (() => { });
        _completePasteSelection = completePasteSelection ?? (() => { });
        _settingsService.EnsureDefaults();

        ShowHome();
    }

    private MainViewModel(
        DefaultServices services,
        Func<IntPtr>? getPasteTargetWindowHandle,
        Action? hideWindowAfterPaste,
        Action? enterEditMode,
        Action? completePasteSelection)
        : this(
            services.CategoryService,
            services.DialogService,
            services.SettingsService,
            services.SlotService,
            services.SnippetService,
            services.ClipboardPasteService,
            getPasteTargetWindowHandle,
            hideWindowAfterPaste,
            enterEditMode,
            completePasteSelection,
            services.LoggingService,
            services.ThumbnailService)
    {
    }

    public string WindowTitle => "DeckDeckDeck";

    public object CurrentViewModel
    {
        get => _currentViewModel;
        private set => SetProperty(ref _currentViewModel, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public void ShowHome()
    {
        CurrentViewModel = new HomeViewModel(
            _categoryService,
            _settingsService,
            _slotService,
            OpenCategory,
            EditCategory,
            CreateCategory,
            () => ShowSettings(ShowHome));
        StatusMessage = "Home";
    }

    public void OpenHomeFromHotkey()
    {
        ShowHome();
        StatusMessage = "Home opened from global hotkey.";
    }

    public void OpenCategoryFromHotkey(SlotKey slotKey)
    {
        if (!IsDirectCategorySlot(slotKey))
        {
            StatusMessage = "Global direct category hotkeys only support Ctrl+Numpad 1~9.";
            return;
        }

        var settings = _settingsService.Load();
        if (settings.EnabledSlotKeys.TryGetValue(slotKey, out var isEnabled) && !isEnabled)
        {
            StatusMessage = $"{slotKey.GetDisplayText()} slot is disabled.";
            return;
        }

        var category = _categoryService.GetBySlotKey(slotKey);
        if (category is null)
        {
            CreateCategory(slotKey);
            return;
        }

        OpenCategory(category);
    }

    public void ReportHotkeyRegistrationFailure(IReadOnlyList<string> failures)
    {
        if (failures.Count == 0)
        {
            return;
        }

        StatusMessage = failures.Count == 1
            ? failures[0]
            : $"global hotkey registration failed for {failures.Count} hotkeys.";
        _loggingService?.Log(StatusMessage);
    }

    public bool SelectSlot(SlotKey slotKey)
    {
        return CurrentViewModel switch
        {
            HomeViewModel homeViewModel => homeViewModel.SelectSlot(slotKey),
            CategoryViewModel categoryViewModel => categoryViewModel.SelectSlot(slotKey),
            _ => false
        };
    }

    private void CreateCategory(SlotKey slotKey)
    {
        _enterEditMode();
        CurrentViewModel = new CategoryEditViewModel(
            slotKey,
            category: null,
            _categoryService,
            _dialogService,
            ShowHome,
            _ => ShowHome(),
            ShowHome,
            ShowStatus,
            _thumbnailService,
            _settingsService,
            _loggingService);
        StatusMessage = $"New category for {slotKey.GetDisplayText()}";
    }

    private void EditCategory(Category category)
    {
        _enterEditMode();
        CurrentViewModel = new CategoryEditViewModel(
            category.SlotKey,
            category,
            _categoryService,
            _dialogService,
            ShowHome,
            _ => ShowHome(),
            ShowHome,
            ShowStatus,
            _thumbnailService,
            _settingsService,
            _loggingService);
        StatusMessage = $"Edit {category.Name}";
    }

    private void OpenCategory(Category category)
    {
        CurrentViewModel = new CategoryViewModel(
            category,
            _snippetService,
            _settingsService,
            _slotService,
            ShowHome,
            () => ShowSettings(() => OpenCategoryById(category.Id)),
            EditSnippet,
            PasteSnippet);
        StatusMessage = $"{category.Name} category";
    }

    private void OpenCategoryById(Guid categoryId)
    {
        var category = _categoryService.GetById(categoryId);

        if (category is null)
        {
            ShowHome();
            return;
        }

        OpenCategory(category);
    }

    private void EditSnippet(Category category, SlotKey slotKey, Snippet? snippet)
    {
        _enterEditMode();
        CurrentViewModel = new SnippetEditViewModel(
            category,
            slotKey,
            snippet,
            _snippetService,
            _dialogService,
            () => OpenCategoryById(category.Id),
            _ => OpenCategoryById(category.Id),
            () => OpenCategoryById(category.Id),
            ShowStatus,
            _thumbnailService,
            _settingsService,
            _loggingService);
        StatusMessage = snippet is null
            ? $"New snippet for {slotKey.GetDisplayText()}"
            : $"Edit {snippet.Title}";
    }

    private void ShowSettings(Action returnTo)
    {
        _enterEditMode();
        CurrentViewModel = new SettingsViewModel(
            _settingsService,
            returnTo,
            returnTo,
            ShowStatus,
            _loggingService);
        StatusMessage = "Settings";
    }

    private async Task PasteSnippet(Snippet snippet)
    {
        var settings = _settingsService.Load();

        try
        {
            if (settings.AutoHideAfterPaste)
            {
                _hideWindowAfterPaste();
            }

            var pasted = await _clipboardPasteService.PasteSnippetAsync(
                snippet,
                _getPasteTargetWindowHandle(),
                settings);

            if (!pasted)
            {
                _loggingService?.Log($"Paste failed for snippet {snippet.Id}.");
            }
        }
        catch (Exception ex)
        {
            _loggingService?.Log($"Paste failed for snippet {snippet.Id}.", ex);
            throw;
        }
        finally
        {
            _completePasteSelection();
        }
    }

    private void ShowStatus(string message)
    {
        StatusMessage = message;
    }

    private static bool IsDirectCategorySlot(SlotKey slotKey)
    {
        return slotKey is SlotKey.Numpad1
            or SlotKey.Numpad2
            or SlotKey.Numpad3
            or SlotKey.Numpad4
            or SlotKey.Numpad5
            or SlotKey.Numpad6
            or SlotKey.Numpad7
            or SlotKey.Numpad8
            or SlotKey.Numpad9;
    }

    private static DefaultServices CreateDefaultServices()
    {
        var fileStorageService = new FileStorageService();
        fileStorageService.EnsureCreated();

        var dbContextFactory = new AppDbContextFactory(fileStorageService.DatabasePath);
        dbContextFactory.EnsureCreated();

        return new DefaultServices(
            new CategoryService(dbContextFactory),
            new DialogService(),
            new SettingsService(dbContextFactory),
            new SlotService(),
            new SnippetService(dbContextFactory),
            new ClipboardPasteService(),
            new LoggingService(fileStorageService),
            new ThumbnailService(fileStorageService));
    }

    private sealed record DefaultServices(
        CategoryService CategoryService,
        DialogService DialogService,
        SettingsService SettingsService,
        SlotService SlotService,
        SnippetService SnippetService,
        IClipboardPasteService ClipboardPasteService,
        LoggingService LoggingService,
        ThumbnailService ThumbnailService);
}
