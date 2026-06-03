using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Services;

namespace DeckDeckDeck.App.ViewModels;

public sealed class HomeViewModel
{
    private readonly Action<Category> _openCategory;
    private readonly Action<SlotKey> _createCategory;

    public HomeViewModel(
        CategoryService categoryService,
        SettingsService settingsService,
        SlotService slotService,
        Action<Category> openCategory,
        Action<SlotKey> createCategory)
    {
        _openCategory = openCategory;
        _createCategory = createCategory;

        var categories = categoryService.GetAll();
        var settings = settingsService.Load();
        NumpadGrid = slotService.BuildCategoryGrid(categories, settings, SelectCategorySlot);
    }

    public string Title => "DeckDeckDeck";

    public string Subtitle => "Categories";

    public NumpadGridViewModel NumpadGrid { get; }

    public bool SelectSlot(SlotKey slotKey)
    {
        NumpadGrid.SelectSlot(slotKey);
        return true;
    }

    private void SelectCategorySlot(SlotKey slotKey, Category? category)
    {
        if (category is null)
        {
            _createCategory(slotKey);
            return;
        }

        _openCategory(category);
    }
}
