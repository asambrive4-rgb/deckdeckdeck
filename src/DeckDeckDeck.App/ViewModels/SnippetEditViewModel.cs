using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeckDeckDeck.App.Domain;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Services;
using DeckDeckDeck.App.UseCases;
using DeckDeckDeck.App.UseCases.Ports;

namespace DeckDeckDeck.App.ViewModels;

public sealed class SnippetEditViewModel : ObservableObject
{
    private readonly Action _afterDelete;
    private readonly Action<Snippet> _afterSave;
    private readonly IAutoBackupCoordinator? _autoBackupCoordinator;
    private readonly Action _cancel;
    private readonly DeleteSnippetUseCase _deleteSnippetUseCase;
    private readonly DialogService _dialogService;
    private readonly EditableImageState _imageState;
    private readonly LoggingService? _loggingService;
    private bool _originalIsSlotEnabled;
    private readonly SaveSnippetUseCase _saveSnippetUseCase;
    private readonly ISettingsStore? _settingsStore;
    private readonly Guid? _snippetId;
    private readonly SnippetImageService? _snippetImageService;
    private readonly ISnippetRepository _snippetRepository;
    private readonly Action<string> _showStatus;
    private readonly ThumbnailService? _thumbnailService;
    private readonly TransferSnippetUseCase _transferSnippetUseCase;
    private AutoIconCacheEntry? _autoIcon;
    private SnippetActionType _actionType = SnippetActionType.PasteText;
    private string _content = string.Empty;
    private string _description = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isSlotEnabled;
    private string _launchPath = string.Empty;
    private string _launchUrl = string.Empty;
    private SnippetMediaCommand _mediaCommand = SnippetMediaCommand.PlayPause;
    private IReadOnlyList<SnippetMediaCommandOption> _mediaCommandOptions = SnippetMediaCommandOption.SystemCommands;
    private SnippetMediaProvider _mediaProvider = SnippetMediaProvider.System;
    private SnippetTransferTargetSlot? _selectedTransferTarget;
    private SlotImageMode _slotImageMode = SlotImageMode.Auto;
    private string _snippetTitle = string.Empty;

    public SnippetEditViewModel(
        Category category,
        SlotKey slotKey,
        Snippet? snippet,
        ISnippetRepository snippetRepository,
        SaveSnippetUseCase saveSnippetUseCase,
        DeleteSnippetUseCase deleteSnippetUseCase,
        TransferSnippetUseCase transferSnippetUseCase,
        DialogService dialogService,
        Action cancel,
        Action<Snippet> afterSave,
        Action afterDelete,
        Action<string> showStatus,
        ThumbnailService? thumbnailService = null,
        ISettingsStore? settingsStore = null,
        LoggingService? loggingService = null,
        SnippetImageService? snippetImageService = null,
        IAutoBackupCoordinator? autoBackupCoordinator = null)
    {
        CategoryId = category.Id;
        CategoryName = category.Name;
        SlotKey = slotKey;
        KeyText = slotKey.GetDisplayText();
        _snippetId = snippet?.Id;
        _snippetRepository = snippetRepository;
        _saveSnippetUseCase = saveSnippetUseCase;
        _deleteSnippetUseCase = deleteSnippetUseCase;
        _transferSnippetUseCase = transferSnippetUseCase;
        _dialogService = dialogService;
        _settingsStore = settingsStore;
        _loggingService = loggingService;
        _snippetImageService = snippetImageService;
        _autoBackupCoordinator = autoBackupCoordinator;
        _cancel = cancel;
        _afterSave = afterSave;
        _afterDelete = afterDelete;
        _showStatus = showStatus;
        _thumbnailService = thumbnailService;
        _imageState = new EditableImageState(snippet?.ImagePath, snippet?.ThumbnailPath, thumbnailService);

        _snippetTitle = snippet?.Title ?? string.Empty;
        _content = snippet?.Content ?? string.Empty;
        _actionType = snippet?.ActionType ?? SnippetActionType.PasteText;
        _launchPath = snippet?.LaunchPath ?? string.Empty;
        _launchUrl = snippet?.LaunchUrl ?? string.Empty;
        _mediaProvider = snippet?.MediaProvider ?? SnippetMediaProvider.System;
        _mediaCommandOptions = SnippetMediaCommandOption.ForProvider(_mediaProvider);
        _mediaCommand = SnippetMediaCommandOption.GetValidCommandForProvider(
            _mediaProvider,
            snippet?.MediaCommand ?? SnippetMediaCommand.PlayPause);
        _slotImageMode = GetInitialSlotImageMode(snippet);
        _autoIcon = AutoIconCacheEntry.FromSnippet(snippet);
        _description = snippet?.Description ?? string.Empty;
        TransferTargetSlots = BuildTransferTargetSlots();
        _selectedTransferTarget = TransferTargetSlots.FirstOrDefault();
        _originalIsSlotEnabled = LoadSlotEnabledState();
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
        get => _snippetTitle;
        set => SetProperty(ref _snippetTitle, value);
    }

    public string Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
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

    public string LaunchPath
    {
        get => _launchPath;
        set => SetProperty(ref _launchPath, value);
    }

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
        && !IsSpotifyConnected();

    public string SpotifyMediaConnectionNotice =>
        "Spotify 연결 전에도 저장할 수 있습니다. 실행하려면 설정에서 Spotify를 연결해 주세요.";

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
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

    public string? ThumbnailPath => GetPreviewThumbnailPath();

    public bool HasImage => _imageState.HasImage;

    public SlotImageMode SlotImageMode => _slotImageMode;

    public ICommand SaveCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand CopySnippetCommand { get; }

    public ICommand MoveSnippetCommand { get; }

    public ICommand ChooseImageCommand { get; }

    public ICommand RemoveImageCommand { get; }

    public ICommand ChooseLaunchFileCommand { get; }

    public ICommand ChooseLaunchFolderCommand { get; }

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
        var autoIcon = PrepareAutoIconForSave();
        var result = _saveSnippetUseCase.Execute(BuildSaveRequest(autoIcon), requestAutoBackup);
        if (!result.Succeeded)
        {
            ErrorMessage = result.ErrorMessage ?? string.Empty;
            return null;
        }

        if (result.SavedSlotOnly)
        {
            _originalIsSlotEnabled = IsSlotEnabled;
            _imageState.DeleteCurrentUnsavedImage();
            _showStatus($"슬롯 {KeyText} 설정을 저장했습니다.");
            _cancel();
            return null;
        }

        if (ActionType == SnippetActionType.LaunchUrl
            && !string.IsNullOrWhiteSpace(result.NormalizedLaunchUrl))
        {
            LaunchUrl = result.NormalizedLaunchUrl;
        }

        var snippet = result.Snippet!;
        _imageState.DeleteOriginalImageIfReplaced();
        _imageState.MarkCurrentAsOriginal();
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
            SnippetTitle,
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
            IsSlotEnabled,
            _originalIsSlotEnabled);
    }

    private bool TryPrepareSaveInput(
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out PreparedSnippetSave? input)
    {
        input = null;
        if (string.IsNullOrWhiteSpace(SnippetTitle))
        {
            if (TrySaveSlotOnly())
            {
                return false;
            }

            ErrorMessage = "슬롯 명을 입력해 주세요.";
            return false;
        }

        if (ActionType == SnippetActionType.PasteText && string.IsNullOrWhiteSpace(Content))
        {
            if (TrySaveSlotOnly())
            {
                return false;
            }

            ErrorMessage = "붙여넣을 문구를 입력해 주세요.";
            return false;
        }

        if (ActionType == SnippetActionType.LaunchFile && string.IsNullOrWhiteSpace(LaunchPath))
        {
            ErrorMessage = "실행할 파일, 폴더 또는 바로 가기를 선택해 주세요.";
            return false;
        }

        var launchUrl = LaunchUrl;
        if (ActionType == SnippetActionType.LaunchUrl)
        {
            if (!UrlAddress.TryNormalize(LaunchUrl, out launchUrl))
            {
                ErrorMessage = "열 웹페이지 주소를 http 또는 https 주소로 입력해 주세요.";
                return false;
            }

            LaunchUrl = launchUrl;
        }

        var mediaProvider = ActionType == SnippetActionType.MediaAction
            ? SelectedMediaProvider
            : (SnippetMediaProvider?)null;
        var mediaCommand = ActionType == SnippetActionType.MediaAction
            ? SelectedMediaCommand
            : (SnippetMediaCommand?)null;
        input = new PreparedSnippetSave(
            SnippetTitle,
            Content,
            Description,
            _imageState.ImagePath,
            _imageState.ThumbnailPath,
            ActionType,
            LaunchPath,
            _slotImageMode,
            launchUrl,
            mediaProvider,
            mediaCommand);

        return true;
    }

    private Snippet SaveSnippetRecord(PreparedSnippetSave input, AutoIconCacheEntry? autoIcon)
    {
        return _snippetId.HasValue
            ? _snippetRepository.Update(
                _snippetId.Value,
                input.Title,
                input.Content,
                input.Description,
                input.ImagePath,
                input.ThumbnailPath,
                input.ActionType,
                input.LaunchPath,
                input.SlotImageMode,
                autoIcon,
                input.LaunchUrl,
                input.MediaProvider,
                input.MediaCommand)
            : _snippetRepository.Create(
                CategoryId,
                SlotKey,
                input.Title,
                input.Content,
                input.Description,
                input.ImagePath,
                input.ThumbnailPath,
                input.ActionType,
                input.LaunchPath,
                input.SlotImageMode,
                autoIcon,
                input.LaunchUrl,
                input.MediaProvider,
                input.MediaCommand);
    }

    private void Cancel()
    {
        _imageState.DeleteCurrentUnsavedImage();
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

        _imageState.DeleteCurrentUnsavedImage();
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
            var autoIcon = PrepareAutoIconForSave();
            var result = ExecuteTransferSnippet(
                operation,
                targetSlotKey,
                autoIcon,
                overwriteConfirmed: false);
            if (result.NeedsOverwriteConfirmation)
            {
                var confirmed = _dialogService.Confirm(
                    $"?ㅽ뻾 ??ぉ {actionText}",
                    $"?щ’ {targetSlotKey.GetDisplayText()}???대? '{result.ExistingTargetTitle}' ?ㅽ뻾 ??ぉ???덉뒿?덈떎.\n湲곗〈 ?ㅽ뻾 ??ぉ????뼱?멸퉴??");
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
                LaunchUrl = result.NormalizedLaunchUrl;
            }

            var transferredSnippet = result.Snippet!;
            _imageState.DeleteOriginalImageIfReplaced();
            _imageState.MarkCurrentAsOriginal();
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

    private bool ConfirmOverwriteIfNeeded(SlotKey targetSlotKey, string actionText)
    {
        var targetSnippet = _snippetRepository
            .GetByCategoryId(CategoryId)
            .FirstOrDefault(snippet => snippet.SlotKey == targetSlotKey);
        if (targetSnippet is null || targetSnippet.Id == _snippetId)
        {
            return true;
        }

        return _dialogService.Confirm(
            $"실행 항목 {actionText}",
            $"슬롯 {targetSlotKey.GetDisplayText()}에 이미 '{targetSnippet.Title}' 실행 항목이 있습니다.\n기존 실행 항목을 덮어쓸까요?");
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
            _loggingService?.Log($"Image processing failed for snippet slot {SlotKey}.", ex);
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
        var selectedPath = _dialogService.SelectLaunchFile();
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

    private string? GetPreviewThumbnailPath()
    {
        return _slotImageMode switch
        {
            SlotImageMode.Custom => _imageState.ThumbnailPath,
            SlotImageMode.Auto when ActionType == SnippetActionType.LaunchFile => _autoIcon?.IconPath,
            SlotImageMode.Auto when ActionType == SnippetActionType.MediaAction =>
                MediaIconResources.GetIconResourcePath(SelectedMediaCommand),
            _ => null
        };
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

    private static SlotImageMode GetInitialSlotImageMode(Snippet? snippet)
    {
        if (snippet is null)
        {
            return SlotImageMode.Auto;
        }

        return snippet.SlotImageMode == SlotImageMode.Auto && !string.IsNullOrWhiteSpace(snippet.ImagePath)
            ? SlotImageMode.Custom
            : snippet.SlotImageMode;
    }

    private IReadOnlyList<SnippetTransferTargetSlot> BuildTransferTargetSlots()
    {
        var snippetsBySlot = _snippetRepository
            .GetByCategoryId(CategoryId)
            .Where(snippet => !_snippetId.HasValue || snippet.Id != _snippetId.Value)
            .ToDictionary(snippet => snippet.SlotKey);

        return SlotKeyCatalog.All
            .Where(slotKey => slotKey != SlotKey)
            .Select(slotKey =>
            {
                snippetsBySlot.TryGetValue(slotKey, out var snippet);

                return new SnippetTransferTargetSlot(
                    slotKey,
                    FormatTransferTargetLabel(slotKey, snippet));
            })
            .ToList();
    }

    private static string FormatTransferTargetLabel(SlotKey slotKey, Snippet? snippet)
    {
        var prefix = $"슬롯 {slotKey.GetDisplayText()}";

        return snippet is null
            ? $"{prefix} - 비어 있음"
            : $"{prefix} - {snippet.Title}";
    }

    private bool LoadSlotEnabledState()
    {
        var settings = _settingsStore?.Load();

        return settings is null
            || !settings.EnabledSnippetSlotKeys.TryGetValue(SlotKey, out var enabled)
            || enabled;
    }

    private bool TrySaveSlotOnly()
    {
        if (IsExisting || IsSlotEnabled == _originalIsSlotEnabled)
        {
            return false;
        }

        if (!SaveSlotEnabled())
        {
            return true;
        }

        _imageState.DeleteCurrentUnsavedImage();
        _autoBackupCoordinator?.RequestAutoBackup();
        _showStatus($"슬롯 {KeyText} 설정을 저장했습니다.");
        _cancel();

        return true;
    }

    private bool SaveSlotEnabled()
    {
        if (_settingsStore is null || IsSlotEnabled == _originalIsSlotEnabled)
        {
            return true;
        }

        try
        {
            _settingsStore.SetSnippetSlotEnabled(SlotKey, IsSlotEnabled);
            _originalIsSlotEnabled = IsSlotEnabled;

            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = "슬롯 설정을 저장하지 못했습니다.";
            _loggingService?.Log($"Setting save failed for snippet slot {SlotKey}.", ex);

            return false;
        }
    }

    private bool IsSpotifyConnected()
    {
        var settings = _settingsStore?.Load();

        return settings is not null
            && !string.IsNullOrWhiteSpace(settings.SpotifyClientId)
            && !string.IsNullOrWhiteSpace(settings.SpotifyAccessToken)
            && !string.IsNullOrWhiteSpace(settings.SpotifyRefreshToken);
    }

    private sealed record PreparedSnippetSave(
        string Title,
        string Content,
        string Description,
        string? ImagePath,
        string? ThumbnailPath,
        SnippetActionType ActionType,
        string LaunchPath,
        SlotImageMode SlotImageMode,
        string? LaunchUrl,
        SnippetMediaProvider? MediaProvider,
        SnippetMediaCommand? MediaCommand);
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
