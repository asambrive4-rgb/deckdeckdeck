using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Tests;

public sealed class ExecutableActionTypeCatalogTests
{
    [Theory]
    [InlineData(SnippetActionType.PasteText, ActionEditorPanel.PasteText)]
    [InlineData(SnippetActionType.LaunchFile, ActionEditorPanel.LaunchFile)]
    [InlineData(SnippetActionType.LaunchUrl, ActionEditorPanel.LaunchUrl)]
    [InlineData(SnippetActionType.MediaAction, ActionEditorPanel.Media)]
    [InlineData(SnippetActionType.TerminalCommand, ActionEditorPanel.TerminalCommand)]
    public void GetEditorPanelMapsEveryKnownActionType(
        SnippetActionType actionType,
        ActionEditorPanel expectedPanel)
    {
        Assert.Equal(expectedPanel, ExecutableActionTypeCatalog.GetEditorPanel(actionType));
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
    [InlineData(SnippetActionType.PasteText, FileActionMode.Launch, "문구 붙여넣기")]
    [InlineData(SnippetActionType.LaunchFile, FileActionMode.Launch, "파일/바로 가기 실행")]
    [InlineData(SnippetActionType.LaunchFile, FileActionMode.Paste, "파일 붙여넣기")]
    [InlineData(SnippetActionType.LaunchUrl, FileActionMode.Launch, "웹 주소 열기")]
    [InlineData(SnippetActionType.MediaAction, FileActionMode.Launch, "음악/미디어 제어")]
    [InlineData(SnippetActionType.TerminalCommand, FileActionMode.Launch, "터미널 명령 실행")]
    public void GetDisplayLabelMatchesExistingUiCopy(
        SnippetActionType actionType,
        FileActionMode fileActionMode,
        string expectedLabel)
    {
        Assert.Equal(
            expectedLabel,
            ExecutableActionTypeCatalog.GetDisplayLabel(actionType, fileActionMode));
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
