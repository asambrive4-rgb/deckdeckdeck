// 역할: 오류 로그가 지정된 형식과 날짜별 파일에 안전하게 기록되는지 검증하는 단위 테스트 모음입니다.
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
public sealed class FileLoggerTests
{
    [Fact]
    public void FileLoggerWritesAppLog()
    {
        var services = CreateServices();
        var logPath = Path.Combine(services.Storage.LogsPath, "app.log");

        services.FileLogger.Log("Paste failed.");

        Assert.True(File.Exists(logPath));
        Assert.Contains("Paste failed.", File.ReadAllText(logPath));
    }
}
