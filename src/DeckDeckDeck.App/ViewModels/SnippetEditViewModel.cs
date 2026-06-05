using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeckDeckDeck.App.Models;
using DeckDeckDeck.App.Services;

namespace DeckDeckDeck.App.ViewModels;

public sealed class SnippetEditViewModel : ObservableObject
{
    private readonly Action _afterDelete;
    private readonly Action<Snippet> _afterSave;
    private readonly IAutoBackupCoordinator? _autoBackupCoordinator;
    private readonly Action _cancel;
    private readonly DialogService _dialogService;
    private readonly EditableImageState _imageState;
    private readonly LoggingService? _loggingService;
    private bool _originalIsSlotEnabled;
    private readonly SettingsService? _settingsService;
    private readonly Guid? _snippetId;
    private readonly SnippetImageService? _snippetImageService;
    private readonly SnippetService _snippetService;
    private readonly Action<string> _showStatus;
    private readonly ThumbnailService? _thumbnailService;
    private AutoIconCacheEntry? _autoIcon;
    private SnippetActionType _actionType = SnippetActionType.PasteText;
    private string _content = string.Empty;
    private string _description = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isSlotEnabled;
    private string _launchPath = string.Empty;
    private string _launchUrl = string.Empty;
    private SlotImageMode _slotImageMode = SlotImageMode.Auto;
    private string _snippetTitle = string.Empty;

    public SnippetEditViewModel(
        Category category,
        SlotKey slotKey,
        Snippet? snippet,
        SnippetService snippetService,
        DialogService dialogService,
        Action cancel,
        Action<Snippet> afterSave,
        Action afterDelete,
        Action<string> showStatus,
        ThumbnailService? thumbnailService = null,
        SettingsService? settingsService = null,
        LoggingService? loggingService = null,
        SnippetImageService? snippetImageService = null,
        IAutoBackupCoordinator? autoBackupCoordinator = null)
    {
        CategoryId = category.Id;
        CategoryName = category.Name;
        SlotKey = slotKey;
        KeyText = slotKey.GetDisplayText();
        _snippetId = snippet?.Id;
        _snippetService = snippetService;
        _dialogService = dialogService;
        _settingsService = settingsService;
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
        _slotImageMode = GetInitialSlotImageMode(snippet);
        _autoIcon = AutoIconCacheEntry.FromSnippet(snippet);
        _description = snippet?.Description ?? string.Empty;
        _originalIsSlotEnabled = LoadSlotEnabledState();
        _isSlotEnabled = _originalIsSlotEnabled;
        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(Cancel);
        DeleteCommand = new RelayCommand(Delete);
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
        if (string.IsNullOrWhiteSpace(SnippetTitle))
        {
            if (TrySaveSlotOnly())
            {
                return;
            }

            ErrorMessage = "슬롯 명을 입력해 주세요.";
            return;
        }

        if (ActionType == SnippetActionType.PasteText && string.IsNullOrWhiteSpace(Content))
        {
            if (TrySaveSlotOnly())
            {
                return;
            }

            ErrorMessage = "붙여넣을 문구를 입력해 주세요.";
            return;
        }

        if (ActionType == SnippetActionType.LaunchFile && string.IsNullOrWhiteSpace(LaunchPath))
        {
            ErrorMessage = "실행할 파일, 폴더 또는 바로 가기를 선택해 주세요.";
            return;
        }

        var launchUrl = LaunchUrl;
        if (ActionType == SnippetActionType.LaunchUrl)
        {
            if (!UrlAddress.TryNormalize(LaunchUrl, out launchUrl))
            {
                ErrorMessage = "열 웹페이지 주소를 http 또는 https 주소로 입력해 주세요.";
                return;
            }

            LaunchUrl = launchUrl;
        }

        if (!SaveSlotEnabled())
        {
            return;
        }

        var autoIcon = PrepareAutoIconForSave();
        var snippet = _snippetId.HasValue
            ? _snippetService.Update(
                _snippetId.Value,
                SnippetTitle,
                Content,
                Description,
                _imageState.ImagePath,
                _imageState.ThumbnailPath,
                ActionType,
                LaunchPath,
                _slotImageMode,
                autoIcon,
                launchUrl)
            : _snippetService.Create(
                CategoryId,
                SlotKey,
                SnippetTitle,
                Content,
                Description,
                _imageState.ImagePath,
                _imageState.ThumbnailPath,
                ActionType,
                LaunchPath,
                _slotImageMode,
                autoIcon,
                launchUrl);

        _imageState.DeleteOriginalImageIfReplaced();
        _imageState.MarkCurrentAsOriginal();
        _autoBackupCoordinator?.RequestAutoBackup();
        _showStatus($"{snippet.Title} 저장됨.");
        _afterSave(snippet);
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
        var deletedImageFiles = _snippetService.Delete(_snippetId.Value);
        _thumbnailService?.DeleteImageFiles(deletedImageFiles);

        _autoBackupCoordinator?.RequestAutoBackup();
        _showStatus("실행 항목을 삭제했습니다.");
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

    private bool LoadSlotEnabledState()
    {
        var settings = _settingsService?.Load();

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
        if (_settingsService is null || IsSlotEnabled == _originalIsSlotEnabled)
        {
            return true;
        }

        try
        {
            _settingsService.SetSnippetSlotEnabled(SlotKey, IsSlotEnabled);
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
}
