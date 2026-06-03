using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Services;

namespace DeckDeckDeck.App.ViewModels;

public sealed class CategoryViewModel
{
    private readonly Category _category;
    private readonly Action<Category, SlotKey, Snippet?> _editSnippet;

    public CategoryViewModel(
        Category category,
        SnippetService snippetService,
        SettingsService settingsService,
        SlotService slotService,
        Action showHome,
        Action<Category> editCategory,
        Action<Category, SlotKey, Snippet?> editSnippet)
    {
        _category = category;
        _editSnippet = editSnippet;

        Title = category.Name;
        Subtitle = $"{category.SlotKey.GetDisplayText()} category";
        BackCommand = new RelayCommand(showHome);
        EditCategoryCommand = new RelayCommand(() => editCategory(_category));

        var snippets = snippetService.GetByCategoryId(category.Id);
        var settings = settingsService.Load();
        NumpadGrid = slotService.BuildSnippetGrid(snippets, settings, SelectSnippetSlot);
    }

    public string Title { get; }

    public string Subtitle { get; }

    public NumpadGridViewModel NumpadGrid { get; }

    public ICommand BackCommand { get; }

    public ICommand EditCategoryCommand { get; }

    public bool SelectSlot(SlotKey slotKey)
    {
        NumpadGrid.SelectSlot(slotKey);
        return true;
    }

    private void SelectSnippetSlot(SlotKey slotKey, Snippet? snippet)
    {
        _editSnippet(_category, slotKey, snippet);
    }
}
