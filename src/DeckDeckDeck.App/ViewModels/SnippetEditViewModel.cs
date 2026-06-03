using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Services;

namespace DeckDeckDeck.App.ViewModels;

public sealed class SnippetEditViewModel : ObservableObject
{
    private readonly Action _afterDelete;
    private readonly Action<Snippet> _afterSave;
    private readonly DialogService _dialogService;
    private readonly Guid? _snippetId;
    private readonly SnippetService _snippetService;
    private readonly Action<string> _showStatus;
    private string _content = string.Empty;
    private string _description = string.Empty;
    private string _errorMessage = string.Empty;
    private string _snippetTitle = string.Empty;

    public SnippetEditViewModel(
        Category category,
        SlotKey slotKey,
        Snippet? snippet,
        SnippetService snippetService,
        DialogService dialogService,
        Action cancel,
        Action<Snippet> afterSave,
        Action afterDelete,
        Action<string> showStatus)
    {
        CategoryId = category.Id;
        CategoryName = category.Name;
        SlotKey = slotKey;
        KeyText = slotKey.GetDisplayText();
        _snippetId = snippet?.Id;
        _snippetService = snippetService;
        _dialogService = dialogService;
        _afterSave = afterSave;
        _afterDelete = afterDelete;
        _showStatus = showStatus;

        _snippetTitle = snippet?.Title ?? string.Empty;
        _content = snippet?.Content ?? string.Empty;
        _description = snippet?.Description ?? string.Empty;
        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(cancel);
        DeleteCommand = new RelayCommand(Delete);
    }

    public string Title => IsExisting ? "Edit Snippet" : "New Snippet";

    public Guid CategoryId { get; }

    public string CategoryName { get; }

    public SlotKey SlotKey { get; }

    public string KeyText { get; }

    public bool IsExisting => _snippetId.HasValue;

    public bool CanDelete => IsExisting;

    public string SnippetTitle
    {
        get => _snippetTitle;
        set => SetProperty(ref _snippetTitle, value);
    }

    public string Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
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
        if (string.IsNullOrWhiteSpace(SnippetTitle))
        {
            ErrorMessage = "Snippet title is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Content))
        {
            ErrorMessage = "Snippet content is required.";
            return;
        }

        var snippet = _snippetId.HasValue
            ? _snippetService.Update(_snippetId.Value, SnippetTitle, Content, Description)
            : _snippetService.Create(CategoryId, SlotKey, SnippetTitle, Content, Description);

        _showStatus($"{snippet.Title} saved.");
        _afterSave(snippet);
    }

    private void Delete()
    {
        if (!_snippetId.HasValue)
        {
            return;
        }

        var confirmed = _dialogService.Confirm(
            "Delete snippet",
            "Delete this snippet?");

        if (!confirmed)
        {
            return;
        }

        _snippetService.Delete(_snippetId.Value);
        _showStatus("Snippet deleted.");
        _afterDelete();
    }
}
