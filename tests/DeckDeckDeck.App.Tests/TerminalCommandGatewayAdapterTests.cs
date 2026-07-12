using System.Diagnostics;
using DeckDeckDeck.App.Infrastructure.Gateways;
using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Tests;

public sealed class TerminalCommandGatewayAdapterTests
{
    [Fact]
    public void CmdCommandWritesCmdScriptAndStartsHiddenBackgroundProcess()
    {
        var tempDirectory = CreateTempDirectory();
        ProcessStartInfo? startInfo = null;
        var service = new TerminalCommandGatewayAdapter(
            tempDirectory,
            info =>
            {
                startInfo = info;
                return null;
            });
        var command = "@echo off\r\nset VALUE=1";

        try
        {
            var executed = service.TryExecute(command, SnippetTerminalShell.Cmd, runAsAdministrator: false);

            Assert.True(executed);
            Assert.NotNull(startInfo);
            Assert.Equal("cmd.exe", startInfo.FileName);
            Assert.Contains("/d /c", startInfo.Arguments);
            Assert.False(startInfo.UseShellExecute);
            Assert.True(startInfo.CreateNoWindow);
            Assert.Equal(ProcessWindowStyle.Hidden, startInfo.WindowStyle);
            var scriptPath = Assert.Single(Directory.GetFiles(tempDirectory, "*.cmd"));
            Assert.Equal(command, File.ReadAllText(scriptPath));
            Assert.Contains(scriptPath, startInfo.Arguments);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void PowerShellCommandWritesPs1Script()
    {
        var tempDirectory = CreateTempDirectory();
        ProcessStartInfo? startInfo = null;
        var service = new TerminalCommandGatewayAdapter(
            tempDirectory,
            info =>
            {
                startInfo = info;
                return null;
            });
        var command = "Get-ChildItem";

        try
        {
            var executed = service.TryExecute(command, SnippetTerminalShell.PowerShell, runAsAdministrator: false);

            Assert.True(executed);
            Assert.NotNull(startInfo);
            Assert.Equal("powershell.exe", startInfo.FileName);
            Assert.Contains("-ExecutionPolicy Bypass", startInfo.Arguments);
            Assert.DoesNotContain("-NoExit", startInfo.Arguments);
            var scriptPath = Assert.Single(Directory.GetFiles(tempDirectory, "*.ps1"));
            Assert.Equal(command, File.ReadAllText(scriptPath));
            Assert.Contains(scriptPath, startInfo.Arguments);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void AdminCommandUsesRunAsVerb()
    {
        var tempDirectory = CreateTempDirectory();
        ProcessStartInfo? startInfo = null;
        var service = new TerminalCommandGatewayAdapter(
            tempDirectory,
            info =>
            {
                startInfo = info;
                return null;
            });

        try
        {
            var executed = service.TryExecute("echo admin", SnippetTerminalShell.Cmd, runAsAdministrator: true);

            Assert.True(executed);
            Assert.NotNull(startInfo);
            Assert.True(startInfo.UseShellExecute);
            Assert.Equal("runas", startInfo.Verb);
            Assert.Equal(ProcessWindowStyle.Hidden, startInfo.WindowStyle);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void OpenTerminalWindowKeepsCmdSessionOpenWithWorkingDirectory()
    {
        var tempDirectory = CreateTempDirectory();
        var workingDirectory = CreateTempDirectory();
        ProcessStartInfo? startInfo = null;
        var service = new TerminalCommandGatewayAdapter(
            tempDirectory,
            info =>
            {
                startInfo = info;
                return null;
            });

        try
        {
            var executed = service.TryExecute(
                "grok",
                SnippetTerminalShell.Cmd,
                runAsAdministrator: false,
                openTerminalWindow: true,
                workingDirectory: workingDirectory);

            Assert.True(executed);
            Assert.NotNull(startInfo);
            Assert.Equal("cmd.exe", startInfo.FileName);
            Assert.Contains("/d /k", startInfo.Arguments);
            Assert.False(startInfo.CreateNoWindow);
            Assert.Equal(ProcessWindowStyle.Normal, startInfo.WindowStyle);
            Assert.Equal(workingDirectory, startInfo.WorkingDirectory);
            Assert.Single(Directory.GetFiles(tempDirectory, "*.cmd"));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void OpenTerminalWindowKeepsPowerShellSessionOpen()
    {
        var tempDirectory = CreateTempDirectory();
        ProcessStartInfo? startInfo = null;
        var service = new TerminalCommandGatewayAdapter(
            tempDirectory,
            info =>
            {
                startInfo = info;
                return null;
            });

        try
        {
            var executed = service.TryExecute(
                "grok",
                SnippetTerminalShell.PowerShell,
                runAsAdministrator: false,
                openTerminalWindow: true);

            Assert.True(executed);
            Assert.NotNull(startInfo);
            Assert.Equal("powershell.exe", startInfo.FileName);
            Assert.Contains("-NoExit", startInfo.Arguments);
            Assert.Contains("-File", startInfo.Arguments);
            Assert.False(startInfo.CreateNoWindow);
            Assert.Equal(ProcessWindowStyle.Normal, startInfo.WindowStyle);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void OpenTerminalWindowWithoutCommandOpensInteractiveShell()
    {
        var tempDirectory = CreateTempDirectory();
        var workingDirectory = CreateTempDirectory();
        ProcessStartInfo? startInfo = null;
        var service = new TerminalCommandGatewayAdapter(
            tempDirectory,
            info =>
            {
                startInfo = info;
                return null;
            });

        try
        {
            var executed = service.TryExecute(
                string.Empty,
                SnippetTerminalShell.Cmd,
                runAsAdministrator: false,
                openTerminalWindow: true,
                workingDirectory: workingDirectory);

            Assert.True(executed);
            Assert.NotNull(startInfo);
            Assert.Equal("cmd.exe", startInfo.FileName);
            Assert.Equal("/d /k", startInfo.Arguments);
            Assert.Equal(workingDirectory, startInfo.WorkingDirectory);
            Assert.Empty(Directory.GetFiles(tempDirectory));
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void MissingWorkingDirectoryReturnsFalse()
    {
        var tempDirectory = CreateTempDirectory();
        var started = false;
        var service = new TerminalCommandGatewayAdapter(
            tempDirectory,
            _ =>
            {
                started = true;
                return null;
            });

        try
        {
            var executed = service.TryExecute(
                "echo hi",
                SnippetTerminalShell.Cmd,
                runAsAdministrator: false,
                openTerminalWindow: true,
                workingDirectory: Path.Combine(tempDirectory, "missing-folder"));

            Assert.False(executed);
            Assert.False(started);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);

        return path;
    }
}
