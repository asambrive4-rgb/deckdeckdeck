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
public sealed class NativeInteropTests
{
    [Fact]
    public void Win32InputStructUsesNativeSize()
    {
        var expectedSize = IntPtr.Size == 8 ? 40 : 28;

        Assert.Equal(expectedSize, Marshal.SizeOf<User32.Input>());
    }
}
