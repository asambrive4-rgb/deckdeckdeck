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

    public static int GetSortOrder(this SlotKey slotKey)
    {
        return All.ToList().IndexOf(slotKey);
    }

    public static int GetGridRow(this SlotKey slotKey)
    {
        return slotKey switch
        {
            SlotKey.NumpadDivide or SlotKey.NumpadMultiply or SlotKey.NumpadSubtract => 0,
            SlotKey.Numpad7 or SlotKey.Numpad8 or SlotKey.Numpad9 or SlotKey.NumpadAdd => 1,
            SlotKey.Numpad4 or SlotKey.Numpad5 or SlotKey.Numpad6 => 2,
            SlotKey.Numpad1 or SlotKey.Numpad2 or SlotKey.Numpad3 => 3,
            SlotKey.Numpad0 or SlotKey.NumpadDecimal => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(slotKey), slotKey, null)
        };
    }

    public static int GetGridColumn(this SlotKey slotKey)
    {
        return slotKey switch
        {
            SlotKey.NumpadDivide or SlotKey.Numpad7 or SlotKey.Numpad4 or SlotKey.Numpad1 or SlotKey.Numpad0 => 0,
            SlotKey.NumpadMultiply or SlotKey.Numpad8 or SlotKey.Numpad5 or SlotKey.Numpad2 => 1,
            SlotKey.NumpadSubtract or SlotKey.Numpad9 or SlotKey.Numpad6 or SlotKey.Numpad3 or SlotKey.NumpadDecimal => 2,
            SlotKey.NumpadAdd => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(slotKey), slotKey, null)
        };
    }

    public static int GetGridRowSpan(this SlotKey slotKey)
    {
        return slotKey == SlotKey.NumpadAdd ? 2 : 1;
    }

    public static int GetGridColumnSpan(this SlotKey slotKey)
    {
        return slotKey == SlotKey.Numpad0 ? 2 : 1;
    }
}
