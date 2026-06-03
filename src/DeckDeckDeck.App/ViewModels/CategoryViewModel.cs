using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.ViewModels;

public sealed class CategoryViewModel
{
    private readonly Action<string> _showStatus;

    public CategoryViewModel(
        SlotKey categorySlotKey,
        string categoryName,
        Action showHome,
        Action<string> showStatus)
    {
        CategorySlotKey = categorySlotKey;
        Title = categoryName;
        Subtitle = $"{categorySlotKey.GetDisplayText()} category";
        BackCommand = new RelayCommand(showHome);
        _showStatus = showStatus;
        NumpadGrid = new NumpadGridViewModel(SlotKeyCatalog.All.Select(CreateSlot));
    }

    public SlotKey CategorySlotKey { get; }

    public string Title { get; }

    public string Subtitle { get; }

    public NumpadGridViewModel NumpadGrid { get; }

    public ICommand BackCommand { get; }

    public void SelectSlot(SlotKey slotKey)
    {
        NumpadGrid.SelectSlot(slotKey);
    }

    private SlotViewModel CreateSlot(SlotKey slotKey)
    {
        var title = slotKey switch
        {
            SlotKey.Numpad1 => "Outline",
            SlotKey.Numpad3 => "Structure",
            SlotKey.Numpad5 => "Translate",
            _ => null
        };

        return new SlotViewModel(
            slotKey,
            title,
            isEnabledSlot: true,
            selectedSlotKey => SelectSnippetSlot(selectedSlotKey, title));
    }

    private void SelectSnippetSlot(SlotKey slotKey, string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            _showStatus($"{slotKey.GetDisplayText()} snippet editor is planned for stage 2.");
            return;
        }

        _showStatus($"{title} paste flow is planned for stage 4.");
    }
}
