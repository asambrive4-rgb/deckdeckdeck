using CommunityToolkit.Mvvm.ComponentModel;
using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Services;

namespace DeckDeckDeck.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly CategoryService _categoryService;
    private readonly DialogService _dialogService;
    private readonly SettingsService _settingsService;
    private readonly SlotService _slotService;
    private readonly SnippetService _snippetService;
    private object _currentViewModel = null!;
    private string _statusMessage = "Ready.";

    public MainViewModel()
    {
        var fileStorageService = new FileStorageService();
        fileStorageService.EnsureCreated();

        var dbContextFactory = new AppDbContextFactory(fileStorageService.DatabasePath);
        dbContextFactory.EnsureCreated();

        _categoryService = new CategoryService(dbContextFactory);
        _dialogService = new DialogService();
        _settingsService = new SettingsService(dbContextFactory);
        _slotService = new SlotService();
        _snippetService = new SnippetService(dbContextFactory);
        _settingsService.EnsureDefaults();

        ShowHome();
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
            CreateCategory);
        StatusMessage = "Home";
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
        CurrentViewModel = new CategoryEditViewModel(
            slotKey,
            category: null,
            _categoryService,
            _dialogService,
            ShowHome,
            _ => ShowHome(),
            ShowHome,
            ShowStatus);
        StatusMessage = $"New category for {slotKey.GetDisplayText()}";
    }

    private void EditCategory(Category category)
    {
        CurrentViewModel = new CategoryEditViewModel(
            category.SlotKey,
            category,
            _categoryService,
            _dialogService,
            () => OpenCategoryById(category.Id),
            savedCategory => OpenCategory(savedCategory),
            ShowHome,
            ShowStatus);
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
            EditCategory,
            EditSnippet);
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
        CurrentViewModel = new SnippetEditViewModel(
            category,
            slotKey,
            snippet,
            _snippetService,
            _dialogService,
            () => OpenCategoryById(category.Id),
            _ => OpenCategoryById(category.Id),
            () => OpenCategoryById(category.Id),
            ShowStatus);
        StatusMessage = snippet is null
            ? $"New snippet for {slotKey.GetDisplayText()}"
            : $"Edit {snippet.Title}";
    }

    private void ShowStatus(string message)
    {
        StatusMessage = message;
    }
}
