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
public sealed class SettingsViewModelTests
{
    [Fact]
    public void SettingsViewModelSavesSettingsAndReturns()
    {
        var services = CreateServices();
        var returned = false;
        var status = string.Empty;
        var viewModel = new SettingsViewModel(
            services.SettingsService,
            () => { },
            () => returned = true,
            message => status = message,
            services.LoggingService)
        {
            BringWindowToFrontOnHotkey = false,
            AutoHideAfterPaste = false,
            RestoreClipboardAfterPaste = false
        };

        viewModel.SaveCommand.Execute(null);

        var reloaded = services.SettingsService.Load();
        Assert.False(reloaded.BringWindowToFrontOnHotkey);
        Assert.False(reloaded.AutoHideAfterPaste);
        Assert.False(reloaded.RestoreClipboardAfterPaste);
        Assert.True(returned);
        Assert.Equal("설정을 저장했습니다.", status);
    }
}
