using System.Diagnostics;
using System.IO;
using System.Text;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.Infrastructure.Gateways;

public sealed class TerminalCommandGatewayAdapter : ITerminalCommandGateway
{
    private static readonly Encoding ScriptEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly Func<ProcessStartInfo, Process?> _startProcess;
    private readonly string _tempDirectory;

    public TerminalCommandGatewayAdapter()
        : this(Path.Combine(Path.GetTempPath(), "DeckDeckDeck", "commands"), Process.Start)
    {
    }

    public TerminalCommandGatewayAdapter(string tempDirectory)
        : this(tempDirectory, Process.Start)
    {
    }

    internal TerminalCommandGatewayAdapter(
        string tempDirectory,
        Func<ProcessStartInfo, Process?> startProcess)
    {
        _tempDirectory = tempDirectory;
        _startProcess = startProcess;
    }

    public bool TryExecute(
        string command,
        SnippetTerminalShell shell,
        bool runAsAdministrator)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        Directory.CreateDirectory(_tempDirectory);
        var scriptPath = WriteScript(command, shell);
        var process = _startProcess(CreateStartInfo(scriptPath, shell, runAsAdministrator));
        DeleteScriptWhenProcessExits(process, scriptPath);

        return true;
    }

    private string WriteScript(string command, SnippetTerminalShell shell)
    {
        var extension = shell == SnippetTerminalShell.PowerShell ? ".ps1" : ".cmd";
        var scriptPath = Path.Combine(_tempDirectory, $"{Guid.NewGuid():N}{extension}");
        File.WriteAllText(scriptPath, command, ScriptEncoding);

        return scriptPath;
    }

    private static ProcessStartInfo CreateStartInfo(
        string scriptPath,
        SnippetTerminalShell shell,
        bool runAsAdministrator)
    {
        var startInfo = shell == SnippetTerminalShell.PowerShell
            ? CreatePowerShellStartInfo(scriptPath)
            : CreateCmdStartInfo(scriptPath);

        if (runAsAdministrator)
        {
            startInfo.UseShellExecute = true;
            startInfo.Verb = "runas";
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            return startInfo;
        }

        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.WindowStyle = ProcessWindowStyle.Hidden;
        return startInfo;
    }

    private static ProcessStartInfo CreateCmdStartInfo(string scriptPath)
    {
        return new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/d /c {QuoteArgument(scriptPath)}"
        };
    }

    private static ProcessStartInfo CreatePowerShellStartInfo(string scriptPath)
    {
        return new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File {QuoteArgument(scriptPath)}"
        };
    }

    private static string QuoteArgument(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }

    private static void DeleteScriptWhenProcessExits(Process? process, string scriptPath)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => TryDeleteFile(scriptPath);
            if (process.HasExited)
            {
                TryDeleteFile(scriptPath);
            }
        }
        catch
        {
            // Leaving a temp script behind is safer than interrupting command launch.
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }
}
