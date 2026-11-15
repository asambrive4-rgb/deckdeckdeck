// 역할: 사용자가 편집 화면에서 작성 중인 다양한 동작(파일 실행, URL 열기 등)의 임시 입력 상태를 관리합니다.
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.UseCases;

public sealed class ExecutableActionEditDraft
{
    private readonly EditableImageDraft _imageDraft;
    private readonly ISnippetImageResolver? _snippetImageResolver;
    private readonly IStoredImagePathResolver? _storedImagePathResolver;
    private readonly bool _canStoreImages;
    private AutoIconCacheEntry? _autoIcon;

    private ExecutableActionEditDraft(
        ExecutableActionEditDraftState state,
        IImageFileRepository? imageFileRepository,
        ISnippetImageResolver? snippetImageResolver,
        IStoredImagePathResolver? storedImagePathResolver)
    {
        _imageDraft = new EditableImageDraft(
            state.ImagePath,
            state.ThumbnailPath,
            imageFileRepository);
        _snippetImageResolver = snippetImageResolver;
        _storedImagePathResolver = storedImagePathResolver;
        _canStoreImages = imageFileRepository is not null;
        Title = state.Title;
        Content = state.Content;
        Description = state.Description;
        ActionType = state.ActionType;
        PasteShortcutMode = state.PasteShortcutMode;
        LaunchPath = state.LaunchPath;
        FileActionMode = state.FileActionMode;
        LaunchUrl = state.LaunchUrl;
        MediaProvider = state.MediaProvider;
        MediaCommand = MediaCommandRules.GetValidCommandForProvider(
            MediaProvider,
            state.MediaCommand);
        TerminalCommand = state.TerminalCommand;
        TerminalShell = state.TerminalShell;
        OpenTerminalWindow = state.ActionType == SnippetActionType.TerminalCommand
            && state.OpenTerminalWindow;
        TerminalWorkingDirectory = state.TerminalWorkingDirectory;
        AdbDeviceIp = state.AdbDeviceIp;
        RunAsAdministrator = state.ActionType == SnippetActionType.TerminalCommand
            ? state.RunAsAdministrator
            : true;
        RemoteInstallProjectPath = state.RemoteInstallProjectPath;
        RemoteInstallModule = state.RemoteInstallModule;
        RemoteInstallDeviceKind = state.RemoteInstallDeviceKind;
        RemoteInstallTargetDevice = state.RemoteInstallTargetDevice;
        RemoteInstallLaunchAfterInstall = state.RemoteInstallLaunchAfterInstall;
        RemoteInstallPromptProject = state.RemoteInstallPromptProject;
        SlotImageMode = GetInitialSlotImageMode(state.SlotImageMode, state.ImagePath);
        _autoIcon = state.AutoIcon;
    }

    public string Title { get; set; }

    public string Content { get; set; }

    public string Description { get; set; }

    public SnippetActionType ActionType { get; private set; }

    public PasteShortcutMode PasteShortcutMode { get; set; }

    public string LaunchPath { get; private set; }

    public FileActionMode FileActionMode { get; private set; }

    public string LaunchUrl { get; private set; }

    public SnippetMediaProvider MediaProvider { get; private set; }

    public SnippetMediaCommand MediaCommand { get; private set; }

    public string TerminalCommand { get; set; }

    public SnippetTerminalShell TerminalShell { get; set; }

    public bool OpenTerminalWindow { get; set; }

    public string TerminalWorkingDirectory { get; set; }

    public string AdbDeviceIp { get; set; }

    public bool RunAsAdministrator { get; set; }

    public string RemoteInstallProjectPath { get; set; }

    public string RemoteInstallModule { get; set; }

    public RemoteInstallDeviceKind RemoteInstallDeviceKind { get; set; }

    public string RemoteInstallTargetDevice { get; set; }

    public bool RemoteInstallLaunchAfterInstall { get; set; }

    public bool RemoteInstallPromptProject { get; set; }

    public SlotImageMode SlotImageMode { get; private set; }

    public AutoIconCacheEntry? AutoIcon => _autoIcon;

    public string? ImagePath => _imageDraft.ImagePath;

    public string? ThumbnailPath => _imageDraft.ThumbnailPath;

    public bool HasImage => _imageDraft.HasImage;

    public bool CanStoreImages => _canStoreImages;

    public static ExecutableActionEditDraft FromSnippet(
        Snippet? snippet,
        IImageFileRepository? imageFileRepository,
        ISnippetImageResolver? snippetImageResolver = null,
        IStoredImagePathResolver? storedImagePathResolver = null)
    {
        return new ExecutableActionEditDraft(
            snippet is null
                ? ExecutableActionEditDraftState.Default()
                : ExecutableActionEditDraftState.FromSnippet(snippet),
            imageFileRepository,
            snippetImageResolver,
            storedImagePathResolver);
    }

    public static ExecutableActionEditDraft FromHotkeyAction(
        HotkeyAction? action,
        IImageFileRepository? imageFileRepository,
        ISnippetImageResolver? snippetImageResolver = null,
        IStoredImagePathResolver? storedImagePathResolver = null)
    {
        return new ExecutableActionEditDraft(
            action is null
                ? ExecutableActionEditDraftState.Default()
                : ExecutableActionEditDraftState.FromHotkeyAction(action),
            imageFileRepository,
            snippetImageResolver,
            storedImagePathResolver);
    }

    public ActionEditorPanel EditorPanel =>
        ExecutableActionTypeCatalog.GetEditorPanel(ActionType, FileActionMode, TerminalCommand);

    public void SetActionType(SnippetActionType actionType)
    {
        var isSwitchingToTerminal = actionType == SnippetActionType.TerminalCommand && ActionType != SnippetActionType.TerminalCommand;
        ActionType = actionType;
        if (isSwitchingToTerminal)
        {
            OpenTerminalWindow = true;
        }
    }

    public void ActivatePasteText() => SetActionType(SnippetActionType.PasteText);

    public void ActivatePasteFile()
    {
        SetActionType(SnippetActionType.LaunchFile);
        SetFileActionMode(FileActionMode.Paste);
    }

    public void ActivateOpenFile()
    {
        SetActionType(SnippetActionType.LaunchFile);
        SetFileActionMode(FileActionMode.Launch);
    }

    public void ActivateOpenUrl() => SetActionType(SnippetActionType.LaunchUrl);

    public void ActivateMedia() => SetActionType(SnippetActionType.MediaAction);

    public void ActivateTurnOffDisplay()
    {
        ActionType = SnippetActionType.TerminalCommand;
        ApplyTurnOffDisplayDefaults();
    }

    public void ActivateBluetoothControl(BluetoothAudioActionKind action, string? target)
    {
        ActionType = SnippetActionType.TerminalCommand;
        ApplyBluetoothControlDefaults(action, target);
    }

    public void ActivateTimer() => SetActionType(SnippetActionType.Timer);

    public void ActivateCustomTerminalCommand()
    {
        if (ExecutableActionTypeCatalog.IsPresetTerminalCommand(TerminalCommand))
        {
            TerminalCommand = string.Empty;
            TerminalShell = SnippetTerminalShell.Cmd;
            OpenTerminalWindow = true;
            RunAsAdministrator = true;
        }

        SetActionType(SnippetActionType.TerminalCommand);
    }

    public void ActivateAdbConnect()
    {
        ActionType = SnippetActionType.TerminalCommand;
        ApplyAdbPairingDefaults();
    }

    public void ActivateRemoteInstall() => SetActionType(SnippetActionType.RemoteInstall);

    public void SetLaunchPath(string launchPath)
    {
        LaunchPath = launchPath;
        UpdateAutoIconPreview();
    }

    public void SetLaunchFolderPath(string launchPath)
    {
        LaunchPath = launchPath;
        _autoIcon = null;
    }

    public void SetFileActionMode(FileActionMode fileActionMode)
    {
        FileActionMode = fileActionMode;
        UpdateAutoIconPreview();
    }

    public void SetLaunchUrl(string launchUrl)
    {
        LaunchUrl = launchUrl;
    }

    public void ApplyNormalizedLaunchUrl(string? normalizedLaunchUrl)
    {
        if (ActionType == SnippetActionType.LaunchUrl
            && !string.IsNullOrWhiteSpace(normalizedLaunchUrl))
        {
            LaunchUrl = normalizedLaunchUrl;
        }
    }

    public void SetMediaProvider(SnippetMediaProvider provider)
    {
        MediaProvider = provider;
        MediaCommand = MediaCommandRules.GetValidCommandForProvider(provider, MediaCommand);
    }

    public void SetMediaCommand(SnippetMediaCommand command)
    {
        MediaCommand = command;
    }

    public void ApplyAdbPairingDefaults()
    {
        TerminalCommand = TerminalCommandParameterRules.AdbWirelessPowerShellExample;
        TerminalShell = SnippetTerminalShell.PowerShell;
        OpenTerminalWindow = true;
        RunAsAdministrator = false;
    }

    public void ApplyTurnOffDisplayDefaults()
    {
        TerminalCommand = TerminalCommandParameterRules.TurnOffDisplayCommand;
        TerminalShell = SnippetTerminalShell.Cmd;
        OpenTerminalWindow = false;
        RunAsAdministrator = false;
    }

    public void ApplyBluetoothControlDefaults(BluetoothAudioActionKind action, string? target)
    {
        TerminalCommand = TerminalCommandParameterRules.BuildBluetoothAudioCommand(action, target);
        TerminalShell = SnippetTerminalShell.Cmd;
        OpenTerminalWindow = false;
        RunAsAdministrator = false;
    }

    public void ReplaceImageFromPath(string sourcePath)
    {
        _imageDraft.ReplaceWithStoredImage(sourcePath);
        SlotImageMode = SlotImageMode.Custom;
    }

    public void RemoveImage()
    {
        _imageDraft.RemoveImage();
        SlotImageMode = SlotImageMode.Auto;
        UpdateAutoIconPreview();
    }

    public void DeleteCurrentUnsavedImage()
    {
        _imageDraft.DeleteCurrentUnsavedImage();
    }

    public void MarkSaved()
    {
        _imageDraft.DeleteOriginalImageIfReplaced();
        _imageDraft.MarkCurrentAsOriginal();
    }

    public string? GetPreviewThumbnailPath()
    {
        return SlotImageMode switch
        {
            SlotImageMode.Custom => ResolveDisplayPath(_imageDraft.ThumbnailPath),
            SlotImageMode.Auto when ActionType == SnippetActionType.LaunchFile =>
                ResolveDisplayPath(_autoIcon?.IconPath),
            SlotImageMode.Auto when ActionType == SnippetActionType.MediaAction =>
                MediaIconResourcePaths.GetIconResourcePath(MediaCommand),
            _ => null
        };
    }

    public AutoIconCacheEntry? PrepareAutoIconForSave()
    {
        if (SlotImageMode == SlotImageMode.None)
        {
            _autoIcon = null;
            return null;
        }

        if (_snippetImageResolver is null)
        {
            return ActionType == SnippetActionType.LaunchFile ? _autoIcon : null;
        }

        _autoIcon = _snippetImageResolver.PrepareAutoIcon(ActionType, LaunchPath, _autoIcon);
        return _autoIcon;
    }

    public SnippetSaveData ToSnippetSaveData(AutoIconCacheEntry? autoIcon = null)
    {
        return new SnippetSaveData(
            Title,
            Content,
            Description,
            ImagePath,
            ThumbnailPath,
            ActionType,
            LaunchPath,
            SlotImageMode,
            autoIcon ?? _autoIcon,
            LaunchUrl,
            MediaProvider,
            MediaCommand,
            PasteShortcutMode,
            TerminalCommand,
            TerminalShell,
            RunAsAdministrator,
            FileActionMode,
            OpenTerminalWindow,
            TerminalWorkingDirectory,
            AdbDeviceIp,
            RemoteInstallProjectPath,
            RemoteInstallModule,
            RemoteInstallDeviceKind,
            RemoteInstallTargetDevice,
            RemoteInstallLaunchAfterInstall,
            RemoteInstallPromptProject);
    }

    public HotkeyActionSaveData ToHotkeyActionSaveData(
        HotkeyGesture? gesture,
        bool isEnabled,
        AutoIconCacheEntry? autoIcon = null)
    {
        return new HotkeyActionSaveData(
            Title,
            gesture,
            isEnabled,
            Content,
            Description,
            ImagePath,
            ThumbnailPath,
            ActionType,
            LaunchPath,
            SlotImageMode,
            autoIcon ?? _autoIcon,
            LaunchUrl,
            MediaProvider,
            MediaCommand,
            PasteShortcutMode,
            TerminalCommand,
            TerminalShell,
            RunAsAdministrator,
            FileActionMode,
            OpenTerminalWindow,
            TerminalWorkingDirectory,
            AdbDeviceIp);
    }

    public void UpdateAutoIconPreview()
    {
        if (SlotImageMode == SlotImageMode.None)
        {
            _autoIcon = null;
            return;
        }

        if (_snippetImageResolver is not null)
        {
            _autoIcon = _snippetImageResolver.PrepareAutoIcon(ActionType, LaunchPath, _autoIcon);
        }
    }

    private string? ResolveDisplayPath(string? storedPath)
    {
        return _storedImagePathResolver?.ResolveDisplayPath(storedPath) ?? storedPath;
    }

    private static SlotImageMode GetInitialSlotImageMode(SlotImageMode slotImageMode, string? imagePath)
    {
        return slotImageMode == SlotImageMode.Auto && !string.IsNullOrWhiteSpace(imagePath)
            ? SlotImageMode.Custom
            : slotImageMode;
    }
}

internal sealed record ExecutableActionEditDraftState(
    string Title,
    string Content,
    string Description,
    string? ImagePath,
    string? ThumbnailPath,
    SnippetActionType ActionType,
    PasteShortcutMode PasteShortcutMode,
    string LaunchPath,
    FileActionMode FileActionMode,
    string LaunchUrl,
    SnippetMediaProvider MediaProvider,
    SnippetMediaCommand MediaCommand,
    string TerminalCommand,
    SnippetTerminalShell TerminalShell,
    bool OpenTerminalWindow,
    string TerminalWorkingDirectory,
    string AdbDeviceIp,
    bool RunAsAdministrator,
    SlotImageMode SlotImageMode,
    AutoIconCacheEntry? AutoIcon,
    string RemoteInstallProjectPath = "",
    string RemoteInstallModule = "",
    RemoteInstallDeviceKind RemoteInstallDeviceKind = RemoteInstallDeviceKind.Phone,
    string RemoteInstallTargetDevice = "",
    bool RemoteInstallLaunchAfterInstall = true,
    bool RemoteInstallPromptProject = false)
{
    public static ExecutableActionEditDraftState Default()
    {
        return new ExecutableActionEditDraftState(
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            null,
            SnippetActionType.PasteText,
            PasteShortcutMode.CtrlV,
            string.Empty,
            FileActionMode.Launch,
            string.Empty,
            SnippetMediaProvider.System,
            SnippetMediaCommand.PlayPause,
            string.Empty,
            SnippetTerminalShell.Cmd,
            false,
            string.Empty,
            string.Empty,
            true,
            SlotImageMode.Auto,
            null,
            string.Empty,
            string.Empty,
            RemoteInstallDeviceKind.Phone,
            string.Empty,
            true,
            false);
    }

    public static ExecutableActionEditDraftState FromSnippet(Snippet snippet)
    {
        return new ExecutableActionEditDraftState(
            snippet.Title,
            snippet.Content,
            snippet.Description ?? string.Empty,
            snippet.ImagePath,
            snippet.ThumbnailPath,
            snippet.ActionType,
            snippet.PasteShortcutMode,
            snippet.LaunchPath ?? string.Empty,
            snippet.FileActionMode,
            snippet.LaunchUrl ?? string.Empty,
            snippet.MediaProvider ?? SnippetMediaProvider.System,
            snippet.MediaCommand ?? SnippetMediaCommand.PlayPause,
            snippet.TerminalCommand ?? string.Empty,
            snippet.TerminalShell ?? SnippetTerminalShell.Cmd,
            snippet.OpenTerminalWindow,
            snippet.TerminalWorkingDirectory ?? string.Empty,
            snippet.AdbDeviceIp ?? string.Empty,
            snippet.RunAsAdministrator,
            snippet.SlotImageMode,
            AutoIconCacheEntry.FromSnippet(snippet),
            snippet.RemoteInstallProjectPath ?? string.Empty,
            snippet.RemoteInstallModule ?? string.Empty,
            snippet.RemoteInstallDeviceKind ?? RemoteInstallRules.GetDefaultDeviceKindForSlot(snippet.SlotKey),
            snippet.RemoteInstallTargetDevice ?? string.Empty,
            snippet.RemoteInstallLaunchAfterInstall,
            snippet.RemoteInstallPromptProject);
    }

    public static ExecutableActionEditDraftState FromHotkeyAction(HotkeyAction action)
    {
        return new ExecutableActionEditDraftState(
            action.Title,
            action.Content,
            action.Description ?? string.Empty,
            action.ImagePath,
            action.ThumbnailPath,
            action.ActionType,
            action.PasteShortcutMode,
            action.LaunchPath ?? string.Empty,
            action.FileActionMode,
            action.LaunchUrl ?? string.Empty,
            action.MediaProvider ?? SnippetMediaProvider.System,
            action.MediaCommand ?? SnippetMediaCommand.PlayPause,
            action.TerminalCommand ?? string.Empty,
            action.TerminalShell ?? SnippetTerminalShell.Cmd,
            action.OpenTerminalWindow,
            action.TerminalWorkingDirectory ?? string.Empty,
            action.AdbDeviceIp ?? string.Empty,
            action.RunAsAdministrator,
            action.SlotImageMode,
            AutoIconCacheEntry.FromHotkeyAction(action));
    }
}
