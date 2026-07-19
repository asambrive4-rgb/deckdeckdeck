using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Tests;

public sealed class DomainRulesTests
{
    [Fact]
    public void SnippetPasteTextRequiresContent()
    {
        var result = SnippetRules.ValidateForSave(
            "Paste",
            string.Empty,
            SnippetActionType.PasteText,
            launchPath: null,
            launchUrl: null,
            SnippetMediaProvider.System,
            SnippetMediaCommand.PlayPause);

        Assert.False(result.Succeeded);
        Assert.Equal("붙여넣을 문구를 입력해 주세요.", result.ErrorMessage);
    }

    [Fact]
    public void SnippetLaunchUrlNormalizesHttpAddress()
    {
        var result = SnippetRules.ValidateForSave(
            "Docs",
            string.Empty,
            SnippetActionType.LaunchUrl,
            launchPath: null,
            launchUrl: "example.com/docs",
            SnippetMediaProvider.System,
            SnippetMediaCommand.PlayPause);

        Assert.True(result.Succeeded);
        Assert.Equal("https://example.com/docs", result.NormalizedLaunchUrl);
    }

    [Fact]
    public void TerminalCommandRequiresCommandUnlessOpeningWindow()
    {
        var backgroundWithoutCommand = SnippetRules.ValidateForSave(
            "Grok",
            string.Empty,
            SnippetActionType.TerminalCommand,
            launchPath: null,
            launchUrl: null,
            SnippetMediaProvider.System,
            SnippetMediaCommand.PlayPause,
            terminalCommand: "  ",
            openTerminalWindow: false);

        Assert.False(backgroundWithoutCommand.Succeeded);
        Assert.Equal(
            SnippetRules.TerminalCommandRequiredMessage,
            backgroundWithoutCommand.ErrorMessage);

        var openWindowWithoutCommand = SnippetRules.ValidateForSave(
            "Grok",
            string.Empty,
            SnippetActionType.TerminalCommand,
            launchPath: null,
            launchUrl: null,
            SnippetMediaProvider.System,
            SnippetMediaCommand.PlayPause,
            terminalCommand: "  ",
            openTerminalWindow: true,
            terminalWorkingDirectory: "  C:\\repos\\demo  ");

        Assert.True(openWindowWithoutCommand.Succeeded);
        Assert.Null(openWindowWithoutCommand.NormalizedTerminalCommand);
        Assert.True(openWindowWithoutCommand.OpenTerminalWindow);
        Assert.Equal(@"C:\repos\demo", openWindowWithoutCommand.TerminalWorkingDirectory);
    }

    [Fact]
    public void TerminalCommandParametersAreExtractedInFirstSeenOrderWithoutDuplicates()
    {
        var names = TerminalCommandParameterRules.ExtractParameterNames(
            "adb connect {{IP}}:{{Port}} && echo {{IP}} {{ }}");

        Assert.Equal(new[] { "IP", "Port" }, names);
    }

    [Fact]
    public void TerminalCommandParametersAreAppliedByName()
    {
        var command = TerminalCommandParameterRules.ApplyParameters(
            "adb connect {{IP}}:{{Port}}",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["IP"] = "10.0.0.1",
                ["Port"] = "5555"
            });

        Assert.Equal("adb connect 10.0.0.1:5555", command);
    }

    [Fact]
    public void TerminalCommandParameterCanBeAddedAndRemoved()
    {
        Assert.True(TerminalCommandParameterRules.TryAddParameter(
            "adb connect",
            "Port",
            out var withPort,
            out var addError));
        Assert.Null(addError);
        Assert.Equal("adb connect {{Port}}", withPort);

        Assert.False(TerminalCommandParameterRules.TryAddParameter(
            withPort,
            "Port",
            out _,
            out var duplicateError));
        Assert.Equal(
            TerminalCommandParameterRules.DuplicateParameterNameMessage,
            duplicateError);

        var removed = TerminalCommandParameterRules.RemoveParameter(
            "adb connect {{IP}}:{{Port}}",
            "IP");
        Assert.Equal("adb connect :{{Port}}", removed);
    }

    [Fact]
    public void AdbEndpointAcceptsNumericIpAndPort()
    {
        Assert.True(TerminalCommandParameterRules.IsAdbWirelessConnectCommand(
            TerminalCommandParameterRules.AdbWirelessPowerShellExample));

        Assert.True(TerminalCommandParameterRules.TryNormalizeAdbIp(
            "10.42.17.83",
            out var ip,
            out var ipError));
        Assert.Null(ipError);
        Assert.Equal("10.42.17.83", ip);

        Assert.True(TerminalCommandParameterRules.TryNormalizeAdbPort(
            "12345",
            out var port,
            out var portError));
        Assert.Null(portError);
        Assert.Equal("12345", port);

        Assert.False(TerminalCommandParameterRules.TryNormalizeAdbIp(
            "abc",
            out _,
            out var invalidIpError));
        Assert.Equal(TerminalCommandParameterRules.InvalidAdbIpMessage, invalidIpError);

        Assert.False(TerminalCommandParameterRules.TryNormalizeAdbPort(
            "70000",
            out _,
            out var invalidPortError));
        Assert.Equal(TerminalCommandParameterRules.InvalidAdbPortMessage, invalidPortError);
    }

    [Fact]
    public void MediaCommandFallsBackWhenProviderDoesNotSupportCommand()
    {
        var command = MediaCommandRules.GetValidCommandForProvider(
            SnippetMediaProvider.System,
            SnippetMediaCommand.CycleRepeat);

        Assert.Equal(SnippetMediaCommand.PlayPause, command);
    }

    [Fact]
    public void SlotIsEnabledWhenStateIsMissing()
    {
        Assert.True(SlotRules.IsEnabled(SlotKey.Numpad3, new Dictionary<SlotKey, bool>()));
    }
}

