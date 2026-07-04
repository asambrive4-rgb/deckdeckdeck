using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Xml.Linq;
using DeckDeckDeck.App.UseCases.Ports;
using Microsoft.Win32;

namespace DeckDeckDeck.App.Infrastructure.Platform;

public sealed class WindowsStartupRegistrationGateway : IStartupRegistrationGateway
{
    private const string AppName = "DeckDeckDeck";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupTaskName = "DeckDeckDeck Startup";
    private const int SchtasksTimeoutMilliseconds = 120_000;

    public StartupRegistrationState GetState()
    {
        return TryReadScheduledTaskState(out var taskState)
            ? taskState
            : new StartupRegistrationState(IsRegistryStartupEnabled(), false);
    }

    public StartupRegistrationResult Save(StartupRegistrationSettings settings)
    {
        if (!settings.IsEnabled)
        {
            DeleteRegistryStartupValue();
            return DeleteScheduledTaskIfExists();
        }

        var executablePath = GetExecutablePath();
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return StartupRegistrationResult.Failure(
                "시작프로그램에 등록할 실행 파일을 찾지 못했습니다.");
        }

        if (settings.RunAsAdministrator)
        {
            DeleteRegistryStartupValue();
            return CreateOrUpdateScheduledTask(executablePath);
        }

        var deleteTaskResult = DeleteScheduledTaskIfExists();
        if (!deleteTaskResult.Succeeded)
        {
            return deleteTaskResult;
        }

        SetRegistryStartupValue(executablePath);
        return StartupRegistrationResult.Success();
    }

    private static string? GetExecutablePath()
    {
        return Environment.ProcessPath;
    }

    private static bool IsRegistryStartupEnabled()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return !string.IsNullOrWhiteSpace(runKey?.GetValue(AppName) as string);
    }

    private static void SetRegistryStartupValue(string executablePath)
    {
        using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        runKey.SetValue(AppName, QuoteArgument(executablePath), RegistryValueKind.String);
    }

    private static void DeleteRegistryStartupValue()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (runKey?.GetValue(AppName) is not null)
        {
            runKey.DeleteValue(AppName, throwOnMissingValue: false);
        }
    }

    private static bool TryReadScheduledTaskState(out StartupRegistrationState state)
    {
        var queryResult = RunSchtasks(
            $"/Query /TN {QuoteArgument(StartupTaskName)} /XML",
            requireAdministrator: false);
        if (!queryResult.Succeeded || string.IsNullOrWhiteSpace(queryResult.Output))
        {
            state = StartupRegistrationState.Disabled;
            return false;
        }

        try
        {
            var document = XDocument.Parse(queryResult.Output);
            var task = document.Root;
            var ns = task?.Name.Namespace ?? XNamespace.None;
            var enabledText = task?
                .Element(ns + "Settings")?
                .Element(ns + "Enabled")?
                .Value;

            var isEnabled = !string.Equals(
                enabledText,
                "false",
                StringComparison.OrdinalIgnoreCase);
            state = new StartupRegistrationState(isEnabled, true);
            return true;
        }
        catch
        {
            state = StartupRegistrationState.Disabled;
            return false;
        }
    }

    private static StartupRegistrationResult DeleteScheduledTaskIfExists()
    {
        if (!ScheduledTaskExists())
        {
            return StartupRegistrationResult.Success();
        }

        var deleteResult = RunSchtasks(
            $"/Delete /TN {QuoteArgument(StartupTaskName)} /F",
            requireAdministrator: false);
        if (deleteResult.Succeeded)
        {
            return StartupRegistrationResult.Success();
        }

        deleteResult = RunSchtasks(
            $"/Delete /TN {QuoteArgument(StartupTaskName)} /F",
            requireAdministrator: true);
        return deleteResult.Succeeded
            ? StartupRegistrationResult.Success()
            : StartupRegistrationResult.Failure(
                deleteResult.ErrorMessage
                ?? "관리자 시작 작업을 제거하지 못했습니다.");
    }

    private static bool ScheduledTaskExists()
    {
        return RunSchtasks(
            $"/Query /TN {QuoteArgument(StartupTaskName)}",
            requireAdministrator: false)
            .Succeeded;
    }

    private static StartupRegistrationResult CreateOrUpdateScheduledTask(string executablePath)
    {
        var taskXmlPath = Path.Combine(
            Path.GetTempPath(),
            $"DeckDeckDeck-startup-{Guid.NewGuid():N}.xml");

        try
        {
            CreateStartupTaskXml(executablePath).Save(taskXmlPath);
            var createResult = RunSchtasks(
                $"/Create /TN {QuoteArgument(StartupTaskName)} /XML {QuoteArgument(taskXmlPath)} /F",
                requireAdministrator: true);

            return createResult.Succeeded
                ? StartupRegistrationResult.Success()
                : StartupRegistrationResult.Failure(
                    createResult.ErrorMessage
                    ?? "관리자 시작 작업을 만들지 못했습니다.");
        }
        finally
        {
            TryDeleteFile(taskXmlPath);
        }
    }

    private static XDocument CreateStartupTaskXml(string executablePath)
    {
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        var userName = WindowsIdentity.GetCurrent().Name;

        return new XDocument(
            new XDeclaration("1.0", "UTF-16", null),
            new XElement(ns + "Task",
                new XAttribute("version", "1.4"),
                new XElement(ns + "RegistrationInfo",
                    new XElement(ns + "Author", userName),
                    new XElement(ns + "Description", "Start DeckDeckDeck when Windows user logs on.")),
                new XElement(ns + "Triggers",
                    new XElement(ns + "LogonTrigger",
                        new XElement(ns + "Enabled", "true"))),
                new XElement(ns + "Principals",
                    new XElement(ns + "Principal",
                        new XAttribute("id", "Author"),
                        new XElement(ns + "UserId", userName),
                        new XElement(ns + "LogonType", "InteractiveToken"),
                        new XElement(ns + "RunLevel", "HighestAvailable"))),
                new XElement(ns + "Settings",
                    new XElement(ns + "MultipleInstancesPolicy", "IgnoreNew"),
                    new XElement(ns + "DisallowStartIfOnBatteries", "false"),
                    new XElement(ns + "StopIfGoingOnBatteries", "false"),
                    new XElement(ns + "AllowHardTerminate", "true"),
                    new XElement(ns + "StartWhenAvailable", "false"),
                    new XElement(ns + "RunOnlyIfNetworkAvailable", "false"),
                    new XElement(ns + "IdleSettings",
                        new XElement(ns + "StopOnIdleEnd", "true"),
                        new XElement(ns + "RestartOnIdle", "false")),
                    new XElement(ns + "AllowStartOnDemand", "true"),
                    new XElement(ns + "Enabled", "true"),
                    new XElement(ns + "Hidden", "false"),
                    new XElement(ns + "RunOnlyIfIdle", "false"),
                    new XElement(ns + "WakeToRun", "false"),
                    new XElement(ns + "ExecutionTimeLimit", "PT0S"),
                    new XElement(ns + "Priority", "7")),
                new XElement(ns + "Actions",
                    new XAttribute("Context", "Author"),
                    new XElement(ns + "Exec",
                        new XElement(ns + "Command", executablePath)))));
    }

    private static SchtasksResult RunSchtasks(
        string arguments,
        bool requireAdministrator)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = arguments,
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = requireAdministrator
        };

        if (requireAdministrator)
        {
            startInfo.Verb = "runas";
        }
        else
        {
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.StandardOutputEncoding = Encoding.UTF8;
            startInfo.StandardErrorEncoding = Encoding.UTF8;
        }

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return SchtasksResult.Failure("Windows 작업 스케줄러를 실행하지 못했습니다.");
            }

            var outputTask = requireAdministrator
                ? Task.FromResult(string.Empty)
                : process.StandardOutput.ReadToEndAsync();
            var errorTask = requireAdministrator
                ? Task.FromResult(string.Empty)
                : process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(SchtasksTimeoutMilliseconds))
            {
                TryKill(process);
                return SchtasksResult.Failure("Windows 작업 스케줄러 응답 시간이 초과되었습니다.");
            }

            var output = outputTask.GetAwaiter().GetResult();
            var error = errorTask.GetAwaiter().GetResult();
            if (process.ExitCode == 0)
            {
                return SchtasksResult.Success(output);
            }

            return SchtasksResult.Failure(
                string.IsNullOrWhiteSpace(error)
                    ? "Windows 작업 스케줄러 명령이 실패했습니다."
                    : error.Trim(),
                output);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return SchtasksResult.Failure(
                "Windows 권한 확인이 취소되어 시작프로그램 설정을 바꾸지 못했습니다.");
        }
        catch (Exception ex)
        {
            return SchtasksResult.Failure(ex.Message);
        }
    }

    private static string QuoteArgument(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
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

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill();
        }
        catch
        {
        }
    }

    private sealed record SchtasksResult(
        bool Succeeded,
        string Output,
        string? ErrorMessage)
    {
        public static SchtasksResult Success(string output)
        {
            return new SchtasksResult(true, output, null);
        }

        public static SchtasksResult Failure(string errorMessage, string output = "")
        {
            return new SchtasksResult(false, output, errorMessage);
        }
    }
}
