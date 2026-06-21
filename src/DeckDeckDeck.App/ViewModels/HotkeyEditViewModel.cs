using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.ViewModels;

public sealed class HotkeyEditViewModel : ObservableObject
{
    private readonly Action<HotkeyAction> _afterSave;
    private readonly Action _afterDelete;
    private readonly Action _cancel;
    private readonly Action _notifyHotkeyCaptureStateChanged;
    private readonly DeleteHotkeyActionUseCase _deleteHotkeyActionUseCase;
    private readonly IDialogAdapter _dialogService;
    private readonly EditableImageDraft _imageState;
    private readonly IAppLogger? _loggingService;
    private readonly bool _isSpotifyConnected;
    private readonly SaveHotkeyActionUseCase _saveHotkeyActionUseCase;
    private readonly Guid? _hotkeyActionId;
    private readonly ISnippetImageResolver? _snippetImageService;
    private readonly IStoredImagePathResolver? _storedImagePathResolver;
    private readonly IImageFileRepository? _thumbnailService;
    private readonly Action<string> _showStatus;
    private AutoIconCacheEntry? _autoIcon;
    private SnippetActionType _actionType = SnippetActionType.PasteText;
    private string _content = string.Empty;
    private string _description = string.Empty;
    private string _errorMessage = string.Empty;
    private FileActionMode _fileActionMode = FileActionMode.Launch;
    private HotkeyGesture? _gesture;
    private string _hotkeyTitle = string.Empty;
    private bool _isCapturingHotkey;
    private bool _isEnabled = true;
    private bool _isSaving;
    private string _launchPath = string.Empty;
    private string _launchUrl = string.Empty;
    private SnippetMediaCommand _mediaCommand = SnippetMediaCommand.PlayPause;
    private IReadOnlyList<SnippetMediaCommandOption> _mediaCommandOptions = SnippetMediaCommandOption.SystemCommands;
    private SnippetMediaProvider _mediaProvider = SnippetMediaProvider.System;
    private PasteShortcutMode _pasteShortcutMode = PasteShortcutMode.CtrlV;
    private bool _runAsAdministrator = true;
    private SlotImageMode _slotImageMode = SlotImageMode.Auto;
    private string _terminalCommand = string.Empty;
    private SnippetTerminalShell _terminalShell = SnippetTerminalShell.Cmd;

    public HotkeyEditViewModel(
        HotkeyAction? action,
        HotkeyActionEditorState editorState,
        SaveHotkeyActionUseCase saveHotkeyActionUseCase,
        DeleteHotkeyActionUseCase deleteHotkeyActionUseCase,
        IDialogAdapter dialogService,
        Action cancel,
        Action<HotkeyAction> afterSave,
        Action afterDelete,
        Action notifyHotkeyCaptureStateChanged,
        Action<string> showStatus,
        IImageFileRepository? thumbnailService = null,
        IAppLogger? loggingService = null,
        ISnippetImageResolver? snippetImageService = null,
        IStoredImagePathResolver? storedImagePathResolver = null)
    {
        _hotkeyActionId = action?.Id;
        _saveHotkeyActionUseCase = saveHotkeyActionUseCase;
        _deleteHotkeyActionUseCase = deleteHotkeyActionUseCase;
        _dialogService = dialogService;
        _cancel = cancel;
        _afterSave = afterSave;
        _afterDelete = afterDelete;
        _notifyHotkeyCaptureStateChanged = notifyHotkeyCaptureStateChanged;
        _showStatus = showStatus;
        _thumbnailService = thumbnailService;
        _loggingService = loggingService;
        _snippetImageService = snippetImageService;
        _storedImagePathResolver = storedImagePathResolver;
        _imageState = new EditableImageDraft(action?.ImagePath, action?.ThumbnailPath, thumbnailService);
        _isSpotifyConnected = editorState.SpotifyConnection.IsConnected;

        _hotkeyTitle = action?.Title ?? string.Empty;
        _gesture = action?.Gesture;
        _isEnabled = action?.IsEnabled ?? true;
        _content = action?.Content ?? string.Empty;
        _actionType = action?.ActionType ?? SnippetActionType.PasteText;
        _pasteShortcutMode = action?.PasteShortcutMode ?? PasteShortcutMode.CtrlV;
        _launchPath = action?.LaunchPath ?? string.Empty;
        _fileActionMode = action?.FileActionMode ?? FileActionMode.Launch;
        _launchUrl = action?.LaunchUrl ?? string.Empty;
        _terminalCommand = action?.TerminalCommand ?? string.Empty;
        _terminalShell = action?.TerminalShell ?? SnippetTerminalShell.Cmd;
        _runAsAdministrator = action?.ActionType == SnippetActionType.TerminalCommand
            ? action.RunAsAdministrator
            : true;
        _mediaProvider = action?.MediaProvider ?? SnippetMediaProvider.System;
        _mediaCommandOptions = SnippetMediaCommandOption.ForProvider(_mediaProvider);
        _mediaCommand = SnippetMediaCommandOption.GetValidCommandForProvider(
            _mediaProvider,
            action?.MediaCommand ?? SnippetMediaCommand.PlayPause);
        _slotImageMode = GetInitialSlotImageMode(action);
        _autoIcon = AutoIconCacheEntry.FromHotkeyAction(action);
        _description = action?.Description ?? string.Empty;

        SaveCommand = new RelayCommand(Save, () => !IsSaving);
        CancelCommand = new RelayCommand(Cancel);
        DeleteCommand = new RelayCommand(Delete);
        BeginHotkeyCaptureCommand = new RelayCommand(BeginHotkeyCapture);
        ClearHotkeyCommand = new RelayCommand(ClearHotkey);
        ChooseImageCommand = new RelayCommand(ChooseImage);
        RemoveImageCommand = new RelayCommand(RemoveImage);
        ChooseLaunchFileCommand = new RelayCommand(ChooseLaunchFile);
        ChooseLaunchFolderCommand = new RelayCommand(ChooseLaunchFolder);
    }

    public string Title => IsExisting ? "핫키 편집" : "새 핫키";

    public bool IsExisting => _hotkeyActionId.HasValue;

    public bool CanDelete => IsExisting;

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

    public string HotkeyTitle
    {
        get => _hotkeyTitle;
        set => SetProperty(ref _hotkeyTitle, value);
    }

    public string HotkeyDisplayText => IsCapturingHotkey
        ? "입력 대기 중..."
        : _gesture?.DisplayText ?? "미지정";

    public bool IsCapturingHotkey
    {
        get => _isCapturingHotkey;
        private set
        {
            if (SetProperty(ref _isCapturingHotkey, value))
            {
                OnPropertyChanged(nameof(HotkeyDisplayText));
                _notifyHotkeyCaptureStateChanged();
            }
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public string Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    public PasteShortcutMode PasteShortcutMode
    {
        get => _pasteShortcutMode;
        set => SetProperty(ref _pasteShortcutMode, value);
    }

    public SnippetActionType ActionType
    {
        get => _actionType;
        private set
        {
            if (!SetProperty(ref _actionType, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsPasteTextAction));
            OnPropertyChanged(nameof(IsLaunchFileAction));
            OnPropertyChanged(nameof(IsLaunchUrlAction));
            OnPropertyChanged(nameof(IsMediaAction));
            OnPropertyChanged(nameof(IsTerminalCommandAction));
            NotifyFileActionModeChanged();
            OnPropertyChanged(nameof(ShowSpotifyMediaConnectionNotice));
            NotifyImageChanged();
        }
    }

    public bool IsPasteTextAction
    {
        get => ActionType == SnippetActionType.PasteText;
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
        get => ActionType == SnippetActionType.LaunchFile;
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
        get => ActionType == SnippetActionType.LaunchUrl;
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
        get => ActionType == SnippetActionType.MediaAction;
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
        get => ActionType == SnippetActionType.TerminalCommand;
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
        get => _launchPath;
        set => SetProperty(ref _launchPath, value);
    }

    public FileActionMode SelectedFileActionMode
    {
        get => _fileActionMode;
        set
        {
            if (!SetProperty(ref _fileActionMode, value))
            {
                return;
            }

            NotifyFileActionModeChanged();
            UpdateAutoIconPreview();
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
        get => _launchUrl;
        set => SetProperty(ref _launchUrl, value);
    }

    public SnippetMediaProvider SelectedMediaProvider
    {
        get => _mediaProvider;
        set
        {
            if (!SetProperty(ref _mediaProvider, value))
            {
                return;
            }

            MediaCommandOptions = SnippetMediaCommandOption.ForProvider(value);
            SelectedMediaCommand = SnippetMediaCommandOption.GetValidCommandForProvider(value, SelectedMediaCommand);
            OnPropertyChanged(nameof(IsSpotifyMediaProvider));
            OnPropertyChanged(nameof(ShowSpotifyMediaConnectionNotice));
        }
    }

    public SnippetMediaCommand SelectedMediaCommand
    {
        get => _mediaCommand;
        set
        {
            if (SetProperty(ref _mediaCommand, value))
            {
                NotifyImageChanged();
            }
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
        get => _terminalCommand;
        set => SetProperty(ref _terminalCommand, value);
    }

    public SnippetTerminalShell SelectedTerminalShell
    {
        get => _terminalShell;
        set => SetProperty(ref _terminalShell, value);
    }

    public bool RunAsAdministrator
    {
        get => _runAsAdministrator;
        set => SetProperty(ref _runAsAdministrator, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool IsSaving
    {
        get => _isSaving;
        private set
        {
            if (SetProperty(ref _isSaving, value) && SaveCommand is RelayCommand relayCommand)
            {
                relayCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string? ThumbnailPath => GetPreviewThumbnailPath();

    public bool HasImage => _imageState.HasImage;

    public SlotImageMode SlotImageMode => _slotImageMode;

    public ICommand SaveCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand BeginHotkeyCaptureCommand { get; }

    public ICommand ClearHotkeyCommand { get; }

    public ICommand ChooseImageCommand { get; }

    public ICommand RemoveImageCommand { get; }

    public ICommand ChooseLaunchFileCommand { get; }

    public ICommand ChooseLaunchFolderCommand { get; }

    public void CaptureHotkey(HotkeyGesture gesture)
    {
        if (!IsCapturingHotkey)
        {
            return;
        }

        if (!gesture.IsComplete)
        {
            ErrorMessage = SaveHotkeyActionUseCase.HotkeyModifierOnlyMessage;
            return;
        }

        _gesture = gesture;
        IsCapturingHotkey = false;
        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(HotkeyDisplayText));
    }

    public void CancelHotkeyCapture()
    {
        IsCapturingHotkey = false;
        ErrorMessage = string.Empty;
    }

    public void DropImageFiles(IReadOnlyList<string> sourcePaths)
    {
        if (sourcePaths.Count != 1)
        {
            ErrorMessage = "이미지는 하나만 드롭해 주세요.";
            return;
        }

        ReplaceImageFromPath(sourcePaths[0]);
    }

    private void BeginHotkeyCapture()
    {
        IsCapturingHotkey = true;
        ErrorMessage = "사용할 키를 눌러 주세요. Esc를 누르면 기존 값을 유지합니다.";
    }

    private void ClearHotkey()
    {
        _gesture = null;
        IsCapturingHotkey = false;
        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(HotkeyDisplayText));
    }

    private void Save()
    {
        if (IsSaving)
        {
            return;
        }

        IsCapturingHotkey = false;
        IsSaving = true;
        try
        {
            var autoIcon = PrepareAutoIconForSave();
            var result = _saveHotkeyActionUseCase.Execute(BuildSaveRequest(autoIcon));
            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage ?? string.Empty;
                return;
            }

            if (ActionType == SnippetActionType.LaunchUrl
                && !string.IsNullOrWhiteSpace(result.NormalizedLaunchUrl))
            {
                LaunchUrl = result.NormalizedLaunchUrl;
            }

            var action = result.HotkeyAction!;
            _imageState.DeleteOriginalImageIfReplaced();
            _imageState.MarkCurrentAsOriginal();
            ErrorMessage = string.Empty;
            _showStatus($"{action.Title} 핫키를 저장했습니다.");
            _afterSave(action);
        }
        finally
        {
            IsSaving = false;
        }
    }

    private SaveHotkeyActionRequest BuildSaveRequest(AutoIconCacheEntry? autoIcon)
    {
        return new SaveHotkeyActionRequest(
            _hotkeyActionId,
            HotkeyTitle,
            _gesture,
            IsEnabled,
            Content,
            Description,
            _imageState.ImagePath,
            _imageState.ThumbnailPath,
            ActionType,
            LaunchPath,
            _slotImageMode,
            autoIcon,
            LaunchUrl,
            SelectedMediaProvider,
            SelectedMediaCommand,
            PasteShortcutMode,
            TerminalCommand,
            SelectedTerminalShell,
            RunAsAdministrator,
            SelectedFileActionMode);
    }

    private void Cancel()
    {
        IsCapturingHotkey = false;
        _imageState.DeleteCurrentUnsavedImage();
        _cancel();
    }

    private void Delete()
    {
        if (!_hotkeyActionId.HasValue)
        {
            return;
        }

        var confirmed = _dialogService.Confirm(
            "핫키 삭제",
            "이 핫키를 삭제할까요?");
        if (!confirmed)
        {
            return;
        }

        IsCapturingHotkey = false;
        _imageState.DeleteCurrentUnsavedImage();
        _deleteHotkeyActionUseCase.Execute(_hotkeyActionId.Value);
        _showStatus("핫키를 삭제했습니다.");
        _afterDelete();
    }

    private void ChooseImage()
    {
        if (_thumbnailService is null)
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
            _imageState.ReplaceWithStoredImage(sourcePath);
            _slotImageMode = SlotImageMode.Custom;
            NotifyImageChanged();
            ErrorMessage = string.Empty;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _loggingService?.Log("Image processing failed for hotkey action.", ex);
        }
    }

    private void RemoveImage()
    {
        _imageState.RemoveImage();
        _slotImageMode = SlotImageMode.Auto;
        UpdateAutoIconPreview();
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
        UpdateAutoIconPreview();
        ErrorMessage = string.Empty;
    }

    private void ChooseLaunchFolder()
    {
        var selectedPath = _dialogService.SelectLaunchFolder();
        if (selectedPath is null)
        {
            return;
        }

        LaunchPath = selectedPath;
        _autoIcon = null;
        ErrorMessage = string.Empty;
        NotifyImageChanged();
    }

    private void NotifyImageChanged()
    {
        OnPropertyChanged(nameof(ThumbnailPath));
        OnPropertyChanged(nameof(HasImage));
        OnPropertyChanged(nameof(SlotImageMode));
    }

    private void NotifyFileActionModeChanged()
    {
        OnPropertyChanged(nameof(IsFileLaunchMode));
        OnPropertyChanged(nameof(IsFilePasteMode));
        OnPropertyChanged(nameof(FilePathLabel));
        OnPropertyChanged(nameof(FilePickerButtonText));
    }

    private string? GetPreviewThumbnailPath()
    {
        return _slotImageMode switch
        {
            SlotImageMode.Custom => ResolveDisplayPath(_imageState.ThumbnailPath),
            SlotImageMode.Auto when ActionType == SnippetActionType.LaunchFile =>
                ResolveDisplayPath(_autoIcon?.IconPath),
            SlotImageMode.Auto when ActionType == SnippetActionType.MediaAction =>
                MediaIconResourcePaths.GetIconResourcePath(SelectedMediaCommand),
            _ => null
        };
    }

    private string? ResolveDisplayPath(string? storedPath)
    {
        return _storedImagePathResolver?.ResolveDisplayPath(storedPath) ?? storedPath;
    }

    private AutoIconCacheEntry? PrepareAutoIconForSave()
    {
        if (_slotImageMode == SlotImageMode.None)
        {
            _autoIcon = null;
            return null;
        }

        if (_snippetImageService is null)
        {
            return ActionType == SnippetActionType.LaunchFile ? _autoIcon : null;
        }

        _autoIcon = _snippetImageService.PrepareAutoIcon(ActionType, LaunchPath, _autoIcon);
        NotifyImageChanged();

        return _autoIcon;
    }

    private void UpdateAutoIconPreview()
    {
        if (_slotImageMode == SlotImageMode.None)
        {
            _autoIcon = null;
            NotifyImageChanged();
            return;
        }

        if (_snippetImageService is not null)
        {
            _autoIcon = _snippetImageService.PrepareAutoIcon(ActionType, LaunchPath, _autoIcon);
        }

        NotifyImageChanged();
    }

    private static SlotImageMode GetInitialSlotImageMode(HotkeyAction? action)
    {
        if (action is null)
        {
            return SlotImageMode.Auto;
        }

        return action.SlotImageMode == SlotImageMode.Auto && !string.IsNullOrWhiteSpace(action.ImagePath)
            ? SlotImageMode.Custom
            : action.SlotImageMode;
    }
}
