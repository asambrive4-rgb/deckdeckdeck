using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace DeckDeckDeck.App.ViewModels;

public sealed class HomeViewModel
{
    private readonly Action<Category> _openCategory;
    private readonly Action<Category> _editCategory;
    private readonly Action<SlotKey> _createCategory;

    public HomeViewModel(
        HomeGridState gridState,
        SlotGridViewModelFactory slotGridViewModelFactory,
        Action<Category> openCategory,
        Action<Category> editCategory,
        Action<SlotKey> createCategory,
        Action showSettings,
        Action showHotkeys)
    {
        _openCategory = openCategory;
        _editCategory = editCategory;
        _createCategory = createCategory;
        SettingsCommand = new RelayCommand(showSettings);

        NumpadGrid = slotGridViewModelFactory.BuildCategoryGrid(
            gridState.Categories,
            gridState.Settings,
            SelectCategorySlot,
            EditCategorySlot,
            showHotkeys);
    }

    public string Title => "DeckDeckDeck";

    public string Subtitle => "카테고리";

    public NumpadGridViewModel NumpadGrid { get; }

    public ICommand SettingsCommand { get; }

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

    private void EditCategorySlot(SlotKey slotKey, Category? category)
    {
        if (category is null)
        {
            _createCategory(slotKey);
            return;
        }

        _editCategory(category);
    }
}
