// 역할: 프로그램에서 지원하는 모든 실행 동작 유형 목록과 기본 표시 정보를 제공합니다.
using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Domain;

/// <summary>
/// Single entry for executable action-type presentation and pre-execute policy.
/// Validation, storage normalization, and runtime execution stay in their own types.
/// When adding <see cref="SnippetActionType"/>, update this catalog first.
/// </summary>
public static class ExecutableActionTypeCatalog
{
    public static ActionEditorPanel GetEditorPanel(SnippetActionType actionType) => actionType switch
    {
        SnippetActionType.LaunchFile => ActionEditorPanel.LaunchFile,
        SnippetActionType.LaunchUrl => ActionEditorPanel.LaunchUrl,
        SnippetActionType.MediaAction => ActionEditorPanel.Media,
        SnippetActionType.TerminalCommand => ActionEditorPanel.TerminalCommand,
        SnippetActionType.Timer => ActionEditorPanel.Timer,
        _ => ActionEditorPanel.PasteText
    };

    public static string GetDisplayLabel(
        SnippetActionType actionType,
        FileActionMode fileActionMode = FileActionMode.Launch) => actionType switch
    {
        SnippetActionType.LaunchFile when fileActionMode == FileActionMode.Paste => "파일 붙여넣기",
        SnippetActionType.LaunchFile => "파일/바로 가기 실행",
        SnippetActionType.LaunchUrl => "웹 주소 열기",
        SnippetActionType.MediaAction => "음악/미디어 제어",
        SnippetActionType.TerminalCommand => "터미널 명령 실행",
        SnippetActionType.Timer => "타이머 실행",
        _ => "문구 붙여넣기"
    };

    /// <summary>
    /// Whether the palette should hide before the action runs (paste-style flows).
    /// </summary>
    public static bool ShouldHideBeforeExecute(
        SnippetActionType actionType,
        FileActionMode fileActionMode,
        bool autoHideAfterPaste) =>
        autoHideAfterPaste && (actionType == SnippetActionType.PasteText
            || (actionType == SnippetActionType.LaunchFile && fileActionMode == FileActionMode.Paste));

    public static bool IsEditorPanel(SnippetActionType actionType, ActionEditorPanel panel) =>
        GetEditorPanel(actionType) == panel;
}

/// <summary>
/// Which editor form section is shown for an action type.
/// </summary>
public enum ActionEditorPanel
{
    PasteText,
    LaunchFile,
    LaunchUrl,
    Media,
    TerminalCommand,
    Timer
}
