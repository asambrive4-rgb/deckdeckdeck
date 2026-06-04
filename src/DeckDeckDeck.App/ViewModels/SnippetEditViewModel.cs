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
    private readonly Action _cancel;
    private readonly DialogService _dialogService;
    private readonly EditableImageState _imageState;
    private readonly LoggingService? _loggingService;
    private bool _originalIsSlotEnabled;
    private readonly SettingsService? _settingsService;
    private readonly Guid? _snippetId;
    private readonly SnippetService _snippetService;
    private readonly Action<string> _showStatus;
    private readonly ThumbnailService? _thumbnailService;
    private SnippetActionType _actionType = SnippetActionType.PasteText;
    private string _content = string.Empty;
    private string _description = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isSlotEnabled;
    private string _launchPath = string.Empty;
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
        LoggingService? loggingService = null)
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

    public string LaunchPath
    {
        get => _launchPath;
        set => SetProperty(ref _launchPath, value);
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

    public string? ThumbnailPath => _imageState.ThumbnailPath;

    public bool HasImage => _imageState.HasImage;

    public ICommand SaveCommand { get; }

    public ICommand CancelCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand ChooseImageCommand { get; }

    public ICommand RemoveImageCommand { get; }

    public ICommand ChooseLaunchFileCommand { get; }

    public ICommand ChooseLaunchFolderCommand { get; }

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
            ErrorMessage = "실행할 파일 또는 폴더를 선택해 주세요.";
            return;
        }

        if (!SaveSlotEnabled())
        {
            return;
        }

        var snippet = _snippetId.HasValue
            ? _snippetService.Update(
                _snippetId.Value,
                SnippetTitle,
                Content,
                Description,
                _imageState.ImagePath,
                _imageState.ThumbnailPath,
                ActionType,
                LaunchPath)
            : _snippetService.Create(
                CategoryId,
                SlotKey,
                SnippetTitle,
                Content,
                Description,
                _imageState.ImagePath,
                _imageState.ThumbnailPath,
                ActionType,
                LaunchPath);

        _imageState.DeleteOriginalImageIfReplaced();
        _imageState.MarkCurrentAsOriginal();
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

        try
        {
            _imageState.ReplaceWithStoredImage(selectedPath);
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
        ErrorMessage = string.Empty;
    }

    private void NotifyImageChanged()
    {
        OnPropertyChanged(nameof(ThumbnailPath));
        OnPropertyChanged(nameof(HasImage));
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
