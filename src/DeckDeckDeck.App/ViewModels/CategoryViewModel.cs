using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;

namespace DeckDeckDeck.App.ViewModels;

public sealed class CategoryViewModel
{
    private readonly Category _category;
    private readonly Action<Category, SlotKey, Snippet?> _editSnippet;
    private readonly Func<Snippet, Task> _pasteSnippet;

    public CategoryViewModel(
        Category category,
        CategoryGridState gridState,
        SlotGridViewModelFactory slotGridViewModelFactory,
        Action showHome,
        Action showSettings,
        Action<Category, SlotKey, Snippet?> editSnippet,
        Func<Snippet, Task> pasteSnippet)
    {
        _category = category;
        _editSnippet = editSnippet;
        _pasteSnippet = pasteSnippet;

        Title = category.Name;
        Subtitle = $"슬롯 {category.SlotKey.GetDisplayText()} 카테고리";
        BackCommand = new RelayCommand(showHome);
        SettingsCommand = new RelayCommand(showSettings);

        NumpadGrid = slotGridViewModelFactory.BuildSnippetGrid(
            gridState.Snippets,
            gridState.Settings,
            SelectSnippetSlot,
            EditSnippetSlot);
    }

    public string Title { get; }

    public string Subtitle { get; }

    public NumpadGridViewModel NumpadGrid { get; }

    public ICommand BackCommand { get; }

    public ICommand SettingsCommand { get; }

    public bool SelectSlot(SlotKey slotKey)
    {
        NumpadGrid.SelectSlot(slotKey);
        return true;
    }

    private void SelectSnippetSlot(SlotKey slotKey, Snippet? snippet)
    {
        if (snippet is null)
        {
            _editSnippet(_category, slotKey, snippet);
            return;
        }

        _ = PasteSnippetSafely(snippet);
    }

    private void EditSnippetSlot(SlotKey slotKey, Snippet? snippet)
    {
        _editSnippet(_category, slotKey, snippet);
    }

    private async Task PasteSnippetSafely(Snippet snippet)
    {
        try
        {
            await _pasteSnippet(snippet);
        }
        catch
        {
            // Paste failures are intentionally silent in this stage.
        }
    }
}

