namespace DeckDeckDeck.App.Models;

public enum SlotKey
{
    Numpad0,
    Numpad1,
    Numpad2,
    Numpad3,
    Numpad4,
    Numpad5,
    Numpad6,
    Numpad7,
    Numpad8,
    Numpad9,
    NumpadDivide,
    NumpadMultiply,
    NumpadSubtract,
    NumpadAdd,
    NumpadDecimal
}

public static class SlotKeyCatalog
{
    public static IReadOnlyList<SlotKey> All { get; } =
    [
        SlotKey.Numpad0,
        SlotKey.Numpad1,
        SlotKey.Numpad2,
        SlotKey.Numpad3,
        SlotKey.Numpad4,
        SlotKey.Numpad5,
        SlotKey.Numpad6,
        SlotKey.Numpad7,
        SlotKey.Numpad8,
        SlotKey.Numpad9,
        SlotKey.NumpadDivide,
        SlotKey.NumpadMultiply,
        SlotKey.NumpadSubtract,
        SlotKey.NumpadAdd,
        SlotKey.NumpadDecimal
    ];

    public static string GetDisplayText(this SlotKey slotKey)
    {
        return slotKey switch
        {
            SlotKey.Numpad0 => "0",
            SlotKey.Numpad1 => "1",
            SlotKey.Numpad2 => "2",
            SlotKey.Numpad3 => "3",
            SlotKey.Numpad4 => "4",
            SlotKey.Numpad5 => "5",
            SlotKey.Numpad6 => "6",
            SlotKey.Numpad7 => "7",
            SlotKey.Numpad8 => "8",
            SlotKey.Numpad9 => "9",
            SlotKey.NumpadDivide => "/",
            SlotKey.NumpadMultiply => "*",
            SlotKey.NumpadSubtract => "-",
            SlotKey.NumpadAdd => "+",
            SlotKey.NumpadDecimal => ".",
            _ => throw new ArgumentOutOfRangeException(nameof(slotKey), slotKey, null)
        };
    }
}
