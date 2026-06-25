using DeckDeckDeck.App.Data;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;
using System.Windows.Input;
using DeckDeckDeck.App.Native;

namespace DeckDeckDeck.App.Infrastructure.Platform;

internal static class NumpadKeyMap
{
    private static readonly IReadOnlyList<(SlotKey SlotKey, uint VirtualKey)> VirtualKeys =
    [
        (SlotKey.Numpad0, Win32Constants.VkNumpad0),
        (SlotKey.Numpad1, Win32Constants.VkNumpad0 + 1),
        (SlotKey.Numpad2, Win32Constants.VkNumpad0 + 2),
        (SlotKey.Numpad3, Win32Constants.VkNumpad0 + 3),
        (SlotKey.Numpad4, Win32Constants.VkNumpad0 + 4),
        (SlotKey.Numpad5, Win32Constants.VkNumpad0 + 5),
        (SlotKey.Numpad6, Win32Constants.VkNumpad0 + 6),
        (SlotKey.Numpad7, Win32Constants.VkNumpad0 + 7),
        (SlotKey.Numpad8, Win32Constants.VkNumpad0 + 8),
        (SlotKey.Numpad9, Win32Constants.VkNumpad0 + 9),
        (SlotKey.NumpadDivide, Win32Constants.VkDivide),
        (SlotKey.NumpadMultiply, Win32Constants.VkMultiply),
        (SlotKey.NumpadSubtract, Win32Constants.VkSubtract),
        (SlotKey.NumpadAdd, Win32Constants.VkAdd),
        (SlotKey.NumpadDecimal, Win32Constants.VkDecimal)
    ];

    public static IReadOnlyList<(SlotKey SlotKey, uint VirtualKey)> GetVirtualKeys()
    {
        return VirtualKeys;
    }

    public static bool TryGetSlotKey(Key key, out SlotKey slotKey)
    {
        slotKey = key switch
        {
            Key.NumPad0 => SlotKey.Numpad0,
            Key.NumPad1 => SlotKey.Numpad1,
            Key.NumPad2 => SlotKey.Numpad2,
            Key.NumPad3 => SlotKey.Numpad3,
            Key.NumPad4 => SlotKey.Numpad4,
            Key.NumPad5 => SlotKey.Numpad5,
            Key.NumPad6 => SlotKey.Numpad6,
            Key.NumPad7 => SlotKey.Numpad7,
            Key.NumPad8 => SlotKey.Numpad8,
            Key.NumPad9 => SlotKey.Numpad9,
            Key.Divide => SlotKey.NumpadDivide,
            Key.Multiply => SlotKey.NumpadMultiply,
            Key.Subtract => SlotKey.NumpadSubtract,
            Key.Add => SlotKey.NumpadAdd,
            Key.Decimal => SlotKey.NumpadDecimal,
            _ => default
        };

        return key is Key.NumPad0
            or Key.NumPad1
            or Key.NumPad2
            or Key.NumPad3
            or Key.NumPad4
            or Key.NumPad5
            or Key.NumPad6
            or Key.NumPad7
            or Key.NumPad8
            or Key.NumPad9
            or Key.Divide
            or Key.Multiply
            or Key.Subtract
            or Key.Add
            or Key.Decimal;
    }
}
