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
        bool runAsAdministrator,
        bool openTerminalWindow = false,
        string? workingDirectory = null)
    {
        var hasCommand = !string.IsNullOrWhiteSpace(command);
        if (!hasCommand && !openTerminalWindow)
        {
            return false;
        }

        if (!TryNormalizeWorkingDirectory(workingDirectory, out var normalizedWorkingDirectory))
        {
            return false;
        }

        string? scriptPath = null;
        if (hasCommand)
        {
            Directory.CreateDirectory(_tempDirectory);
            scriptPath = WriteScript(command, shell);
        }

        var process = _startProcess(
            CreateStartInfo(
                scriptPath,
                shell,
                runAsAdministrator,
                openTerminalWindow,
                normalizedWorkingDirectory));

        if (scriptPath is not null)
        {
            DeleteScriptWhenProcessExits(process, scriptPath);
        }

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
        string? scriptPath,
        SnippetTerminalShell shell,
        bool runAsAdministrator,
        bool openTerminalWindow,
        string? workingDirectory)
    {
        var startInfo = shell == SnippetTerminalShell.PowerShell
            ? CreatePowerShellStartInfo(scriptPath, openTerminalWindow)
            : CreateCmdStartInfo(scriptPath, openTerminalWindow);

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        if (openTerminalWindow)
        {
            if (runAsAdministrator)
            {
                startInfo.UseShellExecute = true;
                startInfo.Verb = "runas";
                startInfo.WindowStyle = ProcessWindowStyle.Normal;
                return startInfo;
            }

            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = false;
            startInfo.WindowStyle = ProcessWindowStyle.Normal;
            return startInfo;
        }

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

    private static ProcessStartInfo CreateCmdStartInfo(string? scriptPath, bool openTerminalWindow)
    {
        if (scriptPath is null)
        {
            return new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/d /k"
            };
        }

        var switchFlag = openTerminalWindow ? "/k" : "/c";
        return new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/d {switchFlag} {QuoteArgument(scriptPath)}"
        };
    }

    private static ProcessStartInfo CreatePowerShellStartInfo(string? scriptPath, bool openTerminalWindow)
    {
        if (scriptPath is null)
        {
            return new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NoExit"
            };
        }

        var keepOpen = openTerminalWindow ? " -NoExit" : string.Empty;
        return new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass{keepOpen} -File {QuoteArgument(scriptPath)}"
        };
    }

    private static bool TryNormalizeWorkingDirectory(string? workingDirectory, out string? normalized)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            normalized = null;
            return true;
        }

        var trimmed = workingDirectory.Trim();
        if (!Directory.Exists(trimmed))
        {
            normalized = null;
            return false;
        }

        normalized = trimmed;
        return true;
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
