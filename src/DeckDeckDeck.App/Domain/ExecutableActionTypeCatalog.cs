// 역할: 프로그램에서 지원하는 6개 실행 타입 분류와 편집 패널/표시 이름 매핑을 제공합니다.
using DeckDeckDeck.App.Models;

namespace DeckDeckDeck.App.Domain;

/// <summary>
/// Single entry for executable action-type presentation and pre-execute policy.
/// Validation, storage normalization, and runtime execution stay in their own types.
/// When adding <see cref="SnippetActionType"/>, update this catalog first.
/// </summary>
public static class ExecutableActionTypeCatalog
{
    public static ActionEditorPanel GetEditorPanel(
        SnippetActionType actionType,
        FileActionMode fileActionMode = FileActionMode.Launch,
        string? terminalCommand = null) => actionType switch
    {
        SnippetActionType.PasteText => ActionEditorPanel.Paste,
        SnippetActionType.LaunchFile when fileActionMode == FileActionMode.Paste => ActionEditorPanel.Paste,
        SnippetActionType.LaunchFile => ActionEditorPanel.OpenLaunch,
        SnippetActionType.LaunchUrl => ActionEditorPanel.OpenLaunch,
        SnippetActionType.MediaAction => ActionEditorPanel.Media,
        SnippetActionType.Timer => ActionEditorPanel.SystemUtility,
        SnippetActionType.RemoteInstall => ActionEditorPanel.Android,
        SnippetActionType.TerminalCommand when IsSystemUtilityTerminalCommand(terminalCommand) =>
            ActionEditorPanel.SystemUtility,
        SnippetActionType.TerminalCommand when TerminalCommandParameterRules.IsAdbWirelessConnectCommand(terminalCommand) =>
            ActionEditorPanel.Android,
        SnippetActionType.TerminalCommand => ActionEditorPanel.Terminal,
        _ => ActionEditorPanel.Paste
    };

    public static string GetDisplayLabel(
        SnippetActionType actionType,
        FileActionMode fileActionMode = FileActionMode.Launch,
        string? terminalCommand = null) => actionType switch
    {
        SnippetActionType.LaunchFile when fileActionMode == FileActionMode.Paste => "붙여넣기 · 파일",
        SnippetActionType.LaunchFile => "열기·실행 · 파일/앱",
        SnippetActionType.LaunchUrl => "열기·실행 · 웹 주소",
        SnippetActionType.MediaAction => "미디어 제어",
        SnippetActionType.Timer => "시스템·편의 · 타이머",
        SnippetActionType.RemoteInstall => "Android · 원격설치",
        SnippetActionType.TerminalCommand when TerminalCommandParameterRules.IsTurnOffDisplayCommand(terminalCommand) =>
            "시스템·편의 · 화면 끄기",
        SnippetActionType.TerminalCommand when TerminalCommandParameterRules.TryParseBluetoothAudioCommand(terminalCommand, out _, allowEmptyTarget: true) =>
            "시스템·편의 · 블루투스",
        SnippetActionType.TerminalCommand when TerminalCommandParameterRules.IsAdbWirelessConnectCommand(terminalCommand) =>
            "Android · 무선 ADB",
        SnippetActionType.TerminalCommand => "터미널",
        _ => "붙여넣기 · 문구"
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

    public static bool IsEditorPanel(
        SnippetActionType actionType,
        ActionEditorPanel panel,
        FileActionMode fileActionMode = FileActionMode.Launch,
        string? terminalCommand = null) =>
        GetEditorPanel(actionType, fileActionMode, terminalCommand) == panel;

    public static bool IsPresetTerminalCommand(string? terminalCommand) =>
        IsSystemUtilityTerminalCommand(terminalCommand)
        || TerminalCommandParameterRules.IsAdbWirelessConnectCommand(terminalCommand);

    private static bool IsSystemUtilityTerminalCommand(string? terminalCommand) =>
        TerminalCommandParameterRules.IsTurnOffDisplayCommand(terminalCommand)
        || TerminalCommandParameterRules.TryParseBluetoothAudioCommand(terminalCommand, out _, allowEmptyTarget: true);
}

/// <summary>
/// Which top-level execution type panel (one of 6 unified categories) is shown for an action.
/// </summary>
public enum ActionEditorPanel
{
    Paste,
    OpenLaunch,
    Media,
    SystemUtility,
    Terminal,
    Android
}
