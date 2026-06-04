using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;
using DeckDeckDeck.App.Services;
using DeckDeckDeck.App.ViewModels;
using DeckDeckDeck.App.Views;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;
public sealed class HotkeyMappingTests
{
    [Fact]
    public void HotkeyServiceRegistersDirectCategorySymbolHotkeys()
    {
        var registeredSlotKeys = HotkeyService.GetRegisteredHotkeys()
            .Select(hotkey => hotkey.SlotKey)
            .ToArray();

        Assert.Equal(SlotKeyCatalog.All, registeredSlotKeys);
    }
}
