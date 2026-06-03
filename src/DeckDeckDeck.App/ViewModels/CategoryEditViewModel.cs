using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Services;

namespace DeckDeckDeck.App.ViewModels;

public sealed class CategoryEditViewModel : ObservableObject
{
    private readonly Action _afterDelete;
    private readonly Action<Category> _afterSave;
    private readonly CategoryService _categoryService;
    private readonly DialogService _dialogService;
    private readonly Guid? _categoryId;
    private readonly Action<string> _showStatus;
    private string _description = string.Empty;
    private string _errorMessage = string.Empty;
    private string _name = string.Empty;

    public CategoryEditViewModel(
        SlotKey slotKey,
        Category? category,
        CategoryService categoryService,
        DialogService dialogService,
        Action cancel,
        Action<Category> afterSave,
        Action afterDelete,
        Action<string> showStatus)
    {
        SlotKey = slotKey;
        KeyText = slotKey.GetDisplayText();
        _categoryId = category?.Id;
        _categoryService = categoryService;
        _dialogService = dialogService;
        _afterSave = afterSave;
        _afterDelete = afterDelete;
        _showStatus = showStatus;

        _name = category?.Name ?? string.Empty;
        _description = category?.Description ?? string.Empty;
        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(cancel);
        DeleteCommand = new RelayCommand(Delete);
    }

    public string Title => IsExisting ? "Edit Category" : "New Category";

    public SlotKey SlotKey { get; }

    public string KeyText { get; }

    public bool IsExisting => _categoryId.HasValue;

    public bool CanDelete => IsExisting;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public ICommand SaveCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand DeleteCommand { get; }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Category name is required.";
            return;
        }

        var category = _categoryId.HasValue
            ? _categoryService.Update(_categoryId.Value, Name, Description)
            : _categoryService.Create(SlotKey, Name, Description);

        _showStatus($"{category.Name} saved.");
        _afterSave(category);
    }

    private void Delete()
    {
        if (!_categoryId.HasValue)
        {
            return;
        }

        var confirmed = _dialogService.Confirm(
            "Delete category",
            "Delete this category and all snippets inside it?");

        if (!confirmed)
        {
            return;
        }

        _categoryService.Delete(_categoryId.Value);
        _showStatus("Category deleted.");
        _afterDelete();
    }
}
