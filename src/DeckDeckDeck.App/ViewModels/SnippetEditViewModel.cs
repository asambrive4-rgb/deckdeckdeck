using System.Windows.Input;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases.Ports;
using DeckDeckDeck.App.UseCases;

namespace DeckDeckDeck.App.ViewModels;

public sealed class SnippetEditViewModel : ObservableObject
{
    private readonly Action _afterDelete;
    private readonly Action<Snippet> _afterSave;
    private readonly Action _cancel;
    private readonly DeleteSnippetUseCase _deleteSnippetUseCase;
    private readonly IDialogAdapter _dialogService;
    private readonly ExecutableActionEditDraft _draft;
    private readonly IAppLogger? _loggingService;
    private readonly bool _isSpotifyConnected;
    private bool _originalIsSlotEnabled;
    private readonly SaveSnippetUseCase _saveSnippetUseCase;
    private readonly Guid? _snippetId;
    private readonly Action<string> _showStatus;
    private readonly TransferSnippetUseCase _transferSnippetUseCase;
    private string _errorMessage = string.Empty;
    private bool _isAdbPairingEnabled;
    private bool _isSlotEnabled;
    private IReadOnlyList<SnippetMediaCommandOption> _mediaCommandOptions = SnippetMediaCommandOption.SystemCommands;
    private SnippetTransferTargetSlot? _selectedTransferTarget;

    public SnippetEditViewModel(
        Category category,
        SlotKey slotKey,
        Snippet? snippet,
        SnippetEditorState editorState,
        SaveSnippetUseCase saveSnippetUseCase,
        DeleteSnippetUseCase deleteSnippetUseCase,
        TransferSnippetUseCase transferSnippetUseCase,
        IDialogAdapter dialogService,
        Action cancel,
        Action<Snippet> afterSave,
        Action afterDelete,
        Action<string> showStatus,
        IImageFileRepository? thumbnailService = null,
        IAppLogger? loggingService = null,
        ISnippetImageResolver? snippetImageService = null,
        IStoredImagePathResolver? storedImagePathResolver = null)
    {
        CategoryId = category.Id;
        CategoryName = category.Name;
        SlotKey = slotKey;
        KeyText = slotKey.GetDisplayText();
        _snippetId = snippet?.Id;
        _saveSnippetUseCase = saveSnippetUseCase;
        _deleteSnippetUseCase = deleteSnippetUseCase;
        _transferSnippetUseCase = transferSnippetUseCase;
        _dialogService = dialogService;
        _loggingService = loggingService;
        _cancel = cancel;
        _afterSave = afterSave;
        _afterDelete = afterDelete;
        _showStatus = showStatus;
        _draft = ExecutableActionEditDraft.FromSnippet(
            snippet,
            thumbnailService,
            snippetImageService,
            storedImagePathResolver);
        _isSpotifyConnected = editorState.SpotifyConnection.IsConnected;

        _mediaCommandOptions = SnippetMediaCommandOption.ForProvider(_draft.MediaProvider);
        TransferTargetSlots = BuildTransferTargetSlots(editorState.TransferTargets);
        _selectedTransferTarget = TransferTargetSlots.FirstOrDefault();
        _originalIsSlotEnabled = editorState.IsSlotEnabled;
        _isSlotEnabled = _originalIsSlotEnabled;
        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(Cancel);
        DeleteCommand = new RelayCommand(Delete);
        CopySnippetCommand = new RelayCommand(CopySnippet);
        MoveSnippetCommand = new RelayCommand(MoveSnippet);
        ChooseImageCommand = new RelayCommand(ChooseImage);
        RemoveImageCommand = new RelayCommand(RemoveImage);
        ChooseLaunchFileCommand = new RelayCommand(ChooseLaunchFile);
        ChooseLaunchFolderCommand = new RelayCommand(ChooseLaunchFolder);
        ChooseTerminalWorkingDirectoryCommand = new RelayCommand(ChooseTerminalWorkingDirectory);
        _isAdbPairingEnabled =
            TerminalCommandParameterRules.IsAdbWirelessConnectCommand(_draft.TerminalCommand);
    }

    public string Title => IsExisting ? "실행 항목 편집" : "새 실행 항목";

    public Guid CategoryId { get; }

    public string CategoryName { get; }

    public SlotKey SlotKey { get; }

    public string KeyText { get; }

    public bool IsExisting => _snippetId.HasValue;

    public bool CanDelete => IsExisting;

    public bool CanTransfer => IsExisting;

    public IReadOnlyList<SnippetTransferTargetSlot> TransferTargetSlots { get; }

    public IReadOnlyList<SnippetMediaProviderOption> MediaProviderOptions { get; } =
        SnippetMediaProviderOption.All;

    public IReadOnlyList<PasteShortcutModeOption> PasteShortcutModeOptions { get; } =
        PasteShortcutModeOption.All;

    public IReadOnlyList<FileActionModeOption> FileActionModeOptions { get; } =
        FileActionModeOption.All;

    public IReadOnlyList<TerminalShellOption> TerminalShellOptions { get; } =
        TerminalShellOption.All;

    public IReadOnlyList<SnippetMediaCommandOption> MediaCommandOptions
    {
        get => _mediaCommandOptions;
        private set => SetProperty(ref _mediaCommandOptions, value);
    }

    public SnippetTransferTargetSlot? SelectedTransferTarget
    {
        get => _selectedTransferTarget;
        set => SetProperty(ref _selectedTransferTarget, value);
    }

    public string SnippetTitle
    {
        get => _draft.Title;
        set => SetDraftValue(_draft.Title, value, static (draft, newValue) => draft.Title = newValue);
    }

    public string Content
    {
        get => _draft.Content;
        set => SetDraftValue(_draft.Content, value, static (draft, newValue) => draft.Content = newValue);
    }

    public PasteShortcutMode PasteShortcutMode
    {
        get => _draft.PasteShortcutMode;
        set => SetDraftValue(
            _draft.PasteShortcutMode,
            value,
            static (draft, newValue) => draft.PasteShortcutMode = newValue);
    }

    public SnippetActionType ActionType
    {
        get => _draft.ActionType;
        private set
        {
            if (_draft.ActionType == value)
            {
                return;
            }

            _draft.SetActionType(value);
            OnPropertyChanged();
            NotifyActionTypePresentationChanged();
            NotifyFileActionModeChanged();
            OnPropertyChanged(nameof(ShowSpotifyMediaConnectionNotice));
            NotifyImageChanged();
        }
    }

    public ActionEditorPanel EditorPanel =>
        ExecutableActionTypeCatalog.GetEditorPanel(ActionType);

    public bool IsPasteTextAction
    {
        get => ExecutableActionTypeCatalog.IsEditorPanel(ActionType, ActionEditorPanel.PasteText);
        set
        {
            if (value)
            {
                ActionType = SnippetActionType.PasteText;
            }
        }
    }

    public bool IsLaunchFileAction
    {
        get => ExecutableActionTypeCatalog.IsEditorPanel(ActionType, ActionEditorPanel.LaunchFile);
        set
        {
            if (value)
            {
                ActionType = SnippetActionType.LaunchFile;
            }
        }
    }

    public bool IsLaunchUrlAction
    {
        get => ExecutableActionTypeCatalog.IsEditorPanel(ActionType, ActionEditorPanel.LaunchUrl);
        set
        {
            if (value)
            {
                ActionType = SnippetActionType.LaunchUrl;
            }
        }
    }

    public bool IsMediaAction
    {
        get => ExecutableActionTypeCatalog.IsEditorPanel(ActionType, ActionEditorPanel.Media);
        set
        {
            if (value)
            {
                ActionType = SnippetActionType.MediaAction;
            }
        }
    }

    public bool IsTerminalCommandAction
    {
        get => ExecutableActionTypeCatalog.IsEditorPanel(
            ActionType,
            ActionEditorPanel.TerminalCommand);
        set
        {
            if (value)
            {
                ActionType = SnippetActionType.TerminalCommand;
            }
        }
    }

    public string LaunchPath
    {
        get => _draft.LaunchPath;
        set
        {
            if (_draft.LaunchPath == value)
            {
                return;
            }

            _draft.SetLaunchPath(value);
            OnPropertyChanged();
            NotifyImageChanged();
        }
    }

    public FileActionMode SelectedFileActionMode
    {
        get => _draft.FileActionMode;
        set
        {
            if (_draft.FileActionMode == value)
            {
                return;
            }

            _draft.SetFileActionMode(value);
            OnPropertyChanged();
            NotifyFileActionModeChanged();
            NotifyImageChanged();
        }
    }

    public bool IsFileLaunchMode =>
        IsLaunchFileAction && SelectedFileActionMode == FileActionMode.Launch;

    public bool IsFilePasteMode =>
        IsLaunchFileAction && SelectedFileActionMode == FileActionMode.Paste;

    public string FilePathLabel => IsFilePasteMode
        ? "붙여넣을 파일"
        : "실행할 파일, 폴더 또는 바로 가기";

    public string FilePickerButtonText => IsFilePasteMode
        ? "파일 선택"
        : "파일/바로 가기 선택";

    public string LaunchUrl
    {
        get => _draft.LaunchUrl;
        set => SetDraftValue(_draft.LaunchUrl, value, static (draft, newValue) => draft.SetLaunchUrl(newValue));
    }

    public SnippetMediaProvider SelectedMediaProvider
    {
        get => _draft.MediaProvider;
        set
        {
            if (_draft.MediaProvider == value)
            {
                return;
            }

            _draft.SetMediaProvider(value);
            OnPropertyChanged();
            MediaCommandOptions = SnippetMediaCommandOption.ForProvider(value);
            OnPropertyChanged(nameof(SelectedMediaCommand));
            OnPropertyChanged(nameof(IsSpotifyMediaProvider));
            OnPropertyChanged(nameof(ShowSpotifyMediaConnectionNotice));
            NotifyImageChanged();
        }
    }

    public SnippetMediaCommand SelectedMediaCommand
    {
        get => _draft.MediaCommand;
        set
        {
            if (_draft.MediaCommand == value)
            {
                return;
            }

            _draft.SetMediaCommand(value);
            OnPropertyChanged();
            NotifyImageChanged();
        }
    }

    public bool IsSpotifyMediaProvider => SelectedMediaProvider == SnippetMediaProvider.Spotify;

    public bool ShowSpotifyMediaConnectionNotice =>
        IsMediaAction
        && IsSpotifyMediaProvider
        && !_isSpotifyConnected;

    public string SpotifyMediaConnectionNotice =>
        "Spotify 연결 전에도 저장할 수 있습니다. 실행하려면 설정에서 Spotify를 연결해 주세요.";

    public string TerminalCommand
    {
        get => _draft.TerminalCommand;
        set
        {
            if (!SetDraftValue(
                    _draft.TerminalCommand,
                    value,
                    static (draft, newValue) => draft.TerminalCommand = newValue))
            {
                return;
            }

            var isAdb = TerminalCommandParameterRules.IsAdbWirelessConnectCommand(value);
            if (_isAdbPairingEnabled != isAdb)
            {
                _isAdbPairingEnabled = isAdb;
                OnPropertyChanged(nameof(IsAdbPairingEnabled));
            }
        }
    }

    public SnippetTerminalShell SelectedTerminalShell
    {
        get => _draft.TerminalShell;
        set => SetDraftValue(
            _draft.TerminalShell,
            value,
            static (draft, newValue) => draft.TerminalShell = newValue);
    }

    public bool RunAsAdministrator
    {
        get => _draft.RunAsAdministrator;
        set => SetDraftValue(
            _draft.RunAsAdministrator,
            value,
            static (draft, newValue) => draft.RunAsAdministrator = newValue);
    }

    public bool OpenTerminalWindow
    {
        get => _draft.OpenTerminalWindow;
        set => SetDraftValue(
            _draft.OpenTerminalWindow,
            value,
            static (draft, newValue) => draft.OpenTerminalWindow = newValue);
    }

    public bool IsAdbPairingEnabled
    {
        get => _isAdbPairingEnabled;
        set
        {
            if (_isAdbPairingEnabled == value)
            {
                return;
            }

            _isAdbPairingEnabled = value;
            OnPropertyChanged();
            if (value)
            {
                ApplyAdbPairingDefaults();
            }
        }
    }

    public string TerminalWorkingDirectory
    {
        get => _draft.TerminalWorkingDirectory;
        set => SetDraftValue(
            _draft.TerminalWorkingDirectory,
            value,
            static (draft, newValue) => draft.TerminalWorkingDirectory = newValue);
    }

    public string AdbDeviceIp
    {
        get => _draft.AdbDeviceIp;
        set => SetDraftValue(
            _draft.AdbDeviceIp,
            value,
            static (draft, newValue) => draft.AdbDeviceIp = newValue);
    }

    public string Description
    {
        get => _draft.Description;
        set => SetDraftValue(_draft.Description, value, static (draft, newValue) => draft.Description = newValue);
    }

    public bool IsSlotEnabled
    {
        get => _isSlotEnabled;
        set => SetProperty(ref _isSlotEnabled, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public string? ThumbnailPath => _draft.GetPreviewThumbnailPath();

    public bool HasImage => _draft.HasImage;

    public SlotImageMode SlotImageMode => _draft.SlotImageMode;

    public ICommand SaveCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand CopySnippetCommand { get; }

    public ICommand MoveSnippetCommand { get; }

    public ICommand ChooseImageCommand { get; }

    public ICommand RemoveImageCommand { get; }

    public ICommand ChooseLaunchFileCommand { get; }

    public ICommand ChooseLaunchFolderCommand { get; }

    public ICommand ChooseTerminalWorkingDirectoryCommand { get; }

    public void DropImageFiles(IReadOnlyList<string> sourcePaths)
    {
        if (sourcePaths.Count != 1)
        {
            ErrorMessage = "이미지는 하나만 드롭해 주세요.";
            return;
        }

        ReplaceImageFromPath(sourcePaths[0]);
    }

    private void Save()
    {
        var snippet = SaveSnippet();
        if (snippet is null)
        {
            return;
        }

        _showStatus($"{snippet.Title} 저장됨.");
        _afterSave(snippet);
    }

    private Snippet? SaveSnippet(bool requestAutoBackup = true)
    {
        var autoIcon = _draft.PrepareAutoIconForSave();
        NotifyImageChanged();
        var result = _saveSnippetUseCase.Execute(BuildSaveRequest(autoIcon), requestAutoBackup);
        if (!result.Succeeded)
        {
            ErrorMessage = result.ErrorMessage ?? string.Empty;
            return null;
        }

        if (result.SavedSlotOnly)
        {
            _originalIsSlotEnabled = IsSlotEnabled;
            _draft.DeleteCurrentUnsavedImage();
            _showStatus($"슬롯 {KeyText} 설정을 저장했습니다.");
            // afterDelete navigates back with cache invalidation (slot enablement changed).
            _afterDelete();
            return null;
        }

        if (ActionType == SnippetActionType.LaunchUrl
            && !string.IsNullOrWhiteSpace(result.NormalizedLaunchUrl))
        {
            ApplyNormalizedLaunchUrl(result.NormalizedLaunchUrl);
        }

        var snippet = result.Snippet!;
        _draft.MarkSaved();
        _originalIsSlotEnabled = IsSlotEnabled;
        ErrorMessage = string.Empty;

        return snippet;
    }

    private SaveSnippetRequest BuildSaveRequest(AutoIconCacheEntry? autoIcon)
    {
        return new SaveSnippetRequest(
            CategoryId,
            SlotKey,
            _snippetId,
            IsSlotEnabled,
            _originalIsSlotEnabled,
            _draft.ToSnippetSaveData(autoIcon));
    }

    private void Cancel()
    {
        _draft.DeleteCurrentUnsavedImage();
        _cancel();
    }

    private void Delete()
    {
        if (!_snippetId.HasValue)
        {
            return;
        }

        var confirmed = _dialogService.Confirm(
            "실행 항목 삭제",
            "이 실행 항목을 삭제할까요?");

        if (!confirmed)
        {
            return;
        }

        _draft.DeleteCurrentUnsavedImage();
        _deleteSnippetUseCase.Execute(_snippetId.Value);
        _showStatus("실행 항목을 삭제했습니다.");
        _afterDelete();
    }

    private void CopySnippet()
    {
        TransferSnippet(
            "복사",
            SnippetTransferOperation.Copy,
            targetSlotKey => $"슬롯 {targetSlotKey.GetDisplayText()}에 실행 항목을 복사했습니다.");
    }

    private void MoveSnippet()
    {
        TransferSnippet(
            "이동",
            SnippetTransferOperation.Move,
            targetSlotKey => $"슬롯 {targetSlotKey.GetDisplayText()}로 실행 항목을 이동했습니다.");
    }

    private void TransferSnippet(
        string actionText,
        SnippetTransferOperation operation,
        Func<SlotKey, string> getStatusMessage)
    {
        if (!_snippetId.HasValue)
        {
            ErrorMessage = "저장된 실행 항목만 복사하거나 이동할 수 있습니다.";
            return;
        }

        if (SelectedTransferTarget is null)
        {
            ErrorMessage = "대상 슬롯을 선택해 주세요.";
            return;
        }

        var targetSlotKey = SelectedTransferTarget.SlotKey;
        try
        {
            var autoIcon = _draft.PrepareAutoIconForSave();
            NotifyImageChanged();
            var result = ExecuteTransferSnippet(
                operation,
                targetSlotKey,
                autoIcon,
                overwriteConfirmed: false);
            if (result.NeedsOverwriteConfirmation)
            {
                var confirmed = _dialogService.Confirm(
                    $"실행 항목 {actionText}",
                    $"슬롯 {targetSlotKey.GetDisplayText()}에 이미 '{result.ExistingTargetTitle}' 실행 항목이 있습니다.\n기존 실행 항목을 덮어쓸까요?");
                if (!confirmed)
                {
                    return;
                }

                result = ExecuteTransferSnippet(
                    operation,
                    targetSlotKey,
                    autoIcon,
                    overwriteConfirmed: true);
            }

            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage ?? string.Empty;
                return;
            }

            if (ActionType == SnippetActionType.LaunchUrl
                && !string.IsNullOrWhiteSpace(result.NormalizedLaunchUrl))
            {
                ApplyNormalizedLaunchUrl(result.NormalizedLaunchUrl);
            }

            var transferredSnippet = result.Snippet!;
            _draft.MarkSaved();
            _originalIsSlotEnabled = IsSlotEnabled;
            _afterSave(transferredSnippet);
            _showStatus(getStatusMessage(targetSlotKey));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"실행 항목 {actionText}에 실패했습니다.";
            _loggingService?.Log($"Snippet {actionText} failed for slot {SlotKey}.", ex);
        }
    }

    private TransferSnippetResult ExecuteTransferSnippet(
        SnippetTransferOperation operation,
        SlotKey targetSlotKey,
        AutoIconCacheEntry? autoIcon,
        bool overwriteConfirmed)
    {
        return _transferSnippetUseCase.Execute(new TransferSnippetRequest(
            CategoryId,
            _snippetId,
            SlotKey,
            targetSlotKey,
            IsSlotEnabled,
            operation,
            BuildSaveRequest(autoIcon),
            overwriteConfirmed));
    }

    private void ChooseImage()
    {
        if (!_draft.CanStoreImages)
        {
            ErrorMessage = "이미지 저장소가 준비되지 않았습니다.";
            return;
        }

        var selectedPath = _dialogService.SelectImageFile();
        if (selectedPath is null)
        {
            return;
        }

        ReplaceImageFromPath(selectedPath);
    }

    private void ReplaceImageFromPath(string sourcePath)
    {
        try
        {
            _draft.ReplaceImageFromPath(sourcePath);
            NotifyImageChanged();
            ErrorMessage = string.Empty;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _loggingService?.Log($"Image processing failed for snippet slot {SlotKey}.", ex);
        }
    }

    private void RemoveImage()
    {
        _draft.RemoveImage();
        NotifyImageChanged();
    }

    private void ChooseLaunchFile()
    {
        var selectedPath = IsFilePasteMode
            ? _dialogService.SelectPasteFile()
            : _dialogService.SelectLaunchFile();
        if (selectedPath is null)
        {
            return;
        }

        LaunchPath = selectedPath;
        ErrorMessage = string.Empty;
    }

    private void ChooseLaunchFolder()
    {
        var selectedPath = _dialogService.SelectLaunchFolder();
        if (selectedPath is null)
        {
            return;
        }

        _draft.SetLaunchFolderPath(selectedPath);
        OnPropertyChanged(nameof(LaunchPath));
        ErrorMessage = string.Empty;
        NotifyImageChanged();
    }

    private void ChooseTerminalWorkingDirectory()
    {
        var selectedPath = _dialogService.SelectLaunchFolder();
        if (selectedPath is null)
        {
            return;
        }

        TerminalWorkingDirectory = selectedPath;
        ErrorMessage = string.Empty;
    }

    private void ApplyAdbPairingDefaults()
    {
        TerminalCommand = TerminalCommandParameterRules.AdbWirelessPowerShellExample;
        SelectedTerminalShell = SnippetTerminalShell.PowerShell;
        OpenTerminalWindow = true;
        RunAsAdministrator = false;
        ErrorMessage = string.Empty;
    }

    private void NotifyImageChanged()
    {
        OnPropertyChanged(nameof(ThumbnailPath));
        OnPropertyChanged(nameof(HasImage));
        OnPropertyChanged(nameof(SlotImageMode));
    }

    private void NotifyActionTypePresentationChanged()
    {
        OnPropertyChanged(nameof(EditorPanel));
        OnPropertyChanged(nameof(IsPasteTextAction));
        OnPropertyChanged(nameof(IsLaunchFileAction));
        OnPropertyChanged(nameof(IsLaunchUrlAction));
        OnPropertyChanged(nameof(IsMediaAction));
        OnPropertyChanged(nameof(IsTerminalCommandAction));
    }

    private void NotifyFileActionModeChanged()
    {
        OnPropertyChanged(nameof(IsFileLaunchMode));
        OnPropertyChanged(nameof(IsFilePasteMode));
        OnPropertyChanged(nameof(FilePathLabel));
        OnPropertyChanged(nameof(FilePickerButtonText));
    }

    private bool SetDraftValue<T>(
        T currentValue,
        T newValue,
        Action<ExecutableActionEditDraft, T> apply,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(currentValue, newValue))
        {
            return false;
        }

        apply(_draft, newValue);
        OnPropertyChanged(propertyName);
        return true;
    }

    private void ApplyNormalizedLaunchUrl(string? normalizedLaunchUrl)
    {
        var previousLaunchUrl = LaunchUrl;
        _draft.ApplyNormalizedLaunchUrl(normalizedLaunchUrl);
        if (previousLaunchUrl != LaunchUrl)
        {
            OnPropertyChanged(nameof(LaunchUrl));
        }
    }

    private static IReadOnlyList<SnippetTransferTargetSlot> BuildTransferTargetSlots(
        IReadOnlyList<TransferTargetState> transferTargets)
    {
        return transferTargets
            .Select(target => new SnippetTransferTargetSlot(
                target.SlotKey,
                FormatTransferTargetLabel(target)))
            .ToList();
    }

    private static string FormatTransferTargetLabel(TransferTargetState target)
    {
        var prefix = $"슬롯 {target.SlotKey.GetDisplayText()}";

        return string.IsNullOrWhiteSpace(target.ExistingTitle)
            ? $"{prefix} - 비어 있음"
            : $"{prefix} - {target.ExistingTitle}";
    }
}

public sealed class SnippetTransferTargetSlot
{
    public SnippetTransferTargetSlot(SlotKey slotKey, string label)
    {
        SlotKey = slotKey;
        Label = label;
    }

    public SlotKey SlotKey { get; }

    public string Label { get; }
}

public sealed class SnippetMediaCommandOption
{
    public static IReadOnlyList<SnippetMediaCommandOption> SystemCommands { get; } =
    [
        new(SnippetMediaCommand.PlayPause, "재생/일시정지"),
        new(SnippetMediaCommand.PreviousTrack, "이전 곡"),
        new(SnippetMediaCommand.NextTrack, "다음 곡"),
        new(SnippetMediaCommand.Stop, "정지"),
        new(SnippetMediaCommand.Mute, "음소거"),
        new(SnippetMediaCommand.VolumeUp, "볼륨 증가"),
        new(SnippetMediaCommand.VolumeDown, "볼륨 감소")
    ];

    public static IReadOnlyList<SnippetMediaCommandOption> SpotifyCommands { get; } =
    [
        new(SnippetMediaCommand.PlayPause, "재생/일시정지"),
        new(SnippetMediaCommand.PreviousTrack, "이전 곡"),
        new(SnippetMediaCommand.NextTrack, "다음 곡"),
        new(SnippetMediaCommand.ToggleShuffle, "셔플 켜기/끄기"),
        new(SnippetMediaCommand.CycleRepeat, "반복 모드 변경"),
        new(SnippetMediaCommand.OpenSpotifyAndResume, "Spotify 앱 열고 재생 시도")
    ];

    public static IReadOnlyList<SnippetMediaCommandOption> All { get; } =
        SystemCommands.Concat(SpotifyCommands)
            .GroupBy(option => option.Command)
            .Select(group => group.First())
            .ToList();

    public static IReadOnlyList<SnippetMediaCommandOption> ForProvider(SnippetMediaProvider provider)
    {
        return provider == SnippetMediaProvider.Spotify
            ? SpotifyCommands
            : SystemCommands;
    }

    public static SnippetMediaCommand GetValidCommandForProvider(
        SnippetMediaProvider provider,
        SnippetMediaCommand command)
    {
        return MediaCommandRules.GetValidCommandForProvider(provider, command);
    }

    public SnippetMediaCommandOption(SnippetMediaCommand command, string label)
    {
        Command = command;
        Label = label;
    }

    public SnippetMediaCommand Command { get; }

    public string Label { get; }
}

public sealed class PasteShortcutModeOption
{
    public static IReadOnlyList<PasteShortcutModeOption> All { get; } =
    [
        new(PasteShortcutMode.CtrlV, "기본 붙여넣기 (Ctrl+V)"),
        new(PasteShortcutMode.CtrlShiftV, "터미널 붙여넣기 (Ctrl+Shift+V)")
    ];

    public PasteShortcutModeOption(PasteShortcutMode mode, string label)
    {
        Mode = mode;
        Label = label;
    }

    public PasteShortcutMode Mode { get; }

    public string Label { get; }
}

public sealed class FileActionModeOption
{
    public static IReadOnlyList<FileActionModeOption> All { get; } =
    [
        new(FileActionMode.Launch, "실행"),
        new(FileActionMode.Paste, "파일 붙여넣기")
    ];

    public FileActionModeOption(FileActionMode mode, string label)
    {
        Mode = mode;
        Label = label;
    }

    public FileActionMode Mode { get; }

    public string Label { get; }
}

public sealed class TerminalShellOption
{
    public static IReadOnlyList<TerminalShellOption> All { get; } =
    [
        new(SnippetTerminalShell.Cmd, "cmd"),
        new(SnippetTerminalShell.PowerShell, "PowerShell")
    ];

    public TerminalShellOption(SnippetTerminalShell shell, string label)
    {
        Shell = shell;
        Label = label;
    }

    public SnippetTerminalShell Shell { get; }

    public string Label { get; }
}

public sealed class SnippetMediaProviderOption
{
    public static IReadOnlyList<SnippetMediaProviderOption> All { get; } =
    [
        new(SnippetMediaProvider.System, "Windows 기본 미디어 제어"),
        new(SnippetMediaProvider.Spotify, "Spotify")
    ];

    public SnippetMediaProviderOption(SnippetMediaProvider provider, string label)
    {
        Provider = provider;
        Label = label;
    }

    public SnippetMediaProvider Provider { get; }

    public string Label { get; }
}

