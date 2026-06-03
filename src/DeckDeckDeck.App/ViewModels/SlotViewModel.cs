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
        : this(slotKey, title, thumbnailPath: null, isEnabledSlot, onSelected, _ => { })
    {
    }

    public SlotViewModel(
        SlotKey slotKey,
        string? title,
        string? thumbnailPath,
        bool isEnabledSlot,
        Action<SlotKey> onSelected,
        Action<SlotKey> onEdit)
    {
        SlotKey = slotKey;
        KeyText = slotKey.GetDisplayText();
        Row = slotKey.GetGridRow();
        Column = slotKey.GetGridColumn();
        RowSpan = slotKey.GetGridRowSpan();
        ColumnSpan = slotKey.GetGridColumnSpan();
        Title = title ?? string.Empty;
        ThumbnailPath = thumbnailPath;
        IsEmpty = string.IsNullOrWhiteSpace(title);
        IsEnabledSlot = isEnabledSlot;
        Caption = IsEnabledSlot ? (IsEmpty ? "Empty" : "Ready") : "Disabled";
        SelectCommand = new RelayCommand(() => onSelected(SlotKey), () => IsEnabledSlot);
        EditCommand = new RelayCommand(() => onEdit(SlotKey));
    }

    public SlotKey SlotKey { get; }

    public string KeyText { get; }

    public int Row { get; }

    public int Column { get; }

    public int RowSpan { get; }

    public int ColumnSpan { get; }

    public string Title { get; }

    public string? ThumbnailPath { get; }

    public bool HasThumbnail => !string.IsNullOrWhiteSpace(ThumbnailPath);

    public bool IsEmpty { get; }

    public bool IsEnabledSlot { get; }

    public string Caption { get; }

    public string DisplayText => IsEmpty ? "+" : Title;

    public ICommand SelectCommand { get; }

    public ICommand EditCommand { get; }
}
