using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Native;
using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.ViewModels;
using DeckDeckDeck.App.Views;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using static DeckDeckDeck.App.Tests.TestAppFactory;

namespace DeckDeckDeck.App.Tests;
public sealed class ClipboardPasteGatewayTests
{
    [Fact]
    public async Task ClipboardPasteBacksUpPastesAndRestoresClipboard()
    {
        var originalClipboard = new TestDataObject();
        var clipboard = new FakeClipboardAdapter(originalClipboard);
        var keyboard = new FakeWin32KeyboardInputAdapter();
        var focus = new FakeWin32WindowFocusAdapter();
        var service = new ClipboardPasteGateway(clipboard, keyboard, focus, TimeSpan.Zero, TimeSpan.Zero);
        var snippet = new Snippet { Content = "Line 1\r\n**Line 2**" };

        var pasted = await service.PasteSnippetAsync(snippet, new IntPtr(123), new AppSettings());

        Assert.True(pasted);
        Assert.Same(originalClipboard, clipboard.Backup);
        Assert.Equal("Line 1\r\n**Line 2**", Assert.Single(clipboard.SetTexts));
        Assert.Equal(new IntPtr(123), focus.ActivatedHandle);
        Assert.True(keyboard.SentCtrlV);
        Assert.False(keyboard.SentCtrlShiftV);
        Assert.Same(originalClipboard, clipboard.Restored);
    }

    [Fact]
    public async Task ClipboardPasteSendsCtrlShiftVForTerminalPasteMode()
    {
        var clipboard = new FakeClipboardAdapter(new TestDataObject());
        var keyboard = new FakeWin32KeyboardInputAdapter();
        var service = new ClipboardPasteGateway(
            clipboard,
            keyboard,
            new FakeWin32WindowFocusAdapter(),
            TimeSpan.Zero,
            TimeSpan.Zero);
        var snippet = new Snippet
        {
            Content = "Paste in terminal",
            PasteShortcutMode = PasteShortcutMode.CtrlShiftV
        };

        var pasted = await service.PasteSnippetAsync(snippet, new IntPtr(123), new AppSettings());

        Assert.True(pasted);
        Assert.False(keyboard.SentCtrlV);
        Assert.True(keyboard.SentCtrlShiftV);
    }

    [Fact]
    public async Task ClipboardPasteDoesNotRestoreWhenSettingIsDisabled()
    {
        var originalClipboard = new TestDataObject();
        var clipboard = new FakeClipboardAdapter(originalClipboard);
        var service = new ClipboardPasteGateway(
            clipboard,
            new FakeWin32KeyboardInputAdapter(),
            new FakeWin32WindowFocusAdapter(),
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
        var clipboard = new FakeClipboardAdapter(new TestDataObject());
        var keyboard = new FakeWin32KeyboardInputAdapter();
        var focus = new FakeWin32WindowFocusAdapter { CanActivate = false };
        var service = new ClipboardPasteGateway(clipboard, keyboard, focus, TimeSpan.Zero, TimeSpan.Zero);

        var pasted = await service.PasteSnippetAsync(
            new Snippet { Content = "Paste me anyway" },
            new IntPtr(123),
            new AppSettings());

        Assert.True(pasted);
        Assert.Equal(new IntPtr(123), focus.ActivatedHandle);
        Assert.True(keyboard.SentCtrlV);
    }

    [Fact]
    public async Task FilePasteSetsFileDropListSendsCtrlVAndRestoresClipboard()
    {
        var filePath = Path.GetTempFileName();
        try
        {
            var originalClipboard = new TestDataObject();
            var clipboard = new FakeClipboardAdapter(originalClipboard);
            var keyboard = new FakeWin32KeyboardInputAdapter();
            var focus = new FakeWin32WindowFocusAdapter();
            var gateway = new ClipboardPasteGateway(
                clipboard,
                keyboard,
                focus,
                TimeSpan.Zero,
                TimeSpan.Zero);

            var result = await gateway.PasteFileAsync(
                filePath,
                new IntPtr(123),
                new AppSettings());

            Assert.Equal(FilePasteGatewayStatus.Succeeded, result.Status);
            Assert.Equal(filePath, Assert.Single(clipboard.SetFilePaths));
            Assert.Equal(new IntPtr(123), focus.ActivatedHandle);
            Assert.True(keyboard.SentCtrlV);
            Assert.False(keyboard.SentCtrlShiftV);
            Assert.Same(originalClipboard, clipboard.Restored);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task FilePasteRejectsMissingFileBeforeChangingClipboard()
    {
        var clipboard = new FakeClipboardAdapter(new TestDataObject());
        var keyboard = new FakeWin32KeyboardInputAdapter();
        var gateway = new ClipboardPasteGateway(
            clipboard,
            keyboard,
            new FakeWin32WindowFocusAdapter(),
            TimeSpan.Zero,
            TimeSpan.Zero);

        var result = await gateway.PasteFileAsync(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            new IntPtr(123),
            new AppSettings());

        Assert.Equal(FilePasteGatewayStatus.FileNotFound, result.Status);
        Assert.Empty(clipboard.SetFilePaths);
        Assert.False(keyboard.SentCtrlV);
    }

    [Fact]
    public void FileDropDataObjectUsesCopyEffect()
    {
        var dataObject = WpfClipboardAdapter.CreateFileDropDataObject(@"C:\notes\memo.md");

        Assert.Equal(
            [@"C:\notes\memo.md"],
            Assert.IsType<string[]>(dataObject.GetData(DataFormats.FileDrop)));
        var dropEffect = Assert.IsType<MemoryStream>(
            dataObject.GetData("Preferred DropEffect"));
        Assert.Equal([5, 0, 0, 0], dropEffect.ToArray());
    }
}

