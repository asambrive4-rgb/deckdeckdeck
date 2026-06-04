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
public sealed class LoggingServiceTests
{
    [Fact]
    public void LoggingServiceWritesAppLog()
    {
        var services = CreateServices();
        var logPath = Path.Combine(services.Storage.LogsPath, "app.log");

        services.LoggingService.Log("Paste failed.");

        Assert.True(File.Exists(logPath));
        Assert.Contains("Paste failed.", File.ReadAllText(logPath));
    }
}
