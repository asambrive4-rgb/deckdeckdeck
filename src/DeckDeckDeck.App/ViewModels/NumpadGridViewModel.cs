using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.ViewModels;

public sealed class NumpadGridViewModel
{
    private readonly Dictionary<SlotKey, SlotViewModel> _slotsByKey;
    private readonly Action<SlotKey, SlotKey>? _onReorder;

    public NumpadGridViewModel(
        IEnumerable<SlotViewModel> slots,
        HotkeyTileViewModel? hotkeyTile = null,
        Action<SlotKey, SlotKey>? onReorder = null)
    {
        Slots = slots.ToList();
        HotkeyTile = hotkeyTile ?? HotkeyTileViewModel.Disabled();
        _onReorder = onReorder;
        _slotsByKey = Slots.ToDictionary(slot => slot.SlotKey);

        Numpad0 = GetRequiredSlot(SlotKey.Numpad0);
        Numpad1 = GetRequiredSlot(SlotKey.Numpad1);
        Numpad2 = GetRequiredSlot(SlotKey.Numpad2);
        Numpad3 = GetRequiredSlot(SlotKey.Numpad3);
        Numpad4 = GetRequiredSlot(SlotKey.Numpad4);
        Numpad5 = GetRequiredSlot(SlotKey.Numpad5);
        Numpad6 = GetRequiredSlot(SlotKey.Numpad6);
        Numpad7 = GetRequiredSlot(SlotKey.Numpad7);
        Numpad8 = GetRequiredSlot(SlotKey.Numpad8);
        Numpad9 = GetRequiredSlot(SlotKey.Numpad9);
        NumpadDivide = GetRequiredSlot(SlotKey.NumpadDivide);
        NumpadMultiply = GetRequiredSlot(SlotKey.NumpadMultiply);
        NumpadSubtract = GetRequiredSlot(SlotKey.NumpadSubtract);
        NumpadAdd = GetRequiredSlot(SlotKey.NumpadAdd);
        NumpadDecimal = GetRequiredSlot(SlotKey.NumpadDecimal);
    }

    public IReadOnlyList<SlotViewModel> Slots { get; }

    public HotkeyTileViewModel HotkeyTile { get; }

    public SlotViewModel Numpad0 { get; }

    public SlotViewModel Numpad1 { get; }

    public SlotViewModel Numpad2 { get; }

    public SlotViewModel Numpad3 { get; }

    public SlotViewModel Numpad4 { get; }

    public SlotViewModel Numpad5 { get; }

    public SlotViewModel Numpad6 { get; }

    public SlotViewModel Numpad7 { get; }

    public SlotViewModel Numpad8 { get; }

    public SlotViewModel Numpad9 { get; }

    public SlotViewModel NumpadDivide { get; }

    public SlotViewModel NumpadMultiply { get; }

    public SlotViewModel NumpadSubtract { get; }

    public SlotViewModel NumpadAdd { get; }

    public SlotViewModel NumpadDecimal { get; }

    public void SelectSlot(SlotKey slotKey)
    {
        if (!_slotsByKey.TryGetValue(slotKey, out var slot) || !slot.IsEnabledSlot)
        {
            return;
        }

        if (slot.SelectCommand.CanExecute(null))
        {
            slot.SelectCommand.Execute(null);
        }
    }

    public void RequestReorder(SlotKey sourceSlotKey, SlotKey targetSlotKey)
    {
        if (sourceSlotKey == targetSlotKey)
        {
            return;
        }

        _onReorder?.Invoke(sourceSlotKey, targetSlotKey);
    }

    private SlotViewModel GetRequiredSlot(SlotKey slotKey)
    {
        return _slotsByKey.TryGetValue(slotKey, out var slot)
            ? slot
            : throw new InvalidOperationException($"Missing numpad slot: {slotKey}");
    }
}

public sealed class HotkeyTileViewModel
{
    private HotkeyTileViewModel(bool isEnabled, Action? onSelected)
    {
        IsEnabled = isEnabled;
        SelectCommand = new RelayCommand(
            () => onSelected?.Invoke(),
            () => IsEnabled);
    }

    public string Text => "Hotkey";

    public bool IsEnabled { get; }

    public bool IsDisabled => !IsEnabled;

    public string ToolTip => IsEnabled
        ? "핫키 관리"
        : "홈 화면에서만 핫키를 관리할 수 있습니다.";

    public ICommand SelectCommand { get; }

    public static HotkeyTileViewModel Enabled(Action onSelected)
    {
        return new HotkeyTileViewModel(true, onSelected);
    }

    public static HotkeyTileViewModel Disabled()
    {
        return new HotkeyTileViewModel(false, null);
    }
}
