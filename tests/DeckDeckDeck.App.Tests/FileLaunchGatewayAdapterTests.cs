// 역할: 외부 파일, 환경변수 경로 및 Windows 앱 실행 요청이 올바르게 호출되는지 검증하는 단위 테스트 모음입니다.
using DeckDeckDeck.App.Composition;
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Infrastructure.Persistence;
using DeckDeckDeck.App.Infrastructure.Platform;
using DeckDeckDeck.App.Infrastructure.Storage;
using DeckDeckDeck.App.UseCases.Ports;
using System.Diagnostics;
using System.IO;

namespace DeckDeckDeck.App.Tests;

public sealed class FileLaunchGatewayAdapterTests
{
    [Fact]
    public void MissingPathReturnsFalse()
    {
        var service = new FileLaunchGatewayAdapter();
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.exe");

        var launched = service.TryLaunch(missingPath);

        Assert.False(launched);
    }

    [Fact]
    public void ShellLaunchReturningNoProcessStillReturnsTrue()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, string.Empty);
        ProcessStartInfo? startInfo = null;
        var service = new FileLaunchGatewayAdapter(info =>
        {
            startInfo = info;
            return null;
        });

        try
        {
            var launched = service.TryLaunch(path);

            Assert.True(launched);
            Assert.NotNull(startInfo);
            Assert.Equal(path, startInfo.FileName);
            Assert.True(startInfo.UseShellExecute);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ShellAppsFolderPathLaunchesViaShellExecute()
    {
        ProcessStartInfo? startInfo = null;
        var service = new FileLaunchGatewayAdapter(info =>
        {
            startInfo = info;
            return null;
        });

        const string appPath = @"shell:AppsFolder\OpenAI.Codex_2p2nqsd0c76g0!App";
        var launched = service.TryLaunch(appPath);

        Assert.True(launched);
        Assert.NotNull(startInfo);
        Assert.Equal(appPath, startInfo.FileName);
        Assert.True(startInfo.UseShellExecute);
    }

    [Fact]
    public void EnvironmentVariablePathExpandsBeforeLaunch()
    {
        var fileName = $"{Guid.NewGuid():N}.txt";
        var realPath = Path.Combine(Path.GetTempPath(), fileName);
        File.WriteAllText(realPath, string.Empty);
        ProcessStartInfo? startInfo = null;
        var service = new FileLaunchGatewayAdapter(info =>
        {
            startInfo = info;
            return null;
        });

        try
        {
            var envPath = @"%TEMP%\" + fileName;
            var launched = service.TryLaunch(envPath);

            Assert.True(launched);
            Assert.NotNull(startInfo);
            Assert.Equal(realPath, startInfo.FileName);
            Assert.True(startInfo.UseShellExecute);
        }
        finally
        {
            if (File.Exists(realPath))
            {
                File.Delete(realPath);
            }
        }
    }

    [Fact]
    public void LaunchPathRules_ToPortablePath_CollapsesProgramFilesAndLocalAppData()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var testLocalPath = Path.Combine(localAppData, "TestApp", "app.exe");
        var portableLocal = LaunchPathRules.ToPortablePath(testLocalPath);
        Assert.Equal(@"%LOCALAPPDATA%\TestApp\app.exe", portableLocal);

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var testProgPath = Path.Combine(programFiles, "TestApp", "app.exe");
        var portableProg = LaunchPathRules.ToPortablePath(testProgPath);
        Assert.Equal(@"%ProgramFiles%\TestApp\app.exe", portableProg);

        const string shellPath = @"shell:AppsFolder\App.Id!App";
        Assert.Equal(shellPath, LaunchPathRules.ToPortablePath(shellPath));
    }
}
