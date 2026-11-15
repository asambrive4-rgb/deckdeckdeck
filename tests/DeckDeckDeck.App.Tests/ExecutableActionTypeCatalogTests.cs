// 역할: 지원하는 6개 실행 타입 카탈로그의 패널 매핑, 표시 라벨, 실행 전 숨김 정책이 올바른지 검증하는 단위 테스트 모음입니다.
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Tests;

public sealed class ExecutableActionTypeCatalogTests
{
    [Theory]
    [InlineData(SnippetActionType.PasteText, FileActionMode.Launch, null, ActionEditorPanel.Paste)]
    [InlineData(SnippetActionType.LaunchFile, FileActionMode.Paste, null, ActionEditorPanel.Paste)]
    [InlineData(SnippetActionType.LaunchFile, FileActionMode.Launch, null, ActionEditorPanel.OpenLaunch)]
    [InlineData(SnippetActionType.LaunchUrl, FileActionMode.Launch, null, ActionEditorPanel.OpenLaunch)]
    [InlineData(SnippetActionType.MediaAction, FileActionMode.Launch, null, ActionEditorPanel.Media)]
    [InlineData(SnippetActionType.Timer, FileActionMode.Launch, null, ActionEditorPanel.SystemUtility)]
    [InlineData(SnippetActionType.TerminalCommand, FileActionMode.Launch, "display off", ActionEditorPanel.SystemUtility)]
    [InlineData(SnippetActionType.TerminalCommand, FileActionMode.Launch, "bt-audio connect --device \"WH-1000XM5\"", ActionEditorPanel.SystemUtility)]
    [InlineData(SnippetActionType.TerminalCommand, FileActionMode.Launch, "bt-audio connect --device", ActionEditorPanel.SystemUtility)]
    [InlineData(SnippetActionType.TerminalCommand, FileActionMode.Launch, "echo hello", ActionEditorPanel.Terminal)]
    [InlineData(SnippetActionType.TerminalCommand, FileActionMode.Launch, null, ActionEditorPanel.Terminal)]
    [InlineData(SnippetActionType.TerminalCommand, FileActionMode.Launch, "adb connect 192.168.0.10:5555", ActionEditorPanel.Android)]
    [InlineData(SnippetActionType.TerminalCommand, FileActionMode.Launch, "adb connect", ActionEditorPanel.Android)]
    [InlineData(SnippetActionType.RemoteInstall, FileActionMode.Launch, null, ActionEditorPanel.Android)]
    public void GetEditorPanelMapsEveryKnownActionTypeAndPreset(
        SnippetActionType actionType,
        FileActionMode fileActionMode,
        string? terminalCommand,
        ActionEditorPanel expectedPanel)
    {
        Assert.Equal(
            expectedPanel,
            ExecutableActionTypeCatalog.GetEditorPanel(actionType, fileActionMode, terminalCommand));
    }

    [Fact]
    public void GetEditorPanelCoversEveryEnumValue()
    {
        foreach (SnippetActionType actionType in Enum.GetValues<SnippetActionType>())
        {
            var panel = ExecutableActionTypeCatalog.GetEditorPanel(actionType);
            Assert.True(Enum.IsDefined(panel), $"No editor panel for {actionType}");
        }
    }

    [Theory]
    [InlineData(SnippetActionType.PasteText, FileActionMode.Launch, null, "붙여넣기 · 문구")]
    [InlineData(SnippetActionType.LaunchFile, FileActionMode.Paste, null, "붙여넣기 · 파일")]
    [InlineData(SnippetActionType.LaunchFile, FileActionMode.Launch, null, "열기·실행 · 파일/앱")]
    [InlineData(SnippetActionType.LaunchUrl, FileActionMode.Launch, null, "열기·실행 · 웹 주소")]
    [InlineData(SnippetActionType.MediaAction, FileActionMode.Launch, null, "미디어 제어")]
    [InlineData(SnippetActionType.Timer, FileActionMode.Launch, null, "시스템·편의 · 타이머")]
    [InlineData(SnippetActionType.TerminalCommand, FileActionMode.Launch, "display off", "시스템·편의 · 화면 끄기")]
    [InlineData(SnippetActionType.TerminalCommand, FileActionMode.Launch, "bt-audio connect --device \"WH-1000XM5\"", "시스템·편의 · 블루투스")]
    [InlineData(SnippetActionType.TerminalCommand, FileActionMode.Launch, "adb connect 192.168.0.10:5555", "Android · 무선 ADB")]
    [InlineData(SnippetActionType.RemoteInstall, FileActionMode.Launch, null, "Android · 원격설치")]
    [InlineData(SnippetActionType.TerminalCommand, FileActionMode.Launch, "echo hello", "터미널")]
    [InlineData(SnippetActionType.TerminalCommand, FileActionMode.Launch, null, "터미널")]
    public void GetDisplayLabelMatchesUnifiedUiCopy(
        SnippetActionType actionType,
        FileActionMode fileActionMode,
        string? terminalCommand,
        string expectedLabel)
    {
        Assert.Equal(
            expectedLabel,
            ExecutableActionTypeCatalog.GetDisplayLabel(actionType, fileActionMode, terminalCommand));
    }

    [Theory]
    [InlineData("display off", true)]
    [InlineData("bt-audio connect --device \"Buds\"", true)]
    [InlineData("adb connect 192.168.0.10:5555", true)]
    [InlineData("echo hi", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsPresetTerminalCommandIdentifiesMovedSystemAndAndroidPresets(
        string? terminalCommand,
        bool expected)
    {
        Assert.Equal(expected, ExecutableActionTypeCatalog.IsPresetTerminalCommand(terminalCommand));
    }

    [Theory]
    [InlineData(SnippetActionType.PasteText, FileActionMode.Launch, true, true)]
    [InlineData(SnippetActionType.PasteText, FileActionMode.Launch, false, false)]
    [InlineData(SnippetActionType.LaunchFile, FileActionMode.Paste, true, true)]
    [InlineData(SnippetActionType.LaunchFile, FileActionMode.Launch, true, false)]
    [InlineData(SnippetActionType.LaunchUrl, FileActionMode.Launch, true, false)]
    [InlineData(SnippetActionType.MediaAction, FileActionMode.Launch, true, false)]
    [InlineData(SnippetActionType.TerminalCommand, FileActionMode.Launch, true, false)]
    public void ShouldHideBeforeExecuteMatchesPreparePolicy(
        SnippetActionType actionType,
        FileActionMode fileActionMode,
        bool autoHide,
        bool expected)
    {
        Assert.Equal(
            expected,
            ExecutableActionTypeCatalog.ShouldHideBeforeExecute(
                actionType,
                fileActionMode,
                autoHide));
    }
}
