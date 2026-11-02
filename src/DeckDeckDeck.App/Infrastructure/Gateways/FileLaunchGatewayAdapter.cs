// 역할: 윈도우 셸을 통해 지정된 파일, 폴더, 환경변수 경로 또는 설치된 앱(shell:AppsFolder)을 실행하는 외부 게이트웨이 어댑터입니다.
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.UseCases.Ports;
using System.Diagnostics;
using System.IO;

namespace DeckDeckDeck.App.Infrastructure.Gateways;

public sealed class FileLaunchGatewayAdapter : IFileLaunchGateway
{
    private readonly Func<ProcessStartInfo, Process?> _startProcess;

    public FileLaunchGatewayAdapter()
        : this(Process.Start)
    {
    }

    internal FileLaunchGatewayAdapter(Func<ProcessStartInfo, Process?> startProcess)
    {
        _startProcess = startProcess;
    }

    public bool TryLaunch(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var expandedPath = LaunchPathRules.ExpandPath(path);
        if (!LaunchPathRules.IsShellAppsFolderPath(expandedPath) && !File.Exists(expandedPath) && !Directory.Exists(expandedPath))
        {
            return false;
        }

        // LaunchFile is intentionally limited to file/folder/app paths, not shell commands or arguments.
        _startProcess(new ProcessStartInfo
        {
            FileName = expandedPath,
            UseShellExecute = true
        });

        return true;
    }
}
