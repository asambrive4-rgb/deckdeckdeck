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
public sealed class ClipboardPasteServiceTests
{
    [Fact]
    public async Task ClipboardPasteBacksUpPastesAndRestoresClipboard()
    {
        var originalClipboard = new TestDataObject();
        var clipboard = new FakeClipboardService(originalClipboard);
        var keyboard = new FakeKeyboardInputService();
        var focus = new FakeWindowFocusService();
        var service = new ClipboardPasteService(clipboard, keyboard, focus, TimeSpan.Zero, TimeSpan.Zero);
        var snippet = new Snippet { Content = "Line 1\r\n**Line 2**" };

        var pasted = await service.PasteSnippetAsync(snippet, new IntPtr(123), new AppSettings());

        Assert.True(pasted);
        Assert.Same(originalClipboard, clipboard.Backup);
        Assert.Equal("Line 1\r\n**Line 2**", Assert.Single(clipboard.SetTexts));
        Assert.Equal(new IntPtr(123), focus.ActivatedHandle);
        Assert.True(keyboard.SentCtrlV);
        Assert.Same(originalClipboard, clipboard.Restored);
    }

    [Fact]
    public async Task ClipboardPasteDoesNotRestoreWhenSettingIsDisabled()
    {
        var originalClipboard = new TestDataObject();
        var clipboard = new FakeClipboardService(originalClipboard);
        var service = new ClipboardPasteService(
            clipboard,
            new FakeKeyboardInputService(),
            new FakeWindowFocusService(),
            TimeSpan.Zero,
            TimeSpan.Zero);
        var settings = new AppSettings { RestoreClipboardAfterPaste = false };

        var pasted = await service.PasteSnippetAsync(
            new Snippet { Content = "Paste me" },
            new IntPtr(123),
            settings);

        Assert.True(pasted);
        Assert.Null(clipboard.Restored);
    }

    [Fact]
    public async Task ClipboardPasteSendsCtrlVEvenWhenFocusRestoreFails()
    {
        var clipboard = new FakeClipboardService(new TestDataObject());
        var keyboard = new FakeKeyboardInputService();
        var focus = new FakeWindowFocusService { CanActivate = false };
        var service = new ClipboardPasteService(clipboard, keyboard, focus, TimeSpan.Zero, TimeSpan.Zero);

        var pasted = await service.PasteSnippetAsync(
            new Snippet { Content = "Paste me anyway" },
            new IntPtr(123),
            new AppSettings());

        Assert.True(pasted);
        Assert.Equal(new IntPtr(123), focus.ActivatedHandle);
        Assert.True(keyboard.SentCtrlV);
    }
}
