using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.ViewModels;

public sealed class SlotViewModel : ObservableObject
{
    public SlotViewModel(
        SlotKey slotKey,
        string? title,
        bool isEnabledSlot,
        Action<SlotKey> onSelected)
    {
        SlotKey = slotKey;
        KeyText = slotKey.GetDisplayText();
        Title = title ?? string.Empty;
        IsEmpty = string.IsNullOrWhiteSpace(title);
        IsEnabledSlot = isEnabledSlot;
        Caption = IsEnabledSlot ? (IsEmpty ? "Empty" : "Ready") : "Disabled";
        SelectCommand = new RelayCommand(() => onSelected(SlotKey));
    }

    public SlotKey SlotKey { get; }

    public string KeyText { get; }

    public string Title { get; }

    public bool IsEmpty { get; }

    public bool IsEnabledSlot { get; }

    public string Caption { get; }

    public string DisplayText => IsEmpty ? "+" : Title;

    public ICommand SelectCommand { get; }
}
