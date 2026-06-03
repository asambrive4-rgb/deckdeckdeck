using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.ViewModels;

public sealed class HomeViewModel
{
    private readonly Action<SlotKey, string> _openCategory;
    private readonly Action<string> _showStatus;

    public HomeViewModel(Action<SlotKey, string> openCategory, Action<string> showStatus)
    {
        _openCategory = openCategory;
        _showStatus = showStatus;
        NumpadGrid = new NumpadGridViewModel(SlotKeyCatalog.All.Select(CreateSlot));
    }

    public string Title => "DeckDeckDeck";

    public string Subtitle => "Categories";

    public NumpadGridViewModel NumpadGrid { get; }

    public void SelectSlot(SlotKey slotKey)
    {
        NumpadGrid.SelectSlot(slotKey);
    }

    private SlotViewModel CreateSlot(SlotKey slotKey)
    {
        var title = slotKey switch
        {
            SlotKey.Numpad1 => "Writing",
            SlotKey.Numpad2 => "Review",
            SlotKey.Numpad3 => "Summary",
            _ => null
        };

        return new SlotViewModel(
            slotKey,
            title,
            isEnabledSlot: true,
            selectedSlotKey => SelectCategorySlot(selectedSlotKey, title));
    }

    private void SelectCategorySlot(SlotKey slotKey, string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            _showStatus($"{slotKey.GetDisplayText()} category editor is planned for stage 2.");
            return;
        }

        _openCategory(slotKey, title);
    }
}
