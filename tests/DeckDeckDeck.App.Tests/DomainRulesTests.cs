// 역할: 슬롯, 카테고리, 텍스트 스니펫, 무선 ADB 페어링/연결 등 도메인 핵심 규칙의 유효성 검증 로직을 시험하는 단위 테스트 모음입니다.
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

    [Theory]
    [InlineData("123456", "123456")]
    [InlineData(" 123 456 ", "123456")]
    [InlineData("123-456", "123456")]
    [InlineData("000000", "000000")]
    [InlineData("98-7 6-5 4", "987654")]
    public void AdbPairCodeAcceptsValidSixDigitInput(string input, string expected)
    {
        Assert.True(TerminalCommandParameterRules.TryNormalizeAdbPairCode(
            input,
            out var normalized,
            out var error));
        Assert.Null(error);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(null, TerminalCommandParameterRules.EmptyAdbPairCodeMessage)]
    [InlineData("", TerminalCommandParameterRules.EmptyAdbPairCodeMessage)]
    [InlineData("   ", TerminalCommandParameterRules.EmptyAdbPairCodeMessage)]
    [InlineData(" - - ", TerminalCommandParameterRules.EmptyAdbPairCodeMessage)]
    [InlineData("12345", TerminalCommandParameterRules.InvalidAdbPairCodeMessage)]
    [InlineData("1234567", TerminalCommandParameterRules.InvalidAdbPairCodeMessage)]
    [InlineData("12a456", TerminalCommandParameterRules.InvalidAdbPairCodeMessage)]
    [InlineData("abcdef", TerminalCommandParameterRules.InvalidAdbPairCodeMessage)]
    [InlineData("12.456", TerminalCommandParameterRules.InvalidAdbPairCodeMessage)]
    public void AdbPairCodeRejectsInvalidOrEmptyInput(string? input, string expectedError)
    {
        Assert.False(TerminalCommandParameterRules.TryNormalizeAdbPairCode(
            input,
            out var normalized,
            out var error));
        Assert.Equal(string.Empty, normalized);
        Assert.Equal(expectedError, error);
    }

    [Fact]
    public void AdbPairCommandBuildsExpectedCommandLines()
    {
        // 1. From PowerShell example with connect {{IP}}:{{Port}}
        var psResult = TerminalCommandParameterRules.BuildAdbPairCommand(
            TerminalCommandParameterRules.AdbWirelessPowerShellExample,
            "192.168.1.50",
            "38291",
            "654321");
        Assert.Equal(
            @"& ""$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe"" pair 192.168.1.50:38291 654321",
            psResult);

        // 2. From PowerShell example with pair {{IP}}:{{Port}} {{PairCode}}
        var psPairResult = TerminalCommandParameterRules.BuildAdbPairCommand(
            TerminalCommandParameterRules.AdbPairPowerShellExample,
            "192.168.1.50",
            "38291",
            "654321");
        Assert.Equal(
            @"& ""$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe"" pair 192.168.1.50:38291 654321",
            psPairResult);

        // 3. Simple adb connect command
        var simpleConnect = TerminalCommandParameterRules.BuildAdbPairCommand(
            "adb connect {{IP}}:{{Port}}",
            "10.0.0.5",
            "5555",
            "123456");
        Assert.Equal("adb pair 10.0.0.5:5555 123456", simpleConnect);

        // 4. Simple adb pair command with {{IP}}:{{Port}}
        var simplePair = TerminalCommandParameterRules.BuildAdbPairCommand(
            "adb pair {{IP}}:{{Port}}",
            "10.0.0.5",
            "5555",
            "123456");
        Assert.Equal("adb pair 10.0.0.5:5555 123456", simplePair);

        // 5. Explicit PairCode placeholder
        var explicitPairCode = TerminalCommandParameterRules.BuildAdbPairCommand(
            "adb pair {{IP}}:{{Port}} {{PairCode}}",
            "10.0.0.5",
            "5555",
            "123456");
        Assert.Equal("adb pair 10.0.0.5:5555 123456", explicitPairCode);

        // 6. Whitespace inside placeholders
        var whitespacePlaceholders = TerminalCommandParameterRules.BuildAdbPairCommand(
            "adb connect {{ IP }} : {{ Port }}",
            "10.0.0.5",
            "5555",
            "123456");
        Assert.Equal("adb pair 10.0.0.5:5555 123456", whitespacePlaceholders);

        // 7. Empty template
        var emptyResult = TerminalCommandParameterRules.BuildAdbPairCommand(
            string.Empty,
            "10.0.0.5",
            "5555",
            "123456");
        Assert.Equal(string.Empty, emptyResult);
    }

    [Fact]
    public void AdbWirelessCommandRecognizesConnectAndPairVariants()
    {
        // Connect command
        Assert.True(TerminalCommandParameterRules.IsAdbWirelessConnectCommand(
            TerminalCommandParameterRules.AdbWirelessPowerShellExample));
        Assert.True(TerminalCommandParameterRules.IsAdbWirelessConnectCommand(
            "adb connect {{IP}}:{{Port}}"));
        Assert.False(TerminalCommandParameterRules.IsAdbWirelessConnectCommand(
            "adb pair {{IP}}:{{Port}}"));

        // Pair command
        Assert.True(TerminalCommandParameterRules.IsAdbWirelessPairCommand(
            TerminalCommandParameterRules.AdbPairPowerShellExample));
        Assert.True(TerminalCommandParameterRules.IsAdbWirelessPairCommand(
            "adb pair {{IP}}:{{Port}}"));
        Assert.True(TerminalCommandParameterRules.IsAdbWirelessPairCommand(
            "adb pair {{IP}}:{{Port}} {{PairCode}}"));
        Assert.False(TerminalCommandParameterRules.IsAdbWirelessPairCommand(
            "adb connect {{IP}}:{{Port}}"));

        // General wireless command
        Assert.True(TerminalCommandParameterRules.IsAdbWirelessCommand(
            TerminalCommandParameterRules.AdbWirelessPowerShellExample));
        Assert.True(TerminalCommandParameterRules.IsAdbWirelessCommand(
            TerminalCommandParameterRules.AdbPairPowerShellExample));
        Assert.True(TerminalCommandParameterRules.IsAdbWirelessCommand(
            "adb connect {{IP}}:{{Port}}"));
        Assert.True(TerminalCommandParameterRules.IsAdbWirelessCommand(
            "adb pair {{IP}}:{{Port}}"));
        Assert.False(TerminalCommandParameterRules.IsAdbWirelessCommand(
            "adb connect 192.168.1.1:5555"));
        Assert.False(TerminalCommandParameterRules.IsAdbWirelessCommand(null));
        Assert.False(TerminalCommandParameterRules.IsAdbWirelessCommand(string.Empty));
    }

    [Fact]
    public void ExtractParameterNamesIncludesPairCode()
    {
        var parameters = TerminalCommandParameterRules.ExtractParameterNames(
            "adb pair {{IP}}:{{Port}} {{PairCode}}");
        Assert.Equal(3, parameters.Count);
        Assert.Contains(TerminalCommandParameterRules.AdbIpParameterName, parameters);
        Assert.Contains(TerminalCommandParameterRules.AdbPortParameterName, parameters);
        Assert.Contains(TerminalCommandParameterRules.AdbPairCodeParameterName, parameters);
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

